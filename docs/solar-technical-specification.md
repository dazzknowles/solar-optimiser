# Solar Optimiser — Technical Specification

Status: **Living specification.** This is the authoritative technical specification for the whole Solar Optimiser system. Phase 1 content in this revision realises the Functional Specification's Phase 1 requirements (`docs/solar-functional-specification.md`), as designed and independently reviewed in [issue #3](https://github.com/dazzknowles/solar-optimiser/issues/3) (proposals v1–v7, five rounds of independent adversarial review by Codex). It records approved outcomes only — issue #3 remains the design workshop and evidence trail for how these decisions were reached, including alternatives that were considered and rejected.

## 1. Alignment and scope

Every technical decision below states which `SOL-F-*` functional requirement(s) it implements or supports. Every Phase 1 functional requirement has identifiable coverage somewhere in this document (see the traceability matrix, §18).

Some structural decisions here are deliberately sized for the known whole-system direction (a future multi-provider, potentially multi-tenant SaaS product), not only for the narrowest possible Phase 1 slice — for example, the provider-abstraction boundary and the project split in §2 are kept even though a smaller Phase 1-only implementation could technically use fewer projects. This is a stated, deliberate choice (reasoned during design review: the cost of the fuller structure now is low, the cost of retrofitting it later once code has coupled across the boundary is not), not scope creep — no generic abstraction is introduced here merely for hypothetical reuse without an identified near-term need.

## 2. Requirement ID scheme and solution architecture (SOL-T-1xx)

Technical requirements/design decisions use stable IDs `SOL-T-<NNN>`, grouped by range in parallel with the functional scheme:

| Range | Area |
|---|---|
| SOL-T-1xx | Solution architecture |
| SOL-T-2xx | Provider abstraction and FoxESS adapter |
| SOL-T-3xx | Domain model and identity persistence |
| SOL-T-4xx | Data model (MariaDB schema) |
| SOL-T-5xx | Capture, provenance and evidence handling |
| SOL-T-6xx | Quality classification and partial-result handling |
| SOL-T-7xx | Mapping and normalisation |
| SOL-T-8xx | Collection scheduling, error and retry policy |
| SOL-T-9xx | Observability and alerting |
| SOL-T-10xx | Retrieval API |
| SOL-T-11xx | Security and access |
| SOL-T-12xx | Configuration and secrets |
| SOL-T-13xx | Data-access technology and naming conventions |
| SOL-T-14xx | Testing strategy |
| SOL-T-15xx | Deployment and runtime |

- **SOL-T-101** *(implements SOL-F-101, SOL-F-102, SOL-F-104)*: One .NET 10 LTS solution, six `src` projects with dependencies flowing in one direction only:

  ```
  src/
    SolarOptimiser.Domain                 // Site, Device, TelemetryQuantity, TelemetryQuality — no provider or DB knowledge
    SolarOptimiser.Providers.Abstractions // ITelemetryProvider + provider-neutral DTOs
    SolarOptimiser.Providers.FoxESS       // FoxESS adapter: auth, HTTP, raw JSON contracts, mapping to abstraction DTOs
    SolarOptimiser.Persistence            // ported DB utility, repository interfaces/implementations, stored-procedure calls
    SolarOptimiser.Collection             // orchestration: discover -> fetch -> classify -> persist -> record run/attempt; owns FoxESS-variable-to-domain mapping
    SolarOptimiser.Host                   // single process: hosts the Collection worker AND the retrieval API
  tests/
    SolarOptimiser.Domain.Tests
    SolarOptimiser.Providers.FoxESS.Tests // fixture-based, no live calls
    SolarOptimiser.Persistence.Tests      // integration, ephemeral MariaDB
    SolarOptimiser.Collection.Tests       // fake provider + test DB; mapping/quality/partial-success unit tests live here
    SolarOptimiser.Host.Tests             // integration, proves retrieval
  ```

  `Collection` depends on `Domain`, `Providers.Abstractions` and `Persistence` — it is the orchestration/application layer, not a thin consumer of the provider alone. `Domain` stays provider-free. `Host` combines the polling worker and the retrieval API into one process for this Phase 1 single-device deployment; splitting them into separate processes later (e.g. on a move to a hosted environment) is a cheap change, not designed now.

- **SOL-T-102**: Target framework is **.NET 10 LTS** (superseding an earlier .NET 8 proposal made without checking current support timelines).

## 3. Provider abstraction and FoxESS adapter (SOL-T-2xx)

