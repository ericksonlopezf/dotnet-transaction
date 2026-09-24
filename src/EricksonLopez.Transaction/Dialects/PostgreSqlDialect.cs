// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Dialects;

internal sealed class PostgreSqlDialect : IDatabaseDialect
{
    public bool CanHandle(DbConnection connection)
    {
        return connection.GetType().FullName?.StartsWith("Npgsql.", StringComparison.Ordinal) == true;
    }

    public async Task ApplyReadOnlyModeAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        await using DbCommand cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SET TRANSACTION READ ONLY;";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public string GetSavepointCreationSql(string savepointName) => $"SAVEPOINT {savepointName};";
    public string GetSavepointRollbackSql(string savepointName) => $"ROLLBACK TO SAVEPOINT {savepointName};";
    public string? GetSavepointReleaseSql(string savepointName) => $"RELEASE SAVEPOINT {savepointName};";
}
