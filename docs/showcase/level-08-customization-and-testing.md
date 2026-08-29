# Level 08: Extensibility, Custom Enlistments & In-Memory Test Doubles

> **Level:** 08 | **Category:** Extensibility | **Executable Reference:** [`Level8_Customization.cs`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/samples/Showcase/Levels/Level8_Customization.cs)

---

## 1. Custom Connection Factories (`DelegateDbConnectionFactory`)

Applications requiring dynamic multi-tenant connection strings, token-based cloud authentication (e.g. Azure AD Managed Identity), or custom connection configuration can supply a [`DelegateDbConnectionFactory`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction/DelegateDbConnectionFactory.cs):

```csharp
IDbConnectionFactory customFactory = new DelegateDbConnectionFactory(async cancellationToken =>
{
    var conn = new NpgsqlConnection(tenantConnectionString);
    await conn.OpenAsync(cancellationToken);
    return conn;
});
```

---

## 2. Transaction Enlistment Lifecycle Hooks (`ITransactionEnlistment`)

[`ITransactionEnlistment`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Abstractions/ITransactionEnlistment.cs) enables attaching custom participants to an active transaction:
- **`BeforeCommitAsync`**: Flush in-memory domain events to the outbox table before physical commit.
- **`AfterCommitAsync`**: Notify background event publishers or invalidate distributed caches.
- **`AfterRollbackAsync`**: Clear dirty entity state or reset cache buffers.

```csharp
public sealed class DomainEventOutboxEnlistment : ITransactionEnlistment
{
    public async Task BeforeCommitAsync(ITransactionContext context, CancellationToken ct)
    {
        // Flush pending events to outbox table inside the active transaction
        await FlushOutboxMessagesAsync(context, ct);
    }

    public Task AfterCommitAsync(ITransactionContext context, CancellationToken ct)
    {
        // Signal background processor to trigger immediate delivery
        return NotifyWorkerAsync(ct);
    }

    public Task AfterRollbackAsync(ITransactionContext context, CancellationToken ct) => Task.CompletedTask;
}

// In Application Service:
await txManager.ExecuteAsync(async context =>
{
    context.Enlist(new DomainEventOutboxEnlistment());
    await repository.UpdateCustomerAsync(customer, context);
});
```

---

## 3. Fast In-Memory Unit Testing (`FakeTransactionManager`)

The `EricksonLopez.Transaction.Testing` package provides production-grade test doubles:
- [`FakeTransactionManager`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Testing/FakeTransactionManager.cs)
- [`FakeTransaction`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Testing/FakeTransaction.cs)
- [`FakeTransactionContext`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Testing/FakeTransactionContext.cs)

```csharp
[Fact]
public async Task PlaceOrder_ShouldCommitTransaction_WhenOrderIsValid()
{
    // Arrange
    var fakeTxManager = new FakeTransactionManager();
    var sut = new BillingService(fakeTxManager);

    // Act
    await sut.ChargeCustomerAsync("CUST-99", 150.0m);

    // Assert
    fakeTxManager.StartedTransactions.Should().HaveCount(1);
    fakeTxManager.StartedTransactions[0].CommitCount.Should().Be(1);
    fakeTxManager.StartedTransactions[0].RollbackCount.Should().Be(0);
}
```