- **SOL-T-201** *(implements SOL-F-101, SOL-F-102, SOL-F-801)*: `SolarOptimiser.Providers.Abstractions` defines a closed, provider-neutral contract with no generic/arbitrary-request capability:

  ```csharp
  public interface ITelemetryProvider
  {
      string ProviderKey { get; } // "FoxESS"
      Task<ProviderDiscoveryResult> DiscoverDevicesAsync(string providerSiteId, CancellationToken ct);
      Task<ProviderCallResult> GetLatestTelemetryAsync(string providerDeviceId, CancellationToken ct);
  }

  public sealed record ProviderSite(string ProviderSiteID, string Name, string? TimeZoneID);

  public sealed record ProviderDevice(
      string ProviderDeviceID, string ProviderSiteID, string Status, string? ModuleSerial,
      bool HasPV, bool HasBattery, string? Model);

  public sealed record ProviderDiscoveryResult(
      ProviderOutcome Outcome, int? HTTPStatus, int? ProviderErrorNumber, string? ProviderMessage,
      string? RequestSanitized, string? RawResponseSanitized,
      IReadOnlyList<ProviderDevice>? Devices); // null unless Outcome allows it

  public sealed record ProviderCallResult(
      ProviderOutcome Outcome, string? DeviceStatus, int? HTTPStatus, int? ProviderErrorNumber,
      string? ProviderMessage, IReadOnlyList<string>? ReturnedVariables,
      string? RequestSanitized, string? RawResponseSanitized,
      ProviderTelemetrySnapshot? Snapshot); // present whenever any readings came back, regardless of Outcome

  public sealed record ProviderTelemetrySnapshot(
      string ProviderDeviceID, DateTimeOffset RetrievedAtUTC, string? ProviderTimestampRaw,
      DateTimeOffset? ProviderTimestampParsed, IReadOnlyList<ProviderReading> Readings);

  public sealed record ProviderReading(string Variable, string? Unit, decimal? Value, string? Channel);

  public enum ProviderOutcome { Success, PartialMissing, DeviceFaultOrOffline, Throttled, AuthFailed, ValidationFailed, ProviderServerError, Transport, ParseFailure }
  ```

  `ProviderDiscoveryResult` and `ProviderCallResult` deliberately mirror each other's shape: `device/list` (discovery/status) and `device/real/query` (telemetry) are two distinct provider calls with independent evidence, and neither is allowed to overwrite or be conflated with the other (see SOL-T-401/SOL-T-402).

