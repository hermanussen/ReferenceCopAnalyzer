using System;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using ReferenceCopAnalyzer.Test.Verifiers;
using Xunit;

namespace ReferenceCopAnalyzer.Test
{
    public class BasicUsageTests
    {
        private const string ReferenceInUsing = @"
namespace A {
    public class AA {
    }
}
namespace B {
    using A;

    public class BB {
    }
}
";

        private const string ReferenceQualified = @"
namespace A {
    public class AA {
    }
}
namespace B {
    public class BB {
        private A.AA a;
    }
}
";

        private const string ReferenceQualifiedDeeper = @"
namespace A.Z {
    public class AA {
    }
}
namespace B {
    public class BB {
        private A.Z.AA a;
    }
}
";

        private const string ReferenceQualifiedDeepest = @"
namespace X.Y.Z {
    public class AA {
    }
}
namespace B {
    public class BB {
        private X.Y.Z.AA a;
    }
}
";

        private const string ReferenceInUsingWithAlias = @"
namespace A.B.C
{
    public class X
    {
    }
}
namespace B
{
    using Z=A.B.C;

    public class BB
    {
        private Z.X x;
    }
}
";

        private const string ReferenceInUsingWithAliasOutsideNamespace = @"
using Z=A.B.C;

namespace A.B.C
{
    public class X
    {
    }
}

namespace B
{
    public class BB
    {
        private Z.X x;
    }
}
";

        private const string ReferenceQualifiedWithGlobal = @"
namespace X.Y.Z {
    public class AA {
    }
}
namespace B {
    public class BB {
        private global::X.Y.Z.AA a;
    }
}
";

        private const string ReferenceNestedClass = @"
namespace A {
    public class AA {
        public class Nested {
        }
    }
}
namespace B {
    public class BB {
        private A.AA.Nested a;
    }
}
";

        private const string ReferenceRelative = @"
namespace One
{
    public class AA
    {
        private Two.BB b;
    }
}
namespace One.Two
{
    public class BB
    {
    }
}
";

        private const string ReferenceInUsingMultiple = @"
namespace A {
    public class AA {
    }
}
namespace B {
    public class BB {
    }
}
namespace C {
    using A;
    using B;

    public class CC {
    }
}
";
        private const string ReferenceInUsingNamedWildcards = @"
namespace A.X.A {
    using global::A.X.B;
    using global::A.Y.B;
    public class ZZ {
    }
}
namespace A.X.B {
    public class AA {
    }
}
namespace A.Y.B {
    public class BB {
    }
}
";

        private const string ReferenceInUsingWildcardMatchesDeep = @"
namespace A {
    using global::A.B.C;
    public class AA {
    }
}
namespace A.B.C {
    public class BB {
    }
}
";

        [Fact]
        public async Task ShouldNotGetDiagnosticsIfEmpty()
        {
            string test = @"";

            NameValueCollection additionalFiles = new()
            {
                { ReferenceCopAnalyzer.RulesFileName, string.Empty }
            };

            await ReferenceCopAnalyzerVerifier.VerifyAnalyzerAsync(test, additionalFiles, Array.Empty<DiagnosticResult>());
        }

        [Fact]
        public async Task ShouldReportNoRulesFile()
        {
            string test = @"";

            await ReferenceCopAnalyzerVerifier.VerifyAnalyzerAsync(
                test,
                new NameValueCollection(),
                new[] { new DiagnosticResult(ReferenceCopAnalyzer.NoRulesFileFoundDiagnosticId, DiagnosticSeverity.Error) });
        }

        [Fact]
        public async Task ShouldReportMultipleRulesFiles()
        {
            string test = @"";

            NameValueCollection additionalFiles = new()
            {
                { ReferenceCopAnalyzer.RulesFileName, string.Empty },
                { $"Folder/{ReferenceCopAnalyzer.RulesFileName}", string.Empty }
            };

            await ReferenceCopAnalyzerVerifier.VerifyAnalyzerAsync(
                test,
                additionalFiles,
                new[] { new DiagnosticResult(ReferenceCopAnalyzer.MultipleRulesFilesFoundDiagnosticId, DiagnosticSeverity.Error) });
        }

        [Fact]
        public async Task ShouldSupportMultipleRules()
        {
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(ReferenceInUsingMultiple,
                @"A C
B C",
                new[]
                {
                    ReferenceCopAnalyzerVerifier
                        .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                        .WithSpan(11, 5, 11, 13)
                        .WithArguments("C", "A"),
                    ReferenceCopAnalyzerVerifier
                        .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                        .WithSpan(12, 5, 12, 13)
                        .WithArguments("C", "B")
                });
        }

