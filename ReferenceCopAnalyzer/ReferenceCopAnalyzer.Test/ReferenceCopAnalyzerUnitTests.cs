using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xunit;
using VerifyCS = ReferenceCopAnalyzer.Test.Verifiers.CSharpAnalyzerVerifier<ReferenceCopAnalyzer.ReferenceCopAnalyzer>;

namespace ReferenceCopAnalyzer.Test
{
    public class ReferenceCopAnalyzerUnitTest
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

        [Fact]
        public async Task ShouldNotGetDiagnosticsIfEmpty()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test, null, new DiagnosticResult[0]);
        }

        [Fact]
        public async Task ShouldReportMultipleRulesFiles()
        {
            var test = @"";

            var additionalFiles = new NameValueCollection()
                {
                    { ReferenceCopAnalyzer.RulesFileName, string.Empty },
                    { $"Folder/{ReferenceCopAnalyzer.RulesFileName}", string.Empty }
                };
            await VerifyCS.VerifyAnalyzerAsync(
                test,
                additionalFiles,
                new[] { new DiagnosticResult(ReferenceCopAnalyzer.MultipleRulesFilesFoundDiagnosticId, DiagnosticSeverity.Error) });
        }

        [Theory]
        [InlineData(ReferenceInUsing, "A B", 7, 5, 7, 13, new [] { "B", "A"})]
        [InlineData(ReferenceQualified, "A B", 8, 17, 8, 21, new[] { "B", "A" })]
        [InlineData(ReferenceQualifiedDeeper, "A.Z B", 8, 17, 8, 23, new[] { "B", "A.Z" })]
        [InlineData(ReferenceQualifiedDeepest, "X.Y.Z B", 8, 17, 8, 25, new[] { "B", "X.Y.Z" })]
        [InlineData(ReferenceQualifiedDeepest, "X.*.Z B", 8, 17, 8, 25, new[] { "B", "X.Y.Z" })]
        [InlineData(ReferenceInUsingWithAlias, "A.B.C B", 10, 5, 10, 19, new[] { "B", "A.B.C" })]
        [InlineData(ReferenceInUsingWithAliasOutsideNamespace, "A.B.C B", 2, 1, 2, 15, new[] { "B", "A.B.C" })]
        [InlineData(ReferenceQualifiedWithGlobal, "X.Y.Z B", 8, 17, 8, 33, new[] { "B", "global::X.Y.Z" })]
        [InlineData(ReferenceNestedClass, "A B", 10, 17, 10, 28, new[] { "B", "A" })]
        [InlineData(ReferenceRelative, "A B", 10, 17, 10, 28, new[] { "B", "A" })]
        public async Task ShouldReportIllegalReference(
            string source,
            string rules,
            int startLine,
            int startColumn,
            int endLine,
            int endColumn,
            object[] arguments)
        {
            var additionalFiles = new NameValueCollection()
            {
                { ReferenceCopAnalyzer.RulesFileName, rules }
            };

            await VerifyCS.VerifyAnalyzerAsync(
                source,
                additionalFiles,
                new[]
                {
                    VerifyCS
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
        public async Task ShouldNotReportIllegalReference(string source, string rules)
        {
            var additionalFiles = new NameValueCollection()
            {
                { ReferenceCopAnalyzer.RulesFileName, rules }
            };

            await VerifyCS.VerifyAnalyzerAsync(
                source,
                additionalFiles,
                new DiagnosticResult[0]);
        }
    }
}
