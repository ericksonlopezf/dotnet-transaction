# ADR-011: Systematic Rejection of Internal Retry Engine

## Status
Accepted (Rejection)

## Context
A recurring temptation in transaction libraries is embedding an internal retry loop (e.g. `options.MaxRetries = 3`) within the `ITransactionManager.ExecuteAsync` method.

## Decision
We **systematically reject** embedding any retry engine inside `EricksonLopez.Transaction`.

### Rationale:
1. **Single Responsibility Principle**: Retries, backoff strategies, jitter, circuit breaking, and rate limiting belong strictly to resilience frameworks (`EricksonLopez.Resilience` / Polly).
2. **Aborted Transaction State (`25P02`)**: In PostgreSQL, retrying commands inside an active transaction fails because the transaction is already aborted. The only valid retry is restarting the entire unit from connection acquisition.
3. **Composability**: Outer resilience pipelines can cleanly wrap `ITransactionManager.ExecuteAsync` without fighting internal hidden retry logic or creating unpredictable exponential backoff compounding.

## Consequences
### Positive
- Zero duplication with `EricksonLopez.Resilience` / Polly.
- Clean, minimal, zero-allocation transaction executor.
- Predictable failure semantics.

### Negative
- Applications must configure outer resilience pipelines explicitly.
