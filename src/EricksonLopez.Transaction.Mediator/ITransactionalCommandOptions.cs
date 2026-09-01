// Copyright © Erickson Lopez. MIT License.

namespace EricksonLopez.Transaction.Mediator;

/// <summary>
/// Optional interface for transactional commands that need to specify custom transaction options
/// such as isolation level, timeout, or nesting behavior. Implement this alongside
/// <see cref="ITransactionalCommand"/> to configure transaction behavior without reflection.
/// </summary>
public interface ITransactionalCommandOptions
{
    /// <summary>
    /// Gets the transaction options to use when executing this command.
    /// </summary>
    TransactionOptions TransactionOptions { get; }
}
