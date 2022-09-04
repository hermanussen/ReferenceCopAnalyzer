using Microsoft.CodeAnalysis.Testing;
using ReferenceCopAnalyzer.Test.Verifiers;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ReferenceCopAnalyzer.Test
{
    public class FileScopedNamespacesTests
    {
        private const string FileA = @"
namespace A;
public class AA {
}
";
        private const string FileB = @"
namespace B;
using A;

public class BB {
}
";

        [Fact]
        public async Task ShouldReportIllegalReference()
        {
            string rules = "A B";
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(new[] { FileB, FileA }, rules, new[]
                {
                    ReferenceCopAnalyzerVerifier
                        .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                            .WithSpan(3, 1, 3, 9)
                            .WithArguments("B", "A"),
                });
        }

        [Fact]
        public async Task ShouldNotReportIllegalReference()
        {
            string rules = "B A";
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(new[] { FileB, FileA }, rules, Array.Empty<DiagnosticResult>());
        }
    }
}
