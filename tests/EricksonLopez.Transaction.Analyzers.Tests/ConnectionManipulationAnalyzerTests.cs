// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace EricksonLopez.Transaction.Analyzers.Tests;

public sealed class ConnectionManipulationAnalyzerTests
{
    private sealed class TestAnalysisContext : AnalysisContext
    {
        public bool ConcurrentExecutionEnabled { get; private set; }
        public GeneratedCodeAnalysisFlags GeneratedCodeFlags { get; private set; }
        public bool SyntaxNodeActionRegistered { get; private set; }

        public override void EnableConcurrentExecution() => ConcurrentExecutionEnabled = true;
        public override void ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags generatedCodeAnalysisFlags) => GeneratedCodeFlags = generatedCodeAnalysisFlags;
        public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(Action<SyntaxNodeAnalysisContext> action, ImmutableArray<TLanguageKindEnum> syntaxKinds) => SyntaxNodeActionRegistered = true;
        public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action) { }
        public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action) { }
        public override void RegisterCompilationAction(Action<CompilationAnalysisContext> action) { }
        public override void RegisterCompilationStartAction(Action<CompilationStartAnalysisContext> action) { }
        public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action) { }
        public override void RegisterSymbolAction(Action<SymbolAnalysisContext> action, ImmutableArray<SymbolKind> symbolKinds) { }
        public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action) { }
    }

    [Fact]
    public void SupportedDiagnostics_ShouldBeConfiguredCorrectly()
    {
        var analyzer = new ConnectionManipulationAnalyzer();
        var rule = analyzer.SupportedDiagnostics.Should().ContainSingle().Which;
        rule.Id.Should().Be("ELT001");
        rule.IsEnabledByDefault.Should().BeTrue();
        rule.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void Initialize_ShouldEnableConcurrentExecutionAndConfigureAnalysis()
    {
        var context = new TestAnalysisContext();
        var analyzer = new ConnectionManipulationAnalyzer();
        analyzer.Initialize(context);

        context.ConcurrentExecutionEnabled.Should().BeTrue();
        context.GeneratedCodeFlags.Should().Be(GeneratedCodeAnalysisFlags.None);
        context.SyntaxNodeActionRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task AllowedMethodsAndProperties_ShouldNotTriggerDiagnostic()
    {
        var test = @"
using System;
using System.Data.Common;
using EricksonLopez.Transaction;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod(ITransactionContext context)
        {
            var state = context.Connection.State;
            var cmd = context.Connection.CreateCommand();
            var str = context.Connection.ToString();
        }
    }
}
" + GetStubs();

        await CSharpAnalyzerVerifier<ConnectionManipulationAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task DangerousMethodOnOtherTypes_ShouldNotTriggerDiagnostic()
    {
        var test = @"
using System;
using Custom;
using Other;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod(OtherStream stream, Custom.DbConnection customConn)
        {
            stream.Close();
            customConn.Close();
        }
    }
}
" + GetStubs();

        await CSharpAnalyzerVerifier<ConnectionManipulationAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(test);
    }

    [Theory]
    [InlineData("Close", "Close()")]
    [InlineData("Dispose", "Dispose()")]
    [InlineData("DisposeAsync", "DisposeAsync()")]
    [InlineData("ChangeDatabase", "ChangeDatabase(\"test\")")]
    [InlineData("BeginTransaction", "BeginTransaction()")]
    [InlineData("BeginTransactionAsync", "BeginTransactionAsync()")]
    public async Task DangerousMethods_ShouldTriggerError(string methodName, string invocationCall)
    {
        var test = $@"
using System;
using System.Data.Common;
using EricksonLopez.Transaction;

namespace TestNamespace
{{
    class TestClass
    {{
        public void TestMethod(ITransactionContext context)
        {{
            context.Connection.{{|#0:{methodName}|}}({invocationCall.Substring(methodName.Length + 1)};
        }}
    }}
}}
" + GetStubs();

        var expected = new DiagnosticResult(ConnectionManipulationAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments(methodName);

        await CSharpAnalyzerVerifier<ConnectionManipulationAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(test, expected);
    }

    private static string GetStubs()
    {
        return @"
namespace System.Data.Common
{
    public abstract class DbConnection : IDisposable
    {
        public virtual string State => ""Open"";
        public virtual void Close() { }
        public virtual void Dispose() { }
        public virtual System.Threading.Tasks.ValueTask DisposeAsync() => default;
        public virtual void ChangeDatabase(string databaseName) { }
        public virtual object BeginTransaction() => null;
        public virtual System.Threading.Tasks.ValueTask<object> BeginTransactionAsync() => default;
        public virtual object CreateCommand() => null;
    }
}
namespace Custom
{
    public class DbConnection
    {
        public void Close() { }
    }
}
namespace Other
{
    public class OtherStream
    {
        public void Close() { }
    }
}
namespace EricksonLopez.Transaction
{
    public interface ITransactionContext
    {
        System.Data.Common.DbConnection Connection { get; }
    }
}";
    }
}
