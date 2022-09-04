﻿using System.Collections.Specialized;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Microsoft.CodeAnalysis.Text;

namespace ReferenceCopAnalyzer.Test.Verifiers
{
    public static class ReferenceCopAnalyzerVerifier
    {
        public static DiagnosticResult Diagnostic(string diagnosticId)
        {
            return CSharpAnalyzerVerifier<ReferenceCopAnalyzer, MSTestVerifier>.Diagnostic(diagnosticId);
        }

        public static Task VerifyAnalyzerAsync(string source, NameValueCollection additionalFiles, DiagnosticResult[] expected)
        {
            return VerifyAnalyzerAsync(new[] { source }, additionalFiles, expected);
        }

        public static async Task VerifyAnalyzerAsync(string[] sources, NameValueCollection additionalFiles, DiagnosticResult[] expected)
        {
            Test test = new(additionalFiles);

            foreach(string? source in sources)
            {
                if (source != null)
                {
                    test.TestState.Sources.Add(source);
                }
            }

            test.ExpectedDiagnostics.AddRange(expected);
            await test.RunAsync(CancellationToken.None);
        }

        public static Task VerifyReferenceCopAnalysis(string source, string rules, DiagnosticResult[] diagnostics)
        {
            return VerifyReferenceCopAnalysis(new[] { source }, rules, diagnostics);
        }

        public static async Task VerifyReferenceCopAnalysis(string[] sources, string rules, DiagnosticResult[] diagnostics)
        {
            NameValueCollection additionalFiles = new()
            {
                { ReferenceCopAnalyzer.RulesFileName, rules }
            };

            await VerifyAnalyzerAsync(
                sources,
                additionalFiles,
                diagnostics);
        }

        public class Test : CSharpAnalyzerTest<ReferenceCopAnalyzer, MSTestVerifier>
        {
            public Test(NameValueCollection additionalFiles)
            {
                SolutionTransforms.Add((solution, projectId) =>
                {
                    CompilationOptions? compilationOptions = solution.GetProject(projectId)?.CompilationOptions;
                    if (compilationOptions == null)
                    {
                        return solution;
                    }

                    compilationOptions = compilationOptions.WithSpecificDiagnosticOptions(
                        compilationOptions.SpecificDiagnosticOptions.SetItems(CSharpVerifierHelper.NullableWarnings));
                    solution = solution.WithProjectCompilationOptions(projectId, compilationOptions);

                    if (additionalFiles.Count > 0)
                    {
                        foreach (string? additionalFile in additionalFiles.AllKeys)
                        {
                            if (additionalFile == null)
                            {
                                continue;
                            }

                            string? fileContents = additionalFiles[additionalFile];
                            if (fileContents == null)
                            {
                                continue;
                            }

                            solution = solution.AddAdditionalDocument(
                                DocumentId.CreateNewId(projectId),
                                additionalFile,
                                SourceText.From(fileContents, Encoding.UTF8));
                        }
                    }

                    return solution;
                });
            }
        }
    }
}
