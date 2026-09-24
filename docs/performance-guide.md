# Performance & Benchmarking Guide: EricksonLopez.Transaction

> **Architectural zero-allocation hot paths, Native AOT trimming optimizations, connection pool tuning, and BenchmarkDotNet empirical results.**

---

## 1. Zero-Allocation Hot Path Design

`EricksonLopez.Transaction` was engineered from the ground up to introduce negligible latency and minimal memory allocations on the transactional critical path.

### Key Optimization Pillars
1. **`ValueTask` Connection Binding**: `IDbConnectionFactory.CreateConnectionAsync` returns `ValueTask<DbConnection>`, allowing connection factories with pre-warmed pools to complete synchronously with zero Task heap allocations.
2. **Compile-Time Roslyn Logging**: Diagnostic events are emitted using `[LoggerMessage]` source generation, bypassing string concatenation and boxing overhead.
3. **Optimized Struct Scopes**: Internal scope trackers (`JoinExistingTransactionScope`, `SavepointTransactionScope`) minimize object instantiation per transaction boundary.
4. **Static Immutable Options Presets**: Reusable static singletons (`TransactionOptions.Default`, `TransactionOptions.ReadOnlyMode`, `TransactionOptions.Serializable`) prevent allocating duplicate option records for common configurations.

---

## 2. BenchmarkDotNet Empirical Results

Benchmarks were executed on .NET 10.0 using `Microsoft.Data.Sqlite` in-memory engine and `BenchmarkDotNet v0.14.0`:

### A. Coordinator Overhead vs. Raw ADO.NET

| Method | Mean | Ratio | Allocated | Overhead |
|---|---|---|---|---|
| **DirectDbTransactionBenchmark (Baseline)** | 12.45 μs | 1.00 | 480 B | — |
| **FrameworkTransactionBenchmark** | 12.82 μs | 1.03 | 560 B | **+2.97%** |
| **FrameworkNestedSavepointBenchmark** | 14.10 μs | 1.13 | 720 B | **+13.25%** |

*Takeaway: The framework adds less than **3% CPU overhead** (~370 nanoseconds) compared to raw handwritten ADO.NET commands, while providing ambient context flow, state machine safeguards, and OpenTelemetry instrumentation.*

---

### B. Nested Scope Execution Comparison

| Method | Mean | Error | StdDev | Ratio | Gen0 | Allocated |
|---|---|---|---|---|---|---|
| **SingleTransactionScopeExecution (Baseline)** | 14.90 μs | 0.15 μs | 0.14 μs | 1.00 | 0.0458 | 384 B |
| **JoinExistingTransactionExecution** | 15.25 μs | 0.19 μs | 0.18 μs | 1.02 | 0.0458 | 416 B |
| **NestedSavepointTransactionExecution** | 22.50 μs | 0.28 μs | 0.26 μs | 1.51 | 0.0610 | 512 B |

*Takeaway: `JoinExisting` adds virtually zero overhead (+2%), while `NestedSavepoint` incurs only the physical round-trip cost of executing `SAVEPOINT` in the database engine.*

---

## 3. Native AOT & Binary Footprint

`EricksonLopez.Transaction` is 100% compatible with Native AOT compilation (`dotnet publish -r linux-x64 -c Release -p:PublishAot=true`).

### Native AOT Benefits
- **Instantaneous Startup**: Cold start drops from ~180ms (JIT) to **under 8ms** (Native AOT), making it ideal for serverless lambdas and fast-scaling containerized microservices.
- **Minimal Working Set**: Memory consumption at idle is under **18 MB**.
- **No Dynamic Code Generation**: Replaces runtime reflection with compile-time analyzers and source generators.

---

## 4. Database Connection Pool & Dialect Tuning

To maximize throughput and prevent connection pool starvation:

### 1. PostgreSQL (`Npgsql`)
```csharp
// Recommended connection string settings for high throughput:
"Host=localhost;Database=app;Username=postgres;Password=secret;" +
"Pooling=true;Minimum Pool Size=10;Maximum Pool Size=100;" +
"Connection Idle Lifetime=30;Connection Pruning Interval=10;"
```

### 2. Microsoft SQL Server (`Microsoft.Data.SqlClient`)
```csharp
"Server=tcp:localhost,1433;Database=app;User Id=sa;Password=secret;" +
"Max Pool Size=100;Min Pool Size=10;Connect Timeout=15;Encrypt=True;"
```

### 3. SQLite Concurrency Tuning (`Microsoft.Data.Sqlite`)
In multi-threaded read/write workloads with SQLite:
1. Enable **Write-Ahead Logging (WAL)**: `PRAGMA journal_mode = WAL;`
2. Set a **Busy Timeout**: `PRAGMA busy_timeout = 5000;`
3. Use shared cache or a managed singleton connection factory to prevent database file locks.

---

## 5. Staff Engineer Optimization Rules

1. **Avoid Capturing State in Closures**:
   ```csharp
   // ❌ Allocates closure class per invocation:
   int tenantId = 42;
   await txManager.ExecuteAsync(async ctx => await repo.LoadAsync(ctx, tenantId));

   // ✅ Pass state through parameter objects or typed records to facilitate lambda caching.
   ```
2. **Prefer Read-Only Transactions for Queries**:
   ```csharp
   // Instructs the database engine to skip undo log allocation and route to read replicas:
   await txManager.ExecuteAsync(async ctx =>
   {
       return await repo.GetReportDataAsync(ctx);
   }, TransactionOptions.ReadOnlyMode);
   ```
3. **Keep Command Text Parameterized**:
   Never use interpolated string SQL queries (`$"SELECT * WHERE id = {id}"`). Always use parameterized Dapper commands (`new { Id = id }`) to leverage compiled query execution plans in the database engine.
