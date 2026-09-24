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

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        var invocationExpr = (InvocationExpressionSyntax)context.Node;
        
        if (invocationExpr.Expression is MemberAccessExpressionSyntax memberAccessExpr)
        {
            var methodName = memberAccessExpr.Name.Identifier.Text;
            
            // Dangerous methods that bypass transaction manager state machine
            if (methodName != "Close" && 
                methodName != "Dispose" && 
                methodName != "DisposeAsync" && 
                methodName != "ChangeDatabase" &&
                methodName != "BeginTransaction" &&
                methodName != "BeginTransactionAsync")
            {
                return;
            }

            // Verify whether expression on the left of invocation (e.g. ctx.Connection)
            // is the Connection property of ITransactionContext or a DbConnection returned by it.
            var targetType = context.SemanticModel.GetTypeInfo(memberAccessExpr.Expression).Type;
            if (targetType == null)
            {
                return;
            }

            // Check if targetType is DbConnection originating from ITransactionContext
            if (targetType.Name == "DbConnection" && targetType.ContainingNamespace?.ToDisplayString() == "System.Data.Common")
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(memberAccessExpr.Expression);
                var symbol = symbolInfo.Symbol;
                
                if (symbol is IPropertySymbol propertySymbol)
                {
                    if (propertySymbol.Name == "Connection" && 
                        propertySymbol.ContainingType?.Name == "ITransactionContext" &&
                        propertySymbol.ContainingType?.ContainingNamespace?.ToDisplayString() == "EricksonLopez.Transaction")
                    {
                        var diagnostic = Diagnostic.Create(Rule, memberAccessExpr.Name.GetLocation(), methodName);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
