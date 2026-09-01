// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Result;

namespace EricksonLopez.Transaction.Mediator;

/// <summary>
/// Pipeline behavior that executes mediator commands within an automatic database transaction boundary.
/// Automatically commits on success and rolls back when an unhandled exception is thrown or when
/// a functional failure (<see cref="IResultOutcome.IsFailure"/>) is returned.
/// </summary>
/// <typeparam name="TRequest">The type of request.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public sealed class TransactionPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
{
    private static readonly bool IsTransactional =
        typeof(ITransactionalCommand).IsAssignableFrom(typeof(TRequest));


    private readonly ITransactionManager _transactionManager;
    private readonly IEnumerable<ITransactionEnlistment> _enlistments;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPipelineBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager instance.</param>
    /// <param name="enlistments">Optional collection of transaction enlistment lifecycle participants.</param>
    public TransactionPipelineBehavior(
        ITransactionManager transactionManager,
        IEnumerable<ITransactionEnlistment>? enlistments = null)
    {
        _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        _enlistments = enlistments ?? [];
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle<TNext>(
        TRequest request,
        TNext next,
        CancellationToken cancellationToken)
        where TNext : struct, INext<TResponse>
    {
        if (!IsTransactional)
        {
            return await next.InvokeAsync().ConfigureAwait(false);
        }

        var options = request is ITransactionalCommandOptions configurable
            ? configurable.TransactionOptions
            : null;

        await using var transaction = await _transactionManager
            .BeginAsync(options, cancellationToken)
            .ConfigureAwait(false);

        foreach (var enlistment in _enlistments)
        {
            transaction.Context.Enlist(enlistment);
        }

        TResponse response;
        try
        {
            response = await next.InvokeAsync().ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (response is IResultOutcome outcome)
        {
            if (outcome.IsSuccess)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
