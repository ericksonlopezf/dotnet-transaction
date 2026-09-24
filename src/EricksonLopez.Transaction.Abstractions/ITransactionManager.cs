// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction;

/// <summary>
/// Defines the primary coordinator for creating, executing, and orchestrating database transaction boundaries.
/// </summary>
public interface ITransactionManager
{
    /// <summary>
    /// Gets the ambient transaction context active on the current asynchronous flow, or <see langword="null"/> if no transaction is active.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> both when no transaction has been started and when the current execution
    /// is inside a <see cref="NestedTransactionBehavior.Suppress"/> scope. In the suppressed case, an outer
    /// transaction may exist on the calling flow, but its context is intentionally hidden from the suppressed
    /// execution to prevent ambient context pollution.
    /// </remarks>
    ITransactionContext? CurrentContext { get; }

    /// <summary>
    /// Begins a new transaction explicitly with the specified options.
    /// </summary>
    /// <param name="options">The transaction options controlling isolation level, timeout, and nesting behavior, or <see langword="null"/> to use defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains an <see cref="ITransaction"/> lifecycle handle.</returns>
    Task<ITransaction> BeginAsync(
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a delegate within an automatic transaction boundary, committing on success and rolling back on failure.
    /// </summary>
    /// <param name="operation">The asynchronous transactional operation to execute.</param>
    /// <param name="options">The transaction options controlling isolation level, timeout, and nesting behavior, or <see langword="null"/> to use defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    ///   <paramref name="options"/> specifies <see cref="NestedTransactionBehavior.Suppress"/>.
    ///   A context-receiving operation cannot run inside a suppressed transaction scope because no ambient
    ///   <see cref="ITransactionContext"/> is available. Use the parameterless
    ///   <see cref="ExecuteAsync(Func{Task}, TransactionOptions, CancellationToken)"/> overload instead.
    /// </exception>
    Task ExecuteAsync(
        Func<ITransactionContext, Task> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a parameterless delegate within an automatic transaction boundary, committing on success and rolling back on failure.
    /// </summary>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="options">The transaction options controlling isolation level, timeout, and nesting behavior, or <see langword="null"/> to use defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteAsync(
        Func<Task> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a delegate within an automatic transaction boundary, returning the computed result on successful commit and rolling back on failure.
    /// </summary>
    /// <typeparam name="TResult">The type of the value returned by the transactional operation.</typeparam>
    /// <param name="operation">The asynchronous transactional operation to execute.</param>
    /// <param name="options">The transaction options controlling isolation level, timeout, and nesting behavior, or <see langword="null"/> to use defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the value produced by the operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    ///   <paramref name="options"/> specifies <see cref="NestedTransactionBehavior.Suppress"/>.
    ///   A context-receiving operation cannot run inside a suppressed transaction scope because no ambient
    ///   <see cref="ITransactionContext"/> is available. Use the parameterless
    ///   <see cref="ExecuteAsync{TResult}(Func{Task{TResult}}, TransactionOptions, CancellationToken)"/> overload instead.
    /// </exception>
    Task<TResult> ExecuteAsync<TResult>(
        Func<ITransactionContext, Task<TResult>> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a parameterless delegate within an automatic transaction boundary, returning the computed result on successful commit and rolling back on failure.
    /// </summary>
    /// <typeparam name="TResult">The type of the value returned by the transactional operation.</typeparam>
    /// <param name="operation">The asynchronous operation to execute.</param>
    /// <param name="options">The transaction options controlling isolation level, timeout, and nesting behavior, or <see langword="null"/> to use defaults.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the value produced by the operation.</returns>
    Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default);
}
