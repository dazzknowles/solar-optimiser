# Candidate FoxESS read-only capability and integration specification

Status: r03 candidate for independent review  
Revision basis: r02 plus evidence-verified review findings; no source refresh  
Scope: pre-implementation, read-only provider capability  
Evidence cut: GitHub issue #9 updated 2026-09-04T00:50:55Z; FoxESS Open API document retrieved 2026-09-05T15:56:34.2682286Z

## 1. Purpose and boundary

This document specifies the evidence and candidate contract for the first bounded Solar/FoxESS integration task: establish whether FoxESS Cloud can supply the read-only tenant-zero data Solar requires before a production adapter is designed.

It does not specify implementation language, component structure, database storage, tariff or weather ingestion, optimiser behaviour, simulation, UI, or inverter control. No endpoint that changes device, site, account, boarding, schedule, SoC, or other state is in scope.

The raw snapshots in section 2 are authoritative for this candidate. Statements use four evidence classes:

- **SOLAR REQUIREMENT** — stated or necessarily implied by the captured issue or comments.
- **FOXESS DOCUMENTED** — explicitly claimed by the captured official FoxESS document.
- **CANDIDATE INTERPRETATION** — a proposed mapping or contract interpretation that is not proven.
- **TENANT-ZERO VERIFICATION** — a fact that must be observed against the physical installation/API account.

## 2. Evidence and provenance

### Solar snapshot

- Repository: `dazzknowles/home-projects-collab`
- Issue: `#9`; issue ID `5175113374`; node ID `I_kwDOT7lSd88AAAABNHX2ng`
- Created: `2026-08-17T20:22:50Z`; updated: `2026-09-04T00:50:55Z`; state: open
- Retrieval: GitHub CLI REST API only (`gh api`)
- Raw issue: `github-dazzknowles-home-projects-collab-issue-9-issue-20260905T155632908Z.json`
- Raw comments: `github-dazzknowles-home-projects-collab-issue-9-comments-all-pages-20260905T155632908Z.json`
- Comments declared/captured: 9/9
- Comment IDs in API order: `5368157208`, `5414342260`, `5414446298`, `5438400203`, `5465379423`, `5465461251`, `5471562039`, `5486376978`, `5534087904`.

### FoxESS snapshot

- Official URL: `https://www.foxesscloud.com/public/i18n/en/OpenApiDocument.html`
- HTTP retrieval: `2026-09-05T15:56:34.2682286Z`; status `200`
- Raw HTML: `foxess-open-api-document-20260905T155632908Z.html`
- HTTP metadata: `foxess-open-api-document-http-metadata-20260905T155632908Z.json`
- Latest change-log entry in the captured document: `v1.1.18`, dated `2026-05-29` (“Add heat pump endpoints and update variable table”).
- The response supplied no `ETag` or `Last-Modified`; the version is therefore content-based.

The separate provenance manifest records comment node IDs/revision timestamps and SHA-256 hashes for all source snapshots.

## 3. What Solar requires from a battery/inverter provider

| ID | Required read-only capability | Solar evidence |
|---|---|---|
| SR-AUTH | Authenticate without exposing provider credentials to clients. | Issue service-layer/provider design. |
| SR-ID | Identify Site/station, inverter, logger where relevant, and batteries using stable provider IDs. | Site model (`5368157208`) and tenant-zero matrix (`5465461251`). |
| SR-CAP | Discover telemetry/equipment capabilities actually exposed by the installation. | Hardware-capability model (`5465379423`) and commissioning (`5414446298`). |
| SR-TIME | Preserve provider update time and local retrieval time so age/order can be assessed. | Freshness/stale-data requirements in issue and `5414446298`. |
| SR-SOC | Battery state of charge, percent, with observation time. | Issue data-retention and Phase 1 requirements. |
| SR-BAT | Instantaneous battery power with unambiguous charge/discharge/idle direction. | Issue battery telemetry requirement. |
| SR-PV | Instantaneous total solar generation and, where exposed, each tenant-zero string. | Issue plus `8 + 8 + 3` string evidence in `5465461251`. |
| SR-GRID | Instantaneous grid import and export with unambiguous direction/units. | Issue and net-value refinement `5465379423`. |
| SR-LOAD | Obtain or defensibly derive total household/site demand for the same period. | Issue plus `5465461251`, `5471562039`, `5534087904`. |
| SR-QUAL | Detect missing, stale, inconsistent, faulted, offline, or partial telemetry; absence is not zero. | Commissioning evidence in `5414446298`: validate telemetry quality and detect missing/stale/inconsistent data. |
| SR-FAIL | Observe and document read-only FoxESS/API/device failure modes relevant to reliability. | Issue checklist and validation sequence require FoxESS failure modes to be documented/validated; they do not prescribe a detailed taxonomy. |

