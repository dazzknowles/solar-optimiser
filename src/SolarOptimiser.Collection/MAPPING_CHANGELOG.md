# FoxESS variable mapping changelog

Each version below is one immutable, complete mapping set (SOL-T-702) — every version bump is recorded here even
when purely additive, since `TelemetryObservations.MappingVersion` must always resolve to a documented meaning.

## Version 1 (initial)

Direct mappings: `SoC` → `BatterySOC`; `invBatPower` → `BatteryPowerSigned`; `batDischargePower` →
`BatteryDischargePower`; `batChargePower` → `BatteryChargePower`; `pvPower` → `PVPowerTotal`; `PVEnergyTotal` →
`PVEnergyTotalCumulative`; `feedinPower` → `GridExportPower`; `gridConsumptionPower` → `GridImportPower`;
`loadsPower` → `LoadPower`; `loads` → `LoadEnergyCumulative`; `meterPower` → `MeterPower`; `meterPower2` →
`MeterPower2`; `generationPower` → `GenerationPowerAC`; `generation` → `GenerationEnergyCumulative`.

Pattern mapping: `pv1Power` … `pv24Power` → `PVStringPower`, with `Channel` set to the numeric suffix (raw vendor
channel index only — no ordinal-to-physical-string assertion per SOL-T-703; tenant-zero's channel layout is 16
across the large inverter's two 8-string MPPTs plus 3 on the small inverter, per the technical spec, but this
mapping does not encode that).

**`GenerationPowerAC`'s physical meaning is explicitly uncertain.** FoxESS's own documentation describes
`generationPower` as "Total AC output power？" (their own question mark) — it may or may not equal true AC
inverter output distinct from PV/battery flow. It is collected as a diagnostic/corroboration quantity only
(SOL-F-503) and must not be substituted for raw PV or treated as validated until tenant-zero evidence resolves it.

A FoxESS variable not listed above is not mapped to any `TelemetryQuantity` — it is still recorded in
`DeviceCapabilities` (unmapped) per SOL-T-302, it simply produces no `TelemetryObservations` row.
