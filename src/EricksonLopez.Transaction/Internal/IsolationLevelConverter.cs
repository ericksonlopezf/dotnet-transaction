// Copyright © Erickson Lopez. MIT License.
using System.Data;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Converts framework <see cref="TransactionIsolationLevel"/> to ADO.NET <see cref="IsolationLevel"/>.
/// </summary>
internal static class IsolationLevelConverter
{
    public static IsolationLevel ToSystemIsolationLevel(TransactionIsolationLevel level) => level switch
    {
        TransactionIsolationLevel.ReadUncommitted => IsolationLevel.ReadUncommitted,
        TransactionIsolationLevel.ReadCommitted => IsolationLevel.ReadCommitted,
        TransactionIsolationLevel.RepeatableRead => IsolationLevel.RepeatableRead,
        TransactionIsolationLevel.Serializable => IsolationLevel.Serializable,
        TransactionIsolationLevel.Snapshot => IsolationLevel.Snapshot,
        _ => IsolationLevel.Unspecified
    };

    public static TransactionIsolationLevel ToFrameworkIsolationLevel(IsolationLevel level) => level switch
    {
        IsolationLevel.ReadUncommitted => TransactionIsolationLevel.ReadUncommitted,
        IsolationLevel.ReadCommitted => TransactionIsolationLevel.ReadCommitted,
        IsolationLevel.RepeatableRead => TransactionIsolationLevel.RepeatableRead,
        IsolationLevel.Serializable => TransactionIsolationLevel.Serializable,
        IsolationLevel.Snapshot => TransactionIsolationLevel.Snapshot,
        _ => TransactionIsolationLevel.Unspecified
    };
}