The issue says “appropriate half-hour resolution” but does not define raw polling cadence, aggregation, or a freshness threshold. These remain decisions.

## 4. What the official FoxESS document claims

### 4.1 Authentication

**FOXESS DOCUMENTED**

FoxESS describes two mutually exclusive forms:

1. A private API key generated in API Management. General headers are `token`, millisecond `timestamp`, `signature`, and `lang`. Signature is MD5 of `url + "\r\n" + token + "\r\n" + timestamp`. Script callers must set/modify `User-Agent`.
2. OAuth 2.0 via `Authorization: Bearer ...`, with separate `data_access` and `device_control` scopes and 24-hour access tokens.

The OAuth text says new integrations use Client Credentials, but its detailed Client Credentials flow sits under VPP enrollment and says VPP features require FoxESS enablement. Authorization Code Grant is described as legacy for existing integrations.

**MISMATCH / UNKNOWN**

- Read endpoint tables, including `/op/v1/device/real/query`, require `token` and omit `Authorization`, conflicting with the global either/or guidance.
- It is unknown whether Client Credentials is available to a new non-VPP read-only service.
- Commercial/support status, approval, API-key expiry/rotation/revocation, refresh-token lifetime, clock skew, and replay protection are undocumented.
- Domain evidence is internally incomplete: the “Request domain” is `https://www.foxesscloud.com/`; the page links an EU OpenPlatform at `https://developer-eu.foxesscloud.com`; OAuth examples use `{domain}`/`{api_domain}` placeholders; bundled sample code sets `https://portal.foxesscloud.us`. None establishes which host/region applies to tenant zero.
- A read-only private-token restriction is not documented; OAuth `data_access` is the only explicit read scope.

**CANDIDATE INTERPRETATION**

The spike should record which authentication mechanism is actually used without fixing the later provider abstraction. If OAuth is available and appropriate, only the documented `data_access` scope is relevant; the spike must not request `device_control`. For a private token, the evidence does not establish any requestable scope or provider-enforced read-only restriction. In that case, read-only safety must come from the spike's explicit endpoint-method allow-list in section 6 and from using no code path capable of arbitrary API calls.

### 4.2 Identity and capability discovery

**FOXESS DOCUMENTED**

- `POST /op/v0/plant/list`: stations belonging to the account; includes `stationID`, name, `ianaTimezone`.
- `GET /op/v0/plant/detail?id=...`: station name, location, installed capacity, timezone. Location is unnecessary for this capability.
- `POST /op/v0/device/list`: includes `deviceSN`, `moduleSN`, station ID/name, status (`1` online, `2` fault, `3` offline), `hasPV`, `hasBattery`, device model/series.
- `GET /op/v1/device/detail?sn=...`: device/logger/station IDs, status, rated capacity, and `batteryList` with battery serial, master/slave type, version, model, `capicty`, and manufacture timestamp.
- `GET /op/v0/device/variable/get`: variable metadata used by real-time/history endpoints.
- The global variable table warns that availability differs by device and is subject to change.

**UNKNOWN**

Solar Site-to-station cardinality, identifier stability, rename/removal behaviour, multi-inverter semantics, whether variable discovery is device-specific, and the unit of `batteryList.capicty` are not documented.

