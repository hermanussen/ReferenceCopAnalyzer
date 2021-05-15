using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;

namespace ReferenceCopAnalyzer.Test.Verifiers
{
    public static class CSharpAnalyzerVerifier<TAnalyzer>
        where TAnalyzer : DiagnosticAnalyzer, new()
    {
        /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic()"/>
        public static DiagnosticResult Diagnostic()
            => CSharpAnalyzerVerifier<TAnalyzer, MSTestVerifier>.Diagnostic();

        /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic(string)"/>
        public static DiagnosticResult Diagnostic(string diagnosticId)
            => CSharpAnalyzerVerifier<TAnalyzer, MSTestVerifier>.Diagnostic(diagnosticId);

        /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.Diagnostic(DiagnosticDescriptor)"/>
        public static DiagnosticResult Diagnostic(DiagnosticDescriptor descriptor)
            => CSharpAnalyzerVerifier<TAnalyzer, MSTestVerifier>.Diagnostic(descriptor);

        /// <inheritdoc cref="AnalyzerVerifier{TAnalyzer, TTest, TVerifier}.VerifyAnalyzerAsync(string, DiagnosticResult[])"/>
        public static async Task VerifyAnalyzerAsync(string source, NameValueCollection additionalFiles, DiagnosticResult[] expected)
        {
            var test = new Test(additionalFiles)
            {
                TestCode = source
            };

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }

        public class Test : CSharpAnalyzerTest<TAnalyzer, MSTestVerifier>
        {
            public Test(NameValueCollection additionalFiles)
            {
                SolutionTransforms.Add((solution, projectId) =>
                {
                    var compilationOptions = solution.GetProject(projectId).CompilationOptions;
                    compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                        compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));
                    solution = solution.WithProjectCompilationOptions(projectId, compilationOptions);
                    
                    if (additionalFiles?.Count > 0)
                    {
                        foreach (var additionalFile in additionalFiles.AllKeys)
                        {
                            solution = solution.AddAdditionalDocument(
                                DocumentId.CreateNewId(projectId),
                                additionalFile,
                                SourceText.From(additionalFiles[additionalFile], Encoding.UTF8));
                        }
                    }

                    return solution;
                });
            }
        }
    }
}
