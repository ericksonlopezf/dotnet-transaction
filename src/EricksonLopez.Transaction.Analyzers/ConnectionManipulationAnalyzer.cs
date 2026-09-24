// Copyright © Erickson Lopez. MIT License.
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace EricksonLopez.Transaction.Analyzers;

/// <summary>
/// Enforces transaction safety by detecting and preventing direct manipulation of <c>DbConnection</c> instances managed by <c>ITransactionContext</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConnectionManipulationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Specifies the diagnostic identifier for connection manipulation violations.
    /// </summary>
    public const string DiagnosticId = "ELT001";
        
    private static readonly LocalizableString Title = "Direct manipulation of DbConnection in ITransactionContext";
    private static readonly LocalizableString MessageFormat = "Do not invoke '{0}' on the Connection property. This corrupts the transaction state machine.";
    private static readonly LocalizableString Description = "Avoid manipulating the physical connection lifecycle manually (Close, Dispose, BeginTransaction) when managed by TransactionManager.";
    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(
        DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax memberAccessExpr })
        {
            return;
        }

        var methodName = memberAccessExpr.Name.Identifier.Text;
        if (!IsDangerousMethod(methodName))
        {
            return;
        }

        var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccessExpr.Expression, context.CancellationToken);
        if (symbolInfo.Symbol is IPropertySymbol
            {
                Name: "Connection",
                ContainingType: { Name: "ITransactionContext", ContainingNamespace: { } ns }
            } && ns.ToDisplayString() == "EricksonLopez.Transaction")
        {
            var diagnostic = Diagnostic.Create(Rule, memberAccessExpr.Name.GetLocation(), methodName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsDangerousMethod(string methodName) =>
        methodName is "Close" or "Dispose" or "DisposeAsync" or "ChangeDatabase" or "BeginTransaction" or "BeginTransactionAsync";
}
