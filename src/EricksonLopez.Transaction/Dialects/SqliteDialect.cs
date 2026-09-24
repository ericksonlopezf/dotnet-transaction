// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Dialects;

internal sealed class SqliteDialect : IDatabaseDialect
{
    public bool CanHandle(DbConnection connection)
    {
        return connection.GetType().FullName?.StartsWith("Microsoft.Data.Sqlite.", StringComparison.Ordinal) == true ||
               connection.GetType().FullName?.StartsWith("System.Data.SQLite.", StringComparison.Ordinal) == true;
    }

    public Task ApplyReadOnlyModeAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        // SQLite PRAGMA query_only is not standard per-transaction.
        return Task.CompletedTask;
    }

    public string GetSavepointCreationSql(string savepointName) => $"SAVEPOINT {savepointName};";
    public string GetSavepointRollbackSql(string savepointName) => $"ROLLBACK TO SAVEPOINT {savepointName};";
    public string? GetSavepointReleaseSql(string savepointName) => $"RELEASE SAVEPOINT {savepointName};";
}
