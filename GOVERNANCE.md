# Project Governance

## 👑 Leadership & Maintainership

`EricksonLopez.Transaction` is designed, maintained, and governed by **Erickson Lopez** as part of the `EricksonLopez.*` enterprise .NET open-source library ecosystem.

---

## 🏛️ Decision Making Process

To preserve the zero-allocation, Native AOT, and architectural integrity of the framework, all evolutionary decisions follow strict criteria:

1. **Architecture Decision Records (ADRs)**:
   - All non-trivial design choices, package topologies, dialect additions, and scope boundaries must be formally documented in `docs/adr/` (e.g., [ADR-001 to ADR-026](docs/decisions/index.md)).
   - Any proposed breaking change or architectural pivot requires a documented ADR before code implementation.

2. **Quality Gates & Invariant Enforcement**:
   No Pull Request or internal commit is accepted into `main` without satisfying 100% of the automated quality gates:
   - **Zero Warnings**: `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` enforced across all projects.
   - **Native AOT Trimming Compliance**: `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>` with zero trimming or dynamic reflection warnings.
   - **Full XML Documentation**: Complete XML doc comments for all public types, methods, and properties (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`).
   - **Mutation Testing**: Stryker mutation score meeting or exceeding the `95%` threshold.
   - **Architectural Boundary Tests**: NetArchTest verification enforcing clean layer separation.
   - **Showcase Synchronization**: The executable reference project `samples/Showcase` must remain fully synced and compile cleanly.

---

## 📬 Contact

For governance questions or inquiries:
- **Lead Maintainer**: Erickson Lopez ([ericksonlopezf@gmail.com](mailto:ericksonlopezf@gmail.com))
