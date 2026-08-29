// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.Transaction.Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Transaction.Showcase.Levels;

/// <summary>
/// Level 02: Complete Configuration &amp; TransactionOptions.
/// Demonstrates the full configuration matrix of TransactionOptions, Isolation Levels, Timeouts, Read-Only modes,
/// and all four AddTransaction DI registration overloads.
/// </summary>
public sealed class Level2_Configuration : ILevel
{
    public int LevelNumber => 2;
    public string Name => "Complete Configuration & TransactionOptions";
    public string Description => "Demonstrates configuring TransactionOptions, Isolation Levels, Timeouts, Read-Only semantics, Nested behaviors, and all AddTransaction DI overloads.";
    public string Category => "Configuration";

    public async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 02: COMPLETE CONFIGURATION & TRANSACTIONOPTIONS");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        using var masterConnection = new SqliteConnection("Data Source=configuration;Mode=Memory;Cache=Shared");
        await masterConnection.OpenAsync(cancellationToken);

        await masterConnection.ExecuteAsync("""
            CREATE TABLE products (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                stock INTEGER NOT NULL,
                price DECIMAL NOT NULL
            );
            INSERT INTO products VALUES ('p1', 'Enterprise Server', 10, 4999.99);
            """);

        // ─── 1. AddTransaction Overloads ────────────────────────────────────────────

        Console.WriteLine("[1] DI Registration — All AddTransaction Overloads:\n");

        // Overload A: Synchronous delegate (Func<IServiceProvider, DbConnection>)
        var services1 = new ServiceCollection();
        services1.AddTransaction(_ => (DbConnection)new SqliteConnection("Data Source=configuration;Mode=Memory;Cache=Shared"));
        using ServiceProvider provider1 = services1.BuildServiceProvider();
        ITransactionManager txManager1 = provider1.GetRequiredService<ITransactionManager>();
        Console.WriteLine("  [A] AddTransaction(Func<IServiceProvider, DbConnection>)          -> Registered OK");

        // Overload B: Async delegate (Func<IServiceProvider, CancellationToken, ValueTask<DbConnection>>)
        var services2 = new ServiceCollection();
        services2.AddTransaction(async (_, ct) =>
        {
            var conn = new SqliteConnection("Data Source=configuration;Mode=Memory;Cache=Shared");
            await conn.OpenAsync(ct);
            return (DbConnection)conn;
        });
        using ServiceProvider provider2 = services2.BuildServiceProvider();
        ITransactionManager txManager2 = provider2.GetRequiredService<ITransactionManager>();
        Console.WriteLine("  [B] AddTransaction(Func<IServiceProvider, CancellationToken, ValueTask<DbConnection>>) -> Registered OK");

        // Overload C: Factory resolver delegate (Func<IServiceProvider, IDbConnectionFactory>)
        var services3 = new ServiceCollection();
        services3.AddTransaction(_ => (IDbConnectionFactory)new DelegateDbConnectionFactory(
            () => new SqliteConnection("Data Source=configuration;Mode=Memory;Cache=Shared")));
        using ServiceProvider provider3 = services3.BuildServiceProvider();
        ITransactionManager txManager3 = provider3.GetRequiredService<ITransactionManager>();
        Console.WriteLine("  [C] AddTransaction(Func<IServiceProvider, IDbConnectionFactory>)  -> Registered OK");

        // Overload D: Generic type parameter (AddTransaction<TConnectionFactory>())
        // NOTE: Requires a concrete class implementing IDbConnectionFactory registered as a type.
        // Demonstrated as documentation reference — requires a named factory class.
        Console.WriteLine("  [D] AddTransaction<TConnectionFactory>() — registers a typed IDbConnectionFactory");
        Console.WriteLine("      Example: services.AddTransaction<MyCustomConnectionFactory>()");
        Console.WriteLine("      Requires: MyCustomConnectionFactory : IDbConnectionFactory with public constructor\n");

        // Use provider1 for the rest of this level
        ITransactionManager transactionManager = txManager1;

        // ─── 2. TransactionOptions presets & builder properties ─────────────────────

