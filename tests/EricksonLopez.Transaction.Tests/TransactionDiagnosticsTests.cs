// Copyright © Erickson Lopez. MIT License.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using AwesomeAssertions;
using EricksonLopez.Transaction.Diagnostics;
using Xunit;

namespace EricksonLopez.Transaction.Tests;

public sealed class TransactionDiagnosticsTests
{
    [Fact]
    public void Constants_ShouldMatchExpectedValues()
    {
        TransactionDiagnostics.SourceName.Should().Be("EricksonLopez.Transaction");
        TransactionDiagnostics.Version.Should().Be("1.0.0");
        TransactionDiagnostics.ActivitySource.Name.Should().Be("EricksonLopez.Transaction");
        TransactionDiagnostics.Meter.Name.Should().Be("EricksonLopez.Transaction");
    }

    [Fact]
    public void StartActivity_WithoutListeners_ShouldReturnNull()
    {
        var txId = Guid.NewGuid();
        Activity? activity = TransactionDiagnostics.StartActivity("TestOp", txId, TransactionIsolationLevel.ReadCommitted);
        activity.Should().BeNull();
    }

    [Fact]
    public void StartActivity_WithListener_ShouldCreateActivityAndSetAllTags()
    {
        var txId = Guid.NewGuid();
        Activity? capturedActivity = null;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TransactionDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = a => capturedActivity = a
        };

        ActivitySource.AddActivityListener(listener);

        Activity? activity = TransactionDiagnostics.StartActivity(
            "Transaction.Custom",
            txId,
            TransactionIsolationLevel.Serializable,
            "TransferFunds");

        activity.Should().NotBeNull();
        activity.Should().BeSameAs(capturedActivity);
        activity!.TagObjects.First(t => t.Key == "db.system").Value.Should().Be("relational");
        activity.TagObjects.First(t => t.Key == "transaction.id").Value.Should().Be(txId.ToString());
        activity.TagObjects.First(t => t.Key == "transaction.isolation_level").Value.Should().Be("Serializable");
        activity.TagObjects.First(t => t.Key == "transaction.name").Value.Should().Be("TransferFunds");

