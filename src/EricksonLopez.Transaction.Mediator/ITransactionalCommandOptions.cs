// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Transaction.Mediator;

/// <summary>
/// Defines the contract for transactional commands that supply custom <see cref="TransactionOptions"/>
/// to <see cref="TransactionPipelineBehavior{TRequest, TResponse}"/> at runtime without requiring reflection.
/// </summary>
/// <remarks>
/// <para>
/// Implementing this interface on a command class is the <strong>only functional mechanism</strong>
/// for configuring per-command transaction behavior in the pipeline. The <see cref="TransactionPipelineBehavior{TRequest, TResponse}"/>
/// performs a simple cast (<c>request as ITransactionalCommandOptions</c>) — no reflection, no attribute inspection.
/// This makes it fully compatible with Native AOT and trimming (<c>PublishAot=true</c>).
/// </para>
/// <para>
/// The <see cref="TransactionalAttribute"/> is a related but distinct concept: it is a <strong>metadata marker</strong>
/// for documentation and tooling purposes and has no runtime effect. Implement this interface instead
/// of (or in addition to) <see cref="TransactionalAttribute"/> to configure actual runtime behavior.
/// </para>
/// <para>
/// Pair with <see cref="ITransactionalCommand"/> for a fully typed, discoverable command contract that
/// communicates both transactional intent (via the interface constraint) and domain semantics.
/// </para>
/// </remarks>
public interface ITransactionalCommandOptions
{
    /// <summary>
    /// Gets the transaction options to use when executing this command.
    /// </summary>
    TransactionOptions TransactionOptions { get; }
}
