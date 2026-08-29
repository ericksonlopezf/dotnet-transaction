# ADR-021: Asynchronous Execution Safety and `Task.WhenAll` Ambient Context Invariants

## Status
Accepted

## Context
In modern .NET applications, `AsyncLocal<T>` allows execution context values to flow automatically down asynchronous call trees (`await Task.Yield()`, asynchronous I/O continuations). However, when developers spawn concurrent asynchronous operations on the same logical execution tree using `Task.WhenAll` or `Parallel.ForEachAsync`, child tasks share a copy of the ambient `ITransactionContext`.

In relational database ADO.NET drivers (Npgsql, Microsoft.Data.SqlClient, MySqlConnector, Microsoft.Data.Sqlite, Oracle), a physical `DbConnection` is **NOT thread-safe** and does not support concurrent active commands on a single connection instance. Executing concurrent queries across parallel tasks on the same `DbConnection` throws driver exceptions (such as `InvalidOperationException: An operation is already in progress` or data stream corruption).

## Decision
We establish explicit architectural invariants and guidelines for asynchronous execution safety:

1. **Single-threaded Active Command Rule**: A single physical transaction context (`ITransactionContext`) must only execute sequential database commands. Developers must `await` each database command sequentially.
2. **`Task.WhenAll` Invariant**:
   - Spawning parallel database operations within the same transactional `ExecuteAsync` scope is an anti-pattern and architecturally prohibited.
   - If parallel independent queries are required, each parallel task must obtain its own independent connection/transaction via `NestedTransactionBehavior.RequireNew` or execute non-transactionally via `NestedTransactionBehavior.Suppress`.
3. **Ambient Context Isolation**:
   - Background tasks (fire-and-forget or `Task.Run`) do not inherit ambient transaction boundaries unless explicitly passed, and transaction managers isolate ambient scopes per asynchronous call tree.
   - Upon scope disposal, `AmbientTransactionScope` and `SuppressedTransactionScope` restore the parent context deterministically.

## Consequences

### Positive
- Prevents subtle driver corruption and concurrency race conditions in production workloads.
- Explicit, unambiguous documentation and architectural boundaries for developers and automated code analyzers.
- Preserves Native AOT safety with zero runtime lock contention overhead on single-connection transactions.

### Negative
- Developers must be aware of ADO.NET connection single-concurrency rules when using parallel LINQ or `Task.WhenAll`.
