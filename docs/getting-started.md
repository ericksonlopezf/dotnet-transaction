# Getting Started with EricksonLopez.Transaction

> **Comprehensive Developer Guide for Architecting Relational Database Transactions in Modern .NET 8, 9, and 10.**

---

## 1. Introduction & Philosophy

`EricksonLopez.Transaction` provides a unified, explicit, and observable transaction abstraction over standard ADO.NET `DbConnection` and `DbTransaction` primitives.

Traditional transaction approaches in .NET suffer from:
- **`System.Transactions.TransactionScope`**: Heavy thread-switching overhead, accidental escalation to distributed Two-Phase Commit (MSDTC), and poor Native AOT compatibility.
- **Manual ADO.NET**: Passing `DbTransaction` handles across dozens of repository method signatures (parameter pollution) or leaking connection ownership to repositories.
- **Silent Commits on Failure**: Functional error returns (`Result.Failure`) committing corrupted database records because no exception was thrown.

`EricksonLopez.Transaction` solves these issues with:
1. **Single Application-Layer Boundary**: The application service decides when a transaction starts and finishes.
2. **Ambient Context Flow**: Active connections and transactions flow contextually via `AsyncLocal<ITransactionContext?>`.
3. **Savepoint Hierarchy**: Nested scopes map deterministically to database savepoints (`SAVEPOINT`) rather than broken ADO.NET exceptions.
4. **Monad Auto-Rollback**: Automatic rollback upon functional `Result.Failure`.
5. **Native AOT Compliance**: Zero runtime code generation, zero reflection on hot paths, 100% compatible with IL trimming.

---

## 2. Package Architecture & Selection

Select the packages suited to your application architecture:

```text
┌───────────────────────────────────────────────────────────┐
│              EricksonLopez.Transaction.Abstractions        │ (Pure BCL Contracts)
└─────────────────────────────┬─────────────────────────────┘
                              │
┌─────────────────────────────▼─────────────────────────────┐
│                 EricksonLopez.Transaction                  │ (Core Manager & Ambient Engine)
└──────┬──────────────┬───────────────┬──────────────┬──────┘
       │              │               │              │
┌──────▼──────┐┌──────▼───────┐┌──────▼──────┐┌──────▼──────┐
│  Dialect    ││ Integration  ││ Integration ││  Testing    │
│ Providers   ││   (Dapper)   ││  (Result)   ││   Doubles   │
│(PG/MSSQL/..)││              ││             ││             │
└─────────────┘└──────────────┘└─────────────┘└─────────────┘
```

---

## 3. Dependency Injection Setup

### Option A: Dialect-Specific Provider (Recommended)

Dialect packages provide pre-configured connection pooling, read-only mode SQL execution, and vendor-specific error classifiers:

```csharp
// PostgreSQL
builder.Services.AddPostgreSqlTransaction(
    connectionString: builder.Configuration.GetConnectionString("Postgres")!,
    configure: options =>
    {
        options.IsolationLevel = TransactionIsolationLevel.ReadCommitted;
        options.Timeout = TimeSpan.FromSeconds(15);
        options.SanitizeTelemetryMetadata = true;
    });

// Microsoft SQL Server
builder.Services.AddSqlServerTransaction(
    connectionString: builder.Configuration.GetConnectionString("SqlServer")!);

// SQLite
builder.Services.AddSqliteTransaction(
    connectionString: "Data Source=app.db;Cache=Shared;");
```

### Option B: Custom Connection Factory

If you manage connections manually or integrate custom pooling:

```csharp
public class TenantAwareConnectionFactory : IDbConnectionFactory
{
    private readonly ITenantProvider _tenantProvider;

    public TenantAwareConnectionFactory(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    public async ValueTask<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connStr = _tenantProvider.GetCurrentConnectionString();
        var connection = new NpgsqlConnection(connStr);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}

// Registration:
builder.Services.AddTransaction<TenantAwareConnectionFactory>();
```

---

## 4. Automatic vs. Explicit Lifecycle Management

### Mode 1: Automatic Boundary (`ExecuteAsync`) — Recommended

The recommended approach delegates lifecycle management entirely to the coordinator:

```csharp
public class OrderService
{
    private readonly ITransactionManager _transactionManager;
    private readonly IOrderRepository _orderRepo;
    private readonly IInventoryRepository _inventoryRepo;

    public OrderService(
        ITransactionManager transactionManager,
        IOrderRepository orderRepo,
        IInventoryRepository inventoryRepo)
    {
        _transactionManager = transactionManager;
        _orderRepo = orderRepo;
        _inventoryRepo = inventoryRepo;
    }

    public async Task PlaceOrderAsync(CreateOrderDto dto, CancellationToken ct)
    {
        await _transactionManager.ExecuteAsync(async context =>
        {
            // Repositories use context.Connection and context.Transaction
            await _orderRepo.InsertAsync(context, dto);
            await _inventoryRepo.DeductStockAsync(context, dto.Items);

            // Commit is executed automatically upon exiting delegate
            // Rollback is executed automatically if an unhandled exception occurs
        }, cancellationToken: ct);
    }
}
```

