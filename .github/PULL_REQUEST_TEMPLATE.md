## 📋 Pull Request Description

### Summary of Changes
<!-- Provide a concise description of the motivation and changes made in this PR. -->

---

## 🏛️ Quality & Architectural Checklist

- [ ] **One Type Per File**: All new or modified C# types reside in dedicated single-type `.cs` files.
- [ ] **English Only**: Code identifiers, comments, XML docs, and documentation use technical English.
- [ ] **License Header**: All new `.cs` files begin with `// Copyright © Erickson Lopez. MIT License.`.
- [ ] **Zero Obsolete**: No `[Obsolete]` attributes or deprecated API invocations introduced.
- [ ] **Zero Warnings**: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` passes cleanly with 0 warnings.
- [ ] **XML Documentation**: Public APIs are fully documented; `CS1591` is resolved through complete XML comments.
- [ ] **Native AOT Compliance**: No reflection patterns violating IL trimming analyzers (`EnableTrimAnalyzer=true`).
- [ ] **Tests & Mutation Gates**: Unit/Integration tests added; Stryker mutation score satisfies `>= 95%`.
- [ ] **Showcase & ADR**: Associated Showcase levels or ADR documents updated where applicable.
