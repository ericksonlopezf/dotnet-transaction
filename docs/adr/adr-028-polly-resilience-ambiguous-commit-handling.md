# ADR-028: Polly Resilience Extensions for Ambiguous Commit Handling

## Status
Accepted

## Date
2026-09-14

## Context
In ADR-005 and ADR-011, we rejected building an internal retry engine inside `ITransactionManager`. Retrying individual SQL statements inside an aborted database transaction violates relational transaction semantics (e.g. PostgreSQL `SQLSTATE 25P02`), and transient retries must encapsulate the entire outer transaction boundary from `BeginAsync`.

Furthermore, ADR-004 identified the commit ambiguity hazard: when network partitions or socket dropouts occur during physical `COMMIT`, client ADO.NET drivers throw transient socket exceptions. At this point, the application cannot ascertain whether the transaction committed durably on the database server or aborted, leading to `TransactionCommitException.IsAmbiguous = true`. Naive retries could trigger duplicate writes unless guarded by idempotency.

## Decision
We introduce `EricksonLopez.Transaction.Resilience` containing targeted Polly policy extensions:

1. **Explicit Policy Target**: Provide `PollyTransactionExtensions.HandleAmbiguousCommit()` extending Polly's `PolicyBuilder`:
   ```csharp
   public static PolicyBuilder HandleAmbiguousCommit(this PolicyBuilder policyBuilder)
   {
       return policyBuilder.Or<TransactionCommitException>(ex => ex.IsAmbiguous);
   }

   public static PolicyBuilder HandleAmbiguousCommit()
   {
       return Policy.Handle<TransactionCommitException>(ex => ex.IsAmbiguous);
   }
   ```
2. **Separation of Concerns**: The policy builder does not mandate how the application handles ambiguous commits; rather, it provides a clean, strongly-typed filter for Polly fallback, retry-with-idempotency, or alerting workflows.
3. **Outer Placement**: Resilience policies wrap outer application service workflows (`ExecuteAsync`), keeping the transaction coordinator focused strictly on ACID coordination.

## Consequences

### Positive
- Prevents naive transient retries of ambiguous commits that cause duplicate side-effects.
- Seamlessly integrates with the modern Polly v8 resilience ecosystem.
- Zero coupling in `EricksonLopez.Transaction` core to Polly abstractions.

### Negative
- Applications must implement idempotency reconciliation or outbox deduplication if they choose to retry ambiguous commits.
