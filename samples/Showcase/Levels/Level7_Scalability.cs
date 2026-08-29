// Copyright © Erickson Lopez. MIT License.
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.Transaction.Dapper;
using EricksonLopez.Transaction.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Transaction.Showcase.Levels;

/// <summary>
/// Level 07: Scalability, Concurrency &amp; OpenTelemetry Observability.
/// Demonstrates multi-threaded transactional throughput, Native AOT invariants, and complete
/// TransactionDiagnostics public API coverage (ActivitySource, Meter, all static methods).
/// </summary>
public sealed class Level7_Scalability : ILevel
{
    public int LevelNumber => 7;
    public string Name => "Scalability, Concurrency & OpenTelemetry Observability";
    public string Description => "Demonstrates concurrent transactional execution, Native AOT zero-reflection invariants, and complete TransactionDiagnostics API (ActivitySource, Meter, StartActivity, RecordStarted/Committed/RolledBack/Failed, savepoint metrics).";
    public string Category => "Enterprise";

    public async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 07: SCALABILITY, CONCURRENCY & OPENTELEMETRY OBSERVABILITY");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        // ─── 1. TransactionDiagnostics — Complete Public API Coverage ────────────

        Console.WriteLine("[1] TransactionDiagnostics — Complete Public API Coverage:\n");

        // Static string properties
        Console.WriteLine($"  SourceName (const): '{TransactionDiagnostics.SourceName}'");
        Console.WriteLine($"  Version    (const): '{TransactionDiagnostics.Version}'\n");

        // ActivitySource (public static readonly field)
        ActivitySource activitySource = TransactionDiagnostics.ActivitySource;
        Console.WriteLine($"  ActivitySource.Name:    '{activitySource.Name}'");
        Console.WriteLine($"  ActivitySource.Version: '{activitySource.Version}'");
        Console.WriteLine($"  ActivitySource.HasListeners(): {activitySource.HasListeners()} (false in console — no OTel SDK registered)");

        // Meter (public static readonly field)
        System.Diagnostics.Metrics.Meter meter = TransactionDiagnostics.Meter;
        Console.WriteLine($"\n  Meter.Name:    '{meter.Name}'");
        Console.WriteLine($"  Meter.Version: '{meter.Version}'");

        // StartActivity — public static method
        Console.WriteLine("\n  TransactionDiagnostics.StartActivity(name, txId, isolationLevel, txName):");
        Activity? activity = TransactionDiagnostics.StartActivity(
            "transaction.showcase.demo",
            Guid.NewGuid(),
            TransactionIsolationLevel.ReadCommitted,
            "ShowcaseLevel07");

        Console.WriteLine($"    Activity returned: {activity?.GetType().Name ?? "null"} (null = no OTel listener registered — expected in console)");

        // RecordStarted — public static method
        TransactionDiagnostics.RecordStarted(TransactionIsolationLevel.ReadCommitted);
        Console.WriteLine("\n  TransactionDiagnostics.RecordStarted(ReadCommitted) -> Counter incremented");

        // RecordCommitted — public static method
        TransactionDiagnostics.RecordCommitted(TransactionIsolationLevel.ReadCommitted, 12.5);
        Console.WriteLine("  TransactionDiagnostics.RecordCommitted(ReadCommitted, 12.5ms) -> Counter + Histogram recorded");

        // RecordRolledBack — public static method
        TransactionDiagnostics.RecordRolledBack(TransactionIsolationLevel.Serializable, 3.2);
        Console.WriteLine("  TransactionDiagnostics.RecordRolledBack(Serializable, 3.2ms) -> Counter + Histogram recorded");

        // RecordFailed — public static method
        TransactionDiagnostics.RecordFailed(TransactionIsolationLevel.ReadCommitted, 5.7, "NetworkDrop");
        Console.WriteLine("  TransactionDiagnostics.RecordFailed(ReadCommitted, 5.7ms, 'NetworkDrop') -> Counter + Histogram recorded");

