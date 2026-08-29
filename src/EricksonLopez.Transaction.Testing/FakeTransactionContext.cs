// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Testing;

/// <summary>
/// Provides an in-memory test double of <see cref="ITransactionContext"/> for testing transactional components.
/// </summary>
/// <remarks>
/// <see cref="Connection"/> and <see cref="Transaction"/> always throw <see cref="System.NotSupportedException"/>.
/// The <see cref="State"/> property exposes a public setter to allow tests to simulate lifecycle transitions
/// without executing physical database operations.
/// </remarks>
public sealed class FakeTransactionContext : ITransactionContext
{
    private readonly List<ITransactionEnlistment> _enlistments = new();
    private readonly List<string> _createdSavepoints = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTransactionContext"/> class.
    /// </summary>
    /// <param name="transactionId">The unique transaction identifier, or <see langword="null"/> to generate a new identifier.</param>
    /// <param name="isolationLevel">The isolation level for the context.</param>
    public FakeTransactionContext(
        Guid? transactionId = null,
        TransactionIsolationLevel isolationLevel = TransactionIsolationLevel.ReadCommitted)
    {
        TransactionId = transactionId ?? Guid.NewGuid();
        IsolationLevel = isolationLevel;
        State = TransactionState.Active;
    }

    /// <inheritdoc/>
    public Guid TransactionId { get; }

    /// <inheritdoc/>
    public DbConnection Connection => throw new NotSupportedException("FakeTransactionContext does not provide a physical DbConnection.");

    /// <inheritdoc/>
    public DbTransaction Transaction => throw new NotSupportedException("FakeTransactionContext does not provide a physical DbTransaction.");

    /// <summary>
    /// Gets or sets the current lifecycle state of the transaction.
    /// </summary>
    /// <remarks>The setter is available to allow tests to simulate state transitions programmatically.</remarks>
    public TransactionState State { get; set; }

    /// <inheritdoc/>
    public TransactionIsolationLevel IsolationLevel { get; }

    /// <summary>
    /// Gets the cancellation token scoped to this transaction execution.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <inheritdoc/>
    public IReadOnlyList<ITransactionEnlistment> Enlistments => _enlistments.ToArray();

    /// <summary>
    /// Gets the list of names of savepoints created on this context.
    /// </summary>
    public IReadOnlyList<string> CreatedSavepoints => _createdSavepoints.ToArray();

    /// <inheritdoc/>
    public Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _createdSavepoints.Add(name);
        return Task.FromResult<ISavepoint>(new FakeSavepoint(name));
    }

    /// <inheritdoc/>
    public void Enlist(ITransactionEnlistment enlistment)
    {
        ArgumentNullException.ThrowIfNull(enlistment);
        _enlistments.Add(enlistment);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private sealed class FakeSavepoint : ISavepoint
    {
        public FakeSavepoint(string name) => Name = name;
        public string Name { get; }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReleaseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
