
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HakoJS.Analyzers
{
    /// <summary>
    /// An analyzer that detects when SetExport is called in a module initializer
    /// but the corresponding AddExport call is missing on the CModule.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ModuleExportAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "HAKO100";
        private const string Category = "Usage";

        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(Resources.HAKO100Title), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(
            nameof(Resources.HAKO100MessageFormat), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString Description = new LocalizableResourceString(
            nameof(Resources.HAKO100Description), Resources.ResourceManager, typeof(Resources));

        private static readonly DiagnosticDescriptor Rule = new(
            DiagnosticId,
            Title,
            MessageFormat,
            Category,
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            // Look for CreateCModule calls
            if (!IsCreateCModuleCall(invocation, context.SemanticModel))
                return;

            // Extract the lambda/delegate passed to CreateCModule (second argument)
            var arguments = invocation.ArgumentList?.Arguments;
            if (arguments == null || arguments.Value.Count < 2)
                return;

            var initializerArg = arguments.Value[1].Expression;

            // Find all SetExport calls in the initializer
            var setExportCalls = FindSetExportCalls(initializerArg);
            if (setExportCalls.Count == 0)
                return;

            // Find all AddExport/AddExports calls chained after CreateCModule
            var declaredExports = FindDeclaredExports(invocation, context.SemanticModel);

            // Check for missing exports
            foreach (var (exportName, location) in setExportCalls)
            {
                if (!declaredExports.Contains(exportName))
                {
                    var diagnostic = Diagnostic.Create(Rule, location, exportName);
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        private bool IsCreateCModuleCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(invocation);
            var method = symbolInfo.Symbol as IMethodSymbol;

            return method?.Name == "CreateCModule" &&
                   method.ContainingType?.Name == "HakoRuntime";
        }

        private List<(string ExportName, Location Location)> FindSetExportCalls(SyntaxNode initializerNode)
        {
            var exports = new List<(string, Location)>();

            if (initializerNode is LambdaExpressionSyntax lambda)
            {
                var invocations = lambda.DescendantNodes().OfType<InvocationExpressionSyntax>();
                foreach (var invocation in invocations)
                {
                    if (IsSetExportCall(invocation, out var exportName))
                    {
                        exports.Add((exportName, invocation.GetLocation()));
                    }
                }
            }

            return exports;
        }

        private bool IsSetExportCall(InvocationExpressionSyntax invocation, out string exportName)
        {
            exportName = string.Empty;

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                (memberAccess.Name.Identifier.Text == "SetExport" ||
                 memberAccess.Name.Identifier.Text == "SetFunction" ||
                 memberAccess.Name.Identifier.Text == "SetClass" ||
                 memberAccess.Name.Identifier.Text == "CompleteClassExport"))
            {
                var args = invocation.ArgumentList?.Arguments;
                if (args != null && args.Value.Count > 0)
                {
                    var firstArg = args.Value[0].Expression;

                    if (firstArg is LiteralExpressionSyntax literal &&
                        literal.Token.Value is string name)
                    {
                        exportName = name;
                        return true;
                    }
                }
            }

            return false;
        }

        private HashSet<string> FindDeclaredExports(InvocationExpressionSyntax createModuleCall,
            SemanticModel semanticModel)
        {
            var exports = new HashSet<string>();

            var statement = createModuleCall.FirstAncestorOrSelf<StatementSyntax>();
            if (statement == null)
                return exports;

            if (statement is LocalDeclarationStatementSyntax localDecl)
            {
                var variable = localDecl.Declaration.Variables.FirstOrDefault();
                if (variable?.Initializer?.Value != null)
                {
                    FindAddExportInChain(variable.Initializer.Value, exports);

                    var variableName = variable.Identifier.Text;
                    var block = statement.FirstAncestorOrSelf<BlockSyntax>();
                    if (block != null)
                    {
                        FindAddExportCallsOnVariable(block, variableName, exports);
                    }
                }
            }
            else if (statement is ExpressionStatementSyntax exprStatement)
            {
                FindAddExportInChain(exprStatement.Expression, exports);
            }

            return exports;
        }

        private void FindAddExportInChain(SyntaxNode node, HashSet<string> exports)
        {
            var current = node;
            while (current != null)
            {
                if (current is InvocationExpressionSyntax invocation &&
                    invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var methodName = memberAccess.Name.Identifier.Text;

                    if (methodName == "AddExport")
                    {
                        ExtractExportName(invocation, exports);
                    }
                    else if (methodName == "AddExports")
                    {
                        ExtractExportNames(invocation, exports);
                    }

                    current = memberAccess.Expression;
                }
                else
                {
                    break;
                }
            }
        }

        private void FindAddExportCallsOnVariable(BlockSyntax block, string variableName,
            HashSet<string> exports)
        {
            var invocations = block.DescendantNodes().OfType<InvocationExpressionSyntax>();

            foreach (var invocation in invocations)
            {
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    var methodName = memberAccess.Name.Identifier.Text;

                    if (methodName == "AddExport" || methodName == "AddExports")
                    {
                        var target = GetInvocationTarget(memberAccess.Expression);
                        if (target == variableName)
                        {
                            if (methodName == "AddExport")
                            {
                                ExtractExportName(invocation, exports);
                            }
                            else
                            {
                                ExtractExportNames(invocation, exports);
                            }
                        }
                    }
                }
            }
        }

        private string? GetInvocationTarget(ExpressionSyntax expression)
        {
            while (expression is MemberAccessExpressionSyntax memberAccess)
            {
                expression = memberAccess.Expression;
            }

            if (expression is InvocationExpressionSyntax invocation)
            {
                return GetInvocationTarget(invocation.Expression);
            }

            if (expression is IdentifierNameSyntax identifier)
            {
                return identifier.Identifier.Text;
            }

            return null;
        }

        private void ExtractExportName(InvocationExpressionSyntax invocation, HashSet<string> exports)
        {
            var args = invocation.ArgumentList?.Arguments;
            if (args != null && args.Value.Count > 0)
            {
                var firstArg = args.Value[0].Expression;
                if (firstArg is LiteralExpressionSyntax literal &&
                    literal.Token.Value is string name)
                {
                    exports.Add(name);
                }
            }
        }

        private void ExtractExportNames(InvocationExpressionSyntax invocation, HashSet<string> exports)
        {
            var args = invocation.ArgumentList?.Arguments;
            if (args != null)
            {
                foreach (var arg in args.Value)
                {
                    if (arg.Expression is LiteralExpressionSyntax literal &&
                        literal.Token.Value is string name)
                    {
                        exports.Add(name);
                    }
                }
            }
        }
    }
}