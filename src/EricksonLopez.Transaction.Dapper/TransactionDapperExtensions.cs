// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Dapper;

namespace EricksonLopez.Transaction.Dapper;

/// <summary>
/// Provides Dapper extension methods bound directly to an active <see cref="ITransactionContext"/>.
/// </summary>
public static class TransactionDapperExtensions
{
    /// <summary>
    /// Constructs a <see cref="CommandDefinition"/> configured with the active transaction and cancellation token.
    /// </summary>
    /// <param name="context">The active transaction context.</param>
    /// <param name="commandText">The SQL command text to execute.</param>
    /// <param name="parameters">The command parameters, or <see langword="null"/> if none.</param>
    /// <param name="commandType">The command type interpretation, or <see langword="null"/> for default.</param>
    /// <param name="flags">The behavior flags for command execution.</param>
    /// <param name="commandTimeout">The per-command timeout in seconds, or <see langword="null"/> to use the default.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A configured <see cref="CommandDefinition"/> bound to the active transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentException"><paramref name="commandText"/> is <see langword="null"/> or whitespace</exception>
    public static CommandDefinition AsCommand(
        this ITransactionContext context,
        string commandText,
        object? parameters = null,
        CommandType? commandType = null,
        CommandFlags flags = CommandFlags.Buffered,
        int? commandTimeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);

        CancellationToken combinedToken = (!cancellationToken.CanBeCanceled || cancellationToken == context.CancellationToken)
            ? context.CancellationToken
            : cancellationToken;

