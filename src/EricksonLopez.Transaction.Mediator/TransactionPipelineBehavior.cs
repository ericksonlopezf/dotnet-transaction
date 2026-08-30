// Copyright © Erickson Lopez. MIT License.
using System;
using System.Reflection;
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
        typeof(ITransactionalCommand).IsAssignableFrom(typeof(TRequest)) ||
        typeof(TRequest).GetCustomAttribute<TransactionalAttribute>(inherit: true) is not null;

    private static readonly TransactionOptions? ConfiguredOptions =
        typeof(TRequest).GetCustomAttribute<TransactionalAttribute>(inherit: true) is { } attr
            ? new TransactionOptions
            {
                IsolationLevel = attr.IsolationLevel,
                Timeout = attr.TimeoutSeconds > 0 ? TimeSpan.FromSeconds(attr.TimeoutSeconds) : null
            }
            : null;

    private readonly ITransactionManager _transactionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPipelineBehavior{TRequest, TResponse}"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction manager instance.</param>
    public TransactionPipelineBehavior(ITransactionManager transactionManager)
    {
        _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
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

        await using var transaction = await _transactionManager
            .BeginAsync(ConfiguredOptions, cancellationToken)
            .ConfigureAwait(false);

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
