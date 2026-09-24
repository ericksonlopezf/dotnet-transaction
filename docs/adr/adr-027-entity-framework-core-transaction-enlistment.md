# ADR-027: Entity Framework Core Transaction Enlistment Bridge

## Status
Accepted

## Date
2026-09-14

## Context
In applications combining Entity Framework Core with Dapper or raw ADO.NET repositories, coordinated transactions across both persistence models are frequently required. In ADR-023, we systematically rejected implementing ORM change tracking inside `ITransactionManager` or coupling the core library to EF Core abstractions. 

However, consumers still require an ergonomic, zero-friction mechanism to attach an existing `DbContext` instance to an active `ITransactionContext` so that both EF Core `SaveChanges()` and Dapper queries participate in the exact same underlying `DbTransaction` boundary.

## Decision
We introduce a dedicated, decoupled integration package: `EricksonLopez.Transaction.EntityFrameworkCore`.

1. **Unidirectional Coupling**: The integration package depends on `EricksonLopez.Transaction.Abstractions` and `Microsoft.EntityFrameworkCore.Relational`. The core coordinator remains 100% free of EF Core references.
2. **Dedicated Extension Method**: The package provides `DbContextTransactionExtensions.UseTransactionAsync`:
   ```csharp
   public static Task UseTransactionAsync(
       this DbContext dbContext, 
       ITransactionContext transactionContext, 
       CancellationToken cancellationToken = default)
   ```
3. **Underlying Mechanism**: It invokes `dbContext.Database.UseTransactionAsync(transactionContext.Transaction, cancellationToken)`. This instructs EF Core's execution strategy to enlist in the external transaction handle managed by `TransactionManager`.
4. **Lifecycle Segregation**: The physical transaction boundary lifecycle (`CommitAsync`, `RollbackAsync`, state machine) continues to be owned exclusively by `ITransactionManager`. EF Core acts solely as a query/command participant.

## Consequences

### Positive
- Core transaction engine remains lightweight, focused, and free of EF Core dependencies.
- Eliminates dual-transaction connection deadlocks when combining Dapper and EF Core within the same use case.
- Transparently works with any relational EF Core provider (PostgreSQL, SQL Server, SQLite, MySQL).

### Negative
- Requires consumers using both EF Core and `EricksonLopez.Transaction` to reference an additional modular package.
