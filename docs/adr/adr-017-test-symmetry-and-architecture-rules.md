# ADR-017: Test Project Symmetry and Architecture Rules

## Status
Accepted

## Context
In accordance with the EricksonLopez ecosystem engineering guidelines, every production assembly in `src/` must have a corresponding symmetric test assembly in `tests/` named `<PackageName>.Tests`. Additionally, cross-cutting architectural invariants and end-to-end integration flows must be validated in dedicated suites (`ArchitectureTests` and `IntegrationTests`).

## Decision
We establish a 1:1 symmetric test topology:
1. `src/EricksonLopez.Transaction.Abstractions` $\leftrightarrow$ `tests/EricksonLopez.Transaction.Abstractions.Tests`
2. `src/EricksonLopez.Transaction` $\leftrightarrow$ `tests/EricksonLopez.Transaction.Tests`
3. `src/EricksonLopez.Transaction.Dapper` $\leftrightarrow$ `tests/EricksonLopez.Transaction.Dapper.Tests`
4. `src/EricksonLopez.Transaction.PostgreSql` $\leftrightarrow$ `tests/EricksonLopez.Transaction.PostgreSql.Tests`
5. `src/EricksonLopez.Transaction.SqlServer` $\leftrightarrow$ `tests/EricksonLopez.Transaction.SqlServer.Tests`
6. `src/EricksonLopez.Transaction.MySql` $\leftrightarrow$ `tests/EricksonLopez.Transaction.MySql.Tests`
7. `src/EricksonLopez.Transaction.MariaDb` $\leftrightarrow$ `tests/EricksonLopez.Transaction.MariaDb.Tests`
8. `src/EricksonLopez.Transaction.Oracle` $\leftrightarrow$ `tests/EricksonLopez.Transaction.Oracle.Tests`
9. `src/EricksonLopez.Transaction.Sqlite` $\leftrightarrow$ `tests/EricksonLopez.Transaction.Sqlite.Tests`
10. `src/EricksonLopez.Transaction.Result` $\leftrightarrow$ `tests/EricksonLopez.Transaction.Result.Tests`
11. `src/EricksonLopez.Transaction.Testing` $\leftrightarrow$ `tests/EricksonLopez.Transaction.Testing.Tests`
12. `tests/EricksonLopez.Transaction.ArchitectureTests` (NetArchTest rules enforcing layer boundaries)
13. `tests/EricksonLopez.Transaction.IntegrationTests` (End-to-end multi-layered transactional workflows)

## Consequences
- **Positive**: Strict isolation of unit test concerns and compiler dependency matrices.
- **Positive**: Eliminates monolithic unit test projects.
- **Positive**: Fast parallel test execution and clean CI/CD test sharding.
