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
    /// <exception cref="ArgumentException"><paramref name="commandText"/> is <see langword="null"/> or empty</exception>
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

        CancellationToken combinedToken = cancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, cancellationToken).Token
            : context.CancellationToken;

        return new CommandDefinition(
            commandText: commandText,
            parameters: parameters,
            transaction: context.Transaction,
            commandTimeout: commandTimeout,
            commandType: commandType,
            flags: flags,
            cancellationToken: combinedToken);
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
    public static Task<int> ExecuteAsync(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        CommandDefinition command = context.AsCommand(sql, param, commandType, CommandFlags.Buffered, commandTimeout, cancellationToken);
        return context.Connection.ExecuteAsync(command);
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
    public static Task<IEnumerable<T>> QueryAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        CommandDefinition command = context.AsCommand(sql, param, commandType, CommandFlags.Buffered, commandTimeout, cancellationToken);
        return context.Connection.QueryAsync<T>(command);
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
    public static Task<T?> QuerySingleOrDefaultAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        CommandDefinition command = context.AsCommand(sql, param, commandType, CommandFlags.Buffered, commandTimeout, cancellationToken);
        return context.Connection.QuerySingleOrDefaultAsync<T>(command);
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
    public static Task<T?> QueryFirstOrDefaultAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        CommandDefinition command = context.AsCommand(sql, param, commandType, CommandFlags.Buffered, commandTimeout, cancellationToken);
        return context.Connection.QueryFirstOrDefaultAsync<T>(command);
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
    public static Task<T?> ExecuteScalarAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        CommandDefinition command = context.AsCommand(sql, param, commandType, CommandFlags.Buffered, commandTimeout, cancellationToken);
        return context.Connection.ExecuteScalarAsync<T>(command);
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
    public static async Task<IDataReader> ExecuteReaderAsync(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        CommandDefinition command = context.AsCommand(sql, param, commandType, CommandFlags.Buffered, commandTimeout, cancellationToken);
        return await context.Connection.ExecuteReaderAsync(command);
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
    public static Task<T> QuerySingleAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        CommandDefinition command = context.AsCommand(sql, param, commandType, CommandFlags.Buffered, commandTimeout, cancellationToken);
        return context.Connection.QuerySingleAsync<T>(command);
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
    public static Task<T> QueryFirstAsync<T>(
        this ITransactionContext context,
        string sql,
        object? param = null,
        int? commandTimeout = null,
        CommandType? commandType = null,
        CancellationToken cancellationToken = default)
    {
        CommandDefinition command = context.AsCommand(sql, param, commandType, CommandFlags.Buffered, commandTimeout, cancellationToken);
        return context.Connection.QueryFirstAsync<T>(command);
    }
}
