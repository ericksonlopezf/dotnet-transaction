# ADR-025: EricksonLopez.Transaction.AspNetCore — Exploratory Deferral

## Status
Accepted (Exploratory — Deferred Pending Demand Validation)

## Context
A proposed `EricksonLopez.Transaction.AspNetCore` package would provide ASP.NET Core middleware and controller attribute support for automatic per-request transaction management:

- `app.UseTransactionMiddleware()` — wraps every HTTP request in a transaction, committing on 2xx, rolling back on 4xx/5xx.
- `[Transactional]` attribute — scopes a transaction to a specific controller action or minimal API endpoint handler.

This would reduce boilerplate for applications that uniformly wrap HTTP requests in database transactions.

## Decision
We **defer** `EricksonLopez.Transaction.AspNetCore` pending explicit demand validation. The feature is not blocked by technical constraints, but by **undetermined semantics and unknown adoption demand**.

### Deferral Rationale

#### Semantic Complexity
HTTP request → database transaction commit semantics are non-trivial:
1. **Response flushing ordering**: In ASP.NET Core, the response body is streamed to the client before the middleware completes `next(context)`. A commit that fails after the response is already written leaves the client with a success response but no data — a silent inconsistency.
2. **Middleware position sensitivity**: Transaction scope must begin before any request body reads that touch the database, and end precisely after the last data access — both of which are impossible to guarantee generically across arbitrary handler topologies.
3. **Long-running requests**: Streaming responses, WebSockets, or Server-Sent Events cannot be wrapped in a single transaction without holding database locks for the entire request duration.
4. **Idempotency conflicts**: Automatic per-request transactions interact poorly with idempotency keys and retry-safe operations.

#### Demand Unknown
- No explicit adopter demand has been registered for this feature.
- Most Clean Architecture applications already manage their transaction boundaries explicitly in Application Layer handlers, rendering middleware-level transactions redundant.

### Validation Criteria for Future Implementation
If adopted, the following must be resolved before implementation:
1. A Response-Commit ordering model that prevents silent commit-after-write inconsistencies.
2. An opt-in decorator (`[Transactional]`) scoped to action-level execution only, not full middleware pipeline.
3. At least one confirmed adopter use case where middleware-level transactions provide a meaningful benefit over explicit Application Layer transaction management.

## Consequences

### Positive
- Prevents premature implementation of semantically complex middleware with unknown ROI.
- Core library remains free of ASP.NET Core dependencies.

### Negative
- Teams wanting per-request transactions must implement the pattern explicitly in their Application Layer until the package ships.
