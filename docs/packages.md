# EricksonLopez.Transaction — Package Reference & Ecosystem Catalog

> **Copyright © Erickson Lopez. MIT License.**  
> **Author:** Erickson Lopez ([ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com))  
> **Repository:** [github.com/ericksonlopezf/dotnet-transaction](https://github.com/ericksonlopezf/dotnet-transaction)

---

## 1. Published Packages Catalog

All packages are published to NuGet targeting **.NET 8.0, .NET 9.0, and .NET 10.0 (`net8.0;net9.0;net10.0`)**, compiled with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<IsAotCompatible>true</IsAotCompatible>`, and `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>`.

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

---

## 2. Central Package Management (CPM) Reference

Dependency versions are centrally declared in `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`):

| Package Dependency | Centrally Pinned Version | Purpose / Scope |
|---|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.11` | Dependency injection container abstractions. |
| `Microsoft.Extensions.DependencyInjection` | `10.0.11` | Dependency injection container provider. |
| `Microsoft.Extensions.Options` | `10.0.11` | Strongly-typed options configuration binding. |
| `Microsoft.Extensions.Logging.Abstractions` | `10.0.11` | High-performance structured logging abstractions. |
| `OpenTelemetry.Api` | `1.11.2` | Distributed tracing ActivitySource and Meter instruments. |
| `Dapper` | `2.1.79` | High-performance micro-ORM object mapper and command definitions. |
| `Npgsql` | `10.0.3` | High-performance ADO.NET provider for PostgreSQL. |
| `Microsoft.Data.SqlClient` | `5.2.2` | Official ADO.NET provider for Microsoft SQL Server. |
| `MySqlConnector` | `2.4.0` | High-performance asynchronous ADO.NET provider for MySQL and MariaDB. |
| `Oracle.ManagedDataAccess.Core` | `23.7.0` | Official managed ADO.NET provider for Oracle Database. |
| `Microsoft.Data.Sqlite` | `10.0.3` | Lightweight ADO.NET provider for SQLite. |
| `Microsoft.NET.Test.Sdk` | `18.9.0` | Test platform runner integration. |
| `xunit` | `2.9.3` | Developer testing framework. |
| `xunit.runner.visualstudio` | `3.0.2` | Test runner adapter for IDEs and CI. |
| `AwesomeAssertions` | `9.6.0` | Fluent assertions for unit test suites. |
| `NSubstitute` | `5.3.0` | Mocking and test double library. |
| `NetArchTest.Rules` | `1.3.2` | Architectural boundary enforcement tests. |
| `coverlet.collector` | `6.0.4` | Cross-platform code coverage collector. |
| `BenchmarkDotNet` | `0.15.8` | Micro-benchmarking harness for hot-path measurements. |

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
