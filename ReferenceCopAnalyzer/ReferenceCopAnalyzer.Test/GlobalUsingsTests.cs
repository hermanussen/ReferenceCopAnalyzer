using Microsoft.CodeAnalysis.Testing;
using ReferenceCopAnalyzer.Test.Verifiers;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ReferenceCopAnalyzer.Test
{
    public class GlobalUsingsTests
    {
        private const string FileA = @"
namespace A;
public class AA {
    private static Encoding E = Encoding.UTF8;
}
";
        private const string FileB = @"
global using System.Text;
";

        [Theory]
        [InlineData("!* System.Text")]
        [InlineData("")]
        [InlineData("B A")]
        public async Task ShouldReportIllegalReference(string rules)
        {
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(new[] { FileB, FileA }, rules, new[]
                {
                    ReferenceCopAnalyzerVerifier
                        .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                            .WithSpan(2, 1, 2, 26)
                            .WithArguments("*", "System.Text"),
                });
        }

        [Theory]
        [InlineData("* System.Text")]
        [InlineData("* System.*")]
        [InlineData("[namedwildcard] System.Text")]
        [InlineData("[namedwildcard] System.*")]
        public async Task ShouldNotReportIllegalReference(string rules)
        {
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(new[] { FileB, FileA }, rules, Array.Empty<DiagnosticResult>());
        }
    }
}
