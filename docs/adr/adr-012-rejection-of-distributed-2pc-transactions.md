# ADR-012: Systematic Rejection of Distributed Two-Phase Commit (2PC) / System.Transactions

## Status
Accepted (Rejection)

## Context
Legacy .NET applications frequently used `System.Transactions.TransactionScope` and Microsoft Distributed Transaction Coordinator (MSDTC) to achieve distributed atomicity across multiple databases or message queues (e.g. SQL Server + MSMQ + Oracle).

In modern cloud architectures, microservices, and high-performance PostgreSQL environments:
- 2PC is notoriously fragile, blocking, and creates availability bottlenecks.
- `TransactionScope` incurs significant allocation overhead, thread switching anomalies, and poor async I/O compatibility with non-Windows OS and modern connection pools.

## Decision
We **systematically reject** integrating Two-Phase Commit (2PC) or distributed transaction managers into `EricksonLopez.Transaction`.

### Rationale:
1. `EricksonLopez.Transaction` is strictly a **local database transaction coordinator** operating over `DbTransaction`.
2. Cross-service or cross-datastore consistency (e.g., PostgreSQL + Kafka + Redis) MUST be resolved via asynchronous event-driven patterns:
   - **Transactional Outbox (`EricksonLopez.Outbox`)**: Dual-write business state and outbound integration events atomically in the same local database transaction.
   - **Sagas / Process Managers (`EricksonLopez.Processes`)**: Orchestrate multi-service distributed workflows with compensating actions.
   - **Idempotent Consumers (`EricksonLopez.Idempotency`)**: Ensure at-least-once message delivery is processed safely.

## Consequences
### Positive
- Zero MSDTC / 2PC operational complexity.
- Ultra-high throughput and sub-millisecond execution times.
- Native AOT compatibility and zero platform-specific dependencies.

### Negative
- Applications cannot span physical transactions across multiple distinct database instances within a single synchronous method.
