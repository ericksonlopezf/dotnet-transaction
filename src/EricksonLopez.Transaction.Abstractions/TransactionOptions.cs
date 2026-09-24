// Copyright © Erickson Lopez. MIT License.
using System;

namespace EricksonLopez.Transaction;

/// <summary>
/// Represents immutable configuration options for controlling transaction behavior, isolation level, timeout, and nesting semantics.
/// </summary>
public sealed record TransactionOptions
{
    /// <summary>
    /// Gets the default transaction options with <see cref="TransactionIsolationLevel.ReadCommitted"/> and <see cref="NestedTransactionBehavior.UseSavepoint"/>.
    /// </summary>
    /// <remarks>
    /// This field is a shared singleton. Accessing it incurs no heap allocation. For the most common
    /// default transaction configuration, prefer this field over constructing a new instance.
    /// </remarks>
    public static readonly TransactionOptions Default = new();

    /// <summary>
    /// Gets the requested isolation level for the transaction.
    /// </summary>
    public TransactionIsolationLevel IsolationLevel { get; init; } = TransactionIsolationLevel.ReadCommitted;

    /// <summary>
    /// Gets the maximum duration allowed for the transaction before timing out, or <see langword="null"/> for default driver timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets a value indicating whether the transaction should be opened in read-only mode where supported by the provider.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Gets the behavior applied when an execution scope is nested inside an existing transaction.
    /// </summary>
    public NestedTransactionBehavior NestedBehavior { get; init; } = NestedTransactionBehavior.UseSavepoint;

    /// <summary>
    /// Gets the optional logical name or identifier for the transaction, used in diagnostics and logging.
    /// </summary>
    public string? TransactionName { get; init; }

    /// <summary>
    /// Gets a value indicating whether sensitive infrastructure metadata should be redacted from
    /// OpenTelemetry distributed tracing spans emitted during transaction execution.
    /// </summary>
    /// <remarks>
    /// When <see langword="true"/>, the <c>transaction.id</c> and <c>transaction.name</c> tags
    /// are replaced with <c>[REDACTED]</c> in all emitted <see cref="System.Diagnostics.Activity"/> spans.
    /// This prevents transaction identifiers and logical names — which may encode PII, business
    /// context, or sensitive infrastructure identifiers — from appearing in distributed tracing
    /// platforms such as Datadog, Jaeger, or Honeycomb.
    /// Enable this option in production environments where tracing data is exported to external
    /// observability services. Disable it in local development environments where raw diagnostic
    /// data is required for troubleshooting.
    /// </remarks>
    public bool SanitizeTelemetryMetadata { get; init; }

    /// <summary>
    /// Gets a new <see cref="TransactionOptions"/> instance configured with <see cref="TransactionIsolationLevel.Serializable"/>.
    /// </summary>
    /// <remarks>
    /// Each access to this property allocates a new <see cref="TransactionOptions"/> instance.
    /// To avoid repeated allocations in hot paths, capture the value in a local variable or a static field.
    /// </remarks>
    public static TransactionOptions Serializable => new() { IsolationLevel = TransactionIsolationLevel.Serializable };

    /// <summary>
    /// Gets a new <see cref="TransactionOptions"/> instance configured for read-only execution.
    /// </summary>
    /// <remarks>
    /// Each access to this property allocates a new <see cref="TransactionOptions"/> instance.
    /// To avoid repeated allocations in hot paths, capture the value in a local variable or a static field.
    /// </remarks>
    public static TransactionOptions ReadOnlyMode => new() { ReadOnly = true };

    /// <summary>
    /// Creates a new <see cref="TransactionOptions"/> instance with the specified timeout.
    /// </summary>
    /// <param name="timeout">The transaction execution timeout.</param>
    /// <returns>A new <see cref="TransactionOptions"/> instance configured with the specified timeout.</returns>
    public static TransactionOptions WithTimeout(TimeSpan timeout) => new() { Timeout = timeout };
}
