// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using EricksonLopez.Transaction.Exceptions;
using EricksonLopez.Transaction.Internal;
using Microsoft.Extensions.Logging;

namespace EricksonLopez.Transaction;

/// <summary>
/// Provides transaction coordination, ambient context propagation, and savepoint management.
/// </summary>
/// <remarks>
/// <para>
/// The ambient transaction context is propagated through <see cref="System.Threading.AsyncLocal{T}"/> and is therefore
/// automatically scoped to the current asynchronous execution flow. Nested calls to
/// <see cref="BeginAsync"/> respect the <see cref="TransactionOptions.NestedBehavior"/> setting
/// of the requested options.
/// </para>
/// <para>
/// This type is registered as a scoped service and is not safe to share across concurrent
/// asynchronous execution flows without separate DI scopes.
/// </para>
/// </remarks>
public sealed partial class TransactionManager : ITransactionManager
{
    private static readonly AsyncLocal<ITransactionContext?> AmbientContextHolder = new();
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<TransactionManager>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionManager"/> class with the specified connection factory.
    /// </summary>
    /// <param name="connectionFactory">The database connection factory used to create database connections.</param>
    /// <param name="logger">An optional logger instance for diagnostic reporting.</param>
    /// <exception cref="ArgumentNullException"><paramref name="connectionFactory"/> is <see langword="null"/></exception>
    public TransactionManager(
        IDbConnectionFactory connectionFactory,
        ILogger<TransactionManager>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger;
    }

    /// <inheritdoc/>
    public ITransactionContext? CurrentContext => AmbientContextHolder.Value;

    /// <inheritdoc/>
    public Task<ITransaction> BeginAsync(
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        TransactionOptions effectiveOptions = options ?? TransactionOptions.Default;
        ITransactionContext? current = CurrentContext;

        if (effectiveOptions.NestedBehavior == NestedTransactionBehavior.Suppress)
        {
            if (_logger is not null)
            {
                Log.SuppressedScopeBeginning(_logger);
            }

            ITransaction suppressedScope = new SuppressedTransactionScope(current, AmbientContextHolder);
            return Task.FromResult(suppressedScope);
        }

        if (current is not null)
        {
            return effectiveOptions.NestedBehavior switch
            {
                NestedTransactionBehavior.UseSavepoint => CreateSavepointScopeAsync(current, effectiveOptions, cancellationToken),
                NestedTransactionBehavior.JoinExisting => Task.FromResult<ITransaction>(new JoinExistingTransactionScope(current)),
                NestedTransactionBehavior.RequireNew => CreatePhysicalTransactionScopeAsync(effectiveOptions, cancellationToken),
                _ => CreateSavepointScopeAsync(current, effectiveOptions, cancellationToken)
            };
        }

        return CreatePhysicalTransactionScopeAsync(effectiveOptions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(
        Func<ITransactionContext, Task> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        TransactionOptions effectiveOptions = options ?? TransactionOptions.Default;

        if (effectiveOptions.NestedBehavior == NestedTransactionBehavior.Suppress)
        {
            throw new InvalidOperationException("Cannot pass an ITransactionContext operation to a suppressed transaction scope. Use ExecuteAsync(Func<Task>) instead.");
        }

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
            await using ITransaction transaction = await BeginAsync(effectiveOptions, combinedToken).ConfigureAwait(false);
            AmbientContextHolder.Value = transaction.Context;
            await operation(transaction.Context).ConfigureAwait(false);
            await transaction.CommitAsync(combinedToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            if (_logger is not null)
            {
                Log.TransactionTimeoutExceeded(_logger, effectiveOptions.Timeout!.Value);
            }

            throw new TransactionTimeoutException(effectiveOptions.Timeout!.Value);
        }
    }

    /// <inheritdoc/>
    public async Task ExecuteAsync(
        Func<Task> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
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
            await using ITransaction transaction = await BeginAsync(effectiveOptions, combinedToken).ConfigureAwait(false);
            if (effectiveOptions.NestedBehavior != NestedTransactionBehavior.Suppress)
            {
                AmbientContextHolder.Value = transaction.Context;
            }

            await operation().ConfigureAwait(false);
            await transaction.CommitAsync(combinedToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            if (_logger is not null)
            {
                Log.TransactionTimeoutExceeded(_logger, effectiveOptions.Timeout!.Value);
            }

            throw new TransactionTimeoutException(effectiveOptions.Timeout!.Value);
        }
    }

    /// <inheritdoc/>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<ITransactionContext, Task<TResult>> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        TransactionOptions effectiveOptions = options ?? TransactionOptions.Default;

        if (effectiveOptions.NestedBehavior == NestedTransactionBehavior.Suppress)
        {
            throw new InvalidOperationException("Cannot pass an ITransactionContext operation to a suppressed transaction scope. Use ExecuteAsync(Func<Task<TResult>>) instead.");
        }

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
            await using ITransaction transaction = await BeginAsync(effectiveOptions, combinedToken).ConfigureAwait(false);
            AmbientContextHolder.Value = transaction.Context;
            TResult result = await operation(transaction.Context).ConfigureAwait(false);
            await transaction.CommitAsync(combinedToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            if (_logger is not null)
            {
                Log.TransactionTimeoutExceeded(_logger, effectiveOptions.Timeout!.Value);
            }

            throw new TransactionTimeoutException(effectiveOptions.Timeout!.Value);
        }
    }

    /// <inheritdoc/>
    public async Task<TResult> ExecuteAsync<TResult>(
        Func<Task<TResult>> operation,
        TransactionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
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
            await using ITransaction transaction = await BeginAsync(effectiveOptions, combinedToken).ConfigureAwait(false);
            if (effectiveOptions.NestedBehavior != NestedTransactionBehavior.Suppress)
            {
                AmbientContextHolder.Value = transaction.Context;
            }

            TResult result = await operation().ConfigureAwait(false);
            await transaction.CommitAsync(combinedToken).ConfigureAwait(false);
            return result;
        }
        catch (OperationCanceledException) when (timeoutCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            if (_logger is not null)
            {
                Log.TransactionTimeoutExceeded(_logger, effectiveOptions.Timeout!.Value);
            }

            throw new TransactionTimeoutException(effectiveOptions.Timeout!.Value);
        }
    }