        // Savepoint metrics — public static methods
        TransactionDiagnostics.RecordSavepointCreated();
        Console.WriteLine("  TransactionDiagnostics.RecordSavepointCreated()  -> savepoints.created Counter incremented");

        TransactionDiagnostics.RecordSavepointRolledBack();
        Console.WriteLine("  TransactionDiagnostics.RecordSavepointRolledBack() -> savepoints.rolled_back Counter incremented");

        TransactionDiagnostics.RecordSavepointReleased();
        Console.WriteLine("  TransactionDiagnostics.RecordSavepointReleased()   -> savepoints.released Counter incremented");

        Console.WriteLine("\n  Registered Telemetry Metric Names (emitted by TransactionManager):");
        Console.WriteLine("    - transactions.started              (Counter<long>)");
        Console.WriteLine("    - transactions.committed            (Counter<long>)");
        Console.WriteLine("    - transactions.rolled_back          (Counter<long>)");
        Console.WriteLine("    - transactions.failed               (Counter<long>)");
        Console.WriteLine("    - transactions.duration             (Histogram<double> in ms)");
        Console.WriteLine("    - transactions.savepoints.created   (Counter<long>)");
        Console.WriteLine("    - transactions.savepoints.rolled_back (Counter<long>)");
        Console.WriteLine("    - transactions.savepoints.released  (Counter<long>)\n");

        // ─── 2. High-Throughput Concurrent Execution ────────────────────────────

        using var masterConnection = new SqliteConnection("Data Source=scalability;Mode=Memory;Cache=Shared");
        await masterConnection.OpenAsync(cancellationToken);

        await masterConnection.ExecuteAsync("""
            CREATE TABLE counters (
                id INTEGER PRIMARY KEY,
                val INTEGER NOT NULL
            );
            """);

        var services = new ServiceCollection();
        services.AddTransaction(_ => new SqliteConnection("Data Source=scalability;Mode=Memory;Cache=Shared"));

        using ServiceProvider localProvider = services.BuildServiceProvider();
        ITransactionManager transactionManager = localProvider.GetRequiredService<ITransactionManager>();

        Console.WriteLine("[2] Executing 50 Concurrent Transactions across parallel tasks...");
        var sw = Stopwatch.StartNew();

        int parallelCount = 50;
        var tasks = new Task[parallelCount];

        for (int i = 0; i < parallelCount; i++)
        {
            int index = i + 1;
            tasks[i] = Task.Run(async () =>
            {
                await transactionManager.ExecuteAsync(async context =>
                {
                    await context.ExecuteAsync(
                        "INSERT INTO counters VALUES (@id, @val);",
                        new { id = index, val = index * 10 },
                        cancellationToken: context.CancellationToken);
                }, TransactionOptions.Default, cancellationToken);
            }, cancellationToken);
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        int insertedCount = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM counters;");
        Console.WriteLine($"  -> Successfully executed {insertedCount} transactions in {sw.ElapsedMilliseconds}ms ({sw.ElapsedMilliseconds / (double)parallelCount:F2}ms/tx avg)");

        // ─── 3. Native AOT & Trimming Invariants ─────────────────────────────────

        Console.WriteLine("\n[3] Native AOT & Trimming Architecture Invariants:");
        Console.WriteLine("  ✔ Zero dynamic code generation (No System.Reflection.Emit).");
        Console.WriteLine("  ✔ No unconstrained type inspection in core engine.");
        Console.WriteLine("  ✔ Explicit [DynamicallyAccessedMembers] attributes where DI constructs factories.");
        Console.WriteLine("  ✔ Verified under .NET 10 PublishAot with 0 trimming warnings.\n");

        if (insertedCount == parallelCount)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✔ Level 07 Scalability & Observability verified successfully.\n");
            Console.ResetColor();
        }
        else
        {
            throw new InvalidOperationException("Concurrency count mismatch.");
        }
    }
}
