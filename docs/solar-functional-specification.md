# Solar Optimiser — Functional Specification

Status: **Living specification.** This is the authoritative functional specification for the whole Solar Optimiser system. It is not a Phase 1-only document: Phase 1 populates the parts of the system that are currently known, designed and approved; later phases extend the same document with their own requirements as they are designed and approved, using the same ID scheme.

Phase 1 content in this revision was derived from the design and independent review recorded in [issue #3](https://github.com/dazzknowles/solar-optimiser/issues/3) (proposals v1–v7, five rounds of independent adversarial review), and from the approved FoxESS read-only capability evidence in `docs/requirements/foxess/read-only-capability/r04/` (r04, and its addenda r04a and r04b).

## 1. Purpose

Solar Optimiser exists to acquire, normalise and retain telemetry from a household's solar/battery/grid installation, and later (in phases not yet specified) to use that data to inform optimisation decisions. This document describes only what has been designed and approved so far: acquiring, storing and retrieving read-only telemetry from one real site via the FoxESS cloud API.

Future phases are expected to add tariff/weather ingestion, optimisation/scheduling logic, device control, a user-facing product, and potentially a multi-tenant/SaaS delivery model serving other manufacturers' equipment as well as FoxESS. None of that is specified here; where a Phase 1 decision was deliberately shaped to accommodate that future direction (see Technical Specification §1), this document notes it as context, not as a current requirement.

## 2. Scope

**In scope for Phase 1**: acquiring read-only telemetry for one real site (currently: one FoxESS station with two inverters — a larger inverter carrying two 8-panel PV strings, and a smaller inverter carrying a 3-panel string, plus a parallel-wired battery pack presented as one ~10kW store in most live telemetry rather than 2×5kW), normalising it into a stable Solar-owned representation, persisting it durably, and retrieving it via a simple query interface.

**Explicitly out of scope for Phase 1** (not specified, not designed against): tariff or weather data ingestion; optimisation, scheduling or forecasting logic; any device/inverter control; a user-facing UI; support for any provider other than FoxESS; multi-tenant or SaaS delivery; authentication of Solar's own retrieval interface beyond a network-level trust boundary; automatic telemetry retention/archiving policy; a validated telemetry-staleness threshold; and physical mapping of PV input channels to physical PV strings (see r04a — this is a permanent product-scope deferral, not a Phase 1 timing one).

## 3. Requirement ID scheme

Functional requirements use stable IDs of the form `SOL-F-<NNN>`, grouped by numeric range so later phases can insert requirements into an existing area without renumbering:

| Range | Area |
|---|---|
| SOL-F-1xx | Telemetry acquisition |
| SOL-F-2xx | Identity and capability |
| SOL-F-3xx | Normalisation, timestamps, units, provenance |
| SOL-F-4xx | Data quality and partial-result semantics |
| SOL-F-5xx | Persistence and retention |
| SOL-F-6xx | Retrieval |
| SOL-F-7xx | Failure handling and observability |
| SOL-F-8xx | Security and access |
| SOL-F-9xx | Governance, evidence and traceability |

Each requirement below cites the FoxESS evidence (r04 `SR-*` IDs, or specific r04/r04a/r04b sections) it derives from, where applicable. That citation records lineage; it does not make r04's own IDs part of Solar's permanent requirement namespace.

## 4. Functional requirements

### 4.1 Telemetry acquisition (SOL-F-1xx)

- **SOL-F-101**: Solar shall periodically acquire telemetry from a configured provider (Phase 1: FoxESS) for a configured real site, using only read-only operations — no device, account, schedule or state-changing operation shall ever be performed as part of acquisition. *(Traces to: r04 §1, §6 read-only safety requirements.)*
- **SOL-F-102**: Acquisition shall cover every inverter-class device actually present at the site, discovered from the provider rather than assumed to be a fixed count. *(Traces to: SR-ID, SR-CAP; validated against tenant zero's two-inverter topology.)*
- **SOL-F-103**: The acquisition cadence shall be configurable. r04 does not establish a required raw polling interval (only that "appropriate half-hour resolution" is needed at some aggregation level), so no fixed cadence is a hard functional requirement in Phase 1. *(Traces to: r04 §3 note under the requirements table, §4.4 rate-limit ambiguity.)*
- **SOL-F-104**: A failure acquiring one device's telemetry shall not prevent acquisition of other devices at the same site in the same cycle.

### 4.2 Identity and capability (SOL-F-2xx)

- **SOL-F-201**: Solar shall identify and persist stable site and device identity using provider-reported identifiers (station, inverter, and logger/module identifiers where the provider exposes them). *(Traces to: SR-ID; r04 §4.2 documents a `moduleSN` alongside `deviceSN`.)*
- **SOL-F-202**: Solar shall discover and record which telemetry variables a device has actually been observed to expose, rather than assuming a fixed, universal variable set — since r04 documents that variable availability differs by device and is not fully known in advance. *(Traces to: SR-CAP; r04 §4.2, §5.)*
- **SOL-F-203**: A newly discovered variable shall not be treated as an expected, reliable variable until it has been reviewed and approved. Its absence before approval shall not itself be treated as a data-quality problem.
- **SOL-F-204**: Once a variable has been approved as expected for a specific device, its later absence in an acquisition shall be detectable and distinguishable from an unreviewed variable's absence.
- **SOL-F-205**: An approved expectation shall be able to be retired (for example, following a confirmed firmware or hardware change that genuinely removes a capability) without erasing the historical record that it was once approved.
- **SOL-F-206**: Where the provider exposes battery identity/capacity evidence (serial, type, model, capacity), Solar shall record it, without assuming a fixed number of batteries per device — the reference site's two physical batteries are wired in parallel and may present as a single aggregate store in live telemetry even though their identity evidence is per-battery. *(Traces to: r04 §4.2, §7 step 3, §9 open question on `batteryList.capicty`.)*

### 4.3 Normalisation, timestamps, units, provenance (SOL-F-3xx)

- **SOL-F-301**: Every acquired telemetry value shall preserve the unit and value as the provider reported them, alongside Solar's own interpretation. No unit conversion or sign reinterpretation shall be invented without tenant-zero evidence supporting it. *(Traces to: SR-TIME, r04 §4.3 candidate interpretation, r04 §5.)*
- **SOL-F-302**: Every acquired telemetry value shall record both the provider-reported timestamp (preserved exactly as reported) and Solar's own retrieval time, so that age and ordering can be assessed even though the provider's timestamp format/timezone behaviour is not yet fully verified (r04 documents a discrepancy between v1's local-time and v0's UTC semantics). *(Traces to: SR-TIME, r04 §4.3.)*
- **SOL-F-303**: Where a provider variable's physical meaning is not established by evidence — most notably, which numbered PV input channel corresponds to which physical PV string — Solar shall preserve the raw provider channel identity without asserting an unverified physical mapping. *(Traces to: r04 §7 step 6; this mapping is a permanent product-scope deferral per r04a, not a Phase 1 timing gap.)*
- **SOL-F-304**: The interpretation Solar applies to a provider variable (which Solar quantity it maps to, and under what sign/unit assumptions) shall be traceable per historical record, so a later correction to that interpretation does not silently reinterpret previously acquired data.

### 4.4 Data quality and partial-result semantics (SOL-F-4xx)

- **SOL-F-401**: Solar shall never substitute a zero, or otherwise invent a value, for telemetry that is missing, invalid, or otherwise unavailable. *(Traces to: SR-QUAL; r04 §6 item 4, "absence is not zero.")*
- **SOL-F-402**: Solar shall distinguish, at minimum: a value successfully obtained and usable; an expected variable absent from a given acquisition; and a variable present but carrying an unusable value (for example, unparseable) — as separate, explicit outcomes, not collapsed into one "no data" state.
- **SOL-F-403**: An acquisition that is only partially usable (some but not all expected variables present) shall not cause the variables that were successfully obtained to be discarded.
- **SOL-F-404**: An acquisition that cannot be trusted as a whole — most notably, where the provider itself reports the device as offline or faulted — shall not have any of its values treated as current, trustworthy telemetry, even if some numeric values were technically present in the response. *(Traces to: r04 §4.6, "distinguish... device fault/offline.")*
- **SOL-F-405**: Solar shall be able to represent that a telemetry value's freshness cannot yet be assessed, without inventing an unvalidated staleness threshold ahead of measurement. Formal stale-detection is deferred pending tenant-zero cadence evidence (tracked in [issue #4](https://github.com/dazzknowles/solar-optimiser/issues/4)), and this deferral is itself part of the current functional behaviour, not an omission. *(Traces to: SR-QUAL; r04 §3, §4.3 explicitly caution against inventing a threshold before measurement.)*

### 4.5 Persistence and retention (SOL-F-5xx)

- **SOL-F-501**: Acquired telemetry shall be persisted durably and shall remain queryable over the long term. Later Solar functionality (such as planning or estimation) is expected to depend on historical telemetry, so retention is a functional need, not merely an operational convenience.
- **SOL-F-502**: Solar shall not automatically delete acquired telemetry in Phase 1. A retention/archiving policy is a deliberate future decision to be made once real data volume and usage patterns are understood, not an automatic background process built ahead of that need.
- **SOL-F-503**: Diagnostic or corroboration quantities that are not yet used by any product feature (for example, raw meter or AC-output fields useful for later reconciliation) may still be acquired and persisted, so that later analysis does not require re-collecting history that could have been captured from the start.

### 4.6 Retrieval (SOL-F-6xx)

- **SOL-F-601**: Solar shall provide a means to retrieve the most recently known value of a given telemetry quantity for a given device.
- **SOL-F-602**: Solar shall provide a means to retrieve telemetry over a specified time range, in a stable and reproducible order suitable for paging through a large result set.
- **SOL-F-603**: Solar shall provide a means to retrieve every telemetry value obtained together in the same acquisition (contemporaneous readings), including finding the acquisition nearest to a given point in time. *(Supports evaluating SR-FLOW — simultaneous solar export and grid battery charging — by inspection, without Solar computing that reconciliation automatically in Phase 1.)*
- **SOL-F-604**: Retrieval shall expose enough information about a value's origin and reliability — at minimum its quality classification and which acquisition it came from — that a consumer can judge whether to trust it, rather than treating every returned value as equally reliable.

### 4.7 Failure handling and observability (SOL-F-7xx)

- **SOL-F-701**: Solar shall record enough detail about each acquisition attempt (its outcome, and provider-reported error information where available) to diagnose a collection failure without needing to reproduce it live. *(Traces to: SR-FAIL.)*
- **SOL-F-702**: A genuine, non-transient acquisition problem shall be surfaced proactively, rather than being discoverable only by someone later noticing wrong-looking numbers.
- **SOL-F-703**: A repeated occurrence of the same ongoing problem shall not repeatedly re-notify; recovery from a problem shall itself be notified.
- **SOL-F-704**: Solar shall not retry a failed provider call in a way that risks making a rate-limiting or quota problem worse. *(Traces to: r04 §4.4, §4.6.)*

### 4.8 Security and access (SOL-F-8xx)

- **SOL-F-801**: Provider credentials shall never be exposed to any client of Solar's own retrieval interface. *(Traces to: SR-AUTH.)*
- **SOL-F-802**: While Solar's telemetry retrieval interface has no authentication of its own, it shall only be reachable by explicitly trusted clients, and that trust boundary shall be an explicit, stated assumption rather than an implicit one.
- **SOL-F-803**: Sensitive account/credential/location data shall be minimised in anything Solar retains or transmits as diagnostic evidence, beyond what is genuinely required to identify and resolve a problem. *(Traces to: r04 §6 item 8, §6.8.)*

### 4.9 Governance, evidence and traceability (SOL-F-9xx)

- **SOL-F-901**: Where a Solar requirement derives from external provider evidence of uncertain completeness (such as the FoxESS capability evidence in r04), that lineage shall remain traceable from the Solar requirement back to the source evidence.
- **SOL-F-902**: A deliberate departure from validated source evidence (such as beginning production build before a dedicated verification spike is complete) shall be explicitly recorded, including its scope and reasoning, rather than left as an undocumented assumption. *(Traces to: r04a, r04b.)*
- **SOL-F-903**: An evidence obligation that is deliberately deferred rather than waived shall be tracked as owned, triggered follow-up work, not left open-ended indefinitely. *(Traces to: r04b; tracked in issue #4.)*

## 5. Explicitly deferred (not specified here)

The following are known future-system areas. They are named so they are not mistaken for oversights, but no requirement for them is established by this document:

- Support for providers other than FoxESS.
- A multi-tenant/SaaS delivery model and any associated authentication/authorisation model.
- Tariff and weather data ingestion.
- Optimisation, scheduling, forecasting or any device-control logic.
- A user-facing interface of any kind.
- Physical PV-channel-to-string mapping as a product feature (permanently deferred per r04a — most installations have only one string, making this low product value generally).
- A validated telemetry-staleness threshold (pending the evidence tracked in issue #4).
- An automatic telemetry retention/archiving policy.
- Hosting beyond a single local device (a future move to a hosted/cloud environment is anticipated but not designed).
