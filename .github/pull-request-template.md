## Description

Please include a summary of the change and which issue is fixed (if any).
Include relevant motivation and context.

## Affected Packages
Please check all packages that are affected by this PR:
- [ ] `EricksonLopez.Transaction` (Core)
- [ ] `EricksonLopez.Transaction.Abstractions`
- [ ] `EricksonLopez.Transaction.Dapper`
- [ ] `EricksonLopez.Transaction.MariaDb`
- [ ] `EricksonLopez.Transaction.MySql`
- [ ] `EricksonLopez.Transaction.Oracle`
- [ ] `EricksonLopez.Transaction.PostgreSql`
- [ ] `EricksonLopez.Transaction.Result`
- [ ] `EricksonLopez.Transaction.Sqlite`
- [ ] `EricksonLopez.Transaction.SqlServer`
- [ ] `EricksonLopez.Transaction.Testing`

## Checklist

Before submitting this PR, please verify the following:
- [ ] I have performed a self-review of my own code.
- [ ] I have updated the `CHANGELOG.md` (if applicable).
- [ ] I have added/updated unit tests or integration tests.
- [ ] Local build passes (`dotnet build EricksonLopez.Transaction.slnx -c Release`).
- [ ] Local tests pass (`dotnet test EricksonLopez.Transaction.slnx`).
- [ ] I verified compliance using `./scripts/verify-compliance.ps1`.
- [ ] Stryker mutation testing maintains the **95%** mutation score threshold.
- [ ] Benchmarks confirmed no regressions.
