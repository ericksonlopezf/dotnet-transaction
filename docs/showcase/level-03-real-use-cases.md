# Level 03: Real-World Business Use Cases & Explicit Lifecycles

> **Level:** 03 | **Category:** Intermediate | **Executable Reference:** [`Level3_RealUseCases.cs`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/samples/Showcase/Levels/Level3_RealUseCases.cs)

---

## 1. Clean Architecture Multi-Repository Coordination

In Clean Architecture and Domain-Driven Design (DDD), Application Use Cases (or Handlers) coordinate business operations across multiple repositories. Individual repositories MUST NOT manage transaction lifecycles—they simply receive [`ITransactionContext`](file:///d:/DevData/ericksonlopez.dev/dotnet-transaction/src/EricksonLopez.Transaction.Abstractions/ITransactionContext.cs) and execute queries.

```csharp
public sealed class PlaceOrderUseCase
{
    private readonly ITransactionManager _txManager;
    private readonly OrderRepository _orderRepo;
    private readonly InventoryRepository _inventoryRepo;
    private readonly PaymentRepository _paymentRepo;

    public PlaceOrderUseCase(
        ITransactionManager txManager,
        OrderRepository orderRepo,
        InventoryRepository inventoryRepo,
        PaymentRepository paymentRepo)
    {
        _txManager = txManager;
        _orderRepo = orderRepo;
        _inventoryRepo = inventoryRepo;
        _paymentRepo = paymentRepo;
    }

    public async Task ExecuteAsync(string orderId, string customerId, string sku, int quantity, decimal unitPrice, CancellationToken ct)
    {
        await _txManager.ExecuteAsync(async context =>
        {
            decimal total = quantity * unitPrice;
            await _orderRepo.CreateOrderAsync(orderId, customerId, total, context);
            await _inventoryRepo.DeductStockAsync(sku, quantity, context);
            await _paymentRepo.RecordPaymentAsync(Guid.NewGuid().ToString("N"), orderId, total, context);
        }, TransactionOptions.Default, ct);
    }
}
```

---

## 2. Explicit Transaction Lifecycle (`BeginAsync`)

When application workflows require manual control over the commit/rollback timing, use `BeginAsync`:

```csharp
await using ITransaction tx = await txManager.BeginAsync(TransactionOptions.Default, cancellationToken);

try
{
    await orderRepo.CreateOrderAsync(orderId, customerId, totalAmount, tx.Context);
    await inventoryRepo.DeductStockAsync(sku, quantity, tx.Context);

    // Explicit physical commit
    await tx.CommitAsync(cancellationToken);
}
catch (Exception)
{
    // If CommitAsync is not reached, tx.DisposeAsync() guarantees physical ROLLBACK
    throw;
}
```

---

## 3. Key Invariants
- **Auto-Rollback on Dispose**: An uncommitted `ITransaction` automatically triggers `RollbackAsync` when disposed at the end of the `await using` block.
- **State Transparency**: `tx.State` exposes the transaction state (`Active`, `Committed`, `RolledBack`, `Failed`, `Disposed`).