### Mode 2: Explicit Lifecycle (`BeginAsync`)

When dealing with asynchronous streams, message consumer acknowledgments, or legacy workflows:

```csharp
await using ITransaction transaction = await transactionManager.BeginAsync(
    TransactionOptions.Default,
    cancellationToken: ct);

try
{
    await repository1.UpdateAsync(transaction.Context);
    await repository2.InsertAsync(transaction.Context);

    // Explicit commit
    await transaction.CommitAsync(ct);
}
catch (Exception)
{
    // Explicit rollback (or automatically triggered upon DisposeAsync)
    await transaction.RollbackAsync(ct);
    throw;
}
```

---

## 5. Ecosystem Integrations

### A. Dapper Command Binding (`EricksonLopez.Transaction.Dapper`)

Eliminate manual parameter passing with fluent `AsCommand` extension methods:

```csharp
using EricksonLopez.Transaction.Dapper;

await transactionManager.ExecuteAsync(async context =>
{
    // Executes on context.Connection bound to context.Transaction
    await context.ExecuteAsync(
        "UPDATE accounts SET balance = balance - @Amount WHERE id = @Id;",
        new { Amount = 100m, Id = "acc_01" });

    // Multi-result queries
    using var multi = await context.QueryMultipleAsync(
        "SELECT * FROM orders WHERE id = @Id; SELECT * FROM order_items WHERE order_id = @Id;",
        new { Id = orderId });

    var order = await multi.ReadSingleAsync<Order>();
    var items = (await multi.ReadAsync<OrderItem>()).ToList();
});
```

### B. Entity Framework Core (`EricksonLopez.Transaction.EntityFrameworkCore`)

Enlist your `DbContext` into the ambient transaction using `UseTransactionAsync`:

```csharp
using EricksonLopez.Transaction.EntityFrameworkCore;

await transactionManager.ExecuteAsync(async context =>
{
    // Enlist DbContext in the active transaction
    await dbContext.UseTransactionAsync(context, context.CancellationToken);

    dbContext.Orders.Add(new Order { Id = orderId, Total = 100m });
    await dbContext.SaveChangesAsync(context.CancellationToken);

    // Also run raw Dapper or ADO.NET SQL on the same connection & transaction:
    await context.ExecuteAsync("INSERT INTO audit_log (msg) VALUES (@M)", new { M = "Order created" });
});
```

### C. Mediator Pipeline Behavior (`EricksonLopez.Transaction.Mediator`)

Implement `ITransactionalCommand` to enlist commands in transactions automatically. To configure custom isolation levels or timeouts in Native AOT without reflection overhead, implement `ITransactionalCommandOptions`:

```csharp
using EricksonLopez.Mediator;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Mediator;

public record CreateInvoiceCommand(string CustomerId, decimal Amount) 
    : ICommand<InvoiceResponse>, ITransactionalCommand, ITransactionalCommandOptions
{
    public TransactionOptions TransactionOptions => new()
    {
        IsolationLevel = TransactionIsolationLevel.ReadCommitted,
        Timeout = TimeSpan.FromSeconds(10)
    };
}

// Registered in DI:
builder.Services.AddTransactionPipelineBehavior();
```

---

## 6. Unit Testing Without a Database (`EricksonLopez.Transaction.Testing`)

Use `FakeTransactionManager` and `FakeTransactionContext` to write lightning-fast unit tests:

```csharp
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Testing;

[Fact]
public async Task ProcessOrder_Commits_When_Successful()
{
    // Arrange
    var fakeManager = new FakeTransactionManager();
    var service = new OrderService(fakeManager);

    // Act
    await service.PlaceOrderAsync(new CreateOrderDto());

    // Assert
    Assert.Single(fakeManager.StartedTransactions);
    var tx = fakeManager.StartedTransactions[0];
    Assert.Equal(1, tx.CommitCount);
    Assert.Equal(0, tx.RollbackCount);
    Assert.Equal(TransactionState.Committed, tx.State);
}
```

---

## 7. Next Steps

- Consult the [Cookbook](cookbook.md) for enterprise patterns (Transactional Outbox, Sagas, Idempotency).
- Read [Troubleshooting](troubleshooting.md) for diagnosing deadlocks and ambiguous commits.
- Check [Best Practices](best-practices.md) for Staff Engineer architectural recommendations.