        return new CommandDefinition(
            commandText: commandText,
            parameters: parameters,
            transaction: context.Transaction,
            commandTimeout: commandTimeout,
            commandType: commandType,
            flags: flags,
            cancellationToken: combinedToken);
    }

    internal static (CancellationToken Token, CancellationTokenSource? LinkedCts) ResolveToken(
        ITransactionContext context,
        CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || cancellationToken == context.CancellationToken)
        {
            return (context.CancellationToken, null);
        }

        if (!context.CancellationToken.CanBeCanceled)
        {
            return (cancellationToken, null);
        }

        CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken);
        return (linkedCts.Token, linkedCts);
    }

    /// <summary>
    /// Executes a SQL statement within the active transaction context.
    /// </summary>
    /// <param name="context">The active transaction context.</param>
    /// <param name="sql">The SQL statement to execute.</param>
    /// <param name="param">The parameters to pass to the command, or <see langword="null"/> if none.</param>
    /// <param name="commandTimeout">The command execution timeout in seconds, or <see langword="null"/> to use the default.</param>
    /// <param name="commandType">The command type interpretation, or <see langword="null"/> for default.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the number of rows affected.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/></exception>
    public static async Task<int> ExecuteAsync(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (token, linkedCts) = ResolveToken(context, cancellationToken);
        try
        {
            CommandDefinition command = new(
                commandText: sql,
                parameters: param,
                transaction: context.Transaction,
                commandTimeout: commandTimeout,
                commandType: commandType,
                flags: CommandFlags.Buffered,
                cancellationToken: token);

            return await context.Connection.ExecuteAsync(command).ConfigureAwait(false);
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }

    /// <summary>
    /// Executes a query within the active transaction context and returns mapped results.
    /// </summary>
    /// <typeparam name="T">The type of elements in the returned sequence.</typeparam>
    /// <param name="context">The active transaction context.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="param">The parameters to pass to the query, or <see langword="null"/> if none.</param>
    /// <param name="commandTimeout">The command execution timeout in seconds, or <see langword="null"/> to use the default.</param>
    /// <param name="commandType">The command type interpretation, or <see langword="null"/> for default.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains an enumerable sequence of mapped entities.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/></exception>
    public static async Task<IEnumerable<T>> QueryAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (token, linkedCts) = ResolveToken(context, cancellationToken);
        try
        {
            CommandDefinition command = new(
                commandText: sql,
                parameters: param,
                transaction: context.Transaction,
                commandTimeout: commandTimeout,
                commandType: commandType,
                flags: CommandFlags.Buffered,
                cancellationToken: token);

            return await context.Connection.QueryAsync<T>(command).ConfigureAwait(false);
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }

    /// <summary>
    /// Executes a query within the active transaction context and returns a single element or a default value.
    /// </summary>
    /// <typeparam name="T">The type of the entity to return.</typeparam>
    /// <param name="context">The active transaction context.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="param">The parameters to pass to the query, or <see langword="null"/> if none.</param>
    /// <param name="commandTimeout">The command execution timeout in seconds, or <see langword="null"/> to use the default.</param>
    /// <param name="commandType">The command type interpretation, or <see langword="null"/> for default.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the single matching element, or the default value if none was found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/></exception>
    public static async Task<T?> QuerySingleOrDefaultAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (token, linkedCts) = ResolveToken(context, cancellationToken);
        try
        {
            CommandDefinition command = new(
                commandText: sql,
                parameters: param,
                transaction: context.Transaction,
                commandTimeout: commandTimeout,
                commandType: commandType,
                flags: CommandFlags.Buffered,
                cancellationToken: token);

            return await context.Connection.QuerySingleOrDefaultAsync<T>(command).ConfigureAwait(false);
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }

    /// <summary>
    /// Executes a query within the active transaction context and returns the first element or a default value.
    /// </summary>
    /// <typeparam name="T">The type of the entity to return.</typeparam>
    /// <param name="context">The active transaction context.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="param">The parameters to pass to the query, or <see langword="null"/> if none.</param>
    /// <param name="commandTimeout">The command execution timeout in seconds, or <see langword="null"/> to use the default.</param>
    /// <param name="commandType">The command type interpretation, or <see langword="null"/> for default.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the first matching element, or the default value if none was found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/></exception>
    public static async Task<T?> QueryFirstOrDefaultAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (token, linkedCts) = ResolveToken(context, cancellationToken);
        try
        {
            CommandDefinition command = new(
                commandText: sql,
                parameters: param,
                transaction: context.Transaction,
                commandTimeout: commandTimeout,
                commandType: commandType,
                flags: CommandFlags.Buffered,
                cancellationToken: token);

            return await context.Connection.QueryFirstOrDefaultAsync<T>(command).ConfigureAwait(false);
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }

    /// <summary>
    /// Executes a query within the active transaction context and returns the first column of the first row.
    /// </summary>
    /// <typeparam name="T">The type of the scalar value.</typeparam>
    /// <param name="context">The active transaction context.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="param">The parameters to pass to the query, or <see langword="null"/> if none.</param>
    /// <param name="commandTimeout">The command execution timeout in seconds, or <see langword="null"/> to use the default.</param>
    /// <param name="commandType">The command type interpretation, or <see langword="null"/> for default.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the scalar value, or the default value if the result set is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/></exception>
    public static async Task<T?> ExecuteScalarAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (token, linkedCts) = ResolveToken(context, cancellationToken);
        try
        {
            CommandDefinition command = new(
                commandText: sql,
                parameters: param,
                transaction: context.Transaction,
                commandTimeout: commandTimeout,
                commandType: commandType,
                flags: CommandFlags.Buffered,
                cancellationToken: token);

            return await context.Connection.ExecuteScalarAsync<T>(command).ConfigureAwait(false);
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }

    /// <summary>
    /// Executes a multiple-result-set query within the active transaction context.
    /// </summary>
    /// <param name="context">The active transaction context.</param>
    /// <param name="sql">The SQL query returning multiple result sets to execute.</param>
    /// <param name="param">The parameters to pass to the query, or <see langword="null"/> if none.</param>
    /// <param name="commandTimeout">The command execution timeout in seconds, or <see langword="null"/> to use the default.</param>
    /// <param name="commandType">The command type interpretation, or <see langword="null"/> for default.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains a <see cref="SqlMapper.GridReader"/> for reading multiple results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/></exception>
    public static Task<SqlMapper.GridReader> QueryMultipleAsync(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        CommandDefinition command = context.AsCommand(sql, param, commandType, CommandFlags.Buffered, commandTimeout, cancellationToken);
        return context.Connection.QueryMultipleAsync(command);
    }

    /// <summary>
    /// Executes a query within the active transaction context and returns an <see cref="IDataReader"/>.
    /// </summary>
    /// <param name="context">The active transaction context.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="param">The parameters to pass to the query, or <see langword="null"/> if none.</param>
    /// <param name="commandTimeout">The command execution timeout in seconds, or <see langword="null"/> to use the default.</param>
    /// <param name="commandType">The command type interpretation, or <see langword="null"/> for default.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the <see cref="IDataReader"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/></exception>
    public static async Task<IDataReader> ExecuteReaderAsync(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        CommandDefinition command = context.AsCommand(sql, param, commandType, CommandFlags.Buffered, commandTimeout, cancellationToken);
        return await context.Connection.ExecuteReaderAsync(command).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a query within the active transaction context and returns a single element.
    /// </summary>
    /// <typeparam name="T">The type of the entity to return.</typeparam>
    /// <param name="context">The active transaction context.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="param">The parameters to pass to the query, or <see langword="null"/> if none.</param>
    /// <param name="commandTimeout">The command execution timeout in seconds, or <see langword="null"/> to use the default.</param>
    /// <param name="commandType">The command type interpretation, or <see langword="null"/> for default.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the single matching element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/></exception>
    public static async Task<T> QuerySingleAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (token, linkedCts) = ResolveToken(context, cancellationToken);
        try
        {
            CommandDefinition command = new(
                commandText: sql,
                parameters: param,
                transaction: context.Transaction,
                commandTimeout: commandTimeout,
                commandType: commandType,
                flags: CommandFlags.Buffered,
                cancellationToken: token);

            return await context.Connection.QuerySingleAsync<T>(command).ConfigureAwait(false);
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }

    /// <summary>
    /// Executes a query within the active transaction context and returns the first element.
    /// </summary>
    /// <typeparam name="T">The type of the entity to return.</typeparam>
    /// <param name="context">The active transaction context.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="param">The parameters to pass to the query, or <see langword="null"/> if none.</param>
    /// <param name="commandTimeout">The command execution timeout in seconds, or <see langword="null"/> to use the default.</param>
    /// <param name="commandType">The command type interpretation, or <see langword="null"/> for default.</param>
    /// <param name="cancellationToken">A token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation. The task result contains the first matching element.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/></exception>
    public static async Task<T> QueryFirstAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var (token, linkedCts) = ResolveToken(context, cancellationToken);
        try
        {
            CommandDefinition command = new(
                commandText: sql,
                parameters: param,
                transaction: context.Transaction,
                commandTimeout: commandTimeout,
                commandType: commandType,
                flags: CommandFlags.Buffered,
                cancellationToken: token);

            return await context.Connection.QueryFirstAsync<T>(command).ConfigureAwait(false);
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }
}
