# ADR-004: Commit Ambiguity and Uncertain Transaction State

## Status
Accepted

## Context
When an application calls `DbTransaction.CommitAsync()`, the database engine writes transaction log records to disk (WAL / redo log) and commits the state. However, if a network partition, TCP connection timeout, or server failover occurs before the acknowledgement (ACK) packet reaches the client application, the client receives an exception (e.g. `SocketException`, `TimeoutException`, `NpgsqlException`).

A critical architectural pitfall is assuming that receiving an exception during `CommitAsync()` implies the transaction was rolled back. In reality, the transaction state is **uncertain (ambiguous)**: the database may have committed the changes successfully.

## Decision
1. When `CommitAsync()` encounters an exception, `EricksonLopez.Transaction` captures the failure, marks the transaction state as `Failed`, and throws a specialized `TransactionCommitException` with property `IsAmbiguous = true`.
2. The framework explicitly documents that applications **MUST NOT** blindly retry non-idempotent operations upon receiving a commit failure.
3. Resilience to commit ambiguity must be achieved through:
   - **Idempotency Keys** (`EricksonLopez.Idempotency`): Ensuring that re-submitted requests can safely recognize already-committed state.
   - **Outbox Pattern** (`EricksonLopez.Outbox`): Ensuring asynchronous side-effects are reconciled by background processors rather than synchronous client retries.

## Consequences
### Positive
- Prevents catastrophic duplicate execution of financial, billing, or state-mutating operations.
- Explicit diagnostic signaling (`TransactionCommitException.IsAmbiguous`).
- Clear architectural separation between transaction coordination and idempotency reconciliation.

### Negative
- Developers must design distributed mutations with idempotency guarantees.
