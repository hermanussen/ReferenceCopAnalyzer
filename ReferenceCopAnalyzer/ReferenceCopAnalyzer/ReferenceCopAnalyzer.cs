using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;

namespace ReferenceCopAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ReferenceCopAnalyzer : DiagnosticAnalyzer
    {
        public const string ReferenceNotAllowedDiagnosticId = "ReferenceCopAnalyzer";

        public const string MultipleRulesFilesFoundDiagnosticId = "ReferenceCopAnalyzerMultipleFiles";

        public const string RulesFileName = ".refrules";

        private const string RuleSeparator = " ";

        private static readonly DiagnosticDescriptor ReferenceNotAllowedDiagnostic = new (
            ReferenceNotAllowedDiagnosticId,
            "Reference is not allowed.",
            "A reference between '{0}' and '{1}' is not allowed according to the rules file",
            "ReferenceCop",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Reference is not allowed.");

        private static readonly DiagnosticDescriptor MultipleRulesFilesFoundDiagnostic = new(
            MultipleRulesFilesFoundDiagnosticId,
            "Multiple rules files found.",
            $"Found multiple {RulesFileName} files; please make sure there is only one",
            "ReferenceCop",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: "Reference is not allowed.");

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(
                ReferenceNotAllowedDiagnostic,
                MultipleRulesFilesFoundDiagnostic);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(compilationStartContext =>
            {
                var rulesFiles = compilationStartContext
                    .Options
                    .AdditionalFiles
                    .Where(f => RulesFileName.Equals(Path.GetFileName(f.Path), StringComparison.InvariantCultureIgnoreCase))
                    .ToList();
                if (!rulesFiles.Any())
                {
                    return;
                }

                if (rulesFiles.Count > 1)
                {
                    compilationStartContext.RegisterCompilationEndAction(compilationEndContext =>
                    {
                        var diagnostic = Diagnostic.Create(MultipleRulesFilesFoundDiagnostic, null);
                        compilationEndContext.ReportDiagnostic(diagnostic);
                    });
                    
                    return;
                }

                List<KeyValuePair<string,string>> allowedReferences = new ();
                foreach (var line in rulesFiles.First().GetText().Lines
                    .Select(l => l.Text.ToString())
                    .Where(l => !string.IsNullOrWhiteSpace(l) && l.Contains(RuleSeparator)))
                {
                    var sourceAndTarget = line
                        .Split(new [] { RuleSeparator }, StringSplitOptions.RemoveEmptyEntries)
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
                        var u = (UsingDirectiveSyntax) modelContext.Node;
                        var targetName = u.Name.ToFullString().Trim();
                        var sourceName = u.FirstAncestorOrSelf<NamespaceDeclarationSyntax>()?.Name.ToFullString().Trim();

                        if (!allowedReferences.Any(r => r.Key == sourceName && r.Value == targetName))
                        {
                            var diagnostic = Diagnostic.Create(ReferenceNotAllowedDiagnostic, u.GetLocation(), sourceName, targetName);

                            modelContext.ReportDiagnostic(diagnostic);
                        }
                    }, SyntaxKind.UsingDirective);

                compilationStartContext.RegisterSyntaxNodeAction(modelContext =>
                {
                    var u = (QualifiedNameSyntax) modelContext.Node;

                    var parentSyntaxKind = u.Parent.Kind();
                    if (parentSyntaxKind == SyntaxKind.NamespaceDeclaration)
                    {
                        // No need to check the namespace declaration itself
                        return;
                    }

                    if (parentSyntaxKind == SyntaxKind.QualifiedName)
                    {
                        // No need to check lesser depth qualified name
                        return;
                    }

                    var targetName = u.Left.ToFullString().Trim();
                    var sourceName = u.FirstAncestorOrSelf<NamespaceDeclarationSyntax>()?.Name.ToFullString().Trim();

                    if (!allowedReferences.Any(r => r.Key == sourceName && r.Value == targetName))
                    {
                        var diagnostic = Diagnostic.Create(ReferenceNotAllowedDiagnostic, u.GetLocation(), sourceName, targetName);

                        modelContext.ReportDiagnostic(diagnostic);
                    }
                }, SyntaxKind.QualifiedName);

                //compilationStartContext.RegisterSyntaxNodeAction(modelContext =>
                //{
                //    var u = (MemberAccessExpressionSyntax)modelContext.Node;
                //    var targetName = u.ToFullString().Trim();
                //    var sourceName = u.FirstAncestorOrSelf<NamespaceDeclarationSyntax>()?.Name.ToFullString().Trim();

                //    if (!allowedReferences.Any(r => r.Key == sourceName && r.Value == targetName))
                //    {
                //        var diagnostic = Diagnostic.Create(ReferenceNotAllowedDiagnostic, u.GetLocation(), sourceName, targetName);

                //        modelContext.ReportDiagnostic(diagnostic);
                //    }
                //}, SyntaxKind.SimpleMemberAccessExpression);
            });
        }
    }
}
