# ADR-006: Result Monad Integration with Automatic Failure Rollback

## Status
Accepted

## Context
The organizational ecosystem adopts the Result Pattern (`EricksonLopez.Result`) for functional domain error handling. Domain validation errors and expected business failures return failure result objects (`Result.Failure(error)`) instead of throwing runtime exceptions.

However, standard ADO.NET transaction control blocks (`try { ... tx.Commit(); } catch { ... tx.Rollback(); }`) only execute rollback when an exception is thrown. When an application service returns a `Result.Failure`, no exception is thrown, causing naive transaction managers to execute `Commit()` and persist invalid or partial state.

## Decision
We introduce a first-class functional bridge package `EricksonLopez.Transaction.Result` providing:
```csharp
public static async Task<Result> ExecuteResultAsync(
    this ITransactionManager manager,
    Func<ITransactionContext, Task<Result>> operation,
    TransactionOptions? options = null,
    CancellationToken cancellationToken = default);

public static async Task<Result<TValue>> ExecuteResultAsync<TValue>(
    this ITransactionManager manager,
    Func<ITransactionContext, Task<Result<TValue>>> operation,
    TransactionOptions? options = null,
    CancellationToken cancellationToken = default);
```

### Invariants:
1. If the delegate returns `Result.IsSuccess == true`, the transaction coordinator calls `CommitAsync()` and returns the successful result.
2. If the delegate returns `Result.IsFailure == true`, the transaction coordinator automatically calls `RollbackAsync()` and returns the failure result without throwing an exception.
3. If an unhandled exception or cancellation occurs during delegate execution, the transaction is rolled back and the exception propagates normally.

## Consequences
### Positive
- Prevents silent persistence of failed functional operations.
- Idiomatic C# monadic composition with zero boilerplate.
- Keeps `EricksonLopez.Transaction.Abstractions` completely decoupled from `EricksonLopez.Result`.

### Negative
- Applications must reference `EricksonLopez.Transaction.Result` to use functional execution methods.
