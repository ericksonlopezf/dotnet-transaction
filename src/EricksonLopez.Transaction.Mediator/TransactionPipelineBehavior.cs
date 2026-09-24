// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Mediator;
using EricksonLopez.Result;

namespace EricksonLopez.Transaction.Mediator;

/// <summary>
/// Encapsulates mediator command execution within an automatic database transaction boundary,
/// committing on success and rolling back when an unhandled exception is thrown or when a functional
/// failure (<see cref="IResultOutcome.IsFailure"/>) is returned.
/// </summary>
/// <typeparam name="TRequest">The type of transactional command request being processed.</typeparam>
/// <typeparam name="TResponse">The type of response returned by the command handler.</typeparam>
public sealed class TransactionPipelineBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ITransactionalCommand
{
    private readonly ITransactionManager _transactionManager;
    private readonly IEnumerable<ITransactionEnlistment> _enlistments;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPipelineBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction coordinator instance.</param>
    /// <param name="enlistments">An optional collection of transaction enlistment lifecycle participants.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transactionManager"/> is <see langword="null"/></exception>
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
        var options = request is ITransactionalCommandOptions configurable
            ? configurable.TransactionOptions
            : null;

        await using var transaction = await _transactionManager
            .BeginAsync(options, cancellationToken)
            .ConfigureAwait(false);

        if (options?.NestedBehavior != NestedTransactionBehavior.Suppress)
        {
            foreach (var enlistment in _enlistments)
            {
                transaction.Context.Enlist(enlistment);
            }
        }

        TResponse response;
        try
        {
            response = await next.InvokeAsync().ConfigureAwait(false);
        }
        catch (Exception primaryEx)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception rollbackEx)
            {
                throw new AggregateException(
                    "Transaction rollback failed following an unhandled command execution error.",
                    primaryEx,
                    rollbackEx);
            }

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
