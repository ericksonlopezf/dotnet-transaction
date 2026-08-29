# ADR-009: Dapper Integration and CommandDefinition Binding

## Status
Accepted

## Context
When executing SQL with Dapper in high-throughput applications, developers frequently forget to pass the `IDbTransaction` or `CancellationToken` to Dapper query methods, causing queries to execute on the connection outside the transaction or ignoring request timeouts.

Furthermore, constructing `CommandDefinition` structs is the recommended high-performance, low-allocation mechanism in Dapper for passing cancellation tokens, timeouts, and command flags.

## Decision
We introduce `EricksonLopez.Transaction.Dapper` providing fluent extensions on `ITransactionContext`:
1. `context.AsCommand(...)`: Builds a configured Dapper `CommandDefinition` struct automatically bound to `context.Transaction` and linking the transaction's cancellation token with any caller-supplied token.
2. `context.ExecuteAsync(...)`, `context.QueryAsync<T>(...)`, `context.QuerySingleOrDefaultAsync<T>(...)`: High-level helper methods that ensure atomic execution within the transaction.

## Consequences
### Positive
- Guaranteed transaction parameter binding on every Dapper invocation.
- Automatic cancellation token linkage.
- Zero boilerplate in repository implementations.

### Negative
- Direct Dapper extensions are located in `EricksonLopez.Transaction.Dapper`, keeping core abstractions clean.