### 4.3 Real-time/history data and time

**FOXESS DOCUMENTED**

Preferred real-time endpoint:

- `POST /op/v1/device/real/query`.
- Mandatory `sns` array, maximum 50 serials; optional `variables`; omitting variables requests all.
- Response has `errno`; each device has `deviceSN`, `datas[]` (`variable`, `unit`, English `name`, numeric `value`) and `time` described as inverter local time in `yyyy-MM-dd HH:mm:ss zZ`.
- A requested variable with no data is omitted.

`POST /op/v0/device/real/query` is deprecated and describes response `time` as UTC. Historical data remains documented at `POST /op/v0/device/history/query`: inverter `sn`, optional variables, `begin`/`end` millisecond timestamps, samples with numeric value and UTC update time.

`POST /op/v0/device/report/query` provides aggregated production/energy reports. It accepts inverter SN, year, optional month/day, dimension `year`, `month`, or `day`, and requested variables such as `generation`, `feedin`, `gridConsumption`, charge/discharge energy totals, and `PVEnergyTotal`. The document says aggregation uses the power station's timezone: year returns monthly values, month returns daily values, and day returns hourly values. This is supporting evidence for aggregate reconciliation; it is not a substitute for timestamped raw telemetry.

**MISMATCH / UNKNOWN**

- V1 local-time and V0 UTC semantics differ. Offset/DST correctness, device clock drift, ordering, and duplicates are unproven.
- History prose says begin/end are required and the requested range is within 24 hours; its table marks them optional and says omission yields the last three days.
- Publication cadence, freshness SLA, delay, history retention, sequence/correlation IDs, and cache behaviour are undocumented.

**CANDIDATE INTERPRETATION**

Preserve raw provider time, parsed instant if valid, station IANA timezone, and Solar retrieval time. Compute age, but do not invent an acceptable-age threshold before measurement and requirement review.

### 4.4 Rate limits

**FOXESS DOCUMENTED**

- Each inverter under one account has 1,440 interface calls per day.
- Each query interface is limited to once per second and calculated separately.
- Error `40400` means requests are too frequent.

**MISMATCH / UNKNOWN**

“Each inverter under a single account” does not settle whether quota is per inverter, account, endpoint, credential, or V1 batch. Reset timezone, concurrency, quota headers, burst rules, and `Retry-After` are undocumented. If 1,440 is one aggregate daily quota it averages one request/minute: enough for half-hour observations, potentially restrictive for finer sampling.

### 4.5 Kafka subscription capability

**FOXESS DOCUMENTED**

The captured document has a “Kafka Integration Documentation” section, added in change-log version `v1.1.14` on `2026-04-03`. It says customers can subscribe to data. Access is vendor provisioned: the customer submits company/system information; FoxESS creates a non-production environment with username/password, topic, group ID, and read permission; joint testing precedes a production environment. Supplied connection information includes bootstrap servers, SASL credentials, topic, optional consumer group, and security protocol.

**RELEVANCE / UNKNOWN**

Kafka is a documented read-only telemetry delivery capability relevant to investigating freshness, REST quota pressure, failure recovery, and commercial/vendor support. The snapshot provides no message schema, device identity mapping, event timestamp, publication frequency, latency, retention, ordering, delivery guarantee, replay policy, quota, service level, eligibility criteria, or commercial terms.

**CANDIDATE INTERPRETATION**

The read-only spike should record Kafka as an alternative capability requiring FoxESS engagement. It must not assume Kafka is available to tenant zero, preferable to REST, outside REST limits, or suitable for Solar. Kafka integration/design is deferred unless later evidence and scope explicitly select it.

### 4.6 Error and failure behaviour

**FOXESS DOCUMENTED**

Endpoint schemas return integer `errno`; device-list documentation says non-zero means failure. Common errors are:

- `40256`: required request headers missing.
- `40257`: invalid request body.
- `40400`: requests too frequent.

