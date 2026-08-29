# Level 01: Quick Start & Minimal Setup

> **Level:** 01 | **Category:** Beginner | **Executable Reference:** [`Level1_QuickStart.cs`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/samples/Showcase/Levels/Level1_QuickStart.cs)

---

## 1. Overview & Setup

Level 01 demonstrates the fundamental setup and usage of `EricksonLopez.Transaction` in a modern .NET 10 application:
1. Registering transaction services into Microsoft's Dependency Injection (`IServiceCollection`).
2. Executing an atomic multi-step balance transfer across two accounts using [`ITransactionManager.ExecuteAsync`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Abstractions/ITransactionManager.cs).
3. Verifying that all operations commit together or roll back on error.

---

## 2. Dependency Injection Registration

```csharp
using EricksonLopez.Transaction;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register the transaction manager with a connection factory delegate
services.AddTransaction(sp => 
    new SqliteConnection("Data Source=app.db;Cache=Shared"));

using ServiceProvider serviceProvider = services.BuildServiceProvider();
ITransactionManager txManager = serviceProvider.GetRequiredService<ITransactionManager>();
```

---

## 3. Atomic Multi-Step Operation (`ExecuteAsync`)

The primary entry point for transactional use cases is `ExecuteAsync`. It manages opening the connection, beginning the transaction, executing the delegate with the active [`ITransactionContext`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Abstractions/ITransactionContext.cs), committing on success, and rolling back if any unhandled exception occurs.

```csharp
decimal transferAmount = 250.00m;
string sourceAccountId = "acc-1";
string targetAccountId = "acc-2";

await txManager.ExecuteAsync(async context =>
{
    // Debit from Source Account
    await context.ExecuteAsync(
        "UPDATE accounts SET balance = balance - @Amount WHERE id = @Id;",
        new { Amount = transferAmount, Id = sourceAccountId },
        cancellationToken: context.CancellationToken);

    // Credit to Target Account
    await context.ExecuteAsync(
        "UPDATE accounts SET balance = balance + @Amount WHERE id = @Id;",
        new { Amount = transferAmount, Id = targetAccountId },
        cancellationToken: context.CancellationToken);
}, TransactionOptions.Default, cancellationToken);
```

---

## 4. Key Takeaways
- **No manual Begin/Commit/Rollback boilerplate**: `ExecuteAsync` guarantees correct lifecycle transitions.
- **Cancellation Token Binding**: `context.CancellationToken` combines caller tokens with transaction timeout limits.
- **Thread Safety**: Transaction context flows naturally across `await` points via `AsyncLocal`.
