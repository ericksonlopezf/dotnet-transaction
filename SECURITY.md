# Security Policy

## Supported Versions

`EricksonLopez.Transaction` follows Semantic Versioning 2.0.0. Only active minor releases of the current major version receive security patches and vulnerability fixes.

| Version | Supported          | Security Fixes |
| ------- | ------------------ | -------------- |
| 1.1.x   | :white_check_mark: | Active         |
| 1.0.x   | :white_check_mark: | Active         |
| < 1.0.0 | :x:                | None (EOL)     |

---

## Reporting a Vulnerability

If you discover a potential security vulnerability in `EricksonLopez.Transaction`, please report it responsibly:

1. **Do NOT open a public GitHub issue or discussion.**
2. Send an email to **[ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com)** with the subject prefix `[SECURITY] Potential Vulnerability in EricksonLopez.Transaction`.
3. Include:
   - Affected package(s) and version(s).
   - Detailed reproduction steps or proof-of-concept code.
   - Analysis of the potential impact (e.g., connection leakage, denial-of-service, unintended commit propagation).
4. You will receive an acknowledgement of your report within 48 hours. We will collaborate with you to validate the vulnerability and coordinate a responsible release timeline.

---

## Supply Chain Security

`EricksonLopez.Transaction` adheres to modern supply chain security best practices:

- **Strong Name Signing**: All production assemblies are signed with a strong name key (`EricksonLopez.snk`) to guarantee binary identity integrity. The key is stored as the GitHub Actions secret `SNK_KEY` (Base64-encoded) and restored at build time — the private key material is never committed to the repository. The corresponding public key token is embedded in `Directory.Build.props`.
- **SourceLink & Symbol Packages**: All NuGet packages embed SourceLink metadata (`PublishRepositoryUrl=true`, `EmbedUntrackedSources=true`) and publish corresponding `.snupkg` symbol packages for deterministic, reproducible debugging.
- **NuGet Publishing via API Key**: Packages are published to `https://api.nuget.org/v3/index.json` using the `NUGET_API_KEY` GitHub Actions secret (stored as a repository secret, never committed). The `--skip-duplicate` flag prevents accidental re-publication of existing versions.
- **Dependency Minimization**: The core library (`EricksonLopez.Transaction`) depends only on standard BCL abstractions and official Microsoft Extensions, avoiding unvetted third-party runtime dependencies. All package versions are centrally pinned in `Directory.Packages.props`.
- **Automated Dependency Updates**: Dependabot is configured to scan NuGet dependencies and GitHub Actions workflows weekly, opening pull requests for outdated packages grouped by ecosystem (Microsoft dependencies, testing dependencies).
- **Native AOT Trimming Verification**: Trimming analyzers (`EnableTrimAnalyzer=true`) and a dedicated `AotSmokeTest` binary ensure no dynamic reflection vulnerabilities or runtime code injection paths exist in any published package.
- **Compliance Auditor**: `scripts/verify-compliance.ps1` enforces repository governance rules on every pull request, verifying copyright headers, zero `[Obsolete]` APIs, single-type-per-file constraints, and canonical maintainer identity URLs.

---

## Known Security & Transactional Boundaries

1. **Connection Lifetime & Pooling**: `EricksonLopez.Transaction` coordinates transactions on connections obtained via `IDbConnectionFactory`. Applications must ensure that connection strings with credentials are securely stored (e.g., Azure Key Vault, AWS Secrets Manager, or protected environment variables) and never hardcoded.
2. **Commit Ambiguity**: When physical commits fail due to network disconnection (`TransactionCommitException.IsAmbiguous = true`), the database engine may have already committed changes. Applications handling high-assurance transactions must combine `EricksonLopez.Transaction` with idempotent request keys or the Transactional Outbox pattern to prevent duplicate side effects.
3. **Transaction Timeouts**: To avoid denial-of-service from dangling locks, configure realistic timeouts via `TransactionOptions.Timeout`. Default options do not enforce a fixed timeout — production applications should configure appropriate values per use case.
4. **Read-Only Mode Enforcement**: When `TransactionOptions.ReadOnly` is configured, the coordinator issues a database-level session directive (e.g., `SET TRANSACTION READ ONLY` on PostgreSQL). However, enforcement is driver-dependent. Applications requiring strict read-only guarantees should validate dialect support for their specific provider version.
