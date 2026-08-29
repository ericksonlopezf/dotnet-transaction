// Copyright © Erickson Lopez. MIT License.
using System.Data;

namespace EricksonLopez.Transaction;

/// <summary>
/// Specifies the transaction locking behavior and isolation level for database operations.
/// </summary>
/// <remarks>
/// <para>
/// Choosing an appropriate isolation level is a trade-off between concurrency throughput and anomaly prevention:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Isolation Level</term>
///     <description>Phenomena Prevented &amp; Operational Characteristics</description>
///   </listheader>
///   <item>
///     <term><see cref="ReadUncommitted"/></term>
///     <description>Allows dirty reads, non-repeatable reads, and phantom reads. Lowest locking overhead, zero consistency guarantees.</description>
///   </item>
///   <item>
///     <term><see cref="ReadCommitted"/></term>
///     <description>Prevents dirty reads. Default in PostgreSQL and SQL Server. Susceptible to non-repeatable reads and phantoms.</description>
///   </item>
///   <item>
///     <term><see cref="RepeatableRead"/></term>
///     <description>Prevents dirty reads and non-repeatable reads. In PostgreSQL, also prevents phantom reads via MVCC snapshotting.</description>
///   </item>
///   <item>
///     <term><see cref="Serializable"/></term>
///     <description>Strict serializable execution. Prevents all anomalies including write skew. Throws serialization failures (PostgreSQL SQLSTATE 40001) under conflict.</description>
///   </item>
///   <item>
///     <term><see cref="Snapshot"/></term>
///     <description>Provides snapshot isolation using row versioning without blocking concurrent readers and writers.</description>
///   </item>
/// </list>
/// </remarks>
public enum TransactionIsolationLevel
{
    /// <summary>
    /// Specifies that an undetermined isolation level different from explicit levels is used.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// Specifies that dirty reads are possible, meaning no shared locks are issued and no exclusive locks are honored.
    /// </summary>
    ReadUncommitted = 1,

    /// <summary>
    /// Specifies that shared locks are held while data is read to avoid dirty reads, but data can be modified before commit.
    /// </summary>
    ReadCommitted = 2,

    /// <summary>
    /// Specifies that locks are placed on all queried data, preventing other concurrent transactions from updating rows.
    /// </summary>
    RepeatableRead = 3,

    /// <summary>
    /// Specifies that range locks are placed on the dataset, preventing other transactions from updating or inserting rows.
    /// </summary>
    Serializable = 4,

    /// <summary>
    /// Specifies snapshot isolation using row versioning, reducing blocking between concurrent readers and writers.
    /// </summary>
    Snapshot = 5
}
