# Migration Guide: Upgrading to EricksonLopez.Transaction

> **Step-by-step migration strategies from legacy `System.Transactions.TransactionScope`, raw ADO.NET, EF Core transactions, and `Dapper.Transaction`.**

---

## 1. Migrating from `System.Transactions.TransactionScope`

### Why Migrate?
`TransactionScope` relies on MSDTC / OLE Transactions, which are fragile in Linux containers, incompatible with Native AOT, prone to thread-switching deadlocks in `async/await`, and inadvertently escalate single-database operations to distributed 2PC.

### Before: Legacy `TransactionScope`
```csharp
// ❌ Legacy .NET Framework / early .NET Core approach
using (var scope = new TransactionScope(
    TransactionScopeOption.Required,
    new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
    TransactionScopeAsyncFlowOption.Enabled))
{
    using (var connection = new SqlConnection(_connectionString))
    {
        await connection.OpenAsync();
        
        await connection.ExecuteAsync("UPDATE accounts SET balance = balance - 100 WHERE id = 1");
        await connection.ExecuteAsync("UPDATE accounts SET balance = balance + 100 WHERE id = 2");
        
        scope.Complete(); // Accidental 2PC escalation if another connection opened
    }
}
```

### After: Modern `EricksonLopez.Transaction`
```csharp
// ✅ Native AOT ready, zero MSDTC overhead, explicit scope
await transactionManager.ExecuteAsync(async context =>
{
    await context.Connection.ExecuteAsync(
        "UPDATE accounts SET balance = balance - 100 WHERE id = 1",
        transaction: context.Transaction);

    await context.Connection.ExecuteAsync(
        "UPDATE accounts SET balance = balance + 100 WHERE id = 2",
        transaction: context.Transaction);
}, new TransactionOptions { IsolationLevel = TransactionIsolationLevel.ReadCommitted });
```

### Key Differences & Mapping
| `TransactionScope` Concept | `EricksonLopez.Transaction` Equivalent | Notes |
|---|---|---|
| `TransactionScopeOption.Required` | `NestedTransactionBehavior.UseSavepoint` (Default) | Uses lightweight SQL Savepoints instead of DTC escalation. |
| `TransactionScopeOption.RequiresNew` | `NestedTransactionBehavior.RequireNew` | Opens an independent physical connection cleanly. |
| `TransactionScopeOption.Suppress` | `NestedTransactionBehavior.Suppress` | Suspends ambient transaction context during delegate execution. |
| `scope.Complete()` | Implicit upon successful delegate exit | Eliminates boilerplate calls; unhandled exceptions trigger rollback. |

---

## 2. Migrating from Raw ADO.NET `DbTransaction`

### Why Migrate?
Raw ADO.NET requires passing `DbConnection` and `DbTransaction` through every single repository method parameter (parameter pollution), has no built-in savepoint support, and lacks OpenTelemetry observability.

### Before: Manual Parameter Passing
```csharp
// ❌ Repositories pollute method signatures with DbTransaction
public async Task TransferFunds(string fromId, string toId, decimal amount)
{
    using var connection = new NpgsqlConnection(_connStr);
    await connection.OpenAsync();
    using var tx = await connection.BeginTransactionAsync();

    try
    {
        await _accountRepo.DebitAsync(connection, tx, fromId, amount);
        await _accountRepo.CreditAsync(connection, tx, toId, amount);
        await tx.CommitAsync();
    }
    catch
    {
        await tx.RollbackAsync();
        throw;
    }
}
```

### After: Ambient Context Coordination
```csharp
// ✅ Repositories accept ITransactionContext; connection and transaction are encapsulated
public async Task TransferFunds(string fromId, string toId, decimal amount)
{
    await _transactionManager.ExecuteAsync(async context =>
    {
        await _accountRepo.DebitAsync(context, fromId, amount);
        await _accountRepo.CreditAsync(context, toId, amount);
    });
}
```

---

## 3. Migrating from EF Core `DbContext.Database.BeginTransaction()`

### Why Migrate?
When an application uses both EF Core (for domain aggregates) and Dapper (for high-performance batch updates or reporting), EF Core's built-in transaction manager does not easily share transactions with external ADO.NET queries without manual enlistment gymnastics.

### Before: EF Core-Centric Transaction
```csharp
// ❌ Hard to coordinate with non-EF repositories or Dapper
await using var transaction = await _dbContext.Database.BeginTransactionAsync();

try
{
    _dbContext.Orders.Add(order);
    await _dbContext.SaveChangesAsync();

    // Awkward sharing with Dapper
    var dbConn = _dbContext.Database.GetDbConnection();
    var dbTx = transaction.GetDbTransaction();
    await dbConn.ExecuteAsync("INSERT INTO audit_log ...", transaction: dbTx);

    await transaction.CommitAsync();
}
catch
{
    await transaction.RollbackAsync();
    throw;
}
```

### After: Unified `EricksonLopez.Transaction.EntityFrameworkCore`
```csharp
// ✅ Unified coordinator manages the physical connection and enlists DbContext
await _transactionManager.ExecuteAsync(async context =>
{
    // Enlist DbContext in the active transaction
    _dbContext.EnlistInTransaction(context);

    _dbContext.Orders.Add(order);
    await _dbContext.SaveChangesAsync(context.CancellationToken);

    // Seamless Dapper query on the exact same connection and transaction
    await context.ExecuteAsync("INSERT INTO audit_log ...", new { ... });
});
```

---

## 4. Migrating from `Dapper.Transaction`

### Why Migrate?
`Dapper.Transaction` is an unmaintained wrapper with no nested savepoint support, no OpenTelemetry tracing, no Native AOT support, and no error classification.

### Migration Steps:
1. Replace package reference:
   ```xml
   <!-- Remove -->
   <PackageReference Include="Dapper.Transaction" Version="..." />
   
   <!-- Add -->
   <PackageReference Include="EricksonLopez.Transaction.Dapper" Version="2.0.0" />
   ```
2. Replace static extension calls with `context.ExecuteAsync(...)`, `context.QueryAsync(...)`, or `context.AsCommand(...)`.
3. Benefit from automatic connection disposal, OpenTelemetry tracing spans, and compile-time Roslyn analyzers.
