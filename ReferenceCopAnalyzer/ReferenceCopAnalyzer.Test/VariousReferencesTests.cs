using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = ReferenceCopAnalyzer.Test.Verifiers.CSharpAnalyzerVerifier<ReferenceCopAnalyzer.ReferenceCopAnalyzer>;

namespace ReferenceCopAnalyzer.Test
{
    public class VariousReferencesTests
    {
        private const string VariableDeclaration = @"
namespace One {
    class Source {
        public Source() {
            Two.SourceTwo a;
        }
    }
}
namespace Two {
    class SourceTwo {}
}
";

        private const string New = @"
namespace One {
    class Source {
        public Source() {
            var a = new Two.SourceTwo();
        }
    }
}
namespace Two {
    class SourceTwo {}
}
";

        private const string StaticImport = @"
namespace One {
    using static Two.SourceTwo;
    class Source {
        public Source() {
            Method();
        }
    }
}
namespace Two {
    public static class SourceTwo {
        public static void Method() {}
    }
}
";
        private const string AliasForClassImport = @"
namespace One {
    using ST=Two.SourceTwo;
    class Source {
        public Source() {
            ST s = new();
        }
    }
}
namespace Two {
    public class SourceTwo {}
}
";

        [Theory]
        [InlineData(VariableDeclaration, "Two One", 5, 13, 5, 26, new[] { "One", "Two" })]
        [InlineData(New, "Two One", 5, 25, 5, 38, new[] { "One", "Two" })]
        [InlineData(StaticImport, "Two One", 3, 5, 3, 32, new[] { "One", "Two" })]
        [InlineData(AliasForClassImport, "Two One", 3, 5, 3, 28, new[] { "One", "Two" })]
        public async Task ShouldReportIllegalReference(
            string source,
            string rules,
            int startLine,
            int startColumn,
            int endLine,
            int endColumn,
            object[] arguments)
        {
            await VerifyCS.VerifyReferenceCopAnalysis(source, rules, new[]
                {
                    VerifyCS
                        .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                        .WithSpan(startLine, startColumn, endLine, endColumn)
                        .WithArguments(arguments)
                });
        }

        [Theory]
        [InlineData(VariableDeclaration, "One Two")]
        [InlineData(New, "One Two")]
        [InlineData(StaticImport, "One Two")]
        [InlineData(AliasForClassImport, "One Two")]
        public async Task ShouldNotReportIllegalReference(string source, string rules)
        {
            await VerifyCS.VerifyReferenceCopAnalysis(source, rules, Array.Empty<DiagnosticResult>());
        }
    }
}
