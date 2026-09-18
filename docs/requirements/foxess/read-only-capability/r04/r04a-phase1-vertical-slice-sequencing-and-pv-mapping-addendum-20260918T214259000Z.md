# FoxESS read-only capability specification — r04a addendum: Phase 1 vertical-slice sequencing and PV-channel-mapping supersession

Status: addendum to r04; r04 itself is unchanged and remains the frozen, immutable requirements baseline
Created: 2026-09-18
Context: Solar Optimiser issue #3 (Phase 1 telemetry vertical slice design)
Authority: Product-owner decision (Dazz Knowles), recorded here per this project's governance discipline of writing departures down explicitly rather than silently deciding around an approved requirements document

## Purpose

r04 is the frozen, approved requirements and evidence baseline for the FoxESS read-only capability. During design of the Phase 1 telemetry vertical slice (issue #3), the product owner made two decisions that depart from what r04 specifies. Consistent with this project's established governance discipline (see `docs/foxess-harness-proving-work.md` §4, on recording departures/conflicts explicitly rather than reinterpreting them), those decisions are recorded here rather than left implicit in chat/issue discussion. r04 itself is not edited; this addendum supplements it.

## 1. Supersession of the verify-before-build ordering (r04 §1, §7, §8, §10)

**What r04 says:** §1 states the purpose of the capability work is to establish whether tenant-zero FoxESS data is usable *before* a production adapter is designed. §7 specifies the required tenant-zero verification steps. §8 specifies the spike's own acceptance evidence. §10 explicitly defers "production adapter design, database schema..." as out of scope for r04.

**Decision:** For the Phase 1 vertical slice (issue #3), the product owner decided to proceed directly to building the production-shaped adapter and schema, using real FoxESS credentials from the earliest implementation tasks, rather than requiring a separately completed, evidence-gated verification spike beforehand. Reasoning given: the FoxESS API is already known to work and is already read by other existing software, so the risk r04's ordering exists to manage — designing a production system on an API that might not behave as documented — is judged low enough that live verification interleaved with early build tasks is an acceptable substitute for a dedicated upfront spike.

**Scope of the supersession:** This supersedes the *ordering* r04 §1/§10 describe (spike, then design) for this Phase 1 slice only. It does not waive the underlying evidence requirements themselves — r04 §7's verification steps and §8's acceptance evidence are still expected to be produced, just as a byproduct of early implementation tasks rather than as a prerequisite gate before implementation begins. If early implementation reveals the API does not behave as r04's evidence assumed, that is new evidence requiring a return to specification per the existing governance pattern, not something to be silently absorbed into the build.

## 2. Deferral of physical PV-channel mapping (r04 §7.6)

**What r04 says:** §7.6 requires tenant-zero verification to map `pvNPower` channels to the physical `8 + 8 + 3` PV strings using installation/observation evidence, explicitly forbidding an assumption based on channel order alone.

**Decision:** Phase 1 stores the raw FoxESS-reported channel identifier only (e.g. `"3"` for `pv3Power`), scoped per device, and does not attempt to establish or record which physical string a channel corresponds to. This is a deliberate product-scope decision, not an oversight: most installations have a single PV string, making channel-to-physical-string mapping a low-value feature for the product generally, even though tenant zero's own multi-string, multi-inverter setup is comparatively unusual. Raw channel data is retained so a physical mapping could be derived later from the data (or supplied manually) if ever needed, but doing so is not a Phase 1 deliverable.

**Scope of the deferral:** r04 §7.6 is not satisfied by Phase 1. This addendum records that explicitly, rather than allowing raw-channel storage to be read as evidence that §7.6 has been addressed. If physical PV-string mapping later becomes a product requirement, it remains open work against r04 §7.6, not something Phase 1 quietly closed out.

## Files changed

r04 and its evidence set are unchanged. This addendum is the only new file.