`GET /op/v0/device/fault/get` is documented as “Get error code information” and returns error-code objects with language-specific messages. `POST /op/v0/device/fault/history` accepts inverter `sn` plus a `YYYY-MM-DD` date and returns faults with `faultCode`, `faultMessage`, and millisecond Unix `timestamp`. The endpoint has no interface-description prose beyond its schema. Device list/detail provide online/fault/offline status. V1 real-time omits missing data.

**MISMATCH / UNKNOWN**

HTTP mappings, expired/denied credential errors, partial batch success, server/maintenance failures, timeouts, retries/backoff, schema stability, and stale-cache behaviour are not established.

**CANDIDATE INTERPRETATION**

Preserve HTTP outcome, `errno`, message, device status, returned-variable set, and timestamps. Distinguish complete success, partial/missing telemetry, device fault/offline, stale data, throttling, authentication/authorization, validation, provider/server, transport, and parse failures. Retry policy is deferred.

## 5. Required telemetry mapping

| Solar need | Official field(s) | Documented semantics | Candidate interpretation | Tenant-zero verification or mismatch |
|---|---|---|---|---|
| Battery SoC | `SoC` `%` | “State of Charge”; listed for energy-storage, not grid-tied devices. | Direct candidate mapping. | Confirm availability; aggregate pack vs battery/BMS meaning; precision/range/cadence/offline behaviour. |
| Battery power/direction | `invBatPower`, `batDischargePower`, `batChargePower` kW | `invBatPower`: positive discharge, negative charge. Split fields expose positive discharge and absolute positive charge values. | Preserve signed and split fields; compare for consistency. | Confirm exposed fields, zero/null/absence, simultaneous values, AC/DC boundary, losses, and two-battery aggregation. |
| Total instantaneous PV | `pvPower` kW | Total PV input power. | Direct DC-side candidate mapping. | Reconcile with active string powers; determine clipping/curtailment behaviour. |
| PV cumulative energy | `PVEnergyTotal` kWh | Total PV-panel-side generation. | Supporting cumulative field only. | Verify precision, reset/rollover and equipment replacement behaviour. |
| Per-string PV | `pv1Power` … `pv24Power` kW, with matching voltage/current | Each is the numbered PV input's value. | Discover available channels; never assume a fixed count. | Map physical `8 + 8 + 3` strings without assuming order; check inactive zero/absence and reconciliation to `pvPower`. |
| Grid export | `feedinPower` kW | Power exported to grid. | Candidate non-negative export. | Confirm sign, meter/CT boundary, gross/net and phase aggregation. |
| Grid import | `gridConsumptionPower` kW | Power drawn from grid. | Candidate non-negative import. | Confirm sign, meter/CT boundary, gross/net and phase aggregation. |
| Grid corroboration | `meterPower`, `meterPower2`, phase fields | Active meter power; sign is not documented in global table. | Diagnostic only. | Identify meters/topology; do not infer direction without evidence. |
| Instantaneous site demand | `loadsPower` kW | Total load power. | Direct only if measurement boundary matches Solar Site. | Confirm inclusion of house/outbuilding/greenhouse, EPS circuits, inverter use/losses, and loads outside CT boundary. |
| Cumulative site demand | `loads` kWh | “Load power consumption.” | Supporting cumulative field; wording is imprecise. | Verify accumulation/reset and integration against `loadsPower`. |
| AC inverter output | `generationPower` kW; `generation` kWh | `generationPower`: “Total AC output power？”; `generation` is AC output affected by battery flow. | Never substitute for raw PV without proof. | Determine physical meaning and possible flow reconciliation. |

At documentation level FoxESS names candidates for all requested categories. This is not proof of tenant-zero support: the official document says availability differs by device and missing variables are omitted.

### Documented-fit gaps

- Timestamps exist, but no freshness/publication SLA.
- A catalogue exists, but device-specific discovery semantics are unclear.
- `loadsPower` exists, but its measurement boundary/derivation is undocumented.
- Numbered PV fields exist, but no evidence maps tenant-zero's three strings.
- Battery sign is documented, but field availability and two-battery aggregation are unverified.
- V1 real-time uses local time while V0 history says UTC.
- New third-party, non-VPP OAuth onboarding is not unambiguously established.

