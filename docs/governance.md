# Solar Optimiser governance adoption

This file records which Eceni Governance baseline governs Solar Optimiser. It does not copy or redefine portfolio Governance. The authoritative source is the [Eceni Governance repository](https://github.com/dazzknowles/eceni-governance); Solar currently adopts [baseline 1.1.0 at commit `90e8b0a`](https://github.com/dazzknowles/eceni-governance/tree/90e8b0a7e051df4e40e058223c7d3aad6b88c166).

Solar requirements, design decisions, exceptions and implementation evidence remain Solar-owned project records. If this file conflicts with the adopted baseline, the baseline governs and the conflict must be resolved rather than silently reinterpreted here.

## Lineage

Solar was created before Eceni had a versioned Governance baseline. It therefore has no retrospective birth baseline. Its first formal adoption is 1.1.0.

| Effective date | Born/current/adopted baseline | Transition decision | Migration state |
|---|---|---|---|
| Project creation to 23 September 2026 | None | No versioned Eceni Governance baseline then existed | Historical state; not a claim that Solar was ungoverned or retrospectively conformant |
| 24 September 2026 | 1.1.0 (first adopted and current) | This adoption record, accepted through its pull request | Adopted with the explicit outstanding work below; no blanket conformance claim |

Every future transition must add a row rather than replace this history, link its decision, and record applicability changes, exceptions and migration work.

## Applicability

### Commandments

All six [Commandments at baseline 1.1.0](https://github.com/dazzknowles/eceni-governance/blob/90e8b0a7e051df4e40e058223c7d3aad6b88c166/COMMANDMENTS.md) apply to all Solar product and engineering work:

1. Bounded Autonomy
2. Data Is Entrusted
3. Faithful Evidence
4. Value Over Waste
5. Pay for a Lesson Once
6. Build Quality In

None is currently classified as not applicable.

### Laws

The authoritative wording, applicability and exception policy remain in the [1.1.0 Laws](https://github.com/dazzknowles/eceni-governance/blob/90e8b0a7e051df4e40e058223c7d3aad6b88c166/LAWS.md).

| Law | Solar applicability and current evidence state |
|---|---|
| EVI-001 — Claim Strength Must Not Exceed Evidence | Applies to specifications, reviews, tests and acceptance. Solar does not claim blanket 1.1.0 conformance. Issue [#4](https://github.com/dazzknowles/solar-optimiser/issues/4) retains the outstanding tenant-zero acceptance evidence. |
| EVI-002 — Evidence Timing Must Be Proportionate | Applies where operation is the proportionate evidence-producing mechanism. The r04a/r04b decisions and issue #4 record the existing deferral and its trigger. |
| GOV-001 — Departures Follow a Governed Lifecycle | Applies to the approved specifications, addenda, product decisions and acceptance criteria. Solar-specific departures and obligations stay in Solar records. |
| GOV-002 — Decisions Remain Valid Only While Their Basis Holds | Applies to provider capabilities, operating assumptions and other consequential decisions with changeable bases. Their triggers belong with the relevant Solar decision. |
| GOV-003 — Projects Retain Their Governance Lineage | Applies without exception. This file is the lineage record. |
| VER-001 — Conformance Evidence Is Independently Derived | Applies particularly to correctness, security, privacy, recovery and data-integrity claims. Issue #3 records prior independent design challenge; issue #5 requires independently derived tests before its boundary can be called conformant. |
| SEC-001 — Capability Is Minimized | Applies to the FoxESS credential and API allow-list, database identities, host boundary, automation and future administrative capabilities. The approved collection boundary is not yet implemented; see issue [#5](https://github.com/dazzknowles/solar-optimiser/issues/5). |
| DATA-001 — Data Use Is Purpose-Bound | Applies to telemetry, provider inventory, raw evidence, logs, diagnostics and operational records. Issue #5 tracks the known account-wide inventory minimisation gap. |
| DATA-002 — Required System Data Is Reconstructible | Applies to Solar system/reference data. Database definitions and the FoxESS mapping catalogue are versioned, but automated clean-bootstrap evidence remains outstanding in issue [#7](https://github.com/dazzknowles/solar-optimiser/issues/7). Environment-specific telemetry and provider state are operational data, not reference data made safe for source control by this Law. |
| ECO-001 — Prefer Bounded Cost to Recurring Waste | Not currently triggered: Phase 1 has no material recurring infrastructure, inference, service or data-transfer cost decision. Apply and record it when such a decision arises, including a reassessment trigger where its basis can change. |
| QUA-001 — Follow the Applicable Implementation Guide | Applies to the C# and MariaDB artefacts identified below. Known database gaps are tracked by issue #7; adoption is not evidence that all existing implementation already conforms. |

ECO-001 is the only Law currently recorded as not applicable. That classification is about Solar's present decisions, not a project exemption from future use of the Law.

### Checks

The [1.1.0 Check catalogue](https://github.com/dazzknowles/eceni-governance/blob/90e8b0a7e051df4e40e058223c7d3aad6b88c166/CHECKS.md) describes objective enforcement, not the source of the rules.

| Check | Solar applicability and implementation state |
|---|---|
| CHK-GOV-001 — Parent Issues with Open Sub-Issues Remain Open | Not currently applicable. Solar has not adopted an agreed governed-issue type or label and the Check expressly limits itself to issues that participate through such a marker. Governed obligations must still satisfy GOV-001 manually. |
| CHK-DB-001 — Database Definitions Match Source | Applies because Solar has Eceni-authored MariaDB definitions. Specified upstream but not implemented in Solar; tracked by issue #7. |
| CHK-DATA-001 — Database Bootstrap Is Reproducible | Applies to Solar's clean bootstrap profile. The integration tests exercise source-controlled definitions when MariaDB is supplied, but the full repeatable Check and visible evidence are not implemented; tracked by issue #7. |

### Implementation guides

- The [C# implementation guide 1.0.0](https://github.com/dazzknowles/eceni-governance/blob/90e8b0a7e051df4e40e058223c7d3aad6b88c166/guides/CSHARP.md) applies to all hand-authored C# source and tests in `src/` and `tests/`. No generated C# is currently committed and therefore no generated-code non-applicability is needed. Existing code is not declared fully conformant merely by adopting the guide.
- The [MariaDB implementation guide 1.0.0](https://github.com/dazzknowles/eceni-governance/blob/90e8b0a7e051df4e40e058223c7d3aad6b88c166/guides/MARIADB.md) applies to the schema and stored procedures under `src/SolarOptimiser.Persistence/Database/`. The missing explicit `SQL SECURITY INVOKER` declarations and Check evidence are tracked by issue #7.

## Project-specific application: FoxESS collection scope

The product-owner decision recorded in issue #5 and incorporated into the Functional and Technical Specifications is a Solar-specific application of SEC-001 and DATA-001: recurring collection receives only an explicitly approved site set, and account-wide discovery data is reduced before durable retention. It is also informed by EVI-001, VER-001 and QUA-001.

That decision does not create a new Eceni Commandment, Law or Check. It specifies how Solar applies the authoritative portfolio rules to the FoxESS account and product context. The current implementation does not yet satisfy the approved boundary, so issue #5 remains open and the specifications classify SOL-T-204 through SOL-T-210 as not implemented.

## Outstanding adoption work

| Issue | Obligation | Closure evidence required |
|---|---|---|
| [#4](https://github.com/dazzknowles/solar-optimiser/issues/4) | Complete the deferred tenant-zero r04 acceptance evidence | The evidence document with honest per-criterion classifications and independent review |
| [#5](https://github.com/dazzknowles/solar-optimiser/issues/5) | Implement the approved FoxESS site approval, capability and data-minimisation boundary | The independently derived acceptance suite and implementation evidence specified by SOL-T-1406/1407 |
| [#7](https://github.com/dazzknowles/solar-optimiser/issues/7) | Close MariaDB guide and database Check gaps | Explicit routine security plus repeatable CHK-DB-001 and CHK-DATA-001 results |

These are governed outstanding obligations, not exceptions and not evidence of compliance. They close only through demonstrated satisfaction or an explicit superseding decision with the required authority.
