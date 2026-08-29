// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.Result;
using EricksonLopez.Transaction.Dapper;
using EricksonLopez.Transaction.Result;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using ResultInstance = EricksonLopez.Result.Result;

namespace EricksonLopez.Transaction.Showcase.Levels;

/// <summary>
/// Level 04: Dapper Extensions &amp; Result&lt;T&gt; Monad Integration.
/// Demonstrates all Dapper extension methods on ITransactionContext and all overloads of ExecuteResultAsync.
/// </summary>
public sealed class Level4_AdvancedIntegration : ILevel
{
    public int LevelNumber => 4;
    public string Name => "Dapper Extensions & Result<T> Monad Integration";
    public string Description => "Demonstrates all 9 Dapper extension methods and all 4 ExecuteResultAsync overloads for automatic transaction rollback on functional Result.Failure.";
    public string Category => "Integration";

    public sealed class CustomerRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
    }

    public async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 04: DAPPER EXTENSIONS & RESULT<T> MONAD INTEGRATION");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        using var masterConnection = new SqliteConnection("Data Source=dapper_result;Mode=Memory;Cache=Shared");
        await masterConnection.OpenAsync(cancellationToken);

        await masterConnection.ExecuteAsync("""
            CREATE TABLE customers (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                email TEXT NOT NULL,
                credit_limit DECIMAL NOT NULL
            );
            CREATE TABLE orders_l4 (
                id TEXT PRIMARY KEY,
                customer_id TEXT NOT NULL,
                amount DECIMAL NOT NULL
            );
            INSERT INTO customers VALUES ('c1', 'John Doe', 'john@example.com', 5000.0);
            INSERT INTO customers VALUES ('c2', 'Jane Smith', 'jane@example.com', 8000.0);
            INSERT INTO orders_l4 VALUES ('o1', 'c1', 250.0);
            INSERT INTO orders_l4 VALUES ('o2', 'c2', 750.0);
            """);

        var services = new ServiceCollection();
        services.AddTransaction(_ => new SqliteConnection("Data Source=dapper_result;Mode=Memory;Cache=Shared"));

        using ServiceProvider localProvider = services.BuildServiceProvider();
        ITransactionManager transactionManager = localProvider.GetRequiredService<ITransactionManager>();

        // ─── Part 1: All 9 Dapper Extension Methods ──────────────────────────────────
        Console.WriteLine("[Part 1] All 9 Dapper Extension Methods on ITransactionContext:\n");

        await transactionManager.ExecuteAsync(async context =>
        {
            // 1. AsCommand: explicit CommandDefinition configuration
            CommandDefinition customCmd = context.AsCommand(
                "SELECT * FROM customers WHERE credit_limit >= @MinLimit;",
                new { MinLimit = 6000.0m },
                commandTimeout: 30,
                flags: CommandFlags.Buffered,
                cancellationToken: context.CancellationToken);

            Console.WriteLine($"  [1] AsCommand: CommandDefinition bound to transaction: {customCmd.Transaction is not null}");

            // 2. QueryAsync<T>
            IEnumerable<CustomerRecord> highCreditCustomers = await context.QueryAsync<CustomerRecord>(
                "SELECT id, name, email, credit_limit as CreditLimit FROM customers WHERE credit_limit >= @MinLimit;",
                new { MinLimit = 5000.0m });

            foreach (var c in highCreditCustomers)
            {
                Console.WriteLine($"  [2] QueryAsync: Found {c.Name} ({c.Email}) - Limit: ${c.CreditLimit:N2}");
            }

            // 3. QueryFirstOrDefaultAsync<T>
            CustomerRecord? john = await context.QueryFirstOrDefaultAsync<CustomerRecord>(
                "SELECT id, name, email, credit_limit as CreditLimit FROM customers WHERE id = @Id;",
                new { Id = "c1" });
            Console.WriteLine($"  [3] QueryFirstOrDefaultAsync: {john?.Name}");

            // 4. QuerySingleOrDefaultAsync<T>
            CustomerRecord? jane = await context.QuerySingleOrDefaultAsync<CustomerRecord>(
                "SELECT id, name, email, credit_limit as CreditLimit FROM customers WHERE id = @Id;",
                new { Id = "c2" });
            Console.WriteLine($"  [4] QuerySingleOrDefaultAsync: {jane?.Name}");

            // 5. QueryFirstAsync<T> (throws if no result)
            CustomerRecord john2 = await context.QueryFirstAsync<CustomerRecord>(
                "SELECT id, name, email, credit_limit as CreditLimit FROM customers WHERE id = @Id;",
                new { Id = "c1" });
            Console.WriteLine($"  [5] QueryFirstAsync: {john2.Name}");

            // 6. QuerySingleAsync<T> (throws if not exactly one result)
            CustomerRecord jane2 = await context.QuerySingleAsync<CustomerRecord>(
                "SELECT id, name, email, credit_limit as CreditLimit FROM customers WHERE id = @Id;",
                new { Id = "c2" });
            Console.WriteLine($"  [6] QuerySingleAsync: {jane2.Name}");

            // 7. ExecuteScalarAsync<T>
            int totalCustomers = await context.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM customers;");
            Console.WriteLine($"  [7] ExecuteScalarAsync: total customers = {totalCustomers}");

            // 8. ExecuteAsync (DML)
            int rowsUpdated = await context.ExecuteAsync(
                "UPDATE customers SET credit_limit = credit_limit + 0.0 WHERE id = 'c1';",
                cancellationToken: context.CancellationToken);
            Console.WriteLine($"  [8] ExecuteAsync: rows affected = {rowsUpdated}");

            // 9. QueryMultipleAsync (multi-resultset)
            await using SqlMapper.GridReader multi = await context.QueryMultipleAsync(
                "SELECT id, name FROM customers LIMIT 1; SELECT COUNT(*) as cnt FROM orders_l4;");
            CustomerRecord? firstCustomer = await multi.ReadFirstOrDefaultAsync<CustomerRecord>();
            int orderCount = await multi.ReadFirstOrDefaultAsync<int>();
            Console.WriteLine($"  [9] QueryMultipleAsync: first={firstCustomer?.Name}, orderCount={orderCount}");

            // 10. ExecuteReaderAsync (raw IDataReader — implements IDisposable, not IAsyncDisposable)
            using IDataReader reader = await context.ExecuteReaderAsync(
                "SELECT id, name FROM customers ORDER BY name;",
                cancellationToken: context.CancellationToken);
            int readerRowCount = 0;
            while (reader.Read()) { readerRowCount++; }
            Console.WriteLine($"  [10] ExecuteReaderAsync: rows iterated = {readerRowCount}");
        }, TransactionOptions.Default, cancellationToken);

        // ─── Part 2: ExecuteResultAsync — Context Overloads ──────────────────────────
        Console.WriteLine("\n[Part 2] ExecuteResultAsync — ITransactionContext-parameterized overloads:\n");

        // Overload 2a: ExecuteResultAsync(Func<ITransactionContext, Task<Result<T>>>) — SUCCESS path
        Console.WriteLine("  [2a] ExecuteResultAsync<T>(Func<ITransactionContext, Task<Result<T>>>) — SUCCESS:");
        Result<string> successResult = await transactionManager.ExecuteResultAsync<string>(async context =>
        {
            await context.ExecuteAsync(
                "INSERT INTO customers VALUES ('c3', 'Robert Davis', 'robert@example.com', 3000.0);",
                cancellationToken: context.CancellationToken);

            return Result<string>.Success("Customer c3 created successfully.");
        }, TransactionOptions.Default, cancellationToken);

        Console.WriteLine($"       IsSuccess={successResult.IsSuccess}, Value='{successResult.Value}'");
        int c3Count = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM customers WHERE id = 'c3';");
        Console.WriteLine($"       DB Verification: c3 exists = {c3Count == 1} (Committed)\n");

        // Overload 2b: ExecuteResultAsync(Func<ITransactionContext, Task<Result<T>>>) — FAILURE path
        Console.WriteLine("  [2b] ExecuteResultAsync<T>(Func<ITransactionContext, Task<Result<T>>>) — FAILURE (auto-rollback):");
        Result<string> failureResult = await transactionManager.ExecuteResultAsync<string>(async context =>
        {
            await context.ExecuteAsync(
                "INSERT INTO customers VALUES ('c4', 'Malicious User', 'invalid@spam.com', 99999.0);",
                cancellationToken: context.CancellationToken);

            Console.WriteLine("       Inserted c4 tentatively... Business validation failed!");
            return Result<string>.Failure(Error.Validation("FRAUD_LIMIT_EXCEEDED", "Credit limit exceeds maximum allowed policy."));
        }, TransactionOptions.Default, cancellationToken);

        Console.WriteLine($"       IsFailure={failureResult.IsFailure}, Error='{failureResult.Error.Code}'");
        int c4Count = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM customers WHERE id = 'c4';");
        Console.WriteLine($"       DB Verification: c4 in DB = {c4Count} (Expected: 0 — Rolled Back)\n");

        // Overload 2c: ExecuteResultAsync(Func<ITransactionContext, Task<Result>>) — non-generic Result
        Console.WriteLine("  [2c] ExecuteResultAsync(Func<ITransactionContext, Task<Result>>) — non-generic SUCCESS:");
        ResultInstance nonGenericResult = await transactionManager.ExecuteResultAsync(async context =>
        {
            await context.ExecuteAsync(
                "UPDATE customers SET credit_limit = 5100.0 WHERE id = 'c1';",
                cancellationToken: context.CancellationToken);

            return ResultInstance.Success();
        }, TransactionOptions.Default, cancellationToken);
        Console.WriteLine($"       Result.IsSuccess = {nonGenericResult.IsSuccess}");

        // ─── Part 3: ExecuteResultAsync — Parameterless Overloads ────────────────────
        Console.WriteLine("\n[Part 3] ExecuteResultAsync — Parameterless delegate overloads (no ITransactionContext param):\n");

        // Overload 3a: ExecuteResultAsync(Func<Task<Result<T>>>) — ambient context injection scenario
        Console.WriteLine("  [3a] ExecuteResultAsync<T>(Func<Task<Result<T>>>) — parameterless (uses ambient context):");
        Result<int> ambientResult = await transactionManager.ExecuteResultAsync<int>(async () =>
        {
            // The ambient context is available via transactionManager.CurrentContext
            ITransactionContext? ctx = transactionManager.CurrentContext;
            Console.WriteLine($"       Ambient context available: {ctx is not null}");
            Console.WriteLine($"       Ambient TransactionId: {ctx?.TransactionId}");

            await Task.Yield(); // Simulate async ambient work
            return Result<int>.Success(42);
        }, TransactionOptions.Default, cancellationToken);
        Console.WriteLine($"       Result: IsSuccess={ambientResult.IsSuccess}, Value={ambientResult.Value}\n");

        // Overload 3b: ExecuteResultAsync(Func<Task<Result>>) — non-generic parameterless
        Console.WriteLine("  [3b] ExecuteResultAsync(Func<Task<Result>>) — non-generic parameterless:");
        ResultInstance ambientNonGeneric = await transactionManager.ExecuteResultAsync(async () =>
        {
            await Task.Yield();
            return ResultInstance.Success();
        }, TransactionOptions.Default, cancellationToken);
        Console.WriteLine($"       ResultInstance.IsSuccess = {ambientNonGeneric.IsSuccess}");

        if (c3Count == 1 && c4Count == 0 && nonGenericResult.IsSuccess && ambientResult.Value == 42)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✔ Level 04 Dapper & Result Monad integration verified successfully.\n");
            Console.ResetColor();
        }
        else
        {
            throw new InvalidOperationException("Result monad rollback verification failed.");
        }
    }
}
