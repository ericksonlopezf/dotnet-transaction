// Copyright © Erickson Lopez. MIT License.
using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Dialects;

internal sealed class SqlServerDialect : IDatabaseDialect
{
    public bool CanHandle(DbConnection connection)
    {
        string? fullName = connection.GetType().FullName;
        return fullName?.StartsWith("Microsoft.Data.SqlClient.", StringComparison.Ordinal) == true ||
               fullName?.StartsWith("System.Data.SqlClient.", StringComparison.Ordinal) == true;
    }

    public Task ApplyReadOnlyModeAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        // SQL Server does not have a session-level "SET TRANSACTION READ ONLY" standard equivalent
        // that works cleanly without application intent locks. Ignored by default.
        return Task.CompletedTask;
    }

    public string GetSavepointCreationSql(string savepointName) => $"SAVE TRANSACTION {savepointName};";
    public string GetSavepointRollbackSql(string savepointName) => $"ROLLBACK TRANSACTION {savepointName};";
    public string? GetSavepointReleaseSql(string savepointName) => null; // SQL Server does not support releasing savepoints
}