## 6. Spike evidence and read-only safety requirements

This section requires enough evidence for independent review without fixing the eventual provider abstraction or serialized result shapes. Identity-result, capability-result, and telemetry-observation API designs belong in the later production adapter technical specification.

1. **Equipment mapping evidence:** record the minimum provider identifiers, labels, model/status and relationships needed to map tenant-zero station, inverter, logger, batteries, meters and PV inputs. Do not prescribe a production identity object.
2. **Capability assessment:** assess each section 5 quantity using the five-state vocabulary in section 8 and cite the request/response evidence. Do not prescribe a production capability object.
3. **Observation evidence:** preserve sanitized raw requests/responses plus retrieval time and the vendor variable, value, unit, device ID and raw timestamp needed to reproduce each conclusion. Parsed time/age may be recorded as derived evidence, clearly separated from raw data. Do not prescribe a production telemetry shape.
4. **Interpretation discipline:** preserve raw values alongside any proposed direction, aggregation or semantic interpretation. Missing, null, invalid, parse-failed, offline/fault, stale and partial states remain explicit and are never converted to zero.
5. **Behavioural boundary versus authorization:** this specification forbids writes regardless of credential power. The private-token documentation does not establish provider-enforced read-only authorization. OAuth `data_access` is relevant only if OAuth is actually available and used.
6. **Spike safeguard:** the spike must use an exact method-and-path allow-list and have no generic/arbitrary request facility in its executable path. Permitted REST calls are limited to:
   - `POST /op/v0/plant/list`
   - `GET /op/v0/plant/detail`
   - `POST /op/v0/device/list`
   - `GET /op/v1/device/detail`
   - `GET /op/v0/device/variable/get`
   - `GET /op/v0/device/fault/get`
   - `POST /op/v1/device/real/query`
   - `POST /op/v0/device/history/query`
   - `POST /op/v0/device/report/query`
   - `POST /op/v0/device/fault/history`
7. **Kafka boundary:** record documented Kafka availability/support questions only. Do not connect or consume unless a later, separately reviewed spike scope selects it and defines equivalent read-only safeguards.
8. **Sensitive-data minimisation:** retain only identity/location data needed for tenant-zero mapping. Plant address, user and installer details are not required.

## 7. Required tenant-zero verification

The later evidence-gathering spike must remain read-only:

1. Record tenant-zero FoxESS region/domain and authentication mechanism. If OAuth is used, request only `data_access`; if a private token is used, do not imply it has a scope or provider-enforced read-only restriction. Apply the section 6 allow-list in either case.
2. Enumerate stations/devices; map candidate inverter/logger using physical records and status/model/PV/battery flags.
3. Query V1 detail; compare returned battery count/model/capacity with issue evidence `2 × 5 kWh`. Preserve disagreement.
4. Capture variable catalogue and one all-variable V1 real-time response; build the section 5 availability matrix.
5. Run bounded targeted V1 reads, recording request/retrieval time, provider time, values, units, omissions and errors. Agree cadence before execution and stay within limits.
6. Map `pvNPower` channels to `8 + 8 + 3` strings using observation/installation evidence, not channel order.
7. Observe naturally occurring battery charge/discharge/idle and validate signed/split field relationships without changing schedules.
8. Observe natural grid import/export and validate separate fields and meter relationships without forcing state.
9. Explicitly look for naturally occurring evidence of simultaneous solar export and grid battery charging, or evidence that these flows are constrained. Do not manipulate inverter state to create the condition. If it is not naturally observed, record `not observed — requires later controlled verification or equivalent evidence`.
10. Reconcile contemporaneous PV/string/battery/grid/load/AC-output values; record residuals instead of inventing topology/loss rules.
11. Query a bounded historical interval for timestamps, cadence, gaps, duplicates, ordering and real-time consistency. Use a bounded production report only where it helps verify timezone aggregation or reconcile energy totals.
12. Query fault history for a bounded relevant date only if useful to verify observed fault/status evidence; avoid expanding the spike into general fault analytics.
13. Record naturally encountered missing, offline/fault, authentication, throttling, validation, transport and provider failures. These categories are candidate engineering handling, not a detailed taxonomy mandated by Solar. Do not deliberately exhaust quota or disrupt the site.

