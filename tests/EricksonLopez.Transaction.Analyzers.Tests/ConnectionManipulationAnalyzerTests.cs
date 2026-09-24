// Copyright © Erickson Lopez. MIT License.
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

namespace EricksonLopez.Transaction.Analyzers.Tests;

public sealed class ConnectionManipulationAnalyzerTests
{
    [Fact]
    public async Task AllowedMethods_ShouldNotTriggerDiagnostic()
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
        }
    }
}
" + GetStubs();

        await CSharpAnalyzerVerifier<ConnectionManipulationAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task CallingClose_ShouldTriggerError()
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
            context.Connection.{|#0:Close|}();
        }
    }
}
" + GetStubs();

        var expected = new DiagnosticResult(ConnectionManipulationAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("Close");
            
        await CSharpAnalyzerVerifier<ConnectionManipulationAnalyzer, DefaultVerifier>.VerifyAnalyzerAsync(test, expected);
    }
    
    [Fact]
    public async Task CallingDispose_ShouldTriggerError()
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
            context.Connection.{|#0:Dispose|}();
        }
    }
}
" + GetStubs();

        var expected = new DiagnosticResult(ConnectionManipulationAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithLocation(0)
            .WithArguments("Dispose");
            
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
