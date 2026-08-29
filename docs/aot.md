# Native AOT & Trimming Compatibility

> **Copyright © Erickson Lopez. MIT License.**  
> **Author:** Erickson Lopez ([ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com))  
> **Repository:** [github.com/ericksonlopezf/dotnet-transaction](https://github.com/ericksonlopezf/dotnet-transaction)

---

## 1. Native AOT & Trimming Mandate

`EricksonLopez.Transaction` is engineered from first principles to be **100% Native AOT Compatible** across modern .NET runtimes (.NET 8, .NET 9, and .NET 10).

In `Directory.Build.props`:

```xml
<PropertyGroup>
  <IsAotCompatible Condition="'$(IsAotCompatible)' == ''">true</IsAotCompatible>
  <EnableTrimAnalyzer Condition="'$(EnableTrimAnalyzer)' == ''">true</EnableTrimAnalyzer>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

---

## 2. Zero Reflection & Strong Typing Invariants

`EricksonLopez.Transaction` eliminates dynamic runtime reflection:
- **Deterministic State Transitions**: Managed via `TransactionStateMachine` with compile-time state validation (no reflection-based handlers).
- **Ambient Context Flow**: Uses `AsyncLocal<ITransactionContext?>` with zero reflection or runtime boxing.
- **Result Monad Integration**: Direct interaction with `Result<T>` monad in `EricksonLopez.Transaction.Result` without unconstrained type inspections.
- **Dapper Command Binding**: Fluent `context.AsCommand(...)` produces immutable `CommandDefinition` structs with strongly-typed parameter references.
- **Provider Connection Factories**: Dialect factories (`PostgreSqlConnectionFactory`, `SqlServerConnectionFactory`, `MySqlConnectionFactory`, `MariaDbConnectionFactory`, `OracleConnectionFactory`, `SqliteConnectionFactory`) instantiate standard `DbConnection` types via direct factory invocations without reflective construction.
- **Structured Logging**: Zero-allocation logging generated at compile-time via `[LoggerMessage]` source generator.

---

## 3. Dedicated Native AOT Smoke Test Suite

To rigorously validate Native AOT compilation and trimming safety in a closed-world runtime environment:
- **Project**: `tests/EricksonLopez.Transaction.AotSmokeTest/EricksonLopez.Transaction.AotSmokeTest.csproj` (`OutputType=Exe`, `PublishAot=true`).
- **Execution**: Exercises all core transactional workflows, nested savepoint scopes, suppressed scopes, Result monad auto-rollback, OpenTelemetry diagnostics, and multi-dialect error classifiers under Native AOT.

### Verification Command

```bash
dotnet run --project tests/EricksonLopez.Transaction.AotSmokeTest/EricksonLopez.Transaction.AotSmokeTest.csproj --framework net10.0 -c Release
```

Expected output: **0 Trimming Warnings, ALL 36 Native AOT Suite Tests Passed.**
