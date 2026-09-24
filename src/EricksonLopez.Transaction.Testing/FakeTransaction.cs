// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Testing;

/// <summary>
/// Provides an in-memory test double of <see cref="ITransaction"/> for tracking commit/rollback calls and injecting failures.
/// </summary>
/// <remarks>
/// <see cref="CommitCount"/> and <see cref="RollbackCount"/> enable assertions on how many times each
/// lifecycle operation was invoked. The <see cref="ExceptionToThrowOnCommit"/> and
/// <see cref="ExceptionToThrowOnRollback"/> properties allow tests to simulate provider-level failures.
/// Disposing without committing automatically transitions the context to <see cref="TransactionState.RolledBack"/>.
/// </remarks>
public sealed class FakeTransaction : ITransaction
{
    private readonly FakeTransactionContext _context;
    private readonly Action? _onDisposed;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeTransaction"/> class with an optional test context.
    /// </summary>
    /// <param name="context">The test transaction context to use, or <see langword="null"/> to create a new context.</param>
    /// <param name="onDisposed">An optional callback invoked when the fake transaction is disposed.</param>
    public FakeTransaction(FakeTransactionContext? context = null, Action? onDisposed = null)
    {
        _context = context ?? new FakeTransactionContext();
        _onDisposed = onDisposed;
        TransactionId = _context.TransactionId;
    }

    /// <inheritdoc/>
    public Guid TransactionId { get; }

    /// <inheritdoc/>
    public ITransactionContext Context => _context;

    /// <inheritdoc/>
    public TransactionState State => _context.State;

    /// <summary>
    /// Gets the number of times <see cref="CommitAsync"/> was invoked.
    /// </summary>
    public int CommitCount { get; private set; }

    /// <summary>
    /// Gets the number of times <see cref="RollbackAsync"/> was invoked.
    /// </summary>
    public int RollbackCount { get; private set; }

    /// <summary>
    /// Gets a value indicating whether this transaction has been disposed.
    /// </summary>
    public bool IsDisposed => _disposed;

    /// <summary>
    /// Gets or sets the optional exception to throw when <see cref="CommitAsync"/> is called.
    /// </summary>
    public Exception? ExceptionToThrowOnCommit { get; set; }

    /// <summary>
    /// Gets or sets the optional exception to throw when <see cref="RollbackAsync"/> is called.
    /// </summary>
    public Exception? ExceptionToThrowOnRollback { get; set; }

    /// <inheritdoc/>
    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        CommitCount++;
        if (ExceptionToThrowOnCommit is not null)
        {
            _context.State = TransactionState.Failed;
            throw ExceptionToThrowOnCommit;
        }

        _context.State = TransactionState.Committed;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        RollbackCount++;
        if (ExceptionToThrowOnRollback is not null)
        {
            _context.State = TransactionState.Failed;
            throw ExceptionToThrowOnRollback;
        }

        _context.State = TransactionState.RolledBack;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ISavepoint> CreateSavepointAsync(string name, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _context.CreateSavepointAsync(name, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _onDisposed?.Invoke();
            if (_context.State == TransactionState.Active)
            {
                _context.State = TransactionState.RolledBack;
            }
            else if (_context.State != TransactionState.Committed && _context.State != TransactionState.RolledBack)
            {
                _context.State = TransactionState.Disposed;
            }
        }

        return ValueTask.CompletedTask;
    }
}
