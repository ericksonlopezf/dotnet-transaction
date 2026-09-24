// Copyright © Erickson Lopez. MIT License.
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Dialects;

internal sealed class GenericSqlDialect : IDatabaseDialect
{
    public static readonly GenericSqlDialect Instance = new();

    public bool CanHandle(DbConnection connection) => true;

    public Task ApplyReadOnlyModeAsync(DbConnection connection, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public string GetSavepointCreationSql(string savepointName) => $"SAVEPOINT {savepointName};";
    public string GetSavepointRollbackSql(string savepointName) => $"ROLLBACK TO SAVEPOINT {savepointName};";
    public string? GetSavepointReleaseSql(string savepointName) => $"RELEASE SAVEPOINT {savepointName};";
}
