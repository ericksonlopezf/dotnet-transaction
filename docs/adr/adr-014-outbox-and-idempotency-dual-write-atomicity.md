# ADR-014: Outbox and Idempotency Dual-Write Atomicity

## Status
Accepted

## Context
In microservices architecture, dual-write operations (e.g. updating an account balance AND publishing an `AccountUpdatedEvent` to a message broker, or recording an idempotency claim AND persisting business data) are vulnerable to split-brain failures when performed across uncoordinated boundaries.

If the business update commits but the outbox write fails, the rest of the distributed system is unaware of the state change. If the outbox write commits but the business update rolls back, phantom events are dispatched.

## Decision
1. `EricksonLopez.Transaction` provides the foundational transaction context (`ITransactionContext`) that enables `EricksonLopez.Outbox` and `EricksonLopez.Idempotency` to participate in the **exact same database transaction** as the business aggregates.
2. The sequence is strictly coordinated within a single atomic boundary:
   ```
   BEGIN TRANSACTION
      1. Verify / Claim Idempotency Key
      2. Execute Business Aggregate Mutations (SQL INSERT/UPDATE)
      3. Insert Outbox Event Records (SQL INSERT)
      4. Complete / Finalize Idempotency Response State
   COMMIT TRANSACTION
   ```
3. If any step fails, the entire transaction is rolled back, preventing phantom messages or corrupted idempotency records.

## Consequences
### Positive
- Strict atomic consistency across business data, outbox messages, and idempotency states.
- Outbox and Idempotency libraries remain focused on their core domain and simply accept `ITransactionContext`.

### Negative
- All three tables (Business Entities, Outbox, Idempotency) must reside in the same physical database instance.
