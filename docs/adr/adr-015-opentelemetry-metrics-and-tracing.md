# ADR-015: OpenTelemetry Diagnostics, Metrics, and Distributed Tracing

## Status
Accepted

## Context
Production monitoring of transaction performance, duration bottlenecks, isolation level contention, and rollback rates is essential for maintaining database health and diagnosing serialization anomalies.

However, logging sensitive database parameters or connection strings introduces critical security vulnerabilities (CWE-532).

## Decision
1. `EricksonLopez.Transaction` integrates directly with the .NET `System.Diagnostics` and `System.Diagnostics.Metrics` APIs under the standard source name `"EricksonLopez.Transaction"`:
   - **ActivitySource**: `"EricksonLopez.Transaction"` (Activity: `Transaction.Execute`).
   - **Meter**: `"EricksonLopez.Transaction"` with counters `transactions.started`, `transactions.committed`, `transactions.rolled_back`, `transactions.failed`, `transactions.savepoints.created`, `transactions.savepoints.rolled_back`, `transactions.savepoints.released`, and histogram `transactions.duration`.
2. **Strict Zero-PII Policy**:
   - Tracing spans and metrics record only high-level metadata (`transaction.id`, `transaction.isolation_level`, `transaction.outcome`, `error.type`).
   - Connection strings, passwords, queries, and parameter payloads are NEVER attached to telemetry spans or logs.

## Consequences
### Positive
- Turn-key observability across Prometheus, Grafana, Jaeger, OpenTelemetry Collector, and Azure Application Insights.
- Zero external runtime dependencies beyond `OpenTelemetry.Api` and standard BCL APIs.
- Absolute compliance with security and privacy regulations (GDPR, HIPAA, PCI-DSS).

### Negative
- Applications must register OpenTelemetry listeners for `"EricksonLopez.Transaction"` in their telemetry configuration.
