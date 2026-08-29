// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Testing;

/// <summary>
/// Provides an in-memory test double of <see cref="ITransactionManager"/> for unit and integration testing.
/// </summary>
/// <remarks>
/// All transactions created by this manager are tracked in <see cref="StartedTransactions"/>,
/// enabling assertions on transaction lifecycle behavior.
/// The <see cref="ExceptionToThrowOnCommit"/> property can be used to simulate commit failures.
/// </remarks>
public sealed class FakeTransactionManager : ITransactionManager
{
    private readonly List<FakeTransaction> _startedTransactions = new();

    /// <summary>
    /// Gets the list of transactions created by this manager.
    /// </summary>
    public IReadOnlyList<FakeTransaction> StartedTransactions => _startedTransactions.ToArray();

    /// <summary>
    /// Gets or sets an optional exception to throw during commit across created transactions.
    /// </summary>
    public Exception? ExceptionToThrowOnCommit { get; set; }

    /// <summary>
    /// Gets or sets the ambient transaction context active on the current asynchronous flow.
    /// </summary>
    /// <remarks>The setter is available to allow tests to pre-configure or inspect ambient state.</remarks>
    public ITransactionContext? CurrentContext { get; set; }

    /// <inheritdoc/>
    public Task<ITransaction> BeginAsync(TransactionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var context = new FakeTransactionContext(isolationLevel: options?.IsolationLevel ?? TransactionIsolationLevel.ReadCommitted)
        {
            CancellationToken = cancellationToken
        };

        var tx = new FakeTransaction(context)
        {
            ExceptionToThrowOnCommit = ExceptionToThrowOnCommit
        };

        _startedTransactions.Add(tx);
        CurrentContext = context;
        return Task.FromResult<ITransaction>(tx);
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(
        Func<ITransactionContext, Task> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using ITransaction tx = await BeginAsync(options, cancellationToken);
        await operation(tx.Context);
        await tx.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(
        Func<Task> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using ITransaction tx = await BeginAsync(options, cancellationToken);
        await operation();
        await tx.CommitAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<ITransactionContext, Task<TResult>> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using ITransaction tx = await BeginAsync(options, cancellationToken);
        TResult result = await operation(tx.Context);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    /// <inheritdoc/>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        await using ITransaction tx = await BeginAsync(options, cancellationToken);
        TResult result = await operation();
        await tx.CommitAsync(cancellationToken);
        return result;
    }
}