        [Fact]
        public async Task ShouldReportIllegalReferenceInUsingWithAlias()
        {
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(
                ReferenceInUsingWithAlias,
                "A.B.C B",
                new[]
                    {
                        ReferenceCopAnalyzerVerifier
                            .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                            .WithSpan(14, 17, 14, 20)
                            .WithArguments("B", "A.B.C"),
                        ReferenceCopAnalyzerVerifier
                            .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                            .WithSpan(10, 5, 10, 19)
                            .WithArguments("B", "A.B.C")
                    });
        }

        [Fact]
        public async Task ShouldReportIllegalReferenceInUsingWithAliasOutsideNamespace()
        {
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(
                ReferenceInUsingWithAliasOutsideNamespace,
                "A.B.C B",
                new[]
                    {
                        ReferenceCopAnalyzerVerifier
                            .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                            .WithSpan(2, 1, 2, 15)
                            .WithArguments("B", "A.B.C"),
                        ReferenceCopAnalyzerVerifier
                            .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                            .WithSpan(15, 17, 15, 20)
                            .WithArguments("B", "A.B.C")
                    });
        }

        [Theory]
        [InlineData(ReferenceInUsing, "A B", 7, 5, 7, 13, new[] { "B", "A" })]
        [InlineData(ReferenceQualified, "A B", 8, 17, 8, 21, new[] { "B", "A" })]
        [InlineData(ReferenceQualifiedDeeper, "A.Z B", 8, 17, 8, 23, new[] { "B", "A.Z" })]
        [InlineData(ReferenceQualifiedDeepest, "X.Y.Z B", 8, 17, 8, 25, new[] { "B", "X.Y.Z" })]
        [InlineData(ReferenceQualifiedDeepest, "X.*.Z B", 8, 17, 8, 25, new[] { "B", "X.Y.Z" })]
        [InlineData(ReferenceQualifiedWithGlobal, "X.Y.Z B", 8, 17, 8, 33, new[] { "B", "X.Y.Z" })]
        [InlineData(ReferenceNestedClass, "A B", 10, 17, 10, 28, new[] { "B", "A" })]
        [InlineData(ReferenceRelative, "One.Two One", 6, 17, 6, 23, new[] { "One", "One.Two" })]
        [InlineData(ReferenceInUsingNamedWildcards, @"[main_ns].[named_wc].A [main_ns].[named_wc].B", 4, 5, 4, 25, new[] { "A.X.A", "global::A.Y.B" })]
        [InlineData(ReferenceInUsingNamedWildcards, @"*.[named_wc].A *.[named_wc].B", 4, 5, 4, 25, new[] { "A.X.A", "global::A.Y.B" })]
        public async Task ShouldReportIllegalReference(
            string source,
            string rules,
            int startLine,
            int startColumn,
            int endLine,
            int endColumn,
            object[] arguments)
        {
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(source, rules, new[]
                {
                    ReferenceCopAnalyzerVerifier
                        .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                        .WithSpan(startLine, startColumn, endLine, endColumn)
                        .WithArguments(arguments)
                });
        }

        [Theory]
        [InlineData(ReferenceInUsing, "B A")]
        [InlineData(ReferenceQualified, "B A")]
        [InlineData(ReferenceQualifiedDeeper, "B A.Z")]
        [InlineData(ReferenceQualifiedDeepest, "B X.Y.Z")]
        [InlineData(ReferenceQualifiedDeepest, "B X.*.Z")]
        [InlineData(ReferenceInUsingWithAlias, "B A.B.C")]
        [InlineData(ReferenceInUsingWithAliasOutsideNamespace, "B A.B.C")]
        [InlineData(ReferenceQualifiedWithGlobal, "B X.Y.Z")]
        [InlineData(ReferenceNestedClass, "B A")]
        [InlineData(ReferenceRelative, "One One.Two")]
        [InlineData(ReferenceInUsingMultiple, @"C A
C B")]
        [InlineData(ReferenceInUsingMultiple, @"# comment 1
// different comment
// -
C A # inline comment
C B #inline comment
// #
# //")]
        [InlineData(ReferenceInUsingNamedWildcards, @"A.X.A A.X.B
A.X.A A.Y.B")]
        [InlineData(ReferenceInUsingWildcardMatchesDeep, @"A A.*")]
        public async Task ShouldNotReportIllegalReference(string source, string rules)
        {
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(source, rules, Array.Empty<DiagnosticResult>());
        }
    }
}
