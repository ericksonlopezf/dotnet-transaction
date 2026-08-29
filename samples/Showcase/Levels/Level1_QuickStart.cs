// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.Transaction.Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Transaction.Showcase.Levels;

/// <summary>
/// Level 01: Quick Start &amp; Minimal Setup.
/// Demonstrates DI registration and basic atomic execution with ExecuteAsync.
/// </summary>
public sealed class Level1_QuickStart : ILevel
{
    public int LevelNumber => 1;
    public string Name => "Quick Start & Minimal Setup";
    public string Description => "Demonstrates DI container registration and basic atomic transaction execution using ExecuteAsync.";
    public string Category => "Beginner";

    public async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 01: QUICK START & MINIMAL SETUP");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        // 1. Configure DI with SQLite in-memory provider
        // Note: For in-memory SQLite, keeping a shared connection open simulates persistent memory DB during lifetime
        using var masterConnection = new SqliteConnection("Data Source=quickstart;Mode=Memory;Cache=Shared");
        await masterConnection.OpenAsync(cancellationToken);

        var services = new ServiceCollection();
        services.AddTransaction(_ => new SqliteConnection("Data Source=quickstart;Mode=Memory;Cache=Shared"));

        using ServiceProvider localProvider = services.BuildServiceProvider();
        ITransactionManager transactionManager = localProvider.GetRequiredService<ITransactionManager>();

        // 2. Initialize database schema
        await masterConnection.ExecuteAsync("""
            CREATE TABLE accounts (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                balance DECIMAL NOT NULL
            );
            INSERT INTO accounts (id, name, balance) VALUES ('acc-1', 'Alice', 1000.0);
            INSERT INTO accounts (id, name, balance) VALUES ('acc-2', 'Bob', 500.0);
            """);

        Console.WriteLine("[Step 1] Initial State:");
        Console.WriteLine("  Alice: $1,000.00 | Bob: $500.00\n");

        // 3. Execute atomic transfer use case inside ExecuteAsync
        Console.WriteLine("[Step 2] Executing atomic transfer ($250 from Alice to Bob) via ITransactionManager.ExecuteAsync...");

        decimal transferAmount = 250.0m;
        string sourceAccountId = "acc-1";
        string targetAccountId = "acc-2";

        await transactionManager.ExecuteAsync(async context =>
        {
            Console.WriteLine($"  -> Active Transaction ID: {context.TransactionId}");
            Console.WriteLine($"  -> Isolation Level: {context.IsolationLevel}");
            Console.WriteLine($"  -> Transaction State: {context.State}");

            // Debit from Alice
            await context.ExecuteAsync(
                "UPDATE accounts SET balance = balance - @Amount WHERE id = @Id;",
                new { Amount = transferAmount, Id = sourceAccountId },
                cancellationToken: context.CancellationToken);

            // Credit to Bob
            await context.ExecuteAsync(
                "UPDATE accounts SET balance = balance + @Amount WHERE id = @Id;",
                new { Amount = transferAmount, Id = targetAccountId },
                cancellationToken: context.CancellationToken);

            Console.WriteLine("  -> Both debit and credit executed on active DbTransaction.");
        }, TransactionOptions.Default, cancellationToken);

        // 4. Verify post-transaction balances
        decimal aliceBalance = await masterConnection.ExecuteScalarAsync<decimal>("SELECT balance FROM accounts WHERE id = 'acc-1';");
        decimal bobBalance = await masterConnection.ExecuteScalarAsync<decimal>("SELECT balance FROM accounts WHERE id = 'acc-2';");

        Console.WriteLine("\n[Step 3] Post-Commit Verification:");
        Console.WriteLine($"  Alice: ${aliceBalance:N2} (Expected: $750.00)");
        Console.WriteLine($"  Bob:   ${bobBalance:N2} (Expected: $750.00)");

        if (aliceBalance == 750.0m && bobBalance == 750.0m)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✔ Level 01 Quick Start demonstration verified successfully.\n");
            Console.ResetColor();
        }
        else
        {
            throw new InvalidOperationException("Balance mismatch after transfer.");
        }
    }
}
