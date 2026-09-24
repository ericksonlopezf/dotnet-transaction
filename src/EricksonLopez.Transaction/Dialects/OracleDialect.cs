// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Dialects;

internal sealed class OracleDialect : IDatabaseDialect
{
    public bool CanHandle(DbConnection connection)
    {
        string? fullName = connection.GetType().FullName;
        return fullName?.IndexOf("Oracle", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public async Task ApplyReadOnlyModeAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
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

    public string GetSavepointCreationSql(string savepointName) => $"SAVEPOINT {savepointName};";
    public string GetSavepointRollbackSql(string savepointName) => $"ROLLBACK TO SAVEPOINT {savepointName};";
    public string? GetSavepointReleaseSql(string savepointName) => null; // Oracle does not support RELEASE SAVEPOINT
}
