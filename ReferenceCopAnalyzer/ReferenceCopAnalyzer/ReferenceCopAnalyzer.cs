using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.Text;

namespace ReferenceCopAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ReferenceCopAnalyzer : DiagnosticAnalyzer
    {
        public const string ReferenceNotAllowedDiagnosticId = "RefCop0001";

        public const string MultipleRulesFilesFoundDiagnosticId = "RefCop0002";

        public const string NoRulesFileFoundDiagnosticId = "RefCop0003";

        public const string RulesFileName = ".refrules";

        private const string RuleSeparator = " ";

        private static readonly string[] CommentIndicators = new[] { "#", "//" };

        private static readonly Regex NamedWildcardRegex = new(@"\[[^]]+\]|\*", RegexOptions.Compiled);

#pragma warning disable RS2008 // Analyzer release tracking is not needed

        private static readonly DiagnosticDescriptor ReferenceNotAllowedDiagnostic = new(
            ReferenceNotAllowedDiagnosticId,
            "Reference is not allowed",
            "A reference between '{0}' and '{1}' is not allowed according to the rules file",
            "ReferenceCop",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Reference is not allowed.");

        private static readonly DiagnosticDescriptor MultipleRulesFilesFoundDiagnostic = new(
            MultipleRulesFilesFoundDiagnosticId,
            "Multiple rules files found",
            $"Found multiple {RulesFileName} files; please make sure there is only one",
            "ReferenceCop",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"There must be exactly one file named {RulesFileName}.");

        private static readonly DiagnosticDescriptor NoRulesFileFoundDiagnostic = new(
            NoRulesFileFoundDiagnosticId,
            "No rules file found",
            $"Did not find a {RulesFileName} file; please make sure there is one, and make sure that it is included in the project as AdditionalFile",
            "ReferenceCop",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: $"There must be exactly one file named {RulesFileName}.");

#pragma warning restore RS2008

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                ReferenceNotAllowedDiagnostic,
                MultipleRulesFilesFoundDiagnostic,
                NoRulesFileFoundDiagnostic);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                List<AdditionalText> rulesFiles = compilationStartContext
                    .Options
                    .AdditionalFiles
                    .Where(f => RulesFileName.Equals(Path.GetFileName(f.Path), StringComparison.InvariantCultureIgnoreCase))
                    .ToList();

                if (rulesFiles.Count > 1)
                {
                    compilationStartContext.RegisterCompilationEndAction(compilationEndContext =>
                    {
                        Diagnostic diagnostic = Diagnostic.Create(MultipleRulesFilesFoundDiagnostic, null);
                        compilationEndContext.ReportDiagnostic(diagnostic);
                    });

                    return;
                }

                SourceText? rulesFile = rulesFiles.FirstOrDefault()?.GetText();
                if (rulesFile == null)
                {
                    compilationStartContext.RegisterCompilationEndAction(compilationEndContext =>
                    {
                        Diagnostic diagnostic = Diagnostic.Create(NoRulesFileFoundDiagnostic, null);
                        compilationEndContext.ReportDiagnostic(diagnostic);
                    });

                    return;
                }

                List<KeyValuePair<string, string>> allowedReferences = new();
                foreach (string line in rulesFile
                    .ToString()
                    .Split(new[] { Environment.NewLine, "\n" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.ToString().Trim())
                    .Where(l =>
                        !string.IsNullOrWhiteSpace(l)
                        && l.Contains(RuleSeparator)))
                {
                    string lineText = line;
                    foreach (string indicator in CommentIndicators.Where(i => lineText.Contains(i)))
                    {
                        int indexOf = lineText.IndexOf(indicator, StringComparison.InvariantCultureIgnoreCase);
                        if (indexOf >= 0)
                        {
                            lineText = lineText.Substring(0, indexOf);
                        }
                    }

                    string[] sourceAndTarget = lineText
                        .Split(new string[] { RuleSeparator }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .ToArray();
                    if (sourceAndTarget.Length != 2)
                    {
                        continue;
                    }

                    allowedReferences.Add(new KeyValuePair<string, string>(sourceAndTarget[0], sourceAndTarget[1]));
                }

                compilationStartContext.RegisterSyntaxNodeAction(modelContext =>
                    {
                        if (modelContext.Node is not UsingDirectiveSyntax u)
                        {
                            return;
                        }

                        string targetName = u.Name.ToFullString().Trim();

                        bool stripClass = false;

                        // A static import has a reference to a class instead of a namespace, so that needs to be stripped
                        if (u.ChildTokens().Any(n => n.IsKind(SyntaxKind.StaticKeyword)))
                        {
                            stripClass = true;
                        }
                        else if (u.ChildNodes().OfType<NameEqualsSyntax>().Any())
                        {
                            // If the alias references a class, that needs to be stripped
                            if (modelContext
                                .SemanticModel
                                .GetTypeInfo(u.ChildNodes().OfType<QualifiedNameSyntax>().First())
                                .Type != null)
                            {
                                stripClass = true;
                            }
                        }

                        if (stripClass)
                        {
                            targetName = u.ChildNodes().OfType<QualifiedNameSyntax>().FirstOrDefault()?.Left.ToFullString().Trim()
                                ?? targetName;
                        }

                        string? sourceName = u.FirstAncestorOrSelf<NamespaceDeclarationSyntax>()?.Name.ToFullString().Trim();

                        if (sourceName == null)
                        {
                            // If the code is not in a namespace, it applies to all namespaces in the compilation unit
                            IEnumerable<NamespaceDeclarationSyntax>? namespaces
                                = u.Parent?.DescendantNodes().OfType<NamespaceDeclarationSyntax>();
                            if (namespaces != null)
                            {
                                foreach (NamespaceDeclarationSyntax ns in namespaces)
                                {
                                    sourceName = ns.Name.ToFullString().Trim();
                                    if (!IsAllowedReference(allowedReferences, sourceName, targetName))
                                    {
                                        Diagnostic diagnostic = Diagnostic.Create(ReferenceNotAllowedDiagnostic,
                                            u.GetLocation(), sourceName, targetName);

                                        modelContext.ReportDiagnostic(diagnostic);
                                    }
                                }
                            }

                            return;
                        }

                        if (!IsAllowedReference(allowedReferences, sourceName, targetName))
                        {
                            Diagnostic diagnostic = Diagnostic.Create(ReferenceNotAllowedDiagnostic, u.GetLocation(), sourceName, targetName);

                            modelContext.ReportDiagnostic(diagnostic);
                        }
                    }, SyntaxKind.UsingDirective);

                compilationStartContext.RegisterSyntaxNodeAction(modelContext =>
                {
                    if (modelContext.Node is not QualifiedNameSyntax u)
                    {
                        return;
                    }

                    switch (u.Parent?.Kind())
                    {
                        // No need to check lesser depth qualified name
                        case SyntaxKind.QualifiedName:
                        // No need to check namespaces themselves
                        case SyntaxKind.NamespaceDeclaration:
                        // Using directives are checked in a different syntax action 
                        case SyntaxKind.UsingDirective:
                            return;
                    }

                    string? targetName;
                    ISymbol? symbol = modelContext.SemanticModel.GetTypeInfo(u).Type
                                      ?? modelContext.SemanticModel.GetSymbolInfo(u).Symbol;
                    if (symbol == null)
                    {
                        return;
                    }

                    switch (symbol.Kind)
                    {
                        case SymbolKind.NamedType:
                            targetName = symbol.ContainingNamespace.ToDisplayString();
                            break;
                        case SymbolKind.Namespace when !((symbol as INamespaceSymbol)?.IsGlobalNamespace ?? false):
                            targetName = symbol.ToDisplayString();
                            break;
                        default:
                            return;
                    }

                    NamespaceDeclarationSyntax? containingNamespace = u.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
                    if (containingNamespace == null)
                    {
                        return;
                    }

                    string sourceName = containingNamespace.Name.ToFullString().Trim();

                    if (!IsAllowedReference(allowedReferences, sourceName, targetName))
                    {
                        Diagnostic diagnostic = Diagnostic.Create(ReferenceNotAllowedDiagnostic, u.GetLocation(), sourceName, targetName);

                        modelContext.ReportDiagnostic(diagnostic);
                    }
                }, SyntaxKind.QualifiedName);

                compilationStartContext.RegisterSyntaxNodeAction(modelContext =>
                {
                    InvocationExpressionSyntax u = (InvocationExpressionSyntax)modelContext.Node;

                    List<IdentifierNameSyntax> idNames = u.ChildNodes().OfType<IdentifierNameSyntax>().ToList();
                    if (!idNames.Any()
                        || idNames.All(n => n.Identifier.ValueText != "nameof"))
                    {
                        return;
                    }

                    foreach (ExpressionSyntax expression in u
                        .ChildNodes()
                        .OfType<ArgumentListSyntax>()
                        .SelectMany(a => a.Arguments.Select(b => b.Expression)))
                    {
                        string targetName;
                        ISymbol? symbol = modelContext.SemanticModel.GetTypeInfo(expression).Type
                                          ?? modelContext.SemanticModel.GetSymbolInfo(expression).Symbol;
                        if (symbol == null)
                        {
                            return;
                        }

                        switch (symbol.Kind)
                        {
                            case SymbolKind.NamedType:
                                targetName = symbol.ContainingNamespace.ToDisplayString();
                                break;
                            case SymbolKind.Namespace when !((symbol as INamespaceSymbol)?.IsGlobalNamespace ?? false):
                                targetName = symbol.ToDisplayString();
                                break;
                            default:
                                return;
                        }

                        NamespaceDeclarationSyntax? containingNamespace = u.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
                        if (containingNamespace == null)
                        {
                            return;
                        }

                        string sourceName = containingNamespace.Name.ToFullString().Trim();

                        if (!IsAllowedReference(allowedReferences, sourceName, targetName))
                        {
                            Diagnostic diagnostic = Diagnostic.Create(ReferenceNotAllowedDiagnostic, u.GetLocation(), sourceName, targetName);

                            modelContext.ReportDiagnostic(diagnostic);
                        }
                    }
                }, SyntaxKind.InvocationExpression);
            });
        }

        private static bool IsAllowedReference(List<KeyValuePair<string, string>> allowedReferences, string sourceName, string targetName)
        {
            const string global = "global::";

            if (sourceName.StartsWith(global))
            {
                sourceName = sourceName.Substring(global.Length);
            }

            if (targetName.StartsWith(global))
            {
                targetName = targetName.Substring(global.Length);
            }

            if (sourceName == targetName)
            {
                // Reference between the same namespace is always ok
                return true;
            }

            return allowedReferences.Any(r =>
                {
                    string ruleSource = r.Key;
                    string ruleTarget = r.Value;

                    // Keep a list of all named wildcard names
                    List<string> replaced = new();

                    // Keep names to actual values mappings, e.g.: [main_ns] = MyNs
                    List<KeyValuePair<string, string>> mappings = new();

                    // Replace named wildcards with actual wildcards, and store the names
                    foreach (Match match in NamedWildcardRegex.Matches(ruleSource))
                    {
                        ruleSource = ruleSource.Replace(match.Value, "*");
                        replaced.Add(match.Value);
                    }

                    // If the source rule is not a match, we can skip further processing
                    if (!IsMatch(ruleSource, sourceName))
                    {
                        return false;
                    }

                    // Build the mappings based on the sourceName
                    foreach (Match match in Regex.Matches(sourceName, WildCardToRegular(ruleSource)))
                    {
                        bool first = true;
                        foreach (Group matchGroup in match.Groups)
                        {
                            if (first)
                            {
                                // Skip the first match group, as it contains the whole thing
                                first = false;
                                continue;
                            }

                            string rp = replaced.Skip(mappings.Count).FirstOrDefault();
                            if (rp != null)
                            {
                                mappings.Add(new KeyValuePair<string, string>(rp, matchGroup.Value));
                            }
                        }
                    }

                    // Replace the named wildcards in the target rule with the actual values from the source
                    foreach (KeyValuePair<string, string> mapping in mappings.Where(m => m.Key != "*"))
                    {
                        ruleTarget = ruleTarget.Replace(mapping.Key, mapping.Value);
                    }

                    return IsMatch(ruleTarget, targetName);
                });
        }

        private static bool IsMatch(string pattern, string reference)
        {
            return pattern == reference
                   || Regex.IsMatch(reference, WildCardToRegular(pattern));
        }

        private static string WildCardToRegular(string value)
        {
            // Based on https://stackoverflow.com/a/30300521
            return $"^{Regex.Escape(value).Replace("\\*", @"(.*)")}$";
        }
    }
}
