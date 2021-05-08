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
        [InlineData("A>B", true)]
        [InlineData("B>A", false)]
        public async Task ShouldReportIllegalReferenceInUsing(string rules, bool reportsDiagnostic)
        {
            var test = @"
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

            var additionalFiles = new NameValueCollection()
                {
                    { ReferenceCopAnalyzer.RulesFileName, rules }
                };

            await VerifyCS.VerifyAnalyzerAsync(
                test,
                additionalFiles,
                reportsDiagnostic
                ? new [] { new DiagnosticResult(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId, DiagnosticSeverity.Error) }
                : new DiagnosticResult[0]);
        }

        [Theory]
        [InlineData("A>B", true)]
        [InlineData("B>A", false)]
        public async Task ShouldReportIllegalReferenceFullyQualified(string rules, bool reportsDiagnostic)
        {
            var test = @"
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

            var additionalFiles = new NameValueCollection()
                {
                    { ReferenceCopAnalyzer.RulesFileName, rules }
                };

            await VerifyCS.VerifyAnalyzerAsync(
                test,
                additionalFiles,
                reportsDiagnostic
                    ? new[] { new DiagnosticResult(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId, DiagnosticSeverity.Error) }
                    : new DiagnosticResult[0]);
        }
    }
}
