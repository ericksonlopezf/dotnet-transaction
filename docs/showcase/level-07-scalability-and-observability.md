# Level 07: Scalability, Concurrency & OpenTelemetry Observability

> **Level:** 07 | **Category:** Enterprise | **Executable Reference:** [`Level7_Scalability.cs`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/samples/Showcase/Levels/Level7_Scalability.cs)

---

## 1. High-Throughput Concurrent Execution

`EricksonLopez.Transaction` is designed for high concurrency with minimal CPU and memory overhead:
- **Zero dynamic reflection** or runtime code generation (`Reflection.Emit`).
- **Full Native AOT and Trimming readiness** under .NET 10.
- Thread-safe isolation across parallel tasks via `AsyncLocal`.

```csharp
int parallelTransactions = 50;
var tasks = new Task[parallelTransactions];

for (int i = 0; i < parallelTransactions; i++)
{
    int index = i + 1;
    tasks[i] = Task.Run(async () =>
    {
        await txManager.ExecuteAsync(async context =>
        {
            await context.ExecuteAsync(
                "INSERT INTO counters VALUES (@id, @val);",
                new { id = index, val = index * 10 },
                cancellationToken: context.CancellationToken);
        }, TransactionOptions.Default, cancellationToken);
    });
}

await Task.WhenAll(tasks);
```

---

## 2. Built-in OpenTelemetry Diagnostic Instruments

The [`TransactionDiagnostics`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction/Diagnostics/TransactionDiagnostics.cs) class provides first-class distributed tracing and metrics:
- **ActivitySource**: `"EricksonLopez.Transaction"` (v1.0.0)
- **Meter**: `"EricksonLopez.Transaction"` (v1.0.0)

### Telemetry Metrics

| Metric Name | Instrument Type | Unit | Description |
|---|---|---|---|
| `transactions.started` | `Counter<long>` | `{transaction}` | Total number of transactions initiated. |
| `transactions.committed` | `Counter<long>` | `{transaction}` | Total number of transactions committed. |
| `transactions.rolled_back` | `Counter<long>` | `{transaction}` | Total number of transactions rolled back. |
| `transactions.failed` | `Counter<long>` | `{transaction}` | Total number of transactions that threw errors. |
| `transactions.duration` | `Histogram<double>` | `ms` | Execution duration distribution of transactions. |
| `transactions.savepoints.created` | `Counter<long>` | `{savepoint}` | Total savepoints created in nested scopes. |
| `transactions.savepoints.rolled_back` | `Counter<long>` | `{savepoint}` | Total savepoints rolled back. |
| `transactions.savepoints.released` | `Counter<long>` | `{savepoint}` | Total savepoints released. |
