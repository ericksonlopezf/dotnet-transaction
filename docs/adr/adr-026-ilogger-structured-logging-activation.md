# ADR-026: ILogger Structured Logging Activation in TransactionManager

## Status
Accepted

## Context
The NEXT-horizon roadmap item "Logging Decision" required an explicit architectural decision: either **activate** `Microsoft.Extensions.Logging.ILogger<TransactionManager>` for structured diagnostic logging within the core transaction engine, or **eliminate** the dependency entirely if unused.

The governing principle was: "No declared dependency without actual usage is acceptable".

`EricksonLopez.Transaction` already declares an optional `Microsoft.Extensions.Logging.Abstractions` dependency in its `.csproj`. The question was whether to activate it with real instrumentation or remove it.

## Decision
We **activate** optional `ILogger<TransactionManager>` structured logging with the following design:

1. **Optional by design**: `ILogger<TransactionManager>?` is accepted as a nullable constructor parameter. When `null` (the default), no logging occurs and there is zero overhead.
2. **Zero-allocation via `[LoggerMessage]` source generator**: All log calls are implemented as `[LoggerMessage]`-attributed partial methods. This eliminates the `IsEnabled` check ceremony and produces zero heap allocations in the hot path.
3. **Debug-only instrumentation**: Log messages are emitted at `LogLevel.Debug` (suppressed scope begin) and `LogLevel.Warning` (timeout exceeded). No `LogLevel.Information` or higher events are generated for normal transaction flows, avoiding log spam in production.
4. **DI-compatible**: When registered via `services.AddTransaction(...)`, the DI container automatically resolves and injects `ILogger<TransactionManager>` from the ambient logging infrastructure.

### Log Events Defined
| Event ID | Level | Message |
|---|---|---|
| `1` | `Debug` | `Beginning suppressed transaction scope. Ambient context will be suspended.` |
| `2` | `Warning` | `Transaction execution exceeded timeout of {Timeout}.` |

### Rejected Alternative: Remove ILogger
Removing the dependency entirely would have eliminated useful operational diagnostic signals:
- Developers have no structured warning when transactions time out — they only see the `TransactionTimeoutException`.
- Suppress scope debugging is invisible without the log entry.
- `Microsoft.Extensions.Logging.Abstractions` has zero runtime cost when no `ILoggerFactory` is registered.

## Consequences

### Positive
- Zero-allocation hot path via `[LoggerMessage]` source generation.
- Fully optional — no impact on Native AOT, trimming, or projects without logging configured.
- Structured timeout warnings visible in production monitoring.

### Negative
- `Microsoft.Extensions.Logging.Abstractions` remains a transitive dependency of the core package (though with zero runtime overhead if no factory is registered).
