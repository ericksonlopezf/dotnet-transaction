// Copyright © Erickson Lopez. MIT License.
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using EricksonLopez.Transaction.Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace EricksonLopez.Transaction.Showcase.Levels;

/// <summary>
/// Level 10: Enterprise Architecture: Dual-Write Outbox &amp; Idempotency.
/// Demonstrates solving the distributed dual-write problem via Transactional Outbox and Idempotency key protection.
/// </summary>
public sealed class Level10_EnterpriseArchitecture : ILevel
{
    public int LevelNumber => 10;
    public string Name => "Enterprise Architecture: Dual-Write Outbox & Idempotency";
    public string Description => "Demonstrates solving the distributed Dual-Write problem with Transactional Outbox and Idempotency key atomicity.";
    public string Category => "Architecture";

    public sealed record OutboxMessage(string Id, string EventType, string Payload, string Status, DateTime CreatedAtUtc);
    public sealed record OrderPlacedEvent(string OrderId, string CustomerId, decimal Amount, DateTime TimestampUtc);

    public async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 10: ENTERPRISE ARCHITECTURE: DUAL-WRITE OUTBOX & IDEMPOTENCY");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        using var masterConnection = new SqliteConnection("Data Source=enterprise_arch;Mode=Memory;Cache=Shared");
        await masterConnection.OpenAsync(cancellationToken);

        // Schema setup
        await masterConnection.ExecuteAsync("""
            CREATE TABLE orders (
                id TEXT PRIMARY KEY,
                customer_id TEXT NOT NULL,
                total_amount DECIMAL NOT NULL,
                status TEXT NOT NULL
            );
            CREATE TABLE outbox_messages (
                id TEXT PRIMARY KEY,
                event_type TEXT NOT NULL,
                payload TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at_utc TEXT NOT NULL
            );
            CREATE TABLE idempotency_keys (
                key TEXT PRIMARY KEY,
                target_id TEXT NOT NULL,
                created_at_utc TEXT NOT NULL
            );
            """);

        var services = new ServiceCollection();
        services.AddTransaction(_ => new SqliteConnection("Data Source=enterprise_arch;Mode=Memory;Cache=Shared"));

        using ServiceProvider localProvider = services.BuildServiceProvider();
        ITransactionManager transactionManager = localProvider.GetRequiredService<ITransactionManager>();

        Console.WriteLine("""
1. The Distributed Dual-Write Problem
-------------------------------------
Writing to a database AND directly publishing to a message broker (RabbitMQ/Kafka) in memory
violates atomicity:
  • If DB commits and network fails before publish -> Message lost forever.
  • If message publishes and DB fails -> Downstream services process phantom state.

2. Solution: Atomic Dual-Write via Transactional Outbox
-------------------------------------------------------
Persist business state AND outbox event record within the SAME DbTransaction boundary:
""");

        string orderId = "ord-9001";
        string customerId = "cust-500";
        decimal orderAmount = 2499.0m;
        string idempotencyKey = "req-idempotency-abc-123";

        await transactionManager.ExecuteAsync(async context =>
        {
            Console.WriteLine($"  -> [Step 1] Guarding request with Idempotency Key '{idempotencyKey}'...");
            await context.ExecuteAsync(
                "INSERT INTO idempotency_keys VALUES (@key, @orderId, @createdAt);",
                new { key = idempotencyKey, orderId, createdAt = DateTime.UtcNow.ToString("O") },
                cancellationToken: context.CancellationToken);

            Console.WriteLine($"  -> [Step 2] Persisting Order '{orderId}' for ${orderAmount:N2}...");
            await context.ExecuteAsync(
                "INSERT INTO orders VALUES (@orderId, @customerId, @orderAmount, 'Confirmed');",
                new { orderId, customerId, orderAmount },
                cancellationToken: context.CancellationToken);

            Console.WriteLine("  -> [Step 3] Storing Domain Event in Transactional Outbox...");
            var evt = new OrderPlacedEvent(orderId, customerId, orderAmount, DateTime.UtcNow);
            string serializedPayload = JsonSerializer.Serialize(evt);

            await context.ExecuteAsync("""
                INSERT INTO outbox_messages (id, event_type, payload, status, created_at_utc)
                VALUES (@id, @eventType, @payload, 'Pending', @createdAt);
                """,
                new
                {
                    id = Guid.NewGuid().ToString("N"),
                    eventType = nameof(OrderPlacedEvent),
                    payload = serializedPayload,
                    createdAt = DateTime.UtcNow.ToString("O")
                },
                cancellationToken: context.CancellationToken);

            Console.WriteLine("  -> [Step 4] Both Order and Outbox Message successfully written to active DbTransaction.");
        }, TransactionOptions.Default, cancellationToken);

        // Verification
        int orderCount = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM orders WHERE id = @orderId;", new { orderId });
        int outboxCount = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM outbox_messages;");
        int keyCount = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM idempotency_keys WHERE key = @key;", new { key = idempotencyKey });

        Console.WriteLine("\n[Post-Commit Verification]:");
        Console.WriteLine($"  • Orders in DB:           {orderCount} (Expected: 1)");
        Console.WriteLine($"  • Outbox Messages in DB:  {outboxCount} (Expected: 1)");
        Console.WriteLine($"  • Idempotency Keys in DB: {keyCount} (Expected: 1)");

        Console.WriteLine("""

3. Clean Architecture Ownership Rules:
--------------------------------------
  ✔ TransactionManager coordinates boundaries in Application Layer (Use Cases / Handlers).
  ✔ Repositories receive ITransactionContext and execute atomic SQL operations.
  ✔ Outer resilience policies wrap the entire ExecuteAsync block to handle transient retry.
""");

        if (orderCount == 1 && outboxCount == 1 && keyCount == 1)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✔ Level 10 Enterprise Architecture verified successfully.\n");
            Console.ResetColor();
        }
        else
        {
            throw new InvalidOperationException("Enterprise architecture verification failed.");
        }
    }
}
