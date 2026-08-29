// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EricksonLopez.Transaction.Diagnostics;

/// <summary>
/// Provides diagnostic, tracing, and metric instruments for transaction monitoring.
/// </summary>
public static class TransactionDiagnostics
{
    /// <summary>Specifies the activity source and meter name used for OpenTelemetry instrumentation.</summary>
    public const string SourceName = "EricksonLopez.Transaction";

    /// <summary>Specifies the semantic version of the instrumentation schema emitted by this source.</summary>
    public const string Version = "1.0.0";

    /// <summary>
    /// Gets the <see cref="ActivitySource"/> used for distributed transaction tracing.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(SourceName, Version);

    /// <summary>
    /// Gets the <see cref="Meter"/> used for transaction rate, duration, and outcome metrics.
    /// </summary>
    public static readonly Meter Meter = new(SourceName, Version);

    // Semantic Attribute Names and Units
    private const string TagIsolationLevel = "transaction.isolation_level";
    private const string TagOutcome = "transaction.outcome";
    private const string TagErrorType = "error.type";
    private const string UnitTransaction = "{transaction}";
    private const string UnitSavepoint = "{savepoint}";

    // Meters and Counters
    private static readonly Counter<long> StartedCounter = Meter.CreateCounter<long>(
        "transactions.started",
        unit: UnitTransaction,
        description: "Total number of transactions started.");

    private static readonly Counter<long> CommittedCounter = Meter.CreateCounter<long>(
        "transactions.committed",
        unit: UnitTransaction,
        description: "Total number of transactions successfully committed.");

    private static readonly Counter<long> RolledBackCounter = Meter.CreateCounter<long>(
        "transactions.rolled_back",
        unit: UnitTransaction,
        description: "Total number of transactions rolled back.");

    private static readonly Counter<long> FailedCounter = Meter.CreateCounter<long>(
        "transactions.failed",
        unit: UnitTransaction,
        description: "Total number of transactions that failed during execution or commit.");

    private static readonly Histogram<double> DurationHistogram = Meter.CreateHistogram<double>(
        "transactions.duration",
        unit: "ms",
        description: "Duration of transaction lifetimes from begin to commit/rollback.");

    private static readonly Counter<long> SavepointCreatedCounter = Meter.CreateCounter<long>(
        "transactions.savepoints.created",
        unit: UnitSavepoint,
        description: "Total number of transaction savepoints created.");

    private static readonly Counter<long> SavepointRolledBackCounter = Meter.CreateCounter<long>(
        "transactions.savepoints.rolled_back",
        unit: UnitSavepoint,
        description: "Total number of savepoint rollbacks executed.");

    private static readonly Counter<long> SavepointReleasedCounter = Meter.CreateCounter<long>(
        "transactions.savepoints.released",
        unit: UnitSavepoint,
        description: "Total number of savepoints released.");

    /// <summary>
    /// Starts a new tracing activity for a transaction.
    /// </summary>
    /// <param name="name">The operation name for the activity.</param>
    /// <param name="transactionId">The unique transaction identifier.</param>
    /// <param name="isolationLevel">The isolation level of the transaction.</param>
    /// <param name="transactionName">An optional logical name or purpose assigned to the transaction.</param>
    /// <returns>The created <see cref="Activity"/>, or <see langword="null"/> if no listeners are registered.</returns>
    public static Activity? StartActivity(
        string name,
        Guid transactionId,
        TransactionIsolationLevel isolationLevel,
        string? transactionName = null)
    {
        Activity? activity = ActivitySource.StartActivity(name, ActivityKind.Internal);
        if (activity is not null)
        {
            activity.SetTag("db.system", "relational");
            activity.SetTag("transaction.id", transactionId.ToString());
            activity.SetTag(TagIsolationLevel, isolationLevel.ToString());

            if (!string.IsNullOrWhiteSpace(transactionName))
            {
                activity.SetTag("transaction.name", transactionName);
            }
        }

        return activity;
    }

    /// <summary>
    /// Records the initiation of a transaction with the specified isolation level.
    /// </summary>
    /// <param name="isolationLevel">The isolation level of the started transaction.</param>
    public static void RecordStarted(TransactionIsolationLevel isolationLevel)
    {
        StartedCounter.Add(1, new KeyValuePair<string, object?>(TagIsolationLevel, isolationLevel.ToString()));
    }

    /// <summary>
    /// Records a successful transaction commit and its elapsed duration.
    /// </summary>
    /// <param name="isolationLevel">The isolation level of the committed transaction.</param>
    /// <param name="durationMs">The total duration of the transaction in milliseconds.</param>
    public static void RecordCommitted(TransactionIsolationLevel isolationLevel, double durationMs)
    {
        CommittedCounter.Add(1, new KeyValuePair<string, object?>(TagIsolationLevel, isolationLevel.ToString()));
        DurationHistogram.Record(durationMs, new KeyValuePair<string, object?>(TagOutcome, "committed"));
    }

    /// <summary>
    /// Records a transaction rollback and its elapsed duration.
    /// </summary>
    /// <param name="isolationLevel">The isolation level of the rolled-back transaction.</param>
    /// <param name="durationMs">The total duration of the transaction in milliseconds.</param>
    public static void RecordRolledBack(TransactionIsolationLevel isolationLevel, double durationMs)
    {
        RolledBackCounter.Add(1, new KeyValuePair<string, object?>(TagIsolationLevel, isolationLevel.ToString()));
        DurationHistogram.Record(durationMs, new KeyValuePair<string, object?>(TagOutcome, "rolled_back"));
    }

    /// <summary>
    /// Records a transaction failure, duration, and error type.
    /// </summary>
    /// <param name="isolationLevel">The isolation level of the failed transaction.</param>
    /// <param name="durationMs">The total duration of the transaction in milliseconds.</param>
    /// <param name="errorType">The error type or classification associated with the failure, or <see langword="null"/> if unknown.</param>
    public static void RecordFailed(TransactionIsolationLevel isolationLevel, double durationMs, string? errorType)
    {
        FailedCounter.Add(1,
            new KeyValuePair<string, object?>(TagIsolationLevel, isolationLevel.ToString()),
            new KeyValuePair<string, object?>(TagErrorType, errorType ?? "Unknown"));
        DurationHistogram.Record(durationMs, new KeyValuePair<string, object?>(TagOutcome, "failed"));
    }

    /// <summary>
    /// Records the creation of a savepoint.
    /// </summary>
    public static void RecordSavepointCreated() => SavepointCreatedCounter.Add(1);

    /// <summary>
    /// Records the rollback of a savepoint.
    /// </summary>
    public static void RecordSavepointRolledBack() => SavepointRolledBackCounter.Add(1);

    /// <summary>
    /// Records the release of a savepoint.
    /// </summary>
    public static void RecordSavepointReleased() => SavepointReleasedCounter.Add(1);
}
