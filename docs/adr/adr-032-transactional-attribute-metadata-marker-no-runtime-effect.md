# ADR-032: TransactionalAttribute as Metadata Marker — No Runtime Pipeline Effect

## Status
Accepted

## Date
2026-09-15

## Context
The `EricksonLopez.Transaction.Mediator` package provides `TransactionPipelineBehavior<TRequest, TResponse>` as a MediatR-compatible pipeline behavior that automatically wraps command handlers in transaction boundaries. There are two mechanisms to specify transaction options:

1. **`[TransactionalAttribute]`**: A compile-time attribute applied to command classes (e.g., `[Transactional(IsolationLevel = Serializable)]`).
2. **`ITransactionalCommandOptions`**: An interface implemented by command classes that returns `TransactionOptions` at runtime.

A foundational question arose: should `TransactionPipelineBehavior` use reflection to inspect `[Transactional]` at runtime and derive `TransactionOptions` from its properties?

## Decision

**`[TransactionalAttribute]` is intentionally a metadata marker only. It has no runtime effect in the pipeline.**

`TransactionPipelineBehavior<TRequest, TResponse>` resolves transaction options **exclusively** via `ITransactionalCommandOptions`:

```csharp
if (request is ITransactionalCommandOptions transactionalOptions)
{
    options = transactionalOptions.Options;
}
```

No reflection is performed on `[Transactional]` at any point.

### Rationale

1. **Native AOT / Trimming Compatibility**: Reading attributes via `Attribute.GetCustomAttribute<T>()` or `typeof(T).GetCustomAttributes()` is a reflection operation. In `PublishAot=true` compilations, unconstrained reflection over arbitrary generic type parameters (`TRequest`) cannot be statically analyzed and may cause trimming warnings or runtime failures. The `ITransactionalCommandOptions` interface approach requires no reflection — it is a simple interface cast (`as ITransactionalCommandOptions`) that the AOT compiler handles correctly.

2. **Zero-Reflection Design Invariant**: The entire framework is designed around zero unconstrained reflection. See `ADR-010` (Native AOT and Trimming Invariants).

3. **Explicit Over Implicit**: Interface implementation creates an explicit, compile-time-visible contract. Attribute-based configuration is invisible to the pipeline without developer awareness of the reflection mechanism.

### What `[Transactional]` is for

`[TransactionalAttribute]` serves as:
- **Architectural documentation**: Communicates transactional intent to developers reading the codebase.
- **Tooling marker**: Can be read by Roslyn Analyzers (ELT001), documentation generators, and code navigation tools.
- **Future-proof metadata**: The attribute can be extended without breaking pipeline contracts.

Developers who want runtime options to be applied by the pipeline **must implement `ITransactionalCommandOptions`** on their command class.

## Consequences

### Positive
- Full Native AOT and trimming compliance is maintained.
- Explicit interface contract prevents silent misconfiguration (a developer who forgets `ITransactionalCommandOptions` will see default behavior immediately, rather than assuming the attribute worked).
- No hidden reflection overhead in the pipeline.

### Negative
- `[Transactional]` being a metadata marker is non-obvious without reading this ADR or the XML documentation.
- Developers familiar with Spring/Java annotations (where `@Transactional` is dynamically applied) may be surprised by the explicit interface requirement.

## Related ADRs
- [ADR-010: Native AOT and Trimming Invariants](adr-010-native-aot-and-trimming-invariants.md)
- [ADR-024: Transaction Mediator Deferred to Separate Package](adr-024-transaction-mediator-deferred-to-separate-package.md)