## 8. Spike acceptance evidence

Independent review must be able to inspect:

- credential type and observed endpoint auth behaviour, with secrets removed;
- stable equipment identifiers and physical mapping;
- device-specific availability status for every required quantity;
- sanitized raw discovery/detail/real-time/history responses, plus production-report or fault-history responses only where those optional checks were used;
- verified units, signs, timestamps, cadence, and observed freshness;
- reconciliation results and unexplained mismatches;
- observed quota accounting or an explicit unverified result;
- recorded Kafka availability/support status as documented capability, without treating Kafka integration as a spike acceptance dependency;
- failure evidence separating the broad Solar requirement from candidate engineering classifications, documented FoxESS behaviour, tenant-zero observations, and unknowns;
- one conclusion per requirement: `supported and verified`, `documented but unverified`, `partially supported`, `not observed`, or `not documented`.

This candidate specification defines the evidence; it does not itself satisfy these criteria.

## 9. Unresolved questions and evidence gaps

### Authentication/support

- Does V1 real-time accept OAuth despite requiring `token` in its endpoint table?
- Is Client Credentials available to a new non-VPP read-only service; what approval is required?
- If OAuth is available, can tenant zero use only `data_access`? Can a private token be provider-restricted to read-only operations, for which the snapshot documents no scope mechanism?
- What are key/token expiry, refresh, rotation, revocation, clock-skew, replay-protection, and commercial-use terms?
- Which host is authoritative for tenant zero, given the captured `https://www.foxesscloud.com/` request domain, EU OpenPlatform link, `{domain}`/`{api_domain}` OAuth placeholders, and US portal sample-code host?

### Identity/topology

- Which station/inverter/logger/meters/batteries/PV channels map to physical tenant-zero assets?
- Is one Solar Site one FoxESS station for tenant zero and future multi-inverter sites?
- What unit/aggregation does `batteryList.capicty` have?

### Telemetry

- Which variables are actually returned by tenant-zero hardware/firmware/account?
- Is `SoC` aggregate across both batteries; how are imbalance/BMS-specific values represented?
- Are battery powers AC-side or DC-side; how are losses represented?
- Do three physical strings map one-to-one to `pv1Power`, `pv2Power`, `pv3Power`?
- Does `pvPower` reconcile with strings through clipping/curtailment?
- Are import/export fields mutually exclusive net powers and what meter/CT boundary applies? Can tenant zero naturally demonstrate simultaneous solar export and grid battery charging, or is later controlled verification/equivalent evidence required?
- Does `loadsPower` cover every intended site load; is it measured or calculated?
- What does `generationPower` mean? Its official description contains a question mark.

### Time/history/limits

- Actual publication cadence/freshness and Solar's stale threshold?
- V1 timestamp offset/DST correctness and device clock drift?
- V0 history UTC/order/uniqueness/completeness and retention?
- 24-hour history limit versus documented three-day default?
- Exact daily quota accounting for account/inverter/endpoint/batch and reset time?
- Undocumented quota headers or `Retry-After`?
- If FoxESS would provision Kafka, what are its schema, timestamps, freshness/latency, ordering/delivery, replay/retention, quota/SLA, eligibility, and commercial terms?

### Failure behaviour

- HTTP/`errno` mappings for expired/denied auth, offline device, server failure, timeout, and partial batch?
- Are cached last-known values returned for offline devices, and how are they identified?
- Supported retry/backoff behaviour?

## 10. Explicitly deferred

Production adapter design, database schema, tariff/weather ingestion, optimiser logic, simulation/replay, UI, Kafka integration, and all inverter control are outside this specification.






