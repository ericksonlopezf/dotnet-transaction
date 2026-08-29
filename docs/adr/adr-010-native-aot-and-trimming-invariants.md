# ADR-010: Native AOT and Trimming Invariants

## Status
Accepted

## Context
.NET 10 provides first-class Native AOT (Ahead-of-Time) compilation and trimming capabilities for ultra-fast startup and minimal memory footprint in microservices and serverless workloads. Libraries that rely on unannotated runtime reflection (`Type.GetType()`, unbounded `MakeGenericType`, or dynamic code emission) generate IL trimmer warnings (`IL2026`, `IL3050`) and fail when compiled to Native AOT.

## Decision
All packages in `EricksonLopez.Transaction` enforce:
1. `EnableTrimAnalyzer = true` and `IsAotCompatible = true` in `Directory.Build.props`.
2. `TreatWarningsAsErrors = true`: Any trimmer warning breaks the build.
3. Explicit `[DynamicallyAccessedMembers]` annotations on dependency injection extensions.
4. Zero runtime code emission, zero reflection over private members, and zero dynamic dispatch.

## Consequences
### Positive
- 100% compatibility with Native AOT binaries and trimmed container images.
- Predictable execution and minimal CPU/memory footprint.

### Negative
- Requires strict static typing and explicit annotations on all generic factories.