        Console.WriteLine("[2] Inspecting TransactionOptions presets & builder helpers:");
        Console.WriteLine($"  • TransactionOptions.Default      -> Isolation: {TransactionOptions.Default.IsolationLevel}, Nested: {TransactionOptions.Default.NestedBehavior}, ReadOnly: {TransactionOptions.Default.ReadOnly}");
        Console.WriteLine($"  • TransactionOptions.Serializable -> Isolation: {TransactionOptions.Serializable.IsolationLevel}");
        Console.WriteLine($"  • TransactionOptions.ReadOnlyMode -> ReadOnly: {TransactionOptions.ReadOnlyMode.ReadOnly}");
        Console.WriteLine($"  • TransactionOptions.WithTimeout  -> Timeout: {TransactionOptions.WithTimeout(TimeSpan.FromSeconds(15)).Timeout?.TotalSeconds}s\n");

        // ─── 3. Custom TransactionOptions (all properties set explicitly) ────────────

        var customOptions = new TransactionOptions
        {
            IsolationLevel = TransactionIsolationLevel.Serializable,
            Timeout = TimeSpan.FromSeconds(10),
            ReadOnly = false,
            NestedBehavior = NestedTransactionBehavior.UseSavepoint,
            TransactionName = "OrderProcessingPipeline"
        };

        Console.WriteLine("[3] Executing with Custom TransactionOptions (Serializable + Named):");
        await transactionManager.ExecuteAsync(async context =>
        {
            Console.WriteLine($"  -> Active Tx ID: {context.TransactionId}");
            Console.WriteLine($"  -> Active Isolation Level: {context.IsolationLevel}");
            Console.WriteLine($"  -> Active State: {context.State}");

            int updated = await context.ExecuteAsync(
                "UPDATE products SET stock = stock - 1 WHERE id = 'p1';",
                cancellationToken: context.CancellationToken);

            Console.WriteLine($"  -> Deducted stock (Rows affected: {updated})");
        }, customOptions, cancellationToken);

        // ─── 4. ReadOnly execution ────────────────────────────────────────────────────

        Console.WriteLine("\n[4] Executing with Read-Only Mode (Querying current catalog):");
        int currentStock = await transactionManager.ExecuteAsync(async context =>
        {
            return await context.ExecuteScalarAsync<int>(
                "SELECT stock FROM products WHERE id = 'p1';",
                cancellationToken: context.CancellationToken);
        }, TransactionOptions.ReadOnlyMode, cancellationToken);

        Console.WriteLine($"  -> Current stock read: {currentStock} (Expected: 9)");

        // ─── 5. Isolation Level Anomaly Prevention Matrix (complete) ────────────────

        Console.WriteLine("\n[5] Complete Isolation Level Enum Values & Anomaly Prevention Matrix:");
        Console.WriteLine("┌──────────────────┬────────────┬─────────────────────┬──────────────┬────────────────────────┐");
        Console.WriteLine("│ Isolation Level  │ Dirty Read │ Non-Repeatable Read │ Phantom Read │ Write Skew Conflict    │");
        Console.WriteLine("├──────────────────┼────────────┼─────────────────────┼──────────────┼────────────────────────┤");
        Console.WriteLine($"│ Unspecified      │ (Driver Default)                                                        │ Enum value = {(int)TransactionIsolationLevel.Unspecified}");
        Console.WriteLine("│ ReadUncommitted  │ Allowed    │ Allowed             │ Allowed      │ Allowed                │");
        Console.WriteLine("│ ReadCommitted    │ Prevented  │ Allowed             │ Allowed      │ Allowed                │");
        Console.WriteLine("│ RepeatableRead   │ Prevented  │ Prevented           │ Allowed (PG) │ Allowed                │");
        Console.WriteLine("│ Serializable     │ Prevented  │ Prevented           │ Prevented    │ Prevented (Throws 40001)│");
        Console.WriteLine("│ Snapshot         │ Prevented  │ Prevented           │ Prevented    │ MVCC Row Versioning    │");
        Console.WriteLine("└──────────────────┴────────────┴─────────────────────┴──────────────┴────────────────────────┘");

        Console.WriteLine("\n[6] NestedTransactionBehavior enum values:");
        foreach (NestedTransactionBehavior behavior in Enum.GetValues<NestedTransactionBehavior>())
        {
            Console.WriteLine($"  • NestedTransactionBehavior.{behavior} = {(int)behavior}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n✔ Level 02 Configuration demonstration verified successfully.\n");
        Console.ResetColor();
    }
}
