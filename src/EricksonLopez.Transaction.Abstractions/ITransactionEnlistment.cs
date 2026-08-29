// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction;

/// <summary>
/// Defines lifecycle hooks for participants enlisting in a transaction boundary.
/// </summary>
public interface ITransactionEnlistment
{
    /// <summary>
    /// Executes immediately prior to committing the physical database transaction.
    /// </summary>
    /// <param name="context">The active transaction context.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeforeCommitAsync(ITransactionContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Executes immediately after the physical database transaction has committed successfully.
    /// </summary>
    /// <param name="context">The committed transaction context.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AfterCommitAsync(ITransactionContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Executes after the transaction has been rolled back.
    /// </summary>
    /// <param name="context">The rolled-back transaction context.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AfterRollbackAsync(ITransactionContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>
    /// Executes when an exception occurs during the execution of the transaction or its commit phase.
    /// </summary>
    /// <param name="context">The active transaction context.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task OnExceptionAsync(ITransactionContext context, Exception exception, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
