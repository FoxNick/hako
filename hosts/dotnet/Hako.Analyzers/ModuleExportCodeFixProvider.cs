namespace HakoJS.Analyzers;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;


/// <summary>
/// A code fix provider that automatically adds missing AddExport calls.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ModuleExportCodeFixProvider)), Shared]
public class ModuleExportCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => 
        ImmutableArray.Create(ModuleExportAnalyzer.DiagnosticId);

    public override FixAllProvider GetFixAllProvider() => 
        new ModuleExportFixAllProvider();

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);
        
        if (root == null)
            return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var diagnosticNode = root.FindNode(diagnosticSpan);

        // Find the SetExport invocation
        var setExportInvocation = diagnosticNode.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (setExportInvocation == null)
            return;

        // Extract the export name
        var exportName = GetExportName(setExportInvocation);
        if (string.IsNullOrEmpty(exportName))
            return;

        // Register code fix
        context.RegisterCodeFix(
            CodeAction.Create(
                title: string.Format(Resources.HAKO100CodeFixTitle, exportName),
                createChangedDocument: c => AddMissingExportAsync(context.Document, setExportInvocation, exportName, c),
                equivalenceKey: nameof(Resources.HAKO100CodeFixTitle)),
            diagnostic);
    }

    private string? GetExportName(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList?.Arguments;
        if (args != null && args.Value.Count > 0)
        {
            var firstArg = args.Value[0].Expression;
            if (firstArg is LiteralExpressionSyntax literal &&
                literal.Token.Value is string name)
            {
                return name;
            }
        }
        return null;
    }

    internal async Task<Document> AddMissingExportAsync(Document document, 
        InvocationExpressionSyntax setExportInvocation, string exportName, CancellationToken cancellationToken)
    {
        return await AddMissingExportsAsync(document, setExportInvocation, new[] { exportName }, cancellationToken);
    }

    internal async Task<Document> AddMissingExportsAsync(Document document,
        InvocationExpressionSyntax setExportInvocation, IEnumerable<string> exportNames, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root == null)
            return document;

        // Find the CreateCModule invocation
        var createModuleInvocation = FindCreateCModuleInvocation(setExportInvocation);
        if (createModuleInvocation == null)
            return document;

        // Find the statement containing the CreateCModule call
        var statement = createModuleInvocation.FirstAncestorOrSelf<StatementSyntax>();
        if (statement == null)
            return document;

        ExpressionStatementSyntax? newStatement = null;

        if (statement is LocalDeclarationStatementSyntax localDecl)
        {
            var variable = localDecl.Declaration.Variables.FirstOrDefault();
            if (variable != null)
            {
                // Add to the chain in the initializer
                var initializer = variable.Initializer;
                if (initializer != null)
                {
                    var newInitializer = AddToChain(initializer.Value, exportNames);
                    var newVariable = variable.WithInitializer(
                        initializer.WithValue(newInitializer));
                    var newDecl = localDecl.WithDeclaration(
                        localDecl.Declaration.WithVariables(
                            SyntaxFactory.SingletonSeparatedList(newVariable)));
                    
                    var newRoot = root.ReplaceNode(statement, newDecl);
                    return document.WithSyntaxRoot(newRoot);
                }
            }
        }
        else if (statement is ExpressionStatementSyntax exprStatement)
        {
            // Add to the chain directly
            var newExpression = AddToChain(exprStatement.Expression, exportNames);
            newStatement = exprStatement.WithExpression(newExpression);
            
            var newRoot = root.ReplaceNode(statement, newStatement);
            return document.WithSyntaxRoot(newRoot);
        }

        return document;
    }

    private InvocationExpressionSyntax? FindCreateCModuleInvocation(SyntaxNode node)
    {
        var current = node;
        while (current != null)
        {
            if (current is InvocationExpressionSyntax invocation &&
                invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.Text == "CreateCModule")
            {
                return invocation;
            }
            current = current.Parent;
        }
        return null;
    }

    private ExpressionSyntax AddToChain(ExpressionSyntax expression, IEnumerable<string> exportNames)
    {
        var result = expression;
        foreach (var exportName in exportNames)
        {
            result = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    result,
                    SyntaxFactory.IdentifierName("AddExport")),
                SyntaxFactory.ArgumentList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(exportName))))));
        }
        return result;
    }

    /// <summary>
    /// Custom FixAllProvider that batches all missing exports for a single CreateCModule call
    /// </summary>
    private class ModuleExportFixAllProvider : FixAllProvider
    {
        public override async Task<CodeAction?> GetFixAsync(FixAllContext fixAllContext)
        {
            var diagnosticsToFix = new List<KeyValuePair<Project, ImmutableArray<Diagnostic>>>();

            switch (fixAllContext.Scope)
            {
                case FixAllScope.Document:
                    {
                        var diagnostics = await fixAllContext.GetDocumentDiagnosticsAsync(fixAllContext.Document).ConfigureAwait(false);
                        diagnosticsToFix.Add(new KeyValuePair<Project, ImmutableArray<Diagnostic>>(fixAllContext.Project, diagnostics));
                        break;
                    }
                case FixAllScope.Project:
                    {
                        var project = fixAllContext.Project;
                        var diagnostics = await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
                        diagnosticsToFix.Add(new KeyValuePair<Project, ImmutableArray<Diagnostic>>(project, diagnostics));
                        break;
                    }
                case FixAllScope.Solution:
                    {
                        foreach (var project in fixAllContext.Solution.Projects)
                        {
                            var diagnostics = await fixAllContext.GetAllDiagnosticsAsync(project).ConfigureAwait(false);
                            if (diagnostics.Any())
                            {
                                diagnosticsToFix.Add(new KeyValuePair<Project, ImmutableArray<Diagnostic>>(project, diagnostics));
                            }
                        }
                        break;
                    }
            }

            return CodeAction.Create(
                Resources.HAKO100CodeFixTitle,
                async ct =>
                {
                    var solution = fixAllContext.Solution;

                    foreach (var projectAndDiagnostics in diagnosticsToFix)
                    {
                        var project = projectAndDiagnostics.Key;
                        var diagnostics = projectAndDiagnostics.Value;

                        // Group diagnostics by document
                        var diagnosticsByDocument = diagnostics
                            .Where(d => d.Location.IsInSource)
                            .GroupBy(d => project.GetDocument(d.Location.SourceTree))
                            .Where(g => g.Key != null);

                        foreach (var documentGroup in diagnosticsByDocument)
                        {
                            var document = documentGroup.Key!;
                            var root = await document.GetSyntaxRootAsync(ct).ConfigureAwait(false);
                            if (root == null)
                                continue;

                            // Group diagnostics by the CreateCModule call they belong to
                            var diagnosticGroups = documentGroup
                                .GroupBy(d =>
                                {
                                    var node = root.FindNode(d.Location.SourceSpan);
                                    var setExportInvocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                                    if (setExportInvocation == null)
                                        return null;

                                    var provider = (ModuleExportCodeFixProvider)fixAllContext.CodeFixProvider;
                                    return provider.FindCreateCModuleInvocation(setExportInvocation);
                                })
                                .Where(g => g.Key != null)
                                .ToList();

                            // Apply fixes for each group
                            var updatedDocument = document;
                            foreach (var group in diagnosticGroups)
                            {
                                var exportNames = new List<string>();

                                foreach (var diagnostic in group)
                                {
                                    var currentRoot = await updatedDocument.GetSyntaxRootAsync(ct).ConfigureAwait(false);
                                    if (currentRoot == null)
                                        continue;

                                    var node = currentRoot.FindNode(diagnostic.Location.SourceSpan);
                                    var setExportInvocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                                    if (setExportInvocation == null)
                                        continue;

                                    var provider = (ModuleExportCodeFixProvider)fixAllContext.CodeFixProvider;
                                    var exportName = provider.GetExportName(setExportInvocation);
                                    if (!string.IsNullOrEmpty(exportName))
                                    {
                                        exportNames.Add(exportName);
                                    }
                                }

                                if (exportNames.Count > 0)
                                {
                                    var currentRoot = await updatedDocument.GetSyntaxRootAsync(ct).ConfigureAwait(false);
                                    if (currentRoot == null)
                                        continue;

                                    // Find the first SetExport invocation in this group to use as anchor
                                    var firstDiagnostic = group.First();
                                    var node = currentRoot.FindNode(firstDiagnostic.Location.SourceSpan);
                                    var setExportInvocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
                                    if (setExportInvocation != null)
                                    {
                                        var provider = (ModuleExportCodeFixProvider)fixAllContext.CodeFixProvider;
                                        updatedDocument = await provider.AddMissingExportsAsync(
                                            updatedDocument, setExportInvocation, exportNames, ct).ConfigureAwait(false);
                                    }
                                }
                            }

                            solution = updatedDocument.Project.Solution;
                        }
                    }

                    return solution;
                },
                equivalenceKey: nameof(Resources.HAKO100CodeFixTitle));
        }
    }
}