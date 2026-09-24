# Quick Start: EricksonLopez.Transaction

> **Get up and running with atomic relational database transactions in under 5 minutes.**

---

## 1. Installation

Install the core package and your database engine dialect provider:

### .NET CLI
```bash
# Core package
dotnet add package EricksonLopez.Transaction

# Choose your database dialect (e.g., PostgreSQL):
dotnet add package EricksonLopez.Transaction.PostgreSql
```

### Supported Database Dialects
| Database | Package | Driver |
|---|---|---|
| **PostgreSQL** | `EricksonLopez.Transaction.PostgreSql` | `Npgsql` |
| **SQL Server** | `EricksonLopez.Transaction.SqlServer` | `Microsoft.Data.SqlClient` |
| **MySQL** | `EricksonLopez.Transaction.MySql` | `MySqlConnector` |
| **MariaDB** | `EricksonLopez.Transaction.MariaDb` | `MySqlConnector` |
| **Oracle** | `EricksonLopez.Transaction.Oracle` | `Oracle.ManagedDataAccess.Core` |
| **SQLite** | `EricksonLopez.Transaction.Sqlite` | `Microsoft.Data.Sqlite` |

---

## 2. Dependency Injection Registration

Register `TransactionManager` and your database connection factory in `Program.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using EricksonLopez.Transaction.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

// Register PostgreSQL transaction coordinator
builder.Services.AddPostgreSqlTransaction(
    connectionString: builder.Configuration.GetConnectionString("DefaultConnection")!);

var app = builder.Build();
```

*(For SQLite, SQL Server, MySQL, MariaDB, or Oracle, replace `.AddPostgreSqlTransaction()` with `.AddSqliteTransaction()`, `.AddSqlServerTransaction()`, etc.)*

---

## 3. Execute Your First Atomic Transaction

Inject `ITransactionManager` into your Application Service or Endpoint:

```csharp
using EricksonLopez.Transaction;
using Dapper;

app.MapPost("/transfers", async (
    TransferRequest request,
    ITransactionManager transactionManager,
    CancellationToken ct) =>
{
    // Execute atomically inside an explicit database transaction
    await transactionManager.ExecuteAsync(async context =>
    {
        // 1. Debit Source Account
        int debited = await context.Connection.ExecuteAsync(
            "UPDATE accounts SET balance = balance - @Amount WHERE id = @Id AND balance >= @Amount;",
            new { Amount = request.Amount, Id = request.FromAccountId },
            transaction: context.Transaction);

        if (debited == 0)
        {
            throw new InvalidOperationException("Insufficient funds or source account not found.");
        }

        // 2. Credit Destination Account
        await context.Connection.ExecuteAsync(
            "UPDATE accounts SET balance = balance + @Amount WHERE id = @Id;",
            new { Amount = request.Amount, Id = request.ToAccountId },
            transaction: context.Transaction);

        // If an exception is thrown, ExecuteAsync automatically rolls back!
        // When this delegate exits successfully, ExecuteAsync commits the transaction.
    }, cancellationToken: ct);

    return Results.Ok(new { Message = "Transfer completed successfully." });
});

app.Run();

public record TransferRequest(string FromAccountId, string ToAccountId, decimal Amount);
```

---

## 4. Returning Values from Transactions

Use the generic `ExecuteAsync<T>` overload to return computation results:

```csharp
string orderId = await transactionManager.ExecuteAsync(async context =>
{
    var id = Guid.NewGuid().ToString("N");
    
    await context.Connection.ExecuteAsync(
        "INSERT INTO orders (id, customer_id, total) VALUES (@Id, @CustomerId, @Total);",
        new { Id = id, CustomerId = "cust_123", Total = 99.50m },
        transaction: context.Transaction);

    return id;
}, cancellationToken: ct);
```

---

## 5. Functional Railway-Oriented Programming (`Result<T>`)

If you prefer functional programming without exceptions, install `EricksonLopez.Transaction.Result`:

```bash
dotnet add package EricksonLopez.Transaction.Result
```

`ExecuteResultAsync` automatically commits on `Result.Success` and rolls back on `Result.Failure`:

```csharp
using EricksonLopez.Result;
using EricksonLopez.Transaction.Result;

Result<OrderSummary> result = await transactionManager.ExecuteResultAsync(async context =>
{
    if (inventoryUnavailable)
    {
        // Automatically triggers rollback without throwing an exception!
        return Result<OrderSummary>.Failure(DomainErrors.OutOfStock);
    }

    await SaveOrderAsync(context);
    return Result<OrderSummary>.Success(new OrderSummary(orderId, total));
}, cancellationToken: ct);
```

---

## Next Steps
- Learn how to configure isolation levels and timeouts in [Getting Started](getting-started.md).
- Explore nested transactions and savepoints in [Cookbook: Recipe 03](cookbook.md#recipe-03-nested-savepoint-isolation-and-partial-rollback).
- Check out the full 11-level executable showcase in [`samples/Showcase`](../samples/Showcase).
