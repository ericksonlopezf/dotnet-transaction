# ADR-020: Parameterless Transaction Execution Overloads and Dapper Multi-Result Extensions

## Status
Accepted

## Context
1. In Clean Architecture / DDD implementations where repositories retrieve `ITransactionContext` from the ambient context (`ITransactionManager.CurrentContext`), requiring delegates passed to `ExecuteAsync` or `ExecuteResultAsync` to accept an unused `ITransactionContext` parameter introduces unnecessary syntactic noise (`async _ =>` or `async ctx =>`).
2. When executing complex reporting queries or loading aggregate roots with related child collections in a single round-trip, developers rely on Dapper's `QueryMultipleAsync` and `ExecuteReaderAsync`. Missing these overloads in `EricksonLopez.Transaction.Dapper` forced developers to drop down to raw `context.Connection.QueryMultipleAsync(context.AsCommand(...))`.

## Decision
1. Provide parameterless overloads across `ITransactionManager`, `TransactionManager`, `FakeTransactionManager`, and `TransactionResultExtensions`:
   - `Task ExecuteAsync(Func<Task> operation, ...)`
   - `Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation, ...)`
   - `Task<Result> ExecuteResultAsync(this ITransactionManager manager, Func<Task<Result>> operation, ...)`
   - `Task<Result<TValue>> ExecuteResultAsync<TValue>(this ITransactionManager manager, Func<Task<Result<TValue>>> operation, ...)`
2. Add full Dapper query parity extensions in `EricksonLopez.Transaction.Dapper`:
   - `QueryMultipleAsync`: returns `Task<SqlMapper.GridReader>`
   - `ExecuteReaderAsync`: returns `Task<IDataReader>`
   - `QuerySingleAsync<T>`: returns `Task<T>`
   - `QueryFirstAsync<T>`: returns `Task<T>`
3. All Dapper extensions strictly construct and forward `CommandDefinition` structs bound to the active `context.Transaction` and linked cancellation tokens.

## Consequences

### Positive
- Cleaner, more idiomatic C# code in application command handlers and use case orchestrators.
- Complete functional parity with Dapper while preserving transaction binding and cancellation propagation.
- Zero reflection and Native AOT compliant.

### Negative
- Slightly expanded public API surface in `ITransactionManager` and `TransactionDapperExtensions`.
