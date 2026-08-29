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
/// Level 03: Real-World Business Use Cases &amp; Explicit Lifecycles.
/// Demonstrates multi-repository coordination and explicit transaction control with BeginAsync.
/// </summary>
public sealed class Level3_RealUseCases : ILevel
{
    public int LevelNumber => 3;
    public string Name => "Real-World Business Use Cases & Explicit Lifecycles";
    public string Description => "Demonstrates multi-repository coordination in Clean Architecture and explicit lifecycle management with BeginAsync.";
    public string Category => "Intermediate";

    public async Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("  LEVEL 03: REAL-WORLD BUSINESS USE CASES & EXPLICIT LIFECYCLES");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        using var masterConnection = new SqliteConnection("Data Source=realusecases;Mode=Memory;Cache=Shared");
        await masterConnection.OpenAsync(cancellationToken);

        // Schema setup
        await masterConnection.ExecuteAsync("""
            CREATE TABLE orders (
                id TEXT PRIMARY KEY,
                customer_id TEXT NOT NULL,
                total_amount DECIMAL NOT NULL,
                status TEXT NOT NULL
            );
            CREATE TABLE inventory (
                sku TEXT PRIMARY KEY,
                stock INTEGER NOT NULL
            );
            CREATE TABLE payment_ledger (
                id TEXT PRIMARY KEY,
                order_id TEXT NOT NULL,
                amount DECIMAL NOT NULL,
                status TEXT NOT NULL
            );

            INSERT INTO inventory VALUES ('SKU-GPU-4090', 5);
            """);

        var services = new ServiceCollection();
        services.AddTransaction(_ => new SqliteConnection("Data Source=realusecases;Mode=Memory;Cache=Shared"));

        // Register repositories
        services.AddScoped<OrderRepository>();
        services.AddScoped<InventoryRepository>();
        services.AddScoped<PaymentRepository>();
        services.AddScoped<PlaceOrderUseCase>();

        using ServiceProvider localProvider = services.BuildServiceProvider();
        PlaceOrderUseCase placeOrderUseCase = localProvider.GetRequiredService<PlaceOrderUseCase>();
        ITransactionManager transactionManager = localProvider.GetRequiredService<ITransactionManager>();

        // Scenario 1: Successful multi-repository purchase
        Console.WriteLine("[Scenario 1] Successful multi-repository order placement ($1,599.99 for 2 units):");
        string orderId1 = Guid.NewGuid().ToString("N");
        await placeOrderUseCase.ExecuteAsync(orderId1, "CUST-101", "SKU-GPU-4090", quantity: 2, unitPrice: 799.99m, cancellationToken);

        int remainingStock = await masterConnection.ExecuteScalarAsync<int>("SELECT stock FROM inventory WHERE sku = 'SKU-GPU-4090';");
        int orderCount = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM orders;");
        int ledgerCount = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM payment_ledger;");

        Console.WriteLine($"  -> Remaining Stock: {remainingStock} (Expected: 3)");
        Console.WriteLine($"  -> Orders Record Count: {orderCount} (Expected: 1)");
        Console.WriteLine($"  -> Payment Ledger Count: {ledgerCount} (Expected: 1)\n");

        // Scenario 2: Explicit BeginAsync lifecycle with automatic rollback on failure
        Console.WriteLine("[Scenario 2] Explicit BeginAsync lifecycle with automatic rollback on business error:");
        string orderId2 = Guid.NewGuid().ToString("N");

        try
        {
            await using ITransaction tx = await transactionManager.BeginAsync(TransactionOptions.Default, cancellationToken);
            Console.WriteLine($"  -> Began explicit transaction '{tx.TransactionId}' (State: {tx.State})");

            var orderRepo = localProvider.GetRequiredService<OrderRepository>();
            var invRepo = localProvider.GetRequiredService<InventoryRepository>();

            await orderRepo.CreateOrderAsync(orderId2, "CUST-102", 9999.0m, tx.Context);
            await invRepo.DeductStockAsync("SKU-GPU-4090", 1, tx.Context);

            Console.WriteLine("  -> Simulating payment authorization decline...");
            throw new InvalidOperationException("Payment declined: Insufficient credit limit.");

            // CommitAsync is never reached
            // await tx.CommitAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"  -> Caught expected error: {ex.Message}");
            Console.WriteLine("  -> Transaction disposed without CommitAsync: automatic rollback performed.");
        }

        // Verify that orderId2 was NOT saved and stock remained unchanged (3)
        int order2Exists = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM orders WHERE id = @Id;", new { Id = orderId2 });
        int finalStock = await masterConnection.ExecuteScalarAsync<int>("SELECT stock FROM inventory WHERE sku = 'SKU-GPU-4090';");

