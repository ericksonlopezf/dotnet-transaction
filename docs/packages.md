# EricksonLopez.Transaction — Package Reference & Ecosystem Catalog

> **Copyright © Erickson Lopez. MIT License.**  
> **Author:** Erickson Lopez ([ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com))  
> **Repository:** [github.com/ericksonlopezf/dotnet-transaction](https://github.com/ericksonlopezf/dotnet-transaction)

---

## 1. Published Packages Catalog

Packages in the ecosystem are published to NuGet with strong typing, compiled with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<IsAotCompatible>true</IsAotCompatible>`, and `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`. Core coordinator, dialect, testing, dapper, result, and mediator packages target **.NET 8.0, .NET 9.0, and .NET 10.0 (`net8.0;net9.0;net10.0`)**, while `EntityFrameworkCore` and `Resilience` target **.NET 10.0 (`net10.0`)**, and `Analyzers` targets **.NET Standard 2.0 (`netstandard2.0`)** as detailed in the matrix below.

| Package ID | Assembly Name | Description | Direct Dependencies | Target Frameworks |
|---|---|---|---|---|
| [`EricksonLopez.Transaction.Abstractions`](https://www.nuget.org/packages/EricksonLopez.Transaction.Abstractions) | `EricksonLopez.Transaction.Abstractions.dll` | Pure contracts, interfaces, options, and primitives. | *None (Pure BCL)* | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction`](https://www.nuget.org/packages/EricksonLopez.Transaction) | `EricksonLopez.Transaction.dll` | Core transaction manager, state machine, ambient context coordinator, and OpenTelemetry instrumentation. | `EricksonLopez.Transaction.Abstractions`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Logging.Abstractions`, `OpenTelemetry.Api` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.Dapper`](https://www.nuget.org/packages/EricksonLopez.Transaction.Dapper) | `EricksonLopez.Transaction.Dapper.dll` | High-performance Dapper extension methods and command definitions. | `EricksonLopez.Transaction.Abstractions`, `Dapper` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.PostgreSql`](https://www.nuget.org/packages/EricksonLopez.Transaction.PostgreSql) | `EricksonLopez.Transaction.PostgreSql.dll` | PostgreSQL connection factory (`NpgsqlDataSource`) and SQLSTATE error classifier. | `EricksonLopez.Transaction.Abstractions`, `EricksonLopez.Transaction`, `Npgsql` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.SqlServer`](https://www.nuget.org/packages/EricksonLopez.Transaction.SqlServer) | `EricksonLopez.Transaction.SqlServer.dll` | SQL Server connection factory and error classifier. | `EricksonLopez.Transaction.Abstractions`, `EricksonLopez.Transaction`, `Microsoft.Data.SqlClient`, `Microsoft.Extensions.DependencyInjection.Abstractions` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.MySql`](https://www.nuget.org/packages/EricksonLopez.Transaction.MySql) | `EricksonLopez.Transaction.MySql.dll` | MySQL connection factory (`MySqlConnector`) and error classifier. | `EricksonLopez.Transaction.Abstractions`, `EricksonLopez.Transaction`, `MySqlConnector`, `Microsoft.Extensions.DependencyInjection.Abstractions` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.MariaDb`](https://www.nuget.org/packages/EricksonLopez.Transaction.MariaDb) | `EricksonLopez.Transaction.MariaDb.dll` | MariaDB connection factory (`MySqlConnector`) and error classifier. | `EricksonLopez.Transaction.Abstractions`, `EricksonLopez.Transaction`, `MySqlConnector`, `Microsoft.Extensions.DependencyInjection.Abstractions` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.Oracle`](https://www.nuget.org/packages/EricksonLopez.Transaction.Oracle) | `EricksonLopez.Transaction.Oracle.dll` | Oracle connection factory and ORA error classifier. | `EricksonLopez.Transaction.Abstractions`, `EricksonLopez.Transaction`, `Oracle.ManagedDataAccess.Core`, `Microsoft.Extensions.DependencyInjection.Abstractions` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.Sqlite`](https://www.nuget.org/packages/EricksonLopez.Transaction.Sqlite) | `EricksonLopez.Transaction.Sqlite.dll` | SQLite connection factory and concurrency error classifier. | `EricksonLopez.Transaction.Abstractions`, `EricksonLopez.Transaction`, `Microsoft.Data.Sqlite`, `Microsoft.Extensions.DependencyInjection.Abstractions` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.Result`](https://www.nuget.org/packages/EricksonLopez.Transaction.Result) | `EricksonLopez.Transaction.Result.dll` | Functional `Result<T>` monad integration with automatic rollback on failure. | `EricksonLopez.Transaction.Abstractions`, `EricksonLopez.Result` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.Testing`](https://www.nuget.org/packages/EricksonLopez.Transaction.Testing) | `EricksonLopez.Transaction.Testing.dll` | In-memory test doubles (`FakeTransactionManager`, `FakeTransactionContext`). | `EricksonLopez.Transaction.Abstractions` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.Mediator`](https://www.nuget.org/packages/EricksonLopez.Transaction.Mediator) | `EricksonLopez.Transaction.Mediator.dll` | Mediator pipeline behavior for declarative command boundaries and auto-rollback. | `Abstractions`, `Result`, `EricksonLopez.Mediator`, `EricksonLopez.Result` | `net8.0;net9.0;net10.0` |
| [`EricksonLopez.Transaction.EntityFrameworkCore`](https://www.nuget.org/packages/EricksonLopez.Transaction.EntityFrameworkCore) | `EricksonLopez.Transaction.EntityFrameworkCore.dll` | EF Core `DbContext` transaction enlistment bridge (`UseTransactionAsync`). | `Abstractions`, `Microsoft.EntityFrameworkCore.Relational` | `net10.0` |
| [`EricksonLopez.Transaction.Resilience`](https://www.nuget.org/packages/EricksonLopez.Transaction.Resilience) | `EricksonLopez.Transaction.Resilience.dll` | Polly retry policy extensions for ambiguous commit handling. | `Abstractions`, `Polly` | `net10.0` |
| [`EricksonLopez.Transaction.Analyzers`](https://www.nuget.org/packages/EricksonLopez.Transaction.Analyzers) | `EricksonLopez.Transaction.Analyzers.dll` | Roslyn diagnostic analyzer enforcing connection safety (`ELT001`). | `Microsoft.CodeAnalysis.CSharp` | `netstandard2.0` |

---

## 2. Central Package Management (CPM) Reference

Dependency versions are centrally declared in `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`):

| Package Dependency | Centrally Pinned Version | Purpose / Scope |
|---|---|---|
| `Microsoft.SourceLink.GitHub` | `8.0.0` | SourceLink Git metadata embedding. |
| `EricksonLopez.Result` | `2.0.0` | Monadic result pattern primitives. |
| `EricksonLopez.Mediator` | `1.0.0` | CQRS mediator interfaces and pipeline abstractions. |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.11` | Dependency injection container abstractions. |
| `Microsoft.Extensions.DependencyInjection` | `10.0.11` | Dependency injection container provider. |
| `Microsoft.Extensions.Options` | `10.0.11` | Strongly-typed options configuration binding. |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.11` | High-performance structured logging abstractions. |
| `OpenTelemetry.Api` | `1.18.0` | Distributed tracing ActivitySource and Meter instruments. |
| `Polly` | `8.4.1` | Fault handling and resilience policies. |
| `Dapper` | `2.1.79` | High-performance micro-ORM object mapper and command definitions. |
| `Npgsql` | `10.0.3` | High-performance ADO.NET provider for PostgreSQL. |
| `Microsoft.Data.SqlClient` | `7.0.2` | Official ADO.NET provider for Microsoft SQL Server. |
| `MySqlConnector` | `2.6.2` | High-performance asynchronous ADO.NET provider for MySQL and MariaDB. |
| `Oracle.ManagedDataAccess.Core` | `23.26.300` | Official managed ADO.NET provider for Oracle Database. |
| `Microsoft.Data.Sqlite` | `10.0.11` | Lightweight ADO.NET provider for SQLite. |
| `Microsoft.EntityFrameworkCore.Relational` | `10.0.11` | Relational database support for Entity Framework Core. |
| `Microsoft.EntityFrameworkCore.Sqlite` | `10.0.11` | SQLite provider for Entity Framework Core. |
| `Microsoft.NET.Test.Sdk` | `18.9.0` | Test platform runner integration. |
| `xunit` | `2.9.3` | Developer testing framework. |
| `xunit.runner.visualstudio` | `4.0.0` | Test runner adapter for IDEs and CI. |
| `AwesomeAssertions` | `9.6.0` | Fluent assertions for unit test suites. |
| `NSubstitute` | `6.2.0` | Mocking and test double library. |
| `NetArchTest.Rules` | `1.3.2` | Architectural boundary enforcement tests. |
| `coverlet.collector` | `10.0.1` | Cross-platform code coverage collector. |
| `BenchmarkDotNet` | `0.15.8` | Micro-benchmarking harness for hot-path measurements. |
| `Microsoft.CodeAnalysis.Analyzers` | `3.11.0` | Roslyn analyzer rules for analyzers. |
| `Microsoft.CodeAnalysis.CSharp` | `4.12.0` | Roslyn C# syntax and semantic analysis. |
| `Testcontainers.PostgreSql` | `3.9.0` | Ephemeral PostgreSQL container for integration testing. |
| `Testcontainers.MsSql` | `3.9.0` | Ephemeral SQL Server container for integration testing. |

---

## 3. Database Engine & Dialect Capabilities

| Engine | Dialect Package | Savepoint Support | Snapshot Isolation | Concurrency Error Diagnostics |
|---|---|---|---|---|
| **PostgreSQL** | `EricksonLopez.Transaction.PostgreSql` | `SAVEPOINT`, `RELEASE SAVEPOINT`, `ROLLBACK TO` | Native MVCC | SQLSTATE `40001` (Serialization), `40P01` (Deadlock), `25P02` (Aborted) |
| **SQL Server** | `EricksonLopez.Transaction.SqlServer` | `SAVE TRANSACTION`, `ROLLBACK TRANSACTION` | TempDB Row Versioning (`ALLOW_SNAPSHOT_ISOLATION`) | Error 1205 (Deadlock), 3960/3961 (Snapshot Conflict) |
| **MySQL** | `EricksonLopez.Transaction.MySql` | InnoDB Savepoints | Repeatable Read MVCC | Error 1213 (Deadlock), 1205 (Lock Wait Timeout) |
| **MariaDB** | `EricksonLopez.Transaction.MariaDb` | InnoDB / Aria Savepoints | Repeatable Read MVCC | Error 1213 (Deadlock), 1205 (Lock Wait Timeout) |
| **Oracle** | `EricksonLopez.Transaction.Oracle` | `SAVEPOINT`, `ROLLBACK TO SAVEPOINT` | Serialized / Read Committed | `ORA-00060` (Deadlock), `ORA-08177` (Serialization Failure) |
| **SQLite** | `EricksonLopez.Transaction.Sqlite` | WAL Mode Savepoints | WAL Mode Read/Write Concurrency | `SQLITE_BUSY` (5), `SQLITE_LOCKED` (6) |

---

## 4. Benchmark Performance Results

Micro-benchmarking executed across runtimes with BenchmarkDotNet:

| Method | Mean | Ratio | Allocated |
|---|---|---|---|
| `DirectDbTransactionBenchmark` | 12.45 μs | 1.00 | 480 B |
| `FrameworkTransactionBenchmark` | 12.82 μs | 1.03 | 560 B |
| `FrameworkNestedSavepointBenchmark` | 14.10 μs | 1.13 | 720 B |

### How to Run Benchmarks

```bash
dotnet run --project benchmarks/EricksonLopez.Transaction.Benchmarks/EricksonLopez.Transaction.Benchmarks.csproj --framework net10.0 -c Release
```

---

## 5. Official Showcase Reference

The repository includes an interactive and batch-executable reference application under `samples/Showcase/`:

```bash
# Run all 11 progressive levels in automated batch mode
dotnet run --project samples/Showcase/EricksonLopez.Transaction.Showcase.csproj --framework net10.0 -- --all

# Run interactive console menu
dotnet run --project samples/Showcase/EricksonLopez.Transaction.Showcase.csproj --framework net10.0
```

---

## 6. GitHub Repository Metadata & SEO Taxonomy

Official GitHub repository metadata configured for [github.com/ericksonlopezf/dotnet-transaction](https://github.com/ericksonlopezf/dotnet-transaction):

### Description
> `High-performance, explicit, composable, and Native AOT-ready relational database transaction coordinator for modern .NET 8, .NET 9, and .NET 10 applications.`

### Official Topics (20 / 20)
| Category | Topics |
|---|---|
| **Platform & Runtime** | `dotnet`, `csharp`, `native-aot` |
| **Transaction Core & Scopes** | `transaction`, `transaction-manager`, `transaction-scope`, `savepoints`, `ambient-context` |
| **Architectural Patterns** | `unit-of-work`, `clean-architecture`, `cqrs`, `result-pattern` |
| **Data Access & Engines** | `ado-net`, `dapper`, `entity-framework-core`, `postgresql`, `sql-server` |
| **Resilience, Quality & Tooling** | `opentelemetry`, `resilience`, `roslyn-analyzer` |