- **SOL-T-202** *(implements SOL-F-101, SOL-F-801)*: `SolarOptimiser.Providers.FoxESS` implements this contract using the r04 §6.6 allow-listed endpoints needed for Phase 1 (`plant/list`, `device/list`, `device/detail`, `device/variable/get`, `device/real/query`), against `www.foxesscloud.com` (the documented OpenAPI request domain, r04 §2; confirmed for tenant zero 2026-09-21 — `developer-eu.foxesscloud.com` is the developer portal's web UI, not the API host, and rejects real API calls with a 405), using the **private API token** authentication mechanism (MD5 signature per r04 §4.1, confirmed for tenant zero). No generic/pass-through HTTP client is exposed to `Collection`.
- **SOL-T-203** *(implements SOL-F-102)*: `GetLatestTelemetryAsync` omits FoxESS's `variables` parameter on every call (requests every variable the device currently exposes) — there is no documented quota difference between requesting a subset and requesting everything (limits are per call, not per variable), and this is what makes continuous capability discovery possible without a separate discovery-only call (see SOL-T-302).

## 4. Domain model and identity persistence (SOL-T-3xx)

- **SOL-T-301** *(implements SOL-F-201)*: `SolarOptimiser.Domain` defines `Site` and `Device` as plain, provider-free types. Identity (`ProviderSiteID`/`ProviderDeviceID`/`ModuleSerial`) is preserved exactly as reported.
- **SOL-T-302** *(implements SOL-F-202, SOL-F-203, SOL-F-204, SOL-F-205)*: Every acquisition run's discovery/status call (SOL-T-401) upserts a `DeviceCapabilities` row for **every** variable seen — mapped to a `TelemetryQuantity` or not — giving continuous, free capability discovery with no separate discovery scheduler. A first-ever sighting is unreviewed (`IsExpected = 0`): present values are still recorded normally, but absence is not flagged. Manual review via `espDeviceCapabilityApprove` sets `IsExpected = 1`, after which absence produces a `Missing` observation. The `0 → 1` transition is one-way by design — a later disappearance of an approved variable is treated as a real signal worth alerting on, not suppressed by auto-reverting the flag. `espDeviceCapabilityRetire` sets `RetiredAtUTC` as a separate, additive state (once investigation confirms a capability is legitimately gone) so alerting can stop without erasing the original approval from the audit trail. `Missing` classification requires `IsExpected = 1 AND RetiredAtUTC IS NULL`.
- **SOL-T-303** *(implements SOL-F-206)*: `DeviceBatteries` records one row per physical battery serial reported in `device/detail`'s `batteryList`, independent of how battery telemetry is aggregated at the device level — the reference site's two parallel-wired 5kW batteries get two `DeviceBatteries` rows, while `BatterySOC`/`BatteryPowerSigned`-family telemetry stays aggregated per device, since that is how FoxESS reports it. This split (per-serial identity vs. per-device telemetry) is deliberate, not an inconsistency to reconcile.

## 5. Data model — MariaDB schema (SOL-T-4xx)

- **SOL-T-401** *(implements SOL-F-101, SOL-F-102, SOL-F-104, SOL-F-701)*:

  ```sql
  CREATE TABLE Sites (
    ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    ProviderKey VARCHAR(32) NOT NULL,
    ProviderSiteID VARCHAR(64) NOT NULL,
    Name VARCHAR(128) NOT NULL,
    TimeZone VARCHAR(64) NULL,
    CreatedAtUTC DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    UpdatedAtUTC DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    UNIQUE KEY UQ_Sites_Provider (ProviderKey, ProviderSiteID)
  );

  CREATE TABLE Devices (
    ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    SiteID BIGINT UNSIGNED NOT NULL,
    ProviderDeviceID VARCHAR(64) NOT NULL,
    ModuleSerial VARCHAR(64) NULL,
    Status VARCHAR(32) NOT NULL,
    Model VARCHAR(64) NULL,
    HasPV TINYINT(1) NOT NULL,
    HasBattery TINYINT(1) NOT NULL,
    LastAlertedOutcome VARCHAR(24) NULL,
    CreatedAtUTC DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    UpdatedAtUTC DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3) ON UPDATE CURRENT_TIMESTAMP(3),
    UNIQUE KEY UQ_Devices_SiteProvider (SiteID, ProviderDeviceID),
    CONSTRAINT FK_Devices_Site FOREIGN KEY (SiteID) REFERENCES Sites(ID)
  );

  CREATE TABLE DeviceCapabilities (
    ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    DeviceID BIGINT UNSIGNED NOT NULL,
    SourceVariable VARCHAR(64) NOT NULL,
    Unit VARCHAR(16) NULL,
    IsExpected TINYINT(1) NOT NULL DEFAULT 0,
    ExpectedSince DATETIME(3) NULL,
    RetiredAtUTC DATETIME(3) NULL,
    DiscoveredAtUTC DATETIME(3) NOT NULL,
    LastSeenAtUTC DATETIME(3) NOT NULL,
    UNIQUE KEY UQ_DeviceCapabilities_Device (DeviceID, SourceVariable),
    CONSTRAINT FK_DeviceCapabilities_Device FOREIGN KEY (DeviceID) REFERENCES Devices(ID)
  );

  CREATE TABLE DeviceBatteries (
    ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    DeviceID BIGINT UNSIGNED NOT NULL,
    BatterySerial VARCHAR(64) NOT NULL,
    BatteryType VARCHAR(32) NULL,
    Model VARCHAR(64) NULL,
    CapacityRaw VARCHAR(32) NULL,
    ManufacturedAtRaw VARCHAR(32) NULL,
    DiscoveredAtUTC DATETIME(3) NOT NULL,
    UNIQUE KEY UQ_DeviceBatteries_Device (DeviceID, BatterySerial),
    CONSTRAINT FK_DeviceBatteries_Device FOREIGN KEY (DeviceID) REFERENCES Devices(ID)
  );

  CREATE TABLE CollectionRuns (
    ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    StartedAtUTC DATETIME(3) NOT NULL,
    CompletedAtUTC DATETIME(3) NULL,
    DevicesAttempted INT NOT NULL DEFAULT 0,
    DevicesSucceeded INT NOT NULL DEFAULT 0,
    ObservationsWritten INT NOT NULL DEFAULT 0,
    Status VARCHAR(16) NOT NULL, -- Success | PartialFailure | Failed
    StatusCheckedAtUTC DATETIME(3) NULL,
    StatusHTTPStatus SMALLINT NULL,
    StatusProviderErrorNumber INT NULL,
    StatusProviderMessage VARCHAR(256) NULL,
    StatusRequestPath VARCHAR(260) NULL,
    StatusResponsePath VARCHAR(260) NULL
  );

  CREATE TABLE CollectionAttempts (
    ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    CollectionRunID BIGINT UNSIGNED NOT NULL,
    DeviceID BIGINT UNSIGNED NOT NULL,
    RequestedAtUTC DATETIME(3) NOT NULL,
    CompletedAtUTC DATETIME(3) NULL,
    Outcome VARCHAR(24) NOT NULL, -- Success | PartialMissing | DeviceFaultOrOffline | Throttled | AuthFailed | ValidationFailed | ProviderServerError | Transport | ParseFailure
    DeviceStatus VARCHAR(32) NULL, -- denormalized from the run's shared status call; drives the trust gate (SOL-T-601)
    HTTPStatus SMALLINT NULL,
    ProviderErrorNumber INT NULL,
    ProviderMessage VARCHAR(256) NULL,
    RequestedVariables TEXT NULL, -- null = "all" (every poll omits FoxESS's variables parameter)
    ReturnedVariables TEXT NULL,
    RequestPath VARCHAR(260) NULL,
    RawResponsePath VARCHAR(260) NULL,
    SentryEventID CHAR(32) NULL, -- SentryId.ToString("n"); null if not alerted or Sentry unavailable
    CONSTRAINT FK_CollectionAttempts_Run FOREIGN KEY (CollectionRunID) REFERENCES CollectionRuns(ID),
    CONSTRAINT FK_CollectionAttempts_Device FOREIGN KEY (DeviceID) REFERENCES Devices(ID),
    KEY IX_CollectionAttempts_DeviceTime (DeviceID, RequestedAtUTC DESC)
  );

  CREATE TABLE TelemetryObservations (
    ID BIGINT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
    CaptureID BIGINT UNSIGNED NOT NULL, -- device is derived via CaptureID -> CollectionAttempts.DeviceID; never stored here directly
    Quantity VARCHAR(32) NOT NULL,
    Channel VARCHAR(16) NULL, -- raw vendor channel only, e.g. "3" for pv3Power; no physical mapping implied
    ValueParsed DECIMAL(14,4) NULL, -- parsed/precision-limited; the exact provider lexical value lives in the raw evidence file
    Unit VARCHAR(16) NULL, -- exactly as FoxESS reported it; no conversion
    Quality VARCHAR(16) NOT NULL, -- Ok | Missing | Invalid | Stale
    SourceVariable VARCHAR(64) NOT NULL,
    MappingVersion SMALLINT NOT NULL, -- the mapping-set version active when this row was written
    ProviderTimestampRaw VARCHAR(64) NULL,
    ObservedAtUTC DATETIME(3) NULL,
    ObservedAtParseStatus VARCHAR(16) NOT NULL, -- Parsed | Unparseable | NotAttempted
    RetrievedAtUTC DATETIME(3) NOT NULL,
    CreatedAtUTC DATETIME(3) NOT NULL DEFAULT CURRENT_TIMESTAMP(3),
    KEY IX_TelemetryObservations_Capture (CaptureID, Quantity, Channel),
    CONSTRAINT FK_TelemetryObservations_Capture FOREIGN KEY (CaptureID) REFERENCES CollectionAttempts(ID)
  );
  -- "latest per device/quantity" is served by joining CollectionAttempts(DeviceID, RequestedAtUTC DESC)
  -- into this table's capture index; TelemetryObservations never denormalizes DeviceID (see SOL-T-402).
  ```

- **SOL-T-402** *(implements SOL-F-604)*: `TelemetryObservations` does not store `DeviceID` directly — an earlier design that stored both `CaptureID` and `DeviceID` independently allowed an observation to claim a capture from one device while tagged with another. Device is always derived via `CaptureID → CollectionAttempts.DeviceID`.
- **SOL-T-403** *(implements SOL-F-501, SOL-F-502)*: There is no unique-constraint-based deduplication on `TelemetryObservations`. An earlier design attempted one keyed on `(DeviceID, Quantity, Channel, ProviderTimestampRaw)`; this was dropped because MariaDB unique indexes do not enforce uniqueness across `NULL` values (most rows have `Channel = NULL`), so it silently deduplicated almost nothing, and — separately — two independent polls legitimately returning the same value and provider timestamp is not a duplicate to suppress; it is evidence the underlying reading had not changed, which the historical-estimates use case (SOL-F-501) actually wants recorded. Every poll writes one row per catalogued variable, full stop. At the proposed cadence (two devices, roughly a dozen quantities each, five-minute polls) this is approximately 6,900 rows/day (~2.5M/year) — trivial for MariaDB with the indexes above.
- **SOL-T-404**: No automatic retention/purge exists (implements SOL-F-502). No formal capacity/backup automation is built into the application; backup is an accepted operational task outside the application for this Phase 1 deployment (see SOL-T-1501).

## 6. Capture, provenance and evidence handling (SOL-T-5xx)

- **SOL-T-501** *(implements SOL-F-701, SOL-F-702)*: `Collection`'s poll loop, per run: (1) calls `DiscoverDevicesAsync` once — one `device/list` call covers every device at the site — recording its own evidence on `CollectionRuns.Status*` and refreshing `Devices.Status`/`ModuleSerial`; (2) for each device, calls `GetLatestTelemetryAsync`, recording per-device evidence on its own `CollectionAttempts` row. The two calls' evidence never share or overwrite each other's fields.
- **SOL-T-502** *(implements SOL-F-701, SOL-F-803)*: All request/response evidence is written to disk, not the database — `<RawResponseRoot>/yyyy/MM/dd/<CaptureID>-request.json` / `-response.json` (and the equivalent for the run-level status call), via a temp-file-then-atomic-rename, with the owning DB row's path columns committed only after the rename succeeds. If the file write/rename fails for any reason, the path column simply stays null — writing evidence is best-effort and secondary, and never blocks or rolls back the primary `CollectionAttempts`/`TelemetryObservations` write. Each file is capped at a configured maximum size (default 1MB); an oversized body is not written, and the path stays null.
- **SOL-T-503** *(implements SOL-F-803)*: Redaction before writing is deterministic, not heuristic: the FoxESS API key and its derived request signature (both known, fixed values) are removed by exact string replacement, and a small fixed field allow-list additionally strips plant address, user and installer fields if a `device/list`/`device/detail` response ever includes them (per r04 §6.8's own list of unnecessary fields) — beyond credential redaction, not only in place of it.
- **SOL-T-504** *(implements SOL-F-803)*: Sentry never receives request/response content, sanitized or not — only bounded tags (`Outcome`, `DeviceID`, `SiteID`, timestamps) and the local file path as a reference for on-device investigation (see SOL-T-901).
- **SOL-T-505** *(implements SOL-F-101)*: `Collection` writes a `CollectionAttempts` row and its `TelemetryObservations` rows inside a single database transaction via an explicit local unit of work (SOL-T-1301) — not `System.Transactions`/`TransactionScope`. A whole-attempt failure with nothing to persist writes only the `CollectionAttempts` row (a single statement needs no transaction). This closes completeness gaps from a mid-write crash; it does not, and is not intended to, deduplicate an exact replay of an already-completed acquisition — a future backfill/self-heal capability (not in Phase 1) would need its own idempotency check against existing coverage before writing.

## 7. Quality classification and partial-result handling (SOL-T-6xx)

- **SOL-T-601** *(implements SOL-F-404)*: A capture-level trust gate sits above per-variable classification. `DeviceStatus` value `1` (online) is trusted; `2` (fault), `3` (offline), null, an unrecognised value, a parse failure, or the run's status call itself failing are all **untrustworthy**. For an untrustworthy capture, `Collection` still writes the `CollectionAttempts` row (`Outcome = DeviceFaultOrOffline`), still writes raw evidence, still evaluates alerting — but writes **zero** `TelemetryObservations` rows for that capture, even if the response happened to carry numeric values, since a value riding along with a self-contradicting or unconfirmed status may be stale/cached rather than live.
- **SOL-T-602** *(implements SOL-F-402, SOL-F-403)*: When `DeviceStatus` is trusted, per-variable classification is additive: one variable's `Missing` or `Invalid` state never suppresses another variable's row in the same response. `Quality` values are `Ok | Missing | Invalid | Stale`. (`Offline`/`Faulted` were considered as per-observation quality values but removed — SOL-T-601's capture-level gate already fully owns that case, making per-row `Offline`/`Faulted` unreachable and redundant.)
- **SOL-T-603** *(implements SOL-F-203, SOL-F-204)*: `Missing` requires the variable to be `IsExpected = 1 AND RetiredAtUTC IS NULL` on `DeviceCapabilities` for that device (SOL-T-302) *and* absent from the current response. A present, mapped variable always gets an `Ok`/`Invalid` row regardless of its expectation state — real telemetry is always captured when seen.
- **SOL-T-604** *(implements SOL-F-401)*: `Invalid` (key present, value null/empty/unparseable/out of range) is a distinct state from `Missing` (key absent from the response) — different evidence, never collapsed together.
- **SOL-T-605** *(implements SOL-F-405)*: `Stale` remains a defined `Quality` value that no code path currently sets — no measured freshness threshold exists yet, and none is invented ahead of the evidence tracked in issue #4.

## 8. Mapping and normalisation (SOL-T-7xx)

- **SOL-T-701** *(implements SOL-F-301)*: `ValueParsed`/`Unit` store exactly what FoxESS returns, with no invented canonical-unit conversion or sign reinterpretation. "Normalisation" in Phase 1 means only the identity mapping from a FoxESS variable name to a canonical `TelemetryQuantity` (+ `Channel`), not a unit/sign transform.
- **SOL-T-702** *(implements SOL-F-304)*: `TelemetryObservations.MappingVersion` records which version of the FoxESS-variable-to-quantity mapping set was active when a row was written. The version increments for **any** change to the mapping set, including a purely additive new mapping, so a version number always denotes one immutable, complete mapping set — not only "semantic" changes. A short markdown changelog (not a database table) documents what each version means, including that `GenerationPowerAC`'s physical meaning is explicitly uncertain (r04's own description of `generationPower` carries a question mark) until tenant-zero evidence resolves it.
- **SOL-T-703** *(implements SOL-F-303)*: `Channel` stores the raw vendor channel index only (e.g. `"3"` for `pv3Power`), scoped per device — the two inverters have different channel layouts (16 across the large inverter's two 8-string MPPTs, 3 on the small inverter) — with no ordinal-to-physical-string mapping asserted anywhere.
- **SOL-T-704** *(implements SOL-F-302)*: `ProviderTimestampRaw` is always stored; `ObservedAtUTC` is populated only when parsing succeeds against the device's station timezone, and `ObservedAtParseStatus` (`Parsed | Unparseable | NotAttempted`) records the outcome explicitly, so a format/DST failure is visible in the data rather than an unexplained null.
- **SOL-T-705**: `Quantity` values in Phase 1: `BatterySOC, BatteryPowerSigned, BatteryChargePower, BatteryDischargePower, PVPowerTotal, PVStringPower, PVEnergyTotalCumulative, GridImportPower, GridExportPower, LoadPower, LoadEnergyCumulative` (product quantities), plus `MeterPower, MeterPower2, GenerationPowerAC, GenerationEnergyCumulative` (diagnostic/corroboration — collected via the same mechanism, not yet used by any product feature per SOL-F-503).

## 9. Collection scheduling, error and retry policy (SOL-T-8xx)

- **SOL-T-801** *(implements SOL-F-103)*: A single `BackgroundService` in `Host`, using `PeriodicTimer`, polls one site's discovered devices — no external scheduler (Quartz/Hangfire) or distributed job infrastructure. `PollInterval`, `RequestTimeoutSeconds`, retry delay, and the per-poll wall-clock budget are all configuration (`CollectionOptions`), not hardcoded. The loop skips a tick outright rather than overlapping if the previous poll is still running.
- **SOL-T-802** *(implements SOL-F-704)*: Retry is conservative and category-specific, not a blanket policy:

  | Category | Retry within this poll? |
  |---|---|
  | Transport / timeout | One retry, short fixed delay |
  | ProviderServerError (5xx) | One retry, short delay |
  | Throttled (`40400`) | No — record it, let the next scheduled poll retry naturally |
  | AuthFailed / ValidationFailed | No — retrying won't fix a credential/request problem |
  | DeviceFaultOrOffline / ParseFailure | No — not a transport issue |

  A minimum 1-second gap between calls to the same FoxESS query interface respects r04 §4.4's documented per-second limit.
- **SOL-T-803** *(implements SOL-F-104)*: Each device's attempt within a run is independent — one device failing does not stop the other device's attempt in the same run; the run is recorded `PartialFailure` rather than aborting entirely.
- **SOL-T-804** *(implements SOL-F-704)*: At startup, the configured poll interval and device count are validated against FoxESS's documented (if scope-ambiguous, per r04 §4.4) daily call budget, including the shared status call and configured retry allowance. **Open item**: whether an estimate that looks likely to exceed the documented limit should cause the application to refuse to start, or only log a warning, was not resolved during design review and is recorded as an open decision in §17 rather than assumed.

## 10. Observability and alerting (SOL-T-9xx)

- **SOL-T-901** *(implements SOL-F-702)*: Every non-`Success` outcome is captured to Sentry (`SentryEventID` stored as `SentryId.ToString("n")` — the 32-character no-dash form Sentry's own UI/API use, not the default dashed `ToString()`), tagged with `Outcome`/`DeviceID`/`SiteID`, with the local evidence file path as a reference (never file content — SOL-T-504).
- **SOL-T-902** *(implements SOL-F-703)*: `Devices.LastAlertedOutcome` tracks each device's last-alerted outcome. A new Sentry event fires only when the current outcome differs from `LastAlertedOutcome` — entering a bad state alerts once, staying in it does not re-alert every poll, and recovering to `Success` fires an explicit "resolved" event too (tagged distinctly from a fresh failure). `LastAlertedOutcome` updates whenever an alert fires, including on recovery.
- **SOL-T-903**: Slack routing and any further frequency-based throttling (e.g. tightening alert sensitivity if a particular outcome proves noisy) is configured entirely as Sentry Alert Rules in Sentry's own project settings — not built into the application. Two rules are the Phase 1 starting point: a `PartialMissing`-tagged rule to a "data quality" Slack channel, everything else to a "real exceptions" channel.
- **SOL-T-904** *(implements SOL-F-701)*: Sentry/Slack are optional observability, never on the capture-correctness critical path — if Sentry itself is unreachable, collection and the database write proceed unaffected, `SentryEventID` stays null, and the notification failure gets a local log line. `CollectionAttempts`/`CollectionRuns` remain the durable source of truth regardless of Sentry's availability.

## 11. Retrieval API (SOL-T-10xx)

`SolarOptimiser.Host`, bound per SOL-T-1101:

- **SOL-T-1001** *(implements SOL-F-601, SOL-F-604)*: `GET /api/sites/{siteId}/telemetry/latest?deviceId=&quantity=` — one row per `(device, quantity, channel)`, ordered `RetrievedAtUTC DESC, ID DESC`, each including `value`, `unit`, `quality`, `sourceVariable`, `captureId`, the owning capture's `outcome`, `providerTimestampRaw`, `observedAtUTC`, `observedAtParseStatus`, `retrievedAtUTC`.
- **SOL-T-1002** *(implements SOL-F-602)*: `GET /api/sites/{siteId}/telemetry?deviceId=&quantity=&channel=&from=&to=&limit=&cursor=` — ordered `(RetrievedAtUTC, ID)` ascending (a total, stable order — `RetrievedAtUTC` alone is not unique, since every row from one capture shares it); `from`/`to` filter on `RetrievedAtUTC` (not `ObservedAtUTC`, which can be null/unparseable); default page size 500, max 5000, cursor-paginated.
- **SOL-T-1003** *(implements SOL-F-603)*: `GET /api/devices/{deviceId}/captures/nearest?at=<timestamp>` — compares against `RequestedAtUTC`; ties break deterministically (smallest absolute time difference, then higher `CaptureID`); a configurable maximum distance (default 3× the poll interval) returns "no suitable capture" rather than something misleadingly distant; the response includes the **signed** time difference (positive = capture after the requested instant) and the attempt's `RequestedVariables`/`ReturnedVariables`, plus all of that capture's `TelemetryObservations` in one response.
- **SOL-T-1004** *(implements SOL-F-603)*: `GET /api/captures/{captureId}` — the same detail shape as SOL-T-1003, addressed by a known ID directly (e.g. one already seen in a SOL-T-1002 row). This endpoint is capture-scoped globally, not device-scoped — acceptable under the LAN trust boundary (SOL-T-1101).
- **SOL-T-1005**: A whole-site (multi-device) snapshot at a point in time is, for Phase 1, just calling SOL-T-1003 once per device rather than a combined endpoint — only worth building if that becomes a common access pattern once something is actually consuming this API.

## 12. Security and access (SOL-T-11xx)

- **SOL-T-1101** *(implements SOL-F-802)*: `Host` fails to start on missing or invalid CIDR allow-list configuration (deny-by-default, not allow-by-default). Requests are checked against the actual socket peer address only — no `X-Forwarded-For` or similar header is trusted, since there is no reverse proxy in front of this service. IPv4-mapped-IPv6 addresses are normalised before comparison. No separate host firewall rule is added — the application-level check is the sole control, judged sufficient since the service is never exposed beyond the LAN in Phase 1. The Technical Specification states plainly: every client able to originate traffic from the configured allow-list is a trusted reader; any untrusted LAN membership, reverse proxy, port forwarding, or exposure beyond the LAN requires real authentication and a fresh security review before being added, not an extension of this allow-list.
- **SOL-T-1102** *(implements SOL-F-801)*: Provider credentials live only in `FoxESSProviderOptions` (SOL-T-1201) and are never included in any API response, log line, or Sentry event.

## 13. Configuration and secrets (SOL-T-12xx)

- **SOL-T-1201**: `FoxESSProviderOptions` (`BaseUrl` = `https://www.foxesscloud.com`, `ApiKey`, `SiteProviderIDs`); `CollectionOptions` (`PollInterval`, `RequestTimeoutSeconds`, `RetryDelay`, `PerPollBudget`, `MaxCapturedEvidenceBytes`); `SentryOptions` (`SentryDsn`); `HostOptions` (`AllowedCIDRRanges`, `RawResponseRoot`); `ConnectionStrings:SolarOptimiser`. Local development uses .NET user-secrets; production secrets are a local, non-committed configuration file (the deployment target is a local device, not a hosted secrets manager — revisit if/when Solar moves to a hosted environment).

## 14. Data-access technology and naming conventions (SOL-T-13xx)

- **SOL-T-1301**: Data access is via stored procedures, invoked through a purpose-built `DBUtility`/`IDBUtility` — a port of an existing internal ADO.NET/MySqlConnector utility (`DBUtilityMySQL`, from an unrelated codebase, reused with its original references stripped and with the following changes: the `[Obsolete]` synchronous method duplicates are dropped (no legacy callers to preserve them for); the batch-insert method's string-literal-embedding SQL builder is replaced with a real parameterized multi-row `INSERT`; and — the significant change — an explicit local unit-of-work is added:

  ```csharp
  public interface IDbUnitOfWork : IAsyncDisposable
  {
      Task ExecuteAsync(string storedProcedure, DbParameter[] parameters, CancellationToken ct);
      Task<T?> ExecuteScalarAsync<T>(string storedProcedure, DbParameter[] parameters, CancellationToken ct);
      Task CommitAsync(CancellationToken ct); // rollback happens on dispose if never called
  }
  ```

  `DBUtility.BeginUnitOfWorkAsync()` opens one connection and one plain local `MySqlTransaction` — deliberately **not** `System.Transactions`/`TransactionScope`. This was evaluated directly against MySqlConnector's own documentation: `TransactionScope` there uses full XA (two-phase-commit) transactions by default (`UseXaTransactions=true`), which MySqlConnector's docs themselves say "may not be compatible with server replication," and which carries an orphaned-prepared-transaction recovery risk on an unattended crash — solving a distributed-transaction problem this single-connection, single-database write does not have. Unit-of-work methods must create commands on the already-open connection and assign the active `MySqlTransaction` to every command (the ported utility's original execute methods each open/close their own connection per call and cannot be reused unchanged for this).
- **SOL-T-1302**: Stored procedures are prefixed `esp` (Eceni Stored Procedure — deliberately not a bare `sp` prefix, avoiding the MSSQL system-procedure-clash habit). Tables, columns and stored procedures are PascalCase with no underscores. Acronyms/initialisms are fully capitalised in both database and code identifiers (`ID`, `UTC`, `PV`, `DB`, `SOC`, `HTTP`, `GUID`, ...) — a deliberate divergence from standard .NET Pascal-casing-of-acronyms guidance. Brand names are spelled per the vendor's own branding rather than the acronym rule (`FoxESS`, not `FoxEss` or `FOXESS`).
- **SOL-T-1303**: Indicative stored procedures: `espSiteGetByProviderID`, `espSiteUpsert`, `espDeviceGetBySite`, `espDeviceUpsert`, `espDeviceCapabilityUpsert`, `espDeviceCapabilityApprove`, `espDeviceCapabilityRetire`, `espDeviceBatteryUpsert`, `espCollectionRunStart`, `espCollectionRunComplete`, `espCollectionAttemptInsert`, `espTelemetryObservationInsertBatch`, `espTelemetryObservationGetLatest`, `espTelemetryObservationQuery`. Exact parameter lists are an implementation-time detail.

## 15. Testing strategy (SOL-T-14xx)

- **SOL-T-1401**: Domain — construction/invariant tests only (no mapping logic lives here).
- **SOL-T-1402**: Providers.FoxESS — fixture-based response-parsing and auth-header tests; no live calls.
- **SOL-T-1403**: Collection — FoxESS-variable-to-`TelemetryQuantity` mapping tests; the trust-gate scenarios (trusted-status additive classification; untrusted-status zero-row suppression); the partial-success scenarios (whole failure → zero rows; full success → all `Ok`; partial → mixed `Ok`/`Missing`); one test per retry-policy category (SOL-T-802); an overlap-prevention test; a rollback/injected-failure test for the unit of work (a deterministic second-command failure must leave neither the attempt nor its observations committed).
- **SOL-T-1404**: Persistence — integration tests against ephemeral MariaDB proving schema, the ported `DBUtility`'s stored-procedure execution path via the unit of work, and insert performance at the estimated daily volume (SOL-T-403).
- **SOL-T-1405**: Host — integration tests seeding rows and asserting the ranged/latest/nearest/capture-detail endpoints (SOL-T-1001–1004) return them correctly, including cursor ordering and pagination bounds; a test proving missing/invalid CIDR configuration fails startup (SOL-T-1101).
- **SOL-T-1406**: Explicitly not included as automated, CI-gating tests: load/performance tests beyond SOL-T-1404's basic volume check, multi-site tests, or any live-FoxESS end-to-end test (live verification happens as part of implementation per r04a/r04b, not as a repeatable test that would consume quota on every run).

## 16. Deployment and runtime (SOL-T-15xx)

- **SOL-T-1501**: Phase 1 runs on a local device (e.g. Raspberry Pi) at the site, with MariaDB local too; development runs under WSL or another Linux host, matching production's Linux table-name case-sensitivity behaviour (avoiding a Windows-vs-Linux dev/prod mismatch). A future move to a hosted environment (e.g. AWS) is anticipated but explicitly not designed against in Phase 1 — no cloud-specific abstraction is introduced ahead of that need.

## 17. Open items requiring product-owner decision

Per the design review's own governance discipline, this section records what genuinely remains undecided rather than resolving it silently:

- **Quota-validation strictness (SOL-T-804)**: should a startup call-budget estimate that looks likely to exceed FoxESS's documented (ambiguous-scope) daily limit cause `Host` to refuse to start, or only log a warning and proceed? Not decided during design review.

No other consequential ambiguity was identified during this consistency pass; the five rounds of independent review on issue #3 concluded with no further decisions flagged as required before implementation.

## 18. Traceability matrix

| FoxESS evidence | Solar functional requirement | Solar technical coverage |
|---|---|---|
| SR-AUTH | SOL-F-801 | SOL-T-1102 |
| SR-ID | SOL-F-201 | SOL-T-301, SOL-T-401 (Sites/Devices) |
| SR-CAP | SOL-F-202 | SOL-T-302, SOL-T-401 (DeviceCapabilities) |
| SR-TIME | SOL-F-301, SOL-F-302 | SOL-T-701, SOL-T-704 |
| SR-SOC | SOL-F-301–304 | SOL-T-705 (`BatterySOC`) |
| SR-BAT | SOL-F-301–304 | SOL-T-705 (`BatteryPowerSigned`/`BatteryChargePower`/`BatteryDischargePower`) |
| SR-PV | SOL-F-301–304 | SOL-T-703, SOL-T-705 (`PVPowerTotal`/`PVStringPower`) |
| SR-GRID | SOL-F-301–304 | SOL-T-705 (`GridImportPower`/`GridExportPower`) |
| SR-FLOW | SOL-F-603 | SOL-T-1003/1004 (query-time inspection; no automatic reconciliation in Phase 1) |
| SR-LOAD | SOL-F-301–304 | SOL-T-705 (`LoadPower`/`LoadEnergyCumulative`) |
| SR-QUAL | SOL-F-401–405 | SOL-T-601–605 |
| SR-FAIL | SOL-F-701–704 | SOL-T-501, SOL-T-802, SOL-T-901–904 |
| r04 §4.2 (`moduleSN`, `batteryList`) | SOL-F-201, SOL-F-206 | SOL-T-301, SOL-T-303 |
| r04 §4.4 (rate limits) | SOL-F-704 | SOL-T-802, SOL-T-804 |
| r04 §6.8 (data minimisation) | SOL-F-803 | SOL-T-503, SOL-T-504 |
| r04 §7.6 / r04a (PV-channel mapping deferral) | SOL-F-303 | SOL-T-703 |
| r04a (ordering supersession) | SOL-F-902 | — (governance decision, not a technical design) |
| r04b (evidence-completion decoupling) | SOL-F-903 | — (tracked via issue #4, not a technical design) |

| Solar functional requirement | Solar technical coverage |
|---|---|
| SOL-F-101 | SOL-T-101, SOL-T-201, SOL-T-202, SOL-T-801 |
| SOL-F-102 | SOL-T-101, SOL-T-203, SOL-T-401 |
| SOL-F-103 | SOL-T-801 |
| SOL-F-104 | SOL-T-101, SOL-T-401, SOL-T-803 |
| SOL-F-201 | SOL-T-301, SOL-T-401 |
| SOL-F-202 | SOL-T-302, SOL-T-401 |
| SOL-F-203 | SOL-T-302, SOL-T-603 |
| SOL-F-204 | SOL-T-302, SOL-T-603 |
| SOL-F-205 | SOL-T-302 |
| SOL-F-206 | SOL-T-303, SOL-T-401 |
| SOL-F-301 | SOL-T-701 |
| SOL-F-302 | SOL-T-704 |
| SOL-F-303 | SOL-T-703 |
| SOL-F-304 | SOL-T-702 |
| SOL-F-401 | SOL-T-604 |
| SOL-F-402 | SOL-T-602 |
| SOL-F-403 | SOL-T-602 |
| SOL-F-404 | SOL-T-601 |
| SOL-F-405 | SOL-T-605 |
| SOL-F-501 | SOL-T-401, SOL-T-403 |
| SOL-F-502 | SOL-T-403, SOL-T-404 |
| SOL-F-503 | SOL-T-705 |
| SOL-F-601 | SOL-T-1001 |
| SOL-F-602 | SOL-T-1002 |
| SOL-F-603 | SOL-T-1003, SOL-T-1004 |
| SOL-F-604 | SOL-T-402, SOL-T-1001 |
| SOL-F-701 | SOL-T-501, SOL-T-904 |
| SOL-F-702 | SOL-T-901 |
| SOL-F-703 | SOL-T-902 |
| SOL-F-704 | SOL-T-802 |
| SOL-F-801 | SOL-T-1102 |
| SOL-F-802 | SOL-T-1101 |
| SOL-F-803 | SOL-T-503, SOL-T-504 |
| SOL-F-901 | This matrix |
| SOL-F-902 | r04a, r04b (governance record; see §17 note) |
| SOL-F-903 | Issue #4 |
