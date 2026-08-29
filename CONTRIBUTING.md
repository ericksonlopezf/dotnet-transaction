# Contributing to EricksonLopez.Transaction

Thank you for your interest in contributing to `EricksonLopez.Transaction`!

This repository contains the high-performance transaction boundary ecosystem for .NET 8, .NET 9, and .NET 10.

---

## 🏛️ Architectural Standards

All contributions must strictly respect our foundational architectural principles:

1. **Clean Architecture & DDD**: Strict unidirectional dependency flow. Transaction boundaries belong in the Application layer, while infrastructure implementations execute atomic SQL.
2. **Result Pattern**: Functional error handling for business errors; exceptions reserved for infrastructure failures, connection timeouts, and state invariant violations.
3. **Native AOT & Trimming**: Zero unannotated reflection, zero dynamic code emission. `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>` and `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` are strictly enforced across all projects.
4. **Zero Allocation in Hot Paths**: Use `ValueTask`, structs, and reusable buffers where appropriate.
5. **English Only**: All code, XML documentation, code comments, commit messages, and documentation files MUST be in English. No exceptions.
6. **Copyright Header**: Every C# source file must begin with:
   ```csharp
   // Copyright © Erickson Lopez. MIT License.
   ```

---

## 🛠️ Prerequisites

- **.NET 10 SDK** (or later) installed. The CI pipeline also tests against .NET 8 and .NET 9, but the .NET 10 SDK is sufficient for local development as it includes multi-targeting support.
- **Git**.
- **Node.js** (for `scripts/record-stryker-result.js` used in mutation testing enforcement).

> **Note**: No `global.json` is committed to this repository. Any version of the .NET 10.x SDK is accepted.

---

## 💻 Development Workflow

### 1. Build Solution

Restore and build the solution in `Release` or `Debug` configuration:

```bash
dotnet build EricksonLopez.Transaction.slnx -c Release
```

### 2. Run Test Suites

Execute all test projects covering unit, integration, architecture, and Native AOT tests:

```bash
# Run all automated tests
dotnet test EricksonLopez.Transaction.slnx

# Run architecture rules (NetArchTest)
dotnet test tests/EricksonLopez.Transaction.ArchitectureTests/EricksonLopez.Transaction.ArchitectureTests.csproj

# Run Native AOT trimming validation smoke test
dotnet run --project tests/EricksonLopez.Transaction.AotSmokeTest/EricksonLopez.Transaction.AotSmokeTest.csproj --framework net10.0 -c Release
```

### 3. Run Stryker Mutation Testing

Validate mutation coverage against the strict 95% threshold. Each package has a dedicated configuration file:

```bash
# Run mutation testing for the core package
dotnet stryker --config-file stryker-core-config.json

# Run for a specific dialect package (e.g., PostgreSql)
dotnet stryker --config-file stryker-postgresql-config.json

# The CI pipeline runs all 11 package-specific configs in a matrix job
```

Available Stryker config files: `stryker-config.json` (root fallback), `stryker-core-config.json`, `stryker-abstractions-config.json`, `stryker-dapper-config.json`, `stryker-postgresql-config.json`, `stryker-sqlserver-config.json`, `stryker-mysql-config.json`, `stryker-mariadb-config.json`, `stryker-oracle-config.json`, `stryker-sqlite-config.json`, `stryker-result-config.json`, `stryker-testing-config.json`.

### 4. Run Benchmarks

Run micro-benchmarks before and after changes affecting hot paths:

```bash
dotnet run --project benchmarks/EricksonLopez.Transaction.Benchmarks/EricksonLopez.Transaction.Benchmarks.csproj --framework net10.0 -c Release
```

Any PR modifying `src/**` or `benchmarks/**` is automatically gated by the **Benchmark Regression Gate** workflow (`benchmark-regression-gate.yml`), which compares performance against the committed baseline with a 10% threshold.

### 5. Verify Showcase Execution

Verify that all 11 progressive levels in the official Showcase pass with 100% success:

```bash
dotnet run --project samples/Showcase/EricksonLopez.Transaction.Showcase.csproj --framework net10.0 -- --all
```

### 6. Verify Code Formatting & XML Documentation

Ensure zero formatting issues and full XML doc coverage for all public APIs:

```bash
dotnet format EricksonLopez.Transaction.slnx --verify-no-changes
```

### 7. Run Repository Compliance Audit

Verify architectural governance rules enforced by CI:

```powershell
pwsh -File scripts/verify-compliance.ps1
```

The auditor checks:
- Kebab-case naming for all `docs/**` markdown files.
- Zero `[Obsolete]` attribute usages in `src/`.
- Canonical MIT copyright header on line 1 of every `.cs` file.
- One top-level type declared per `.cs` file in `src/`.
- Canonical GitHub identity URLs (`ericksonlopezf/dotnet-transaction`).
- Canonical maintainer email (`ericksonlopezf@gmail.com`).

---

## 🔀 Branching & Commit Conventions

- **Branch Naming**: `feature/feature-name`, `fix/bug-description`, `docs/doc-update`.
- **Commit Messages**: Follow [Conventional Commits](https://www.conventionalcommits.org/):
  - `feat(core): add delegate db connection factory`
  - `fix(postgresql): handle 25P02 transaction aborted error state`
  - `docs(adr): add ADR-016 for multi-dialect provider topology`
  - `test(dapper): add test coverage for QuerySingleOrDefaultAsync`

Release automation uses [Release Please](https://github.com/googleapis/release-please-action) triggered on `push` to `main`. Conventional Commits prefixes drive automatic version bumping and CHANGELOG generation.

---

## ✅ Pull Request Checklist

Before submitting a Pull Request, verify:

- [ ] Solution compiles with `0` warnings and `0` errors (`TreatWarningsAsErrors=true`).
- [ ] All unit, integration, and architecture tests pass (`dotnet test`).
- [ ] Native AOT smoke test succeeds (`EricksonLopez.Transaction.AotSmokeTest`).
- [ ] All public types and members contain comprehensive XML documentation (`<summary>`, `<param>`, `<returns>`, `<exception>`).
- [ ] Public API changes are reflected in `samples/Showcase` and documented in an ADR under `docs/adr/`.
- [ ] Stryker mutation testing satisfies the `>= 95%` threshold.
- [ ] Repository compliance audit passes (`scripts/verify-compliance.ps1`).

---

## 📜 Code of Conduct

Please note that this project is released with a Contributor Code of Conduct. By participating in this project you agree to abide by its terms. See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
