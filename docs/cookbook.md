# EricksonLopez.Transaction — Enterprise Integration Cookbook

> **Author:** Erickson Lopez ([ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com))  
> **Repository:** [github.com/ericksonlopezf/dotnet-transaction](https://github.com/ericksonlopezf/dotnet-transaction)  
> **Target Framework:** .NET 8.0 | .NET 9.0 | .NET 10.0 | C# 14 | Native AOT Ready  

---

## 📖 Table of Contents

1. [Recipe 01: Atomic Multi-Repository Coordination in Clean Architecture](#recipe-01-atomic-multi-repository-coordination-in-clean-architecture)
2. [Recipe 02: Functional DDD with Monadic Result Auto-Rollback](#recipe-02-functional-ddd-with-monadic-result-auto-rollback)
3. [Recipe 03: Resilient Batch Ingestion with Hierarchical Savepoints](#recipe-03-resilient-batch-ingestion-with-hierarchical-savepoints)
4. [Recipe 04: Commit Ambiguity Handling & Idempotency Reconciliation](#recipe-04-commit-ambiguity-handling--idempotency-reconciliation)
5. [Recipe 05: Transactional Outbox Pattern via Lifecycle Enlistments](#recipe-05-transactional-outbox-pattern-via-lifecycle-enlistments)
6. [Recipe 06: Entity Framework Core & Dapper Hybrid Transaction Sharing](#recipe-06-entity-framework-core--dapper-hybrid-transaction-sharing)
7. [Recipe 07: AOT-Compatible Mediator Transaction Pipeline Behavior](#recipe-07-aot-compatible-mediator-transaction-pipeline-behavior)
8. [Recipe 08: Outer Transient Error Resilience with Polly & Error Classifiers](#recipe-08-outer-transient-error-resilience-with-polly--error-classifiers)
9. [Recipe 09: Mock-Free In-Memory Unit Testing with FakeTransactionManager](#recipe-09-mock-free-in-memory-unit-testing-with-faketransactionmanager)
10. [Recipe 10: Pluggable Custom Database Dialect Engine](#recipe-10-pluggable-custom-database-dialect-engine)

---

## Recipe 01: Atomic Multi-Repository Coordination in Clean Architecture

### Problem
In a Clean Architecture application, a business use case requires updating state across two independent repositories (`AccountRepository` and `AuditRepository`). If repositories create their own `DbTransaction`, atomicity is broken and operations risk partial commits. If repositories expose raw `DbTransaction` parameters, domain logic leaks infrastructure primitives.

### Solution
Inject `ITransactionManager` into the Application Service (Use Case). The service coordinates the transaction boundary using `ExecuteAsync` and passes `ITransactionContext` into repository methods.

### Complete Code
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Dapper;

namespace MyApp.Application.UseCases;

public interface IAccountRepository
{
    Task DebitAsync(string accountId, decimal amount, ITransactionContext context);
    Task CreditAsync(string accountId, decimal amount, ITransactionContext context);
}

public sealed class TransferFundsUseCase
{
    private readonly ITransactionManager _transactionManager;
    private readonly IAccountRepository _accountRepository;

    public TransferFundsUseCase(
        ITransactionManager transactionManager,
        IAccountRepository accountRepository)
    {
        _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
    }

    public async Task TransferAsync(
        string sourceAccountId,
        string targetAccountId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);

        var options = new TransactionOptions
        {
            IsolationLevel = TransactionIsolationLevel.ReadCommitted,
            Timeout = TimeSpan.FromSeconds(10),
            TransactionName = "TransferFundsUseCase"
        };

        await _transactionManager.ExecuteAsync(async context =>
        {
            await _accountRepository.DebitAsync(sourceAccountId, amount, context);
            await _accountRepository.CreditAsync(targetAccountId, amount, context);
        }, options, cancellationToken);
    }
}
```

### Explanation
- `_transactionManager.ExecuteAsync` automatically opens an asynchronous connection via `IDbConnectionFactory`, begins a physical database transaction, and binds `context.CancellationToken` to the configured timeout.
- Both repository operations execute on the exact same physical `DbConnection` and `DbTransaction` referenced inside `context`.
- If either repository throws an exception, `ExecuteAsync` intercepts it, rolls back the transaction, disposes resources, and rethrows the exception.

### Best Practices
- **Application Layer owns boundaries**: Application services or use case handlers control transaction initiation and commitment.
- **Repositories accept `ITransactionContext`**: Never instantiate connections or call `BeginTransaction` inside repositories.
- **Pass CancellationTokens**: Repositories should bind operations to `context.CancellationToken`.

### Common Pitfalls
- ❌ Calling `context.Connection.Close()` inside a repository. This triggers Roslyn analyzer error `ELT001` because it corrupts the coordinator's state machine.
- ❌ Creating a second transaction inside a repository. Nested calls without `NestedTransactionBehavior` can lead to deadlocks.

---

## Recipe 02: Functional DDD with Monadic Result Auto-Rollback

### Problem
When adopting functional programming patterns (such as `Result<T>` from `EricksonLopez.Result`), business validation failures return `Result.Failure(...)` without throwing exceptions. In traditional transaction blocks, delegates that return normally without throwing are assumed successful and trigger an accidental `CommitAsync`, persisting corrupt or invalid domain state.

### Solution
Use `ExecuteResultAsync` from `EricksonLopez.Transaction.Result`. The coordinator inspects the returned `Result<T>`. If `Result.IsSuccess` is `true`, it commits; if `Result.IsFailure` is `true`, it triggers an automatic physical rollback.

### Complete Code
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Dapper;
using EricksonLopez.Transaction.Result;

namespace MyApp.Application.Orders;

public sealed record CreateOrderCommand(string CustomerId, decimal Amount);
public sealed record OrderConfirmation(string OrderId, decimal Amount);

public sealed class CreateOrderHandler
{
    private readonly ITransactionManager _transactionManager;

    public CreateOrderHandler(ITransactionManager transactionManager)
    {
        _transactionManager = transactionManager;
    }

    public async Task<Result<OrderConfirmation>> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        return await _transactionManager.ExecuteResultAsync<OrderConfirmation>(async context =>
        {
            // Step 1: Query customer credit limit using Dapper extensions
            decimal creditLimit = await context.ExecuteScalarAsync<decimal>(
                "SELECT credit_limit FROM customers WHERE id = @CustomerId;",
                new { command.CustomerId },
                cancellationToken: context.CancellationToken);

            // Step 2: Domain validation
            if (command.Amount > creditLimit)
            {
                // Returning Failure AUTOMATICALLY executes physical RollbackAsync!
                return Result<OrderConfirmation>.Failure(
                    Error.Validation("CREDIT_LIMIT_EXCEEDED", $"Order amount {command.Amount:C} exceeds customer limit {creditLimit:C}."));
            }

            // Step 3: Persist order
            string orderId = Guid.NewGuid().ToString("N");
            await context.ExecuteAsync(
                "INSERT INTO orders (id, customer_id, amount, status) VALUES (@orderId, @CustomerId, @Amount, 'Confirmed');",
                new { orderId, command.CustomerId, command.Amount },
                cancellationToken: context.CancellationToken);

            // Returning Success triggers physical CommitAsync
            return Result<OrderConfirmation>.Success(new OrderConfirmation(orderId, command.Amount));
        }, TransactionOptions.Default, cancellationToken);
    }
}
```

### Explanation
- `ExecuteResultAsync<T>` wraps execution in a try/finally block and inspects `IResultOutcome.IsSuccess`.
- If `Result.Failure` is returned, `_transaction.RollbackAsync()` is invoked before disposing the transaction.
- The caller receives the clean `Result<OrderConfirmation>` failure without needing try/catch blocks for domain flow control.

### Best Practices
- Use `ExecuteResultAsync` whenever business logic uses Railway-Oriented Programming (ROP) or Functional Error Handling.
- Return descriptive `Error` instances explaining the reason for failure.

### Common Pitfalls
- ❌ Using `_transactionManager.ExecuteAsync` and returning a `Result<T>` inside the delegate. `ExecuteAsync` treats any non-exceptional completion as success and commits the database transaction even if the result was a failure!

---

## Recipe 03: Resilient Batch Ingestion with Hierarchical Savepoints

### Problem
During batch ingestion of thousands of external records, a single malformed record should not abort the entire batch job. However, processing items individually in separate transactions causes excessive disk I/O and loses atomic batch header accounting.

### Solution
Open an outer transaction for the batch, and wrap each item's processing in a nested `ExecuteAsync` scope configured with `NestedTransactionBehavior.UseSavepoint`. If an item fails, only its savepoint is rolled back; valid records commit with the outer transaction.

### Complete Code
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Dapper;

namespace MyApp.Application.Batching;

public sealed record IngestionItem(string Id, string Payload, decimal Value);

public sealed class BatchIngestionService
{
    private readonly ITransactionManager _transactionManager;

    public BatchIngestionService(ITransactionManager transactionManager)
    {
        _transactionManager = transactionManager;
    }

    public async Task ProcessBatchAsync(
        string batchId,
        IEnumerable<IngestionItem> items,
        CancellationToken cancellationToken = default)
    {
        var savepointOptions = new TransactionOptions
        {
            NestedBehavior = NestedTransactionBehavior.UseSavepoint
        };

        // Outer Physical Transaction
        await _transactionManager.ExecuteAsync(async batchContext =>
        {
            await batchContext.ExecuteAsync(
                "INSERT INTO batch_headers (id, status, started_at) VALUES (@batchId, 'Processing', CURRENT_TIMESTAMP);",
                new { batchId },
                cancellationToken: batchContext.CancellationToken);

            foreach (var item in items)
            {
                try
                {
                    // Nested Scope: Creates a named savepoint (e.g. SAVEPOINT sp_xxx)
                    await _transactionManager.ExecuteAsync(async itemContext =>
                    {
                        if (item.Value < 0)
                        {
                            throw new InvalidOperationException($"Invalid negative item value: {item.Value}");
                        }

                        await itemContext.ExecuteAsync(
                            "INSERT INTO batch_items (id, batch_id, payload, value) VALUES (@Id, @batchId, @Payload, @Value);",
                            new { item.Id, batchId, item.Payload, item.Value },
                            cancellationToken: itemContext.CancellationToken);
                    }, savepointOptions, batchContext.CancellationToken);
                }
                catch (Exception ex)
                {
                    // Savepoint rolled back automatically for this item!
                    // Outer transaction remains healthy and records the item error:
                    await batchContext.ExecuteAsync(
                        "INSERT INTO batch_errors (batch_id, item_id, error_message) VALUES (@batchId, @id, @message);",
                        new { batchId, id = item.Id, message = ex.Message },
                        cancellationToken: batchContext.CancellationToken);
                }
            }

            await batchContext.ExecuteAsync(
                "UPDATE batch_headers SET status = 'Completed' WHERE id = @batchId;",
                new { batchId },
                cancellationToken: batchContext.CancellationToken);
        }, TransactionOptions.Default, cancellationToken);
    }
}
```

### Explanation
- When the inner `ExecuteAsync` detects `NestedTransactionBehavior.UseSavepoint` and finds an active transaction in `AsyncLocal`, it invokes `context.CreateSavepointAsync("sp_...")`.
- If an exception occurs, the inner scope calls `savepoint.RollbackAsync()` and releases the savepoint.
- The outer physical transaction continues uncorrupted, persisting valid items and error audit logs together upon completion.

### Best Practices
- Always catch the exception around the nested `ExecuteAsync` in the outer scope to prevent the error from bubbling up and rolling back the outer transaction.
- Use savepoints on database engines that support them (PostgreSQL, SQL Server, MySQL InnoDB, MariaDB, Oracle, SQLite WAL).

### Common Pitfalls
- ❌ Using `NestedTransactionBehavior.JoinExisting` for batch items. With `JoinExisting`, a failure in an item marks the parent transaction as rollback-only, preventing outer commitment.

---

## Recipe 04: Commit Ambiguity Handling & Idempotency Reconciliation

### Problem
During the physical `CommitAsync` phase, a network disconnect or TCP timeout can occur after the database engine has written the transaction to disk but before the client receives the acknowledgment packet. Treating this error as an automatic rollback leads to duplicate payments, double inventory deductions, and data corruption.

### Solution
Catch `TransactionCommitException` and inspect the `IsAmbiguous` flag. If `IsAmbiguous` is `true`, query an Idempotency Store or verify the business state before deciding whether to retry or report success.

### Complete Code
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Dapper;
using EricksonLopez.Transaction.Exceptions;

namespace MyApp.Application.Payments;

public interface IIdempotencyStore
{
    Task<bool> HasKeyAsync(string idempotencyKey, CancellationToken cancellationToken);
    Task RecordKeyAsync(string idempotencyKey, ITransactionContext context);
}

public sealed class ResilientPaymentProcessor
{
    private readonly ITransactionManager _transactionManager;
    private readonly IIdempotencyStore _idempotencyStore;

    public ResilientPaymentProcessor(
        ITransactionManager transactionManager,
        IIdempotencyStore idempotencyStore)
    {
        _transactionManager = transactionManager;
        _idempotencyStore = idempotencyStore;
    }

    public async Task ProcessPaymentAsync(
        string paymentId,
        string idempotencyKey,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        // 1. Pre-check idempotency key
        if (await _idempotencyStore.HasKeyAsync(idempotencyKey, cancellationToken))
        {
            // Already committed in a previous attempt
            return;
        }

        try
        {
            await _transactionManager.ExecuteAsync(async context =>
            {
                // Write idempotency key inside the transaction boundary
                await _idempotencyStore.RecordKeyAsync(idempotencyKey, context);

                await context.ExecuteAsync(
                    "INSERT INTO payments (id, amount, status) VALUES (@paymentId, @amount, 'Captured');",
                    new { paymentId, amount },
                    cancellationToken: context.CancellationToken);
            }, TransactionOptions.Default, cancellationToken);
        }
        catch (TransactionCommitException ex) when (ex.IsAmbiguous)
        {
            // Network dropped during physical COMMIT acknowledgment.
            // Check if the transaction actually succeeded on the database engine:
            bool committed = await _idempotencyStore.HasKeyAsync(idempotencyKey, cancellationToken);
            if (committed)
            {
                // Reconciled: Transaction committed despite client-side connection timeout!
                return;
            }

            // Truly failed: Re-throw to permit outer retry
            throw;
        }
    }
}
```

### Explanation
- `TransactionCommitException.IsAmbiguous` is set to `true` by the coordinator whenever the underlying driver throws during physical `CommitAsync` (e.g. TCP reset, timeout).
- Checking `_idempotencyStore.HasKeyAsync` determines whether the disk write succeeded, preventing accidental re-execution.

### Best Practices
- Store idempotency keys in the same relational database table inside the active transaction so key insertion is atomic with business mutations.
- Catch `TransactionCommitException` specifically with `when (ex.IsAmbiguous)` filter.

### Common Pitfalls
- ❌ Blindly retrying on any commit exception. Retrying an ambiguous commit without idempotency guarantees causes duplicate payments.

---

## Recipe 05: Transactional Outbox Pattern via Lifecycle Enlistments

### Problem
When business state is updated, a domain event must be published to a message broker (RabbitMQ/Kafka). If the application writes to the database and then sends the message over HTTP/AMQP, a server crash between the two operations causes data inconsistency (dual-write anomaly).

### Solution
Implement `ITransactionEnlistment`. Attach the enlistment to `context.Enlist(...)`. The `BeforeCommitAsync` hook inserts the event into the database Outbox table within the same transaction. The `AfterCommitAsync` hook signals the background publisher to flush messages.

### Complete Code
```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Dapper;

namespace MyApp.Infrastructure.Outbox;

public sealed record OutboxMessage(string Id, string EventType, string Payload, DateTime OccurredAt);

public sealed class TransactionalOutboxEnlistment : ITransactionEnlistment
{
    private readonly List<OutboxMessage> _pendingMessages = [];

    public void AddEvent<T>(T domainEvent) where T : class
    {
        _pendingMessages.Add(new OutboxMessage(
            Guid.NewGuid().ToString("N"),
            typeof(T).Name,
            JsonSerializer.Serialize(domainEvent),
            DateTime.UtcNow));
    }

    public async Task BeforeCommitAsync(ITransactionContext context, CancellationToken cancellationToken = default)
    {
        // Executes prior to physical commit within the active database transaction!
        foreach (var message in _pendingMessages)
        {
            await context.ExecuteAsync("""
                INSERT INTO outbox_messages (id, event_type, payload, status, created_at)
                VALUES (@Id, @EventType, @Payload, 'Pending', @OccurredAt);
                """,
                message,
                cancellationToken: context.CancellationToken);
        }
    }

    public Task AfterCommitAsync(ITransactionContext context, CancellationToken cancellationToken = default)
    {
        // Executes after successful durable commit — notify in-memory channel / background worker
        return Task.CompletedTask;
    }

    public Task AfterRollbackAsync(ITransactionContext context, CancellationToken cancellationToken = default)
    {
        // Transaction aborted — discard pending in-memory events
        _pendingMessages.Clear();
        return Task.CompletedTask;
    }
}
```

### Explanation
- By implementing `ITransactionEnlistment`, participants hook into the four critical lifecycle phases: `BeforeCommitAsync`, `AfterCommitAsync`, `AfterRollbackAsync`, and `OnExceptionAsync`.
- Outbox messages are inserted in `BeforeCommitAsync`, ensuring 100% atomicity with the business aggregate changes.

### Best Practices
- Keep `BeforeCommitAsync` operations lightweight (SQL writes only); avoid slow network calls to external brokers inside this hook.
- Move external network dispatching to background workers reading from the committed outbox table.

### Common Pitfalls
- ❌ Publishing to Kafka or RabbitMQ inside `BeforeCommitAsync`. If the database physical commit fails afterwards, the message has already been broadcast to downstream consumers.

---

## Recipe 06: Entity Framework Core & Dapper Hybrid Transaction Sharing

### Problem
In high-throughput systems, complex domain models are manipulated via EF Core aggregates while batch inserts or high-speed reporting use Dapper. By default, EF Core creates its own connection and transaction, making it impossible to share atomic boundaries with Dapper.

### Solution
Use `EricksonLopez.Transaction.EntityFrameworkCore`. Invoke `dbContext.UseTransactionAsync(context)` to enlist the EF Core context in the active `ITransactionContext`.

### Complete Code
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Dapper;
using EricksonLopez.Transaction.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MyApp.Application.Hybrid;

public sealed class HybridOrderService
{
    private readonly ITransactionManager _transactionManager;
    private readonly AppDbContext _dbContext;

    public HybridOrderService(ITransactionManager transactionManager, AppDbContext dbContext)
    {
        _transactionManager = transactionManager;
        _dbContext = dbContext;
    }

    public async Task ProcessHybridWorkflowAsync(string customerId, CancellationToken cancellationToken = default)
    {
        await _transactionManager.ExecuteAsync(async context =>
        {
            // 1. Instruct EF Core to enlist in the active transaction
            await _dbContext.UseTransactionAsync(context, context.CancellationToken);

            // 2. EF Core Domain Entity Mutation
            var customer = await _dbContext.Customers.FindAsync([customerId], context.CancellationToken);
            if (customer is not null)
            {
                customer.LastActiveAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(context.CancellationToken);
            }

            // 3. High-Performance Bulk Dapper Write on the exact same transaction
            await context.ExecuteAsync(
                "UPDATE analytics_counters SET total_events = total_events + 1 WHERE counter_id = 'global';",
                cancellationToken: context.CancellationToken);

        }, TransactionOptions.Default, cancellationToken);
    }
}

public sealed class Customer
{
    public string Id { get; set; } = string.Empty;
    public DateTime LastActiveAt { get; set; }
}

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Customer> Customers => Set<Customer>();
}
```

### Explanation
- `_dbContext.UseTransactionAsync(context)` invokes `dbContext.Database.UseTransactionAsync(context.Transaction)` under the hood.
- Both EF Core's `SaveChangesAsync` and Dapper's `ExecuteAsync` execute within the same physical ACID transaction.

### Best Practices
- Call `UseTransactionAsync` at the beginning of the transactional delegate before executing queries on the `DbContext`.
- Let `_transactionManager` commit the transaction; do NOT call `dbContext.Database.CommitTransactionAsync()`.

### Common Pitfalls
- ❌ Calling `dbContext.Database.BeginTransactionAsync()` inside `ExecuteAsync`. This throws because the connection already has an active transaction.

---

## Recipe 07: AOT-Compatible Mediator Transaction Pipeline Behavior

### Problem
Mediator command handlers should not manually manage transaction boundaries. However, using reflection-heavy attributes (like `[Transactional]`) breaks Native AOT compilation and causes runtime trimming exceptions in .NET 8, 9, and 10.

### Solution
Implement `ITransactionalCommand` (marker interface) and optionally `ITransactionalCommandOptions` on your command contract. Register `AddTransactionPipelineBehavior()` in DI. The pipeline behavior automatically orchestrates transactions in Native AOT without reflection.

### Complete Code
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Result;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Dapper;
using EricksonLopez.Transaction.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace MyApp.Application.MediatorRecipes;

// 1. Command definition implementing ITransactionalCommand & ITransactionalCommandOptions
public sealed record RegisterUserCommand(string UserId, string Email) 
    : ITransactionalCommand, ITransactionalCommandOptions
{
    // Strongly-typed options without reflection (Trimming-safe)
    public TransactionOptions TransactionOptions => new()
    {
        IsolationLevel = TransactionIsolationLevel.ReadCommitted,
        Timeout = TimeSpan.FromSeconds(5),
        TransactionName = nameof(RegisterUserCommand)
    };
}

// 2. Command Handler
public sealed class RegisterUserHandler : ICommandHandler<RegisterUserCommand, Result<string>>
{
    private readonly ITransactionManager _transactionManager;

    public RegisterUserHandler(ITransactionManager transactionManager)
    {
        _transactionManager = transactionManager;
    }

    public async ValueTask<Result<string>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        // Access ambient context injected by TransactionPipelineBehavior
        ITransactionContext? context = _transactionManager.CurrentContext;
        if (context is null)
        {
            return Result<string>.Failure(Error.Internal("NO_TX", "No ambient transaction context available."));
        }

        await context.ExecuteAsync(
            "INSERT INTO users (id, email) VALUES (@UserId, @Email);",
            new { command.UserId, command.Email },
            cancellationToken: context.CancellationToken);

        return Result<string>.Success(command.UserId);
    }
}

// 3. Program.cs DI Configuration
public static class ServiceRegistration
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // Registers TransactionPipelineBehavior<,> as IPipelineBehavior<,>
        services.AddTransactionPipelineBehavior();
    }
}
```

### Explanation
- `TransactionPipelineBehavior<TRequest, TResponse>` checks if `request` implements `ITransactionalCommandOptions`.
- If implemented, it passes `options.TransactionOptions` to `_transactionManager.BeginAsync()`.
- On return, if `TResponse` implements `IResultOutcome` (from `EricksonLopez.Result`), it inspects `outcome.IsSuccess` to commit or `outcome.IsFailure` to rollback automatically.

### Best Practices
- Use `ITransactionalCommandOptions` instead of `TransactionalAttribute` in modern .NET applications targeting Native AOT.
- Design command handlers to return `Result<T>` to benefit from automatic rollback without exception overhead.

### Common Pitfalls
- ❌ Using reflection to inspect attributes at runtime. This causes linker warnings under `<EnableTrimAnalyzer>true`.

---

## Recipe 08: Outer Transient Error Resilience with Polly & Error Classifiers

### Problem
Under high concurrency, PostgreSQL emits `SQLSTATE 40001` (serialization failure) or `40P01` (deadlock). If an application attempts to retry queries *inside* an active PostgreSQL transaction, PostgreSQL throws `SQLSTATE 25P02` (current transaction is aborted, commands ignored until end of transaction block).

### Solution
Resilience retry loops must wrap the **entire** transaction boundary (`ExecuteAsync`). Use dialect-specific error classifiers (e.g. `PostgreSqlErrorClassifier.IsTransient`) to filter transient exceptions for Polly retry pipelines.

### Complete Code
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Dapper;
using EricksonLopez.Transaction.PostgreSql;
using Polly;
using Polly.Retry;

namespace MyApp.Application.Resilience;

public sealed class ResilientInventoryService
{
    private readonly ITransactionManager _transactionManager;
    private readonly ResiliencePipeline _retryPipeline;

    public ResilientInventoryService(ITransactionManager transactionManager)
    {
        _transactionManager = transactionManager;

        // Configure Polly retry pipeline targeting transient database errors
        _retryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => 
                    PostgreSqlErrorClassifier.IsTransient(ex)),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential
            })
            .Build();
    }

    public async Task DeductStockResilientlyAsync(string sku, int quantity, CancellationToken cancellationToken = default)
    {
        // Wrap the ENTIRE transaction coordinator call inside the Polly resilience pipeline!
        await _retryPipeline.ExecuteAsync(async ct =>
        {
            await _transactionManager.ExecuteAsync(async context =>
            {
                int current = await context.ExecuteScalarAsync<int>(
                    "SELECT stock FROM inventory WHERE sku = @sku FOR UPDATE;",
                    new { sku },
                    cancellationToken: context.CancellationToken);

                if (current < quantity)
                {
                    throw new InvalidOperationException("Insufficient inventory available.");
                }

                await context.ExecuteAsync(
                    "UPDATE inventory SET stock = stock - @quantity WHERE sku = @sku;",
                    new { sku, quantity },
                    cancellationToken: context.CancellationToken);

            }, TransactionOptions.Serializable, ct);
        }, cancellationToken);
    }
}
```

### Explanation
- When `PostgreSqlErrorClassifier.IsTransient(ex)` returns `true`, Polly catches the error *after* the previous failed transaction has been rolled back and disposed.
- The retry starts a fresh `ExecuteAsync`, acquiring a new connection and fresh transaction.

### Best Practices
- Choose the correct classifier for your engine:
  - PostgreSQL: `PostgreSqlErrorClassifier.IsTransient`
  - SQL Server: `SqlServerErrorClassifier.IsTransient`
  - MySQL: `MySqlErrorClassifier.IsTransient`
  - MariaDB: `MariaDbErrorClassifier.IsTransient`
  - Oracle: `OracleErrorClassifier.IsTransient`
  - SQLite: `SqliteErrorClassifier.IsTransient`

### Common Pitfalls
- ❌ Retrying individual SQL statements inside an active transaction. Once an engine enters aborted state, all subsequent statements fail until physical rollback.

---

## Recipe 09: Mock-Free In-Memory Unit Testing with FakeTransactionManager

### Problem
Unit testing application use cases that depend on `ITransactionManager` often involves fragile mocking frameworks (e.g. Moq, NSubstitute) with complex setups for `BeginAsync`, `Context`, and `DisposeAsync`.

### Solution
Use `EricksonLopez.Transaction.Testing`. Replace `ITransactionManager` with `FakeTransactionManager`. Assert against `fakeManager.StartedTransactions`, `CommitCount`, and `RollbackCount` without mocking or database containers.

### Complete Code
```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction;
using EricksonLopez.Transaction.Testing;
using Xunit;
using AwesomeAssertions;

namespace MyApp.Tests.Unit;

public sealed class BillingUseCaseTests
{
    [Fact]
    public async Task ChargeCustomer_WhenSuccessful_CommitsTransaction()
    {
        // Arrange
        var fakeManager = new FakeTransactionManager();
        var sut = new BillingService(fakeManager);

        // Act
        await sut.ChargeCustomerAsync("cust-101", 150.0m);

        // Assert
        fakeManager.StartedTransactions.Should().HaveCount(1);
        FakeTransaction tx = fakeManager.StartedTransactions[0];
        tx.CommitCount.Should().Be(1);
        tx.RollbackCount.Should().Be(0);
        tx.State.Should().Be(TransactionState.Committed);
        tx.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task ChargeCustomer_WhenSimulatedCommitFails_ThrowsException()
    {
        // Arrange
        var fakeManager = new FakeTransactionManager
        {
            ExceptionToThrowOnCommit = new TimeoutException("Simulated commit timeout.")
        };
        var sut = new BillingService(fakeManager);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() => 
            sut.ChargeCustomerAsync("cust-102", 99.0m));
    }
}

public sealed class BillingService
{
    private readonly ITransactionManager _transactionManager;

    public BillingService(ITransactionManager transactionManager)
    {
        _transactionManager = transactionManager;
    }

    public async Task ChargeCustomerAsync(string customerId, decimal amount)
    {
        await _transactionManager.ExecuteAsync(async context =>
        {
            // Business logic
            await Task.Yield();
        });
    }
}
```

### Explanation
- `FakeTransactionManager` tracks every transaction created in its `StartedTransactions` collection.
- `FakeTransaction` records `CommitCount`, `RollbackCount`, and `IsDisposed`.
- Tests run in microseconds with zero network or database dependencies.

### Best Practices
- Inject `FakeTransactionManager` directly via constructor injection in unit test suites.
- Use `ExceptionToThrowOnCommit` to verify how your application handles commit exceptions.

### Common Pitfalls
- ❌ Attempting to access `fakeContext.Connection` or `fakeContext.Transaction`. Test doubles throw `NotSupportedException` on physical ADO.NET properties because they are in-memory fakes.

---

## Recipe 10: Pluggable Custom Database Dialect Engine

### Problem
When extending the framework to an unsupported relational database or enterprise proxy, savepoint syntax and read-only transaction configuration must be customized without modifying core library code.

### Solution
Implement `IDatabaseDialect` and pass your custom dialect instance to `TransactionManager` or register it in the dependency injection container.

### Complete Code
```csharp
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction;

namespace MyApp.Infrastructure.Dialects;

public sealed class CustomCockroachDbDialect : IDatabaseDialect
{
    public bool CanHandle(DbConnection connection)
    {
        // Identify provider by type or connection string
        return connection.GetType().Name.Contains("Npgsql", System.StringComparison.OrdinalIgnoreCase);
    }

    public Task ApplyReadOnlyModeAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        // Custom read-only mode SQL if applicable
        return Task.CompletedTask;
    }

    public string GetSavepointCreationSql(string savepointName) =>
        $"SAVEPOINT {savepointName};";

    public string GetSavepointRollbackSql(string savepointName) =>
        $"ROLLBACK TO SAVEPOINT {savepointName};";

    public string? GetSavepointReleaseSql(string savepointName) =>
        $"RELEASE SAVEPOINT {savepointName};";
}
```

### Explanation
- `IDatabaseDialect` defines the extension point for SQL generation of savepoint commands and read-only configurations.
- `TransactionManager` queries `dialect.CanHandle(connection)` during initialization to resolve the appropriate dialect.

### Best Practices
- Ensure SQL syntax complies strictly with the targeted database dialect specifications.
- Return `null` from `GetSavepointReleaseSql` if the database engine does not support explicit savepoint destruction (such as SQL Server).

### Common Pitfalls
- ❌ Hardcoding savepoint names into SQL commands. Always use the `savepointName` parameter provided by the coordinator.
