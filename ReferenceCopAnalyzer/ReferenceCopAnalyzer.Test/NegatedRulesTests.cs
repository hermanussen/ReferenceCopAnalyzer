using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using ReferenceCopAnalyzer.Test.Verifiers;
using Xunit;

namespace ReferenceCopAnalyzer.Test
{
    public class NegatedRulesTests
    {
        private const string Src = @"
namespace X {
    using A.B.C.D;
}
namespace A {
}
namespace A.B {
}
namespace A.B.C {
}
namespace A.B.C.D {
}
";

        [Theory]
        [InlineData(Src, @"X A.*
!X A.B.C.D", new[] { "X", "A.B.C.D" })]
        [InlineData(Src, @"!X A.*
X A.B.C.*
!X A.B.C.D", new[] { "X", "A.B.C.D" })]
        public async Task ShouldReportIllegalReference(
            string source,
            string rules,
            object[] arguments)
        {
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(source, rules, new[]
            {
                ReferenceCopAnalyzerVerifier
                    .Diagnostic(ReferenceCopAnalyzer.ReferenceNotAllowedDiagnosticId)
                    .WithSpan(3, 5, 3, 19)
                    .WithArguments(arguments)
            });
        }

        [Theory]
        [InlineData(Src, @"X A.*
!X A.B")]
        [InlineData(Src, @"!X A.*
X A.B.C.D")]
        [InlineData(Src, @"* *
!X A.B.C.*
X A.B.C.D")]
        public async Task ShouldNotReportIllegalReference(string source, string rules)
        {
            await ReferenceCopAnalyzerVerifier.VerifyReferenceCopAnalysis(source, rules, Array.Empty<DiagnosticResult>());
        }
    }
}