    private async Task<ITransaction> CreatePhysicalTransactionScopeAsync(
        TransactionOptions options,
        CancellationToken cancellationToken)
    {
        DbConnection connection = await _connectionFactory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        System.Data.IsolationLevel systemIsolation = IsolationLevelConverter.ToSystemIsolationLevel(options.IsolationLevel);
        DbTransaction dbTx = await connection.BeginTransactionAsync(systemIsolation, cancellationToken).ConfigureAwait(false);

        if (options.ReadOnly)
        {
            await ApplyReadOnlyModeAsync(connection, dbTx, cancellationToken).ConfigureAwait(false);
        }

        var stateMachine = new TransactionStateMachine(TransactionState.Active);
        var context = new TransactionContext(
            Guid.NewGuid(),
            connection,
            dbTx,
            options.IsolationLevel,
            stateMachine,
            cancellationToken);

        var physicalTx = new PhysicalTransaction(
            context,
            stateMachine,
            connection,
            dbTx,
            ownsConnection: true,
            transactionName: options.TransactionName);

        return new AmbientTransactionScope(physicalTx, CurrentContext, AmbientContextHolder);
    }

    private static async Task<ITransaction> CreateSavepointScopeAsync(
        ITransactionContext currentContext,
        TransactionOptions options,
        CancellationToken cancellationToken)
    {
        string savepointName = options.TransactionName ?? $"sp_{Guid.NewGuid():N}";
        ISavepoint savepoint = await currentContext.CreateSavepointAsync(savepointName, cancellationToken).ConfigureAwait(false);
        var savepointScope = new SavepointTransactionScope(currentContext, savepoint);
        return new AmbientTransactionScope(savepointScope, currentContext, AmbientContextHolder);
    }

    private static async Task ApplyReadOnlyModeAsync(
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        string connectionTypeName = connection.GetType().Name;
        // PostgreSQL: enforce transaction-level read-only mode via SQL statement
        if (connectionTypeName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await using DbCommand cmd = connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "SET TRANSACTION READ ONLY;";
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (connectionTypeName.Contains("MySql", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await using DbCommand cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "SET TRANSACTION READ ONLY;";
                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Silently ignore if not supported by driver in current state
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "Beginning suppressed transaction scope. Ambient context will be suspended.")]
        public static partial void SuppressedScopeBeginning(ILogger logger);

        [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Transaction execution exceeded timeout of {Timeout}.")]
        public static partial void TransactionTimeoutExceeded(ILogger logger, TimeSpan timeout);
    }
}
