// Copyright © Erickson Lopez. MIT License.
using EricksonLopez.Mediator;

namespace EricksonLopez.Transaction.Mediator.Tests;

public sealed record TestCommandWithNonResultResponse(string Name)
    : ICommand<string>, ITransactionalCommand;
