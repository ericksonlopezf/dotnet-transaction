# ADR-033: SanitizeTelemetryMetadata — Scope and Implementation of PII Redaction in OpenTelemetry Spans

## Status
Accepted

## Date
2026-09-15

## Context
`TransactionOptions.SanitizeTelemetryMetadata` was added to the API surface to provide a mechanism for preventing personally identifiable information (PII), sensitive business identifiers, or sensitive infrastructure metadata from appearing in distributed tracing platforms (Datadog, Jaeger, Honeycomb, etc.).

During an exhaustive coherence audit (2026-09-15), it was discovered that the property existed in `TransactionOptions` and was documented extensively in multiple Markdown files (FAQ, best-practices, troubleshooting, architecture) but **was never read by any implementation code**. Setting `SanitizeTelemetryMetadata = true` had no effect whatsoever — a false security guarantee.

This ADR documents the decision made to remediate this gap.

## Decision

**`SanitizeTelemetryMetadata` is implemented in the OpenTelemetry tracing layer with the following scope:**

When `TransactionOptions.SanitizeTelemetryMetadata = true`:

1. **`transaction.id` span tag** — Replaced with the literal string `[REDACTED]`. The GUID-based transaction identifier is not considered directly PII, but it may correlate with business records (e.g., an order ID embedded in a transaction name) and is redacted for safety.
2. **`transaction.name` span tag** — Replaced with `[REDACTED]`. `TransactionName` is a developer-specified logical label that may embed business entity references, PII-adjacent identifiers, or sensitive batch job descriptions.

All other tags emitted by `TransactionDiagnostics` (e.g., `db.system`, `transaction.isolation_level`, `transaction.outcome`, `error.type`) are **not redacted**. These are metadata-level operational attributes that are not typically PII-sensitive.

### Out of Scope

The following are **not** redacted by this option and remain the responsibility of the application layer or OTel SDK configuration:

- SQL command text embedded in spans (not emitted by this framework).
- Exception message strings (not embedded in framework-emitted spans).
- Connection string fragments or server hostnames.
- Query parameters.

These items are outside the boundary of `EricksonLopez.Transaction`'s telemetry layer and should be addressed by application-level OTel `Sampler` or `Processor` configuration.

## Implementation

The `sanitizeTelemetryMetadata` parameter was threaded through the call chain:

```
TransactionOptions.SanitizeTelemetryMetadata
  → TransactionManager.CreatePhysicalTransactionScopeAsync()
  → PhysicalTransaction(sanitizeTelemetryMetadata: ...)
  → TransactionDiagnostics.StartActivity(sanitizeTelemetryMetadata: ...)
```

`TransactionDiagnostics.StartActivity` applies conditional redaction:

```csharp
activity.SetTag("transaction.id", sanitizeTelemetryMetadata ? "[REDACTED]" : transactionId.ToString());
if (!string.IsNullOrWhiteSpace(transactionName))
{
    activity.SetTag("transaction.name", sanitizeTelemetryMetadata ? "[REDACTED]" : transactionName);
}
```

## Consequences

### Positive
- Closes the false security guarantee identified in the coherence audit.
- Developers who enable `SanitizeTelemetryMetadata = true` now have documented, verifiable protection for `transaction.id` and `transaction.name` span tags.
- The API surface and documentation are now coherent.

### Negative
- Redaction scope is intentionally narrow. Developers expecting blanket SQL parameter redaction must configure OTel processors at the application level.
- The redaction is applied at activity tag creation time (not as a post-processor), which means it cannot be toggled dynamically without creating a new transaction scope.

## Related ADRs
- [ADR-015: OpenTelemetry Metrics and Tracing](adr-015-opentelemetry-metrics-and-tracing.md)
- [ADR-010: Native AOT and Trimming Invariants](adr-010-native-aot-and-trimming-invariants.md)