        Console.WriteLine($"  -> Order2 in database: {order2Exists} (Expected: 0)");
        Console.WriteLine($"  -> Final Stock: {finalStock} (Expected: 3)");

        // Scenario 3: Explicit ITransaction.RollbackAsync (manual rollback without exception path)
        Console.WriteLine("\n[Scenario 3] Explicit ITransaction.RollbackAsync (manual programmatic rollback):");
        string orderId3 = Guid.NewGuid().ToString("N");

        await using (ITransaction tx3 = await transactionManager.BeginAsync(TransactionOptions.Default, cancellationToken))
        {
            Console.WriteLine($"  -> Transaction '{tx3.TransactionId}' started (State: {tx3.State})");

            var orderRepo3 = localProvider.GetRequiredService<OrderRepository>();
            await orderRepo3.CreateOrderAsync(orderId3, "CUST-103", 777.0m, tx3.Context);

            Console.WriteLine("  -> Order inserted into active transaction...");
            Console.WriteLine("  -> Business logic decided to abort: calling RollbackAsync explicitly...");

            // Explicit RollbackAsync call — does NOT require exception propagation
            await tx3.RollbackAsync(cancellationToken);
            Console.WriteLine($"  -> Transaction state after RollbackAsync: {tx3.State}");
        }

        int order3Exists = await masterConnection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM orders WHERE id = @Id;", new { Id = orderId3 });
        Console.WriteLine($"  -> Order3 in database after explicit rollback: {order3Exists} (Expected: 0)");

        if (order2Exists == 0 && finalStock == 3 && order3Exists == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n✔ Level 03 Real-World Use Cases verified successfully.\n");
            Console.ResetColor();
        }
        else
        {
            throw new InvalidOperationException("Rollback verification failed.");
        }
    }

    private sealed class PlaceOrderUseCase
    {
        private readonly ITransactionManager _transactionManager;
        private readonly OrderRepository _orderRepository;
        private readonly InventoryRepository _inventoryRepository;
        private readonly PaymentRepository _paymentRepository;

        public PlaceOrderUseCase(
            ITransactionManager transactionManager,
            OrderRepository orderRepository,
            InventoryRepository inventoryRepository,
            PaymentRepository paymentRepository)
        {
            _transactionManager = transactionManager;
            _orderRepository = orderRepository;
            _inventoryRepository = inventoryRepository;
            _paymentRepository = paymentRepository;
        }

        public async Task ExecuteAsync(
            string orderId,
            string customerId,
            string sku,
            int quantity,
            decimal unitPrice,
            CancellationToken cancellationToken)
        {
            decimal total = quantity * unitPrice;

            await _transactionManager.ExecuteAsync(async context =>
            {
                await _inventoryRepository.DeductStockAsync(sku, quantity, context);
                await _orderRepository.CreateOrderAsync(orderId, customerId, total, context);
                await _paymentRepository.RecordPaymentAsync(Guid.NewGuid().ToString("N"), orderId, total, context);
            }, TransactionOptions.Default, cancellationToken);
        }
    }

    private sealed class OrderRepository
    {
        public Task CreateOrderAsync(string id, string customerId, decimal total, ITransactionContext context)
        {
            return context.ExecuteAsync(
                "INSERT INTO orders (id, customer_id, total_amount, status) VALUES (@id, @customerId, @total, 'Confirmed');",
                new { id, customerId, total },
                cancellationToken: context.CancellationToken);
        }
    }

    private sealed class InventoryRepository
    {
        public async Task DeductStockAsync(string sku, int quantity, ITransactionContext context)
        {
            int currentStock = await context.ExecuteScalarAsync<int>(
                "SELECT stock FROM inventory WHERE sku = @sku;",
                new { sku },
                cancellationToken: context.CancellationToken);

            if (currentStock < quantity)
            {
                throw new InvalidOperationException($"Insufficient inventory for SKU '{sku}'. Available: {currentStock}, Requested: {quantity}");
            }

            await context.ExecuteAsync(
                "UPDATE inventory SET stock = stock - @quantity WHERE sku = @sku;",
                new { sku, quantity },
                cancellationToken: context.CancellationToken);
        }
    }

    private sealed class PaymentRepository
    {
        public Task RecordPaymentAsync(string id, string orderId, decimal amount, ITransactionContext context)
        {
            return context.ExecuteAsync(
                "INSERT INTO payment_ledger (id, order_id, amount, status) VALUES (@id, @orderId, @amount, 'Captured');",
                new { id, orderId, amount },
                cancellationToken: context.CancellationToken);
        }
    }
}
