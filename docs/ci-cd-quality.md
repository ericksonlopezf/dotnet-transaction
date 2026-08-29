# EricksonLopez.Transaction — Build, Quality & CI/CD Specification

> **Copyright © Erickson Lopez. MIT License.**  
> **Author:** Erickson Lopez ([ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com))  
> **Repository:** [github.com/ericksonlopezf/dotnet-transaction](https://github.com/ericksonlopezf/dotnet-transaction)

---

## 1. Build Pipeline Lifecycle

The continuous integration and build verification pipeline enforces strict automated quality gates. Mutation testing is decoupled from PR CI to maintain rapid feedback cycles and is enforced as an **asynchronous deferred quality gate on `main`** and a **conditional validation gate on release** matching the `EricksonLopez.SqlBuilder` architecture:

```mermaid
graph TD
    subgraph PR_CI ["PR Fast Gate (.github/workflows/ci.yml)"]
        A[Pull Request] --> B[1. Repo Compliance Audit<br/>scripts/verify-compliance.ps1]
        B --> C[2. Build, Test & Coverage<br/>net8.0, net9.0, net10.0]
        C --> D[3. Codecov Upload<br/>Cobertura Coverage]
        C --> E[4. Native AOT Smoke Test<br/>linux-x64 Self-Contained Binary]
        E --> F[PR Merge Eligible<br/>No Stryker Blocking Gate]
    end

    subgraph Main_Mutation ["Async Deferred Quality Gate (.github/workflows/mutation-testing.yml)"]
        G[Push to main / Weekly Cron / Dispatch] --> H[Stryker Matrix: 11 Packages<br/>Timeout: 180m | Concurrency: Cancel In-Progress]
        H --> I[Record Package Summaries<br/>HTML/JSON Artifacts]
        I --> J[Consolidated Quality Gate Job]
        J --> K[Publish Commit Status<br/>mutation-testing/stryker >=95%]
    end

    subgraph ReleaseFlow ["Release Pipeline (.github/workflows/publish.yml)"]
        L[Release Please / Published Release] --> M[Verify Mutation Gate<br/>scripts/verify-mutation-gate.js]
        M -->|Evidence Valid & Fresh <= 7d & No src Drift| N[Reuse main Result<br/>needs_stryker=false]
        M -->|Expired >7d, src Drift, or Missing| O[Run Stryker Conditionally<br/>needs_stryker=true]
        N --> P[Pack Packages -c Release]
        O -->|Score >= 95%| P
        O -->|Score < 95%| Q[❌ Release Blocked]
        P --> R[Push to NuGet.org]
    end
```

---

## 2. Centralized MSBuild Configuration (`Directory.Build.props`)

Build and packaging rules are centrally declared in `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningLevel>5</WarningLevel>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <GenerateDocumentationFile Condition="!$(MSBuildProjectDirectory.Contains('tests')) and !$(MSBuildProjectDirectory.Contains('benchmarks')) and !$(MSBuildProjectDirectory.Contains('samples'))">true</GenerateDocumentationFile>

    <!-- Native AOT & Trimming Mandate -->
    <IsAotCompatible Condition="'$(IsAotCompatible)' == ''">true</IsAotCompatible>
    <EnableTrimAnalyzer Condition="'$(EnableTrimAnalyzer)' == ''">true</EnableTrimAnalyzer>

    <!-- Strong Naming -->
    <SignAssembly Condition="Exists('$(MSBuildThisFileDirectory)EricksonLopez.snk')">true</SignAssembly>
    <AssemblyOriginatorKeyFile Condition="Exists('$(MSBuildThisFileDirectory)EricksonLopez.snk')">$(MSBuildThisFileDirectory)EricksonLopez.snk</AssemblyOriginatorKeyFile>

    <!-- SourceLink and Symbol Packaging -->
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>
</Project>
```

---

## 3. GitHub Actions Workflows Inventory

The repository defines 10 specialized GitHub Actions workflows:

| Workflow | File | Trigger | Purpose / Key Jobs |
|---|---|---|---|
| **Continuous Integration** | `.github/workflows/ci.yml` | `push`, `pull_request` (`main`, `develop`) | Fast PR entry point calling compliance, build-test, and AOT smoke test. **Stryker is omitted** to preserve fast PR turnaround. |
| **Build & Test** | `.github/workflows/dotnet-build-test.yml` | `workflow_call`, `workflow_dispatch` | Restores, builds Release, executes tests on .NET 8, 9, 10, collects Cobertura coverage, and uploads to Codecov. |
| **Native AOT Smoke Test** | `.github/workflows/aot-smoke-test.yml` | `workflow_call`, `workflow_dispatch` | Publishes `EricksonLopez.Transaction.AotSmokeTest` as self-contained Linux-x64 binary and runs all 36 AOT tests. |
| **Mutation Testing** | `.github/workflows/mutation-testing.yml` | `push` (`main`), Weekly Sunday cron (`0 3 * * 0`), `workflow_dispatch`, `workflow_call` | Runs Stryker.NET across 11-package matrix, records scores, aggregates quality gate, and posts `mutation-testing/stryker` commit status. |
| **Publish Packages** | `.github/workflows/publish.yml` | `release` (`published`), `workflow_dispatch` | Validates mutation gate for target commit SHA via `scripts/verify-mutation-gate.js` with conditional Stryker execution before packing and publishing to NuGet. |
| **Repository Compliance** | `.github/workflows/repo-compliance.yml` | `workflow_call`, `pull_request`, `workflow_dispatch` | Runs `scripts/verify-compliance.ps1` enforcing 9 architectural and governance rules. |
| **Release Please** | `.github/workflows/release-please.yml` | `push` (`main`) | Automates Semantic Versioning releases and changelog generation. |
| **Benchmarks** | `.github/workflows/benchmarks.yml` | `workflow_call`, `workflow_dispatch` | Executes on-demand BenchmarkDotNet suites and uploads markdown summaries to GitHub step summaries. |
| **Benchmark Regression Gate** | `.github/workflows/benchmark-regression-gate.yml` | `pull_request` on `src/**` & `benchmarks/**` | Compares PR benchmark performance against committed baselines with a 10% regression threshold gate. |
| **Weekly Benchmarks** | `.github/workflows/weekly-benchmarks.yml` | Weekly Sunday cron (`0 2 * * 0`), `workflow_dispatch` | Cross-TFM benchmark suite run; commits updated baselines to `benchmarks/results/`. |

---

## 4. Required CI/CD Secrets

| Secret Name | Referenced In | Purpose |
|---|---|---|
| `SNK_KEY` | `ci.yml`, `dotnet-build-test.yml`, `aot-smoke-test.yml`, `publish.yml`, `mutation-testing.yml`, `benchmarks.yml`, `weekly-benchmarks.yml`, `benchmark-regression-gate.yml` | Base64-encoded strong naming private key restored as `EricksonLopez.snk`. |
| `CODECOV_TOKEN` | `ci.yml`, `dotnet-build-test.yml` | Authentication token for uploading Cobertura coverage reports to Codecov. |
| `NUGET_API_KEY` | `publish.yml` | NuGet publishing API key for pushes to `https://api.nuget.org/v3/index.json`. |

---

## 5. Automated Quality Gates

| Quality Gate | Tool / Mechanism | Target Threshold | Enforced Invariant |
|---|---|---|---|
| **Compilation Safety** | Roslyn Compiler | `0 Warnings, 0 Errors` | `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` |
| **XML Documentation** | Roslyn XML Generator | `100% Coverage` | `CS1591` treated as error across all production packages |
| **Native AOT & Trimming** | IL Trim Analyzers | `0 Trimming Warnings` | `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>` + `AotSmokeTest` (36/36 passing) |
| **Architectural Boundaries** | `NetArchTest.Rules` | `100% Passing` | Enforces layer boundaries and pure abstraction segregation |
| **Mutation Testing** | Stryker.NET | `>= 95% Mutation Score` | Matrix configuration with break threshold at 95% (`mutation-testing/stryker`) |
| **Release Mutation Gate** | `scripts/verify-mutation-gate.js` | `>= 95% Mutation Score` | Verifies target commit SHA passed mutation testing or triggers conditional execution |
| **Compliance Auditor** | `scripts/verify-compliance.ps1` | `0 Violations` | Checks kebab-case docs, single-type files, copyright headers, and zero `[Obsolete]` |
| **Executable Reference** | Official Showcase | `11/11 Levels Passing` | Verified via `--all` batch execution |

---

## 6. Mutation Testing Matrix & Release Quality Gate Architecture

### 6.1. Strategy Overview

1. **Non-Blocking PRs**: Pull Requests execute fast unit tests and coverage analysis, never running full Stryker suites.
2. **Asynchronous `main` Gate**: Pushes to `main` trigger `mutation-testing.yml`. If a new commit arrives on `main`, `concurrency` cancels the superseded run to conserve compute.
3. **Execution Depth Selection**: `workflow_dispatch` and `workflow_call` allow triggering with configurable depth profiles (`Standard`, `Basic`, `Advanced`) and optional single-package filters.
4. **Timeout Configuration**: Configured with `timeout-minutes: 180` per matrix job, accommodating comprehensive multi-target mutation runs exceeding 60 minutes without false-positive cancellations.
5. **Conditional Pre-Release Validation**: `publish.yml` queries the GitHub Commit Status API for `mutation-testing/stryker`.
   - If valid, fresh (≤ 7 days TTL), and zero `src/` drift: reuses existing evidence (`needs_stryker=false`).
   - If missing, expired (> 7 days), or code drift in `src/`: executes Stryker conditionally (`needs_stryker=true`).
   - Release condition:
     ```yaml
     if: |
       always() &&
       (needs.mutation-gate-check.result == 'success') &&
       (
         (needs.mutation-gate-check.outputs.needs_stryker == 'false' && needs.mutation-gate-check.outputs.can_proceed == 'true') ||
         (needs.mutation-gate-check.outputs.needs_stryker == 'true' && needs.stryker-gate.result == 'success')
       )
     ```

### 6.2. Five Core Release Gate Questions

The release gate script (`scripts/verify-mutation-gate.js`) programmatically answers:

```text
1. Which commit was analyzed?        -> Commit SHA evaluated by Stryker
2. When?                             -> Timestamp of the mutation run
3. What mutation score was achieved? -> Actual mutation score percentage
4. Did it exceed the break threshold? -> Evaluation against break threshold (>= 95%)
5. Can the release proceed?          -> PERMITTED (>= 95%) vs BLOCKED (< 95%)
```

### 6.3. Threshold Policy & Single Source of Truth

Thresholds are centralized in `stryker-config.json` and 11 package-specific configs:

```json
{
  "thresholds": {
    "high": 100,
    "low": 98,
    "break": 95
  }
}
```

- **`>= 100%`**: `✅ HIGH`
- **`>= 98% && < 100%`**: `🟡 LOW`
- **`>= 95% && < 98%`**: `🟠 WARNING` (Allowed for release, non-breaking)
- **`< 95%`**: `❌ FAILED` (Hard quality gate block)

### 6.4. Matrix Configuration Files

1. `stryker-config.json` (Root / Core fallback)
2. `stryker-core-config.json` (`EricksonLopez.Transaction`)
3. `stryker-abstractions-config.json` (`EricksonLopez.Transaction.Abstractions`)
4. `stryker-dapper-config.json` (`EricksonLopez.Transaction.Dapper`)
5. `stryker-postgresql-config.json` (`EricksonLopez.Transaction.PostgreSql`)
6. `stryker-sqlserver-config.json` (`EricksonLopez.Transaction.SqlServer`)
7. `stryker-mysql-config.json` (`EricksonLopez.Transaction.MySql`)
8. `stryker-mariadb-config.json` (`EricksonLopez.Transaction.MariaDb`)
9. `stryker-oracle-config.json` (`EricksonLopez.Transaction.Oracle`)
10. `stryker-sqlite-config.json` (`EricksonLopez.Transaction.Sqlite`)
11. `stryker-result-config.json` (`EricksonLopez.Transaction.Result`)
12. `stryker-testing-config.json` (`EricksonLopez.Transaction.Testing`)

---

## 7. Compliance Verification Script (`scripts/verify-compliance.ps1`)

The repository includes a PowerShell compliance auditor that verifies:
1. **Documentation Naming**: Kebab-case naming for all markdown files under `docs/`.
2. **Zero Obsolete APIs**: No `[Obsolete]` attribute usages in `src/`.
3. **Canonical MIT Header**: Presence of `// Copyright © Erickson Lopez. MIT License.` on line 1 of every `.cs` file.
4. **One Type Per File**: Exactly one top-level type declared per `.cs` file in `src/`.
5. **GitHub Identity URLs**: Target links pointing to `ericksonlopezf/dotnet-transaction`.
6. **Normalized Security Email**: Maintainer email referencing `ericksonlopezf@gmail.com`.
7. **Zero Prohibited Suppressions**: No unauthorized `<NoWarn>` suppressions.
8. **NuGet Package Metadata**: Verification of `<PackageIcon>`, `<PackageReadmeFile>`, and `<TreatWarningsAsErrors>`.
9. **Analyzer Severity Mapping**: Presence of mandatory rules in `.editorconfig`.

Execution:
```powershell
pwsh -File scripts/verify-compliance.ps1
```
