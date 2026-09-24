// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Result;
using EricksonLopez.Transaction.Exceptions;
using ResultInstance = EricksonLopez.Result.Result;

namespace EricksonLopez.Transaction.Result;

/// <summary>
/// Provides functional extension methods for <see cref="ITransactionManager"/> integrating with the <see cref="ResultInstance"/> type.
/// </summary>
public static class TransactionResultExtensions
{
    /// <summary>
    /// Executes a contextual asynchronous operation returning a <see cref="ResultInstance"/> within an automatic transaction boundary, committing on success and rolling back on failure.
    /// </summary>
    /// <param name="manager">The transaction manager.</param>
    /// <param name="operation">The asynchronous operation returning a <see cref="ResultInstance"/>.</param>
    /// <param name="options">The transaction options controlling isolation level, timeout, and nesting behavior, or <see langword="null"/> to use defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the resulting <see cref="ResultInstance"/> produced by the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> or <paramref name="operation"/> is <see langword="null"/></exception>
    public static async Task<ResultInstance> ExecuteResultAsync(
        this ITransactionManager manager,
        Func<ITransactionContext, Task<ResultInstance>> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(operation);

        TransactionOptions effectiveOptions = options ?? TransactionOptions.Default;

        CancellationToken combinedToken = cancellationToken;
        using CancellationTokenSource? timeoutCts = effectiveOptions.Timeout.HasValue
            ? new CancellationTokenSource(effectiveOptions.Timeout.Value)
            : null;

        using CancellationTokenSource? linkedCts = timeoutCts is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
            : null;

        if (linkedCts is not null)
        {
            combinedToken = linkedCts.Token;
        }

        try
        {
            await using ITransaction transaction = await manager.BeginAsync(effectiveOptions, combinedToken).ConfigureAwait(false);

            ResultInstance outcome = await operation(transaction.Context).ConfigureAwait(false);

            if (outcome.IsSuccess)
            {
                await transaction.CommitAsync(combinedToken).ConfigureAwait(false);
            }
            else
            {
                await transaction.RollbackAsync(combinedToken).ConfigureAwait(false);
            }

            return outcome;
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            throw new TransactionTimeoutException(effectiveOptions.Timeout!.Value);
        }
    }

    /// <summary>
    /// Executes a contextual asynchronous operation returning a value-typed <see cref="Result{TValue}"/> within an automatic transaction boundary, committing on success and rolling back on failure.
    /// </summary>
    /// <typeparam name="TValue">The type of the value contained in a successful result.</typeparam>
    /// <param name="manager">The transaction manager.</param>
    /// <param name="operation">The asynchronous operation returning a <see cref="Result{TValue}"/>.</param>
    /// <param name="options">The transaction options controlling isolation level, timeout, and nesting behavior, or <see langword="null"/> to use defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the resulting <see cref="Result{TValue}"/> produced by the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> or <paramref name="operation"/> is <see langword="null"/></exception>
    public static async Task<Result<TValue>> ExecuteResultAsync<TValue>(
        this ITransactionManager manager,
        Func<ITransactionContext, Task<Result<TValue>>> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(operation);

        TransactionOptions effectiveOptions = options ?? TransactionOptions.Default;

        CancellationToken combinedToken = cancellationToken;
        using CancellationTokenSource? timeoutCts = effectiveOptions.Timeout.HasValue
            ? new CancellationTokenSource(effectiveOptions.Timeout.Value)
            : null;

        using CancellationTokenSource? linkedCts = timeoutCts is not null
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token)
            : null;

        if (linkedCts is not null)
        {
            combinedToken = linkedCts.Token;
        }

        try
        {
            await using ITransaction transaction = await manager.BeginAsync(effectiveOptions, combinedToken).ConfigureAwait(false);

            Result<TValue> outcome = await operation(transaction.Context).ConfigureAwait(false);

            if (outcome.IsSuccess)
            {
                await transaction.CommitAsync(combinedToken).ConfigureAwait(false);
            }
            else
            {
                await transaction.RollbackAsync(combinedToken).ConfigureAwait(false);
            }

            return outcome;
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            throw new TransactionTimeoutException(effectiveOptions.Timeout!.Value);
        }
    }

    /// <summary>
    /// Executes a parameterless asynchronous operation returning a <see cref="ResultInstance"/> within an automatic transaction boundary, committing on success and rolling back on failure.
    /// </summary>
    /// <param name="manager">The transaction manager.</param>
    /// <param name="operation">The asynchronous operation returning a <see cref="ResultInstance"/>.</param>
    /// <param name="options">The transaction options controlling isolation level, timeout, and nesting behavior, or <see langword="null"/> to use defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the resulting <see cref="ResultInstance"/> produced by the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> or <paramref name="operation"/> is <see langword="null"/></exception>
    public static Task<ResultInstance> ExecuteResultAsync(
        this ITransactionManager manager,
        Func<Task<ResultInstance>> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return manager.ExecuteResultAsync(_ => operation(), options, cancellationToken);
    }

    /// <summary>
    /// Executes a parameterless asynchronous operation returning a value-typed <see cref="Result{TValue}"/> within an automatic transaction boundary, committing on success and rolling back on failure.
    /// </summary>
    /// <typeparam name="TValue">The type of the value contained in a successful result.</typeparam>
    /// <param name="manager">The transaction manager.</param>
    /// <param name="operation">The asynchronous operation returning a <see cref="Result{TValue}"/>.</param>
    /// <param name="options">The transaction options controlling isolation level, timeout, and nesting behavior, or <see langword="null"/> to use defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the resulting <see cref="Result{TValue}"/> produced by the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="manager"/> or <paramref name="operation"/> is <see langword="null"/></exception>
    public static Task<Result<TValue>> ExecuteResultAsync<TValue>(
        this ITransactionManager manager,
        Func<Task<Result<TValue>>> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return manager.ExecuteResultAsync(_ => operation(), options, cancellationToken);
    }
}
