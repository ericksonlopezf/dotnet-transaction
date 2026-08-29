# ADR-007: Ambient Context Propagation via AsyncLocal

## Status
Accepted

## Context
When orchestrating complex domain use cases, passing `ITransactionContext` explicitly as an argument through every repository method and intermediate service can create signature pollution across the domain and application layers. Conversely, relying purely on unmanaged globals causes cross-thread race conditions and leaking transactions across concurrent requests.

## Decision
1. `TransactionManager` utilizes an `AsyncLocal<ITransactionContext?>` to maintain the active ambient transaction context across the asynchronous execution flow.
2. When `BeginAsync` or `ExecuteAsync` begins a transaction, it installs the active context into the ambient holder and wraps the returned `ITransaction` in an `AmbientTransactionScope`.
3. Upon disposal of the transaction scope, the ambient context is deterministically restored to its previous value (handling deeply nested scopes safely).
4. For maximum architectural clarity and testing flexibility, both patterns are fully supported:
   - **Explicit context passing**: Methods accept `ITransactionContext context` directly.
   - **Ambient context resolution**: Repositories or middleware query `transactionManager.CurrentContext`.

## Consequences
### Positive
- Transparent propagation across asynchronous pipelines (`async/await`) without thread-affinity issues.
- Seamless coexistence of explicit parameter passing and ambient coordinator queries.
- Clean isolation between concurrent HTTP requests and background worker tasks.

### Negative
- Parallel concurrent executions within the same transaction scope (`Task.WhenAll`) share the same ambient context, which is hazardous on a single `DbConnection`.
