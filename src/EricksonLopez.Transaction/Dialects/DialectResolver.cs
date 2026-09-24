// Copyright © Erickson Lopez. MIT License.
using System.Collections.Generic;
using System.Data.Common;

namespace EricksonLopez.Transaction.Dialects;

/// <summary>
/// Resolves the appropriate database dialect for a given connection.
/// </summary>
internal static class DialectResolver
{
    private static readonly IDatabaseDialect[] DefaultDialects = 
    [
        new PostgreSqlDialect(),
        new SqlServerDialect(),
        new MySqlDialect(),
        new SqliteDialect(),
        new OracleDialect()
    ];

    public static IDatabaseDialect Resolve(DbConnection connection, IEnumerable<IDatabaseDialect>? customDialects = null)
    {
        if (customDialects != null)
        {
            foreach (var dialect in customDialects)
            {
                if (dialect.CanHandle(connection))
                {
                    return dialect;
                }
            }
        }

        foreach (var dialect in DefaultDialects)
        {
            if (dialect.CanHandle(connection))
            {
                return dialect;
            }
        }

        return GenericSqlDialect.Instance;
    }
}