        activity.Dispose();
    }

    [Fact]
    public void StartActivity_WithoutTransactionName_ShouldNotIncludeNameTag()
    {
        var txId = Guid.NewGuid();

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TransactionDiagnostics.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };

        ActivitySource.AddActivityListener(listener);

        Activity? activity = TransactionDiagnostics.StartActivity(
            "Transaction.Unnamed",
            txId,
            TransactionIsolationLevel.ReadCommitted,
            null);

        activity.Should().NotBeNull();
        activity!.TagObjects.Any(t => t.Key == "transaction.name").Should().BeFalse();

        activity.Dispose();
    }

    [Fact]
    public void Instruments_UnitsAndDescriptions_ShouldMatch()
    {
        var published = new Dictionary<string, (string? Unit, string? Description)>();
        using var meterListener = new MeterListener();
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == TransactionDiagnostics.SourceName)
            {
                published[instrument.Name] = (instrument.Unit, instrument.Description);
            }
        };
        meterListener.Start();

        published["transactions.started"].Unit.Should().Be("{transaction}");
        published["transactions.started"].Description.Should().Be("Total number of transactions started.");

        published["transactions.committed"].Unit.Should().Be("{transaction}");
        published["transactions.committed"].Description.Should().Be("Total number of transactions successfully committed.");

        published["transactions.rolled_back"].Unit.Should().Be("{transaction}");
        published["transactions.rolled_back"].Description.Should().Be("Total number of transactions rolled back.");

        published["transactions.failed"].Unit.Should().Be("{transaction}");
        published["transactions.failed"].Description.Should().Be("Total number of transactions that failed during execution or commit.");

        published["transactions.duration"].Unit.Should().Be("ms");
        published["transactions.duration"].Description.Should().Be("Duration of transaction lifetimes from begin to commit/rollback.");

        published["transactions.savepoints.created"].Unit.Should().Be("{savepoint}");
        published["transactions.savepoints.created"].Description.Should().Be("Total number of transaction savepoints created.");

        published["transactions.savepoints.rolled_back"].Unit.Should().Be("{savepoint}");
        published["transactions.savepoints.rolled_back"].Description.Should().Be("Total number of savepoint rollbacks executed.");

        published["transactions.savepoints.released"].Unit.Should().Be("{savepoint}");
        published["transactions.savepoints.released"].Description.Should().Be("Total number of savepoints released.");
    }

    [Fact]
    public void RecordStarted_ShouldRecordStartedCounterWithTag()
    {
        var recorded = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) => { if (inst.Name == "transactions.started") l.EnableMeasurementEvents(inst); };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add((inst.Name, val, tags.ToArray())));
        listener.Start();

        TransactionDiagnostics.RecordStarted(TransactionIsolationLevel.Snapshot);

        recorded.Should().ContainSingle();
        recorded[0].Value.Should().Be(1L);
        recorded[0].Tags.Should().Contain(t => t.Key == "transaction.isolation_level" && Equals(t.Value, "Snapshot"));
    }

    [Fact]
    public void RecordCommitted_ShouldRecordCommittedCounterAndDurationWithTags()
    {
        var longRecords = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        var doubleRecords = new List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name is "transactions.committed" or "transactions.duration") l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, state) => longRecords.Add((inst.Name, val, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((inst, val, tags, state) => doubleRecords.Add((inst.Name, val, tags.ToArray())));
        listener.Start();

        TransactionDiagnostics.RecordCommitted(TransactionIsolationLevel.RepeatableRead, 12.34);

        longRecords.Should().ContainSingle();
        longRecords[0].Name.Should().Be("transactions.committed");
        longRecords[0].Value.Should().Be(1L);
        longRecords[0].Tags.Should().Contain(t => t.Key == "transaction.isolation_level" && Equals(t.Value, "RepeatableRead"));

        doubleRecords.Should().ContainSingle();
        doubleRecords[0].Name.Should().Be("transactions.duration");
        doubleRecords[0].Value.Should().Be(12.34);
        doubleRecords[0].Tags.Should().Contain(t => t.Key == "transaction.outcome" && Equals(t.Value, "committed"));
    }

    [Fact]
    public void RecordRolledBack_ShouldRecordRolledBackCounterAndDurationWithTags()
    {
        var longRecords = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        var doubleRecords = new List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name is "transactions.rolled_back" or "transactions.duration") l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, state) => longRecords.Add((inst.Name, val, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((inst, val, tags, state) => doubleRecords.Add((inst.Name, val, tags.ToArray())));
        listener.Start();

        TransactionDiagnostics.RecordRolledBack(TransactionIsolationLevel.Serializable, 56.78);

        longRecords.Should().ContainSingle();
        longRecords[0].Name.Should().Be("transactions.rolled_back");
        longRecords[0].Value.Should().Be(1L);
        longRecords[0].Tags.Should().Contain(t => t.Key == "transaction.isolation_level" && Equals(t.Value, "Serializable"));

        doubleRecords.Should().ContainSingle();
        doubleRecords[0].Name.Should().Be("transactions.duration");
        doubleRecords[0].Value.Should().Be(56.78);
        doubleRecords[0].Tags.Should().Contain(t => t.Key == "transaction.outcome" && Equals(t.Value, "rolled_back"));
    }

    [Fact]
    public void RecordFailed_WithErrorType_ShouldRecordFailedCounterAndDurationWithTags()
    {
        var longRecords = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        var doubleRecords = new List<(string Name, double Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name is "transactions.failed" or "transactions.duration") l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, state) => longRecords.Add((inst.Name, val, tags.ToArray())));
        listener.SetMeasurementEventCallback<double>((inst, val, tags, state) => doubleRecords.Add((inst.Name, val, tags.ToArray())));
        listener.Start();

        TransactionDiagnostics.RecordFailed(TransactionIsolationLevel.ReadUncommitted, 90.12, "SqlException");

        longRecords.Should().ContainSingle();
        longRecords[0].Name.Should().Be("transactions.failed");
        longRecords[0].Value.Should().Be(1L);
        longRecords[0].Tags.Should().Contain(t => t.Key == "transaction.isolation_level" && Equals(t.Value, "ReadUncommitted"));
        longRecords[0].Tags.Should().Contain(t => t.Key == "error.type" && Equals(t.Value, "SqlException"));

        doubleRecords.Should().ContainSingle();
        doubleRecords[0].Name.Should().Be("transactions.duration");
        doubleRecords[0].Value.Should().Be(90.12);
        doubleRecords[0].Tags.Should().Contain(t => t.Key == "transaction.outcome" && Equals(t.Value, "failed"));
    }

    [Fact]
    public void RecordFailed_WithNullErrorType_ShouldRecordDefaultUnknownTag()
    {
        var longRecords = new List<(string Name, long Value, KeyValuePair<string, object?>[] Tags)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name == "transactions.failed") l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, state) => longRecords.Add((inst.Name, val, tags.ToArray())));
        listener.Start();

        TransactionDiagnostics.RecordFailed(TransactionIsolationLevel.ReadCommitted, 34.56, null);

        longRecords.Should().ContainSingle();
        longRecords[0].Tags.Should().Contain(t => t.Key == "error.type" && Equals(t.Value, "Unknown"));
        longRecords[0].Tags.Should().Contain(t => t.Key == "transaction.isolation_level" && Equals(t.Value, "ReadCommitted"));
    }

    [Fact]
    public void RecordSavepointMethods_ShouldIncrementCounters()
    {
        var recorded = new List<(string Name, long Value)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (inst, l) =>
        {
            if (inst.Name.StartsWith("transactions.savepoints.", StringComparison.Ordinal)) l.EnableMeasurementEvents(inst);
        };
        listener.SetMeasurementEventCallback<long>((inst, val, tags, state) => recorded.Add((inst.Name, val)));
        listener.Start();

        TransactionDiagnostics.RecordSavepointCreated();
        TransactionDiagnostics.RecordSavepointRolledBack();
        TransactionDiagnostics.RecordSavepointReleased();

        recorded.Should().Contain(r => r.Name == "transactions.savepoints.created" && r.Value == 1L);
        recorded.Should().Contain(r => r.Name == "transactions.savepoints.rolled_back" && r.Value == 1L);
        recorded.Should().Contain(r => r.Name == "transactions.savepoints.released" && r.Value == 1L);
    }
}
