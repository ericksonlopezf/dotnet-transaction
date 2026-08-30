using EricksonLopez.Mediator;

namespace EricksonLopez.Transaction.Mediator.Tests;

public sealed record TestTransactionalCommand(string Name) : ICommand<EricksonLopez.Result.Result>, ITransactionalCommand;
