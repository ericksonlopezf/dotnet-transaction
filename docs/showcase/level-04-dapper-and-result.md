# Level 04: Dapper Extensions & Result<T> Monad Integration

> **Level:** 04 | **Category:** Integration | **Executable Reference:** [`Level4_AdvancedIntegration.cs`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/samples/Showcase/Levels/Level4_AdvancedIntegration.cs)

---

## 1. High-Performance Dapper Extension Methods

The `EricksonLopez.Transaction.Dapper` package adds fluent extensions directly onto [`ITransactionContext`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Abstractions/ITransactionContext.cs), automatically binding the active `DbConnection`, `DbTransaction`, and `CancellationToken`:

```csharp
using EricksonLopez.Transaction.Dapper;

await txManager.ExecuteAsync(async context =>
{
    // 1. AsCommand: Pre-configured CommandDefinition bound to active transaction
    CommandDefinition cmd = context.AsCommand(
        "SELECT * FROM customers WHERE credit_limit >= @MinLimit;",
        new { MinLimit = 5000.0m });

    // 2. QueryAsync<T>
    IEnumerable<CustomerRecord> customers = await context.QueryAsync<CustomerRecord>(
        "SELECT id, name, email, credit_limit AS CreditLimit FROM customers WHERE credit_limit >= @MinLimit;",
        new { MinLimit = 5000.0m });

    // 3. QueryFirstOrDefaultAsync<T>
    CustomerRecord? customer = await context.QueryFirstOrDefaultAsync<CustomerRecord>(
        "SELECT id, name, email, credit_limit AS CreditLimit FROM customers WHERE id = @Id;",
        new { Id = "c1" });

    // 4. QuerySingleOrDefaultAsync<T>
    CustomerRecord? uniqueCustomer = await context.QuerySingleOrDefaultAsync<CustomerRecord>(
        "SELECT id, name, email, credit_limit AS CreditLimit FROM customers WHERE id = @Id;",
        new { Id = "c2" });

    // 5. ExecuteScalarAsync<T>
    int count = await context.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM customers;");
});
```

---

## 2. Functional `Result<T>` Integration (`ExecuteResultAsync`)

In Functional DDD architectures using `EricksonLopez.Result`, domain operations return `Result<T>` instead of throwing exceptions. With raw transactions, returning a `Result.Failure` would mistakenly commit dirty database changes.

`EricksonLopez.Transaction.Result` introduces `ExecuteResultAsync`, which automatically inspects `Result.IsFailure` and performs a physical rollback:

```csharp
using EricksonLopez.Result;
using EricksonLopez.Transaction.Result;

Result<OrderSummary> result = await txManager.ExecuteResultAsync<OrderSummary>(async context =>
{
    // Tentative database mutation
    await context.ExecuteAsync(
        "INSERT INTO orders VALUES (@Id, @CustomerId, @Total);",
        new { Id = orderId, CustomerId = customerId, Total = total });

    // Business validation check
    if (total > creditLimit)
    {
        // Returning Failure AUTOMATICALLY rolls back the database transaction!
        return Result<OrderSummary>.Failure(Error.Validation("LIMIT_EXCEEDED", "Order total exceeds credit limit."));
    }

    // Success COMMITS the transaction
    return Result<OrderSummary>.Success(new OrderSummary(orderId, total));
}, TransactionOptions.Default, cancellationToken);
```
