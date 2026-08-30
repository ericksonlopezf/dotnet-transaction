// Copyright © Erickson Lopez. MIT License.
using EricksonLopez.Mediator;
using EricksonLopez.Result;

namespace EricksonLopez.Transaction.Mediator.Tests;

public sealed record TestNonTransactionalRequest(string Query) : IQuery<Result<string>>;
