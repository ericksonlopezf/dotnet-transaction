// Copyright © Erickson Lopez. MIT License.
using EricksonLopez.Mediator;

namespace EricksonLopez.Transaction.Mediator.Tests;

public sealed record TestConfigurableCommand(string Name, TransactionOptions TransactionOptions)
    : ICommand<EricksonLopez.Result.Result>, ITransactionalCommand, ITransactionalCommandOptions;
