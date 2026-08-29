# Level 02: Complete Configuration & TransactionOptions

> **Level:** 02 | **Category:** Configuration | **Executable Reference:** [`Level2_Configuration.cs`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/samples/Showcase/Levels/Level2_Configuration.cs)

---

## 1. Overview & `TransactionOptions` Record

[`TransactionOptions`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Abstractions/TransactionOptions.cs) is an immutable record that governs the execution characteristics of a transaction:
- **IsolationLevel**: [`TransactionIsolationLevel`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Abstractions/TransactionIsolationLevel.cs) (`ReadCommitted`, `RepeatableRead`, `Serializable`, `Snapshot`, `ReadUncommitted`).
- **Timeout**: Optional `TimeSpan?` enforcing strict execution lifetime limits.
- **ReadOnly**: Boolean flag signaling read-only intent for database engine query optimizations.
- **NestedBehavior**: [`NestedTransactionBehavior`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Abstractions/NestedTransactionBehavior.cs) (`UseSavepoint`, `RequireNew`, `Suppress`, `JoinExisting`).
- **TransactionName**: Optional descriptor for debugging and OpenTelemetry spans.

---

## 2. Configuration Presets & Helper Methods

```csharp
// Standard default (ReadCommitted, UseSavepoint)
TransactionOptions defaultOpts = TransactionOptions.Default;

// Strict serializable isolation
TransactionOptions serializableOpts = TransactionOptions.Serializable;

// Read-only execution
TransactionOptions readOnlyOpts = TransactionOptions.ReadOnlyMode;

// Custom timeout helper
TransactionOptions timeoutOpts = TransactionOptions.WithTimeout(TimeSpan.FromSeconds(5));
```

---

## 3. Custom Options Configuration

```csharp
var customOptions = new TransactionOptions
{
    IsolationLevel = TransactionIsolationLevel.Serializable,
    Timeout = TimeSpan.FromSeconds(10),
    ReadOnly = false,
    NestedBehavior = NestedTransactionBehavior.UseSavepoint,
    TransactionName = "OrderProcessingPipeline"
};

await txManager.ExecuteAsync(async context =>
{
    await context.ExecuteAsync(
        "UPDATE products SET stock = stock - 1 WHERE id = @Id;",
        new { Id = "p1" },
        cancellationToken: context.CancellationToken);
}, customOptions, cancellationToken);
```

---

## 4. Isolation Level Anomaly Prevention Matrix

| Isolation Level | Dirty Read | Non-Repeatable Read | Phantom Read | Serialization Conflict |
|---|---|---|---|---|
| `ReadUncommitted` | Allowed | Allowed | Allowed | Allowed |
| `ReadCommitted` | Prevented | Allowed | Allowed | Allowed |
| `RepeatableRead` | Prevented | Prevented | Allowed (PG) | Allowed |
| `Serializable` | Prevented | Prevented | Prevented | Prevented (Throws 40001 / 1205) |
| `Snapshot` | Prevented | Prevented | Prevented | MVCC Row Versioning |
