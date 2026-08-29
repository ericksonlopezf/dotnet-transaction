// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Showcase.Levels;

/// <summary>
/// Level 00: Conceptual &amp; Architectural Foundations.
/// Explains why the library exists, problems with traditional approaches, and core design invariants.
/// </summary>
public sealed class Level0_Conceptual : ILevel
{
    public int LevelNumber => 0;
    public string Name => "Conceptual & Architectural Foundations";
    public string Description => "Explains why EricksonLopez.Transaction exists, comparing it with raw DbTransaction and System.Transactions.TransactionScope.";
    public string Category => "Conceptual";

    public Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 00: CONCEPTUAL & ARCHITECTURAL FOUNDATIONS");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        Console.WriteLine("""

1. What is EricksonLopez.Transaction?
-------------------------------------
EricksonLopez.Transaction is a high-performance, explicit, composable, and Native AOT-ready
transaction boundary coordinator built natively on top of ADO.NET DbConnection and DbTransaction.

It solves core consistency challenges in modern .NET 10 Clean Architecture / DDD enterprise systems:
  • Eliminates leaky transaction boundaries across Application and Infrastructure layers.
  • Provides automatic hierarchical Savepoints for nested transaction scopes.
  • Integrates seamlessly with functional Result<T> monads for auto-rollback on failure.
  • Accurately flags ambiguous commit failures (network drops vs DB aborts).
  • Emits OpenTelemetry metrics and distributed tracing activities natively.
  • Guarantees 100% Native AOT and trimming compatibility with zero reflection.

2. Comparative Matrix: Traditional Approaches vs EricksonLopez.Transaction
---------------------------------------------------------------------------
┌──────────────────────────────┬───────────────────┬──────────────────────┬──────────────────────────────┐
│ Capability                   │ Raw DbTransaction │ TransactionScope     │ EricksonLopez.Transaction    │
├──────────────────────────────┼───────────────────┼──────────────────────┼──────────────────────────────┤
│ API Paradigm                 │ Low-level ADO.NET │ Legacy Ambient DTC   │ Explicit Async Coordinator   │
│ Nested Scope Handling        │ Throws Exception  │ Escalates to 2PC/DTC │ Automatic SQL Savepoints     │
│ Result<T> Monad Integration  │ Manual inspection │ No awareness         │ Automatic Failure Rollback   │
│ Async Flow & AOT Safety      │ Native / Manual   │ Thread/Alloc issues  │ 100% Native AOT & Trimmable  │
│ Commit Ambiguity Detection   │ Generic Exception │ Generic Exception    │ IsAmbiguous explicit flag    │
│ OpenTelemetry Telemetry      │ None              │ Limited              │ Built-in Activity & Meter    │
│ Multi-Dialect Error Analysis │ Manual SQLSTATE   │ Manual SQLSTATE      │ 6 Engine Error Classifiers   │
│ Test Doubles (Fakes)         │ Complex Mocks     │ Not supported        │ FakeTransactionManager       │
└──────────────────────────────┴───────────────────┴──────────────────────┴──────────────────────────────┘

3. Architectural Scope & Boundary Invariants
--------------------------------------------
[WHAT IT DOES]
  ✔ Controls physical DbTransaction lifecycles (Begin, Commit, Rollback, Dispose).
  ✔ Manages nested execution scopes via SQL Savepoints (SAVEPOINT, ROLLBACK TO, RELEASE).
  ✔ Propagates ambient transaction contexts across async execution flows (AsyncLocal).
  ✔ Binds Dapper queries and cancellation tokens to active transactions.
  ✔ Integrates with EricksonLopez.Result for monadic failure rollbacks.
  ✔ Exports OpenTelemetry metrics and distributed tracing spans.

[WHAT IT DOES NOT DO - ANTI-PATTERNS REJECTED]
  ❌ NOT an ORM (Does not perform change tracking or SQL generation).
  ❌ NOT an Entity Unit of Work (Does not track aggregate identity maps).
  ❌ NOT an internal retry engine (Delegated to outer resilience policies).
  ❌ NOT a distributed 2PC manager (Rejects MSDTC; favors Sagas & Outbox).
""");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("✔ Level 00 conceptual foundations reviewed successfully.\n");
        Console.ResetColor();

        return Task.CompletedTask;
    }
}
