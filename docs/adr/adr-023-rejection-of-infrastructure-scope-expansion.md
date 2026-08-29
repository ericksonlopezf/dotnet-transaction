# ADR-023: Rejection of Infrastructure Scope Expansion (ORM Tracking, Connection Pooling, Query Caching)

## Status
Accepted (Rejection)

## Context
Several proposals have been put forward to extend `EricksonLopez.Transaction` beyond its core mandate of local database transaction coordination. These proposals involve infrastructure responsibilities that belong to other layers, libraries, or frameworks.

## Decision
We **systematically reject** the following infrastructure scope expansions:

### 1. ORM Change Tracking
**Proposed**: Integrating change tracking for domain aggregate mutations (similar to `DbContext.ChangeTracker`) directly into `ITransactionContext`.

**Rejection Rationale**:
- Change tracking is the exclusive domain of the **Unit of Work pattern**, which is deliberately orthogonal to the transaction boundary (see ADR-013).
- Implementing change tracking inside a transaction coordinator transforms it into a partial ORM — conflating infrastructure persistence state with domain aggregate lifecycle.
- `EricksonLopez.Transaction` provides the atomic boundary (`DbTransaction`); change tracking and dirty-detection must live in the application-layer Unit of Work or a dedicated `IRepository<T>` implementation.
- EF Core's `DbContext` is the canonical change-tracking solution for applications that need it. Dapper-based workflows explicitly opt out of change tracking in favor of explicit SQL.

### 2. Custom Connection Pooling
**Proposed**: Replacing or augmenting driver-level connection pooling with a custom implementation inside the library.

**Rejection Rationale**:
- Modern ADO.NET drivers (Npgsql, Microsoft.Data.SqlClient, MySqlConnector) implement production-grade connection pooling that is far superior to any custom implementation.
- Overriding or duplicating pooling logic adds latency, increases memory pressure, and risks subtle connection lifetime bugs.
- The library's connection factory (`IDbConnectionFactory`) is intentionally thin, allowing the underlying driver's pool to manage connections correctly.

### 3. Transactional Query Caching
**Proposed**: Caching query results within the scope of a transaction and invalidating them on commit or rollback.

**Rejection Rationale**:
- Query result caching is a cross-cutting concern owned by the **Repository layer** or a dedicated caching abstraction (`IMemoryCache`, `IDistributedCache`).
- Transactional cache invalidation semantics are complex, subtly incorrect at different isolation levels (READ COMMITTED vs. SNAPSHOT), and invisible to outer layers — creating hidden behavior.
- The `EricksonLopez.Transaction` library's role ends at providing an ACID-safe data boundary. Caching decisions beyond that boundary belong to the consumer.

### 4. EF Core `DbContext` Integration
**Proposed**: Providing integration points between `ITransactionManager` and EF Core's `DbContext.Database.BeginTransactionAsync()`.

**Rejection Rationale**:
- EF Core already manages its own transaction lifecycle through `DbContext`. Integrating with it would require coupling in the wrong direction: the library would depend on EF Core abstractions.
- Teams using EF Core should use EF Core's built-in transaction management. `EricksonLopez.Transaction` is designed for ADO.NET / Dapper-based workflows where EF Core's `DbContext` is not present.
- Mixing EF Core change tracking with raw ADO.NET transactions creates ordering and concurrency hazards for `SaveChangesAsync()` and `DbTransaction` commits.
- See also ADR-013 for the orthogonal coexistence model.

## Consequences

### Positive
- The library remains a focused, minimal coordinator. Each infrastructure concern lives in its designated layer.
- Zero coupling to ORM-specific APIs or caching abstractions.
- Native AOT and trimming compatibility is preserved.

### Negative
- Teams wanting ORM-level change tracking alongside this library must implement or use a separate Unit of Work implementation.
