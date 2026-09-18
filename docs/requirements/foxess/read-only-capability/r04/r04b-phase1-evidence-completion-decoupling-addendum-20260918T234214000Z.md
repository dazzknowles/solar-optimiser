# FoxESS read-only capability specification — r04b addendum: Phase 1 evidence-completion decoupling

Status: addendum to r04 and r04a; r04 and r04a themselves are unchanged
Created: 2026-09-19
Context: Solar Optimiser issue #3 (Phase 1 telemetry vertical slice design), continuing from r04a
Authority: Product-owner decision (Dazz Knowles), recorded here per this project's governance discipline of writing departures down explicitly rather than letting them drift undocumented

## Purpose

`r04a` recorded that r04's verify-before-build *ordering* (§1/§7/§8/§10) is superseded for the Phase 1 vertical slice, while explicitly stating that r04 §7's verification steps and §8's acceptance evidence remain required — to be produced as a byproduct of early implementation tasks, not waived. During further design review (an independent adversarial review of the resulting design), a gap was found between that statement and where the design had actually landed: the design had moved production of the r04 §8 evidence document to an explicit post-build deliverable, decoupled from whether the Phase 1 build itself counts as "complete." That is a broader claim than r04a recorded, since it means Phase 1 could be declared delivered before the required tenant-zero evidence exists. This addendum records that broader decision explicitly, rather than leaving it as an undocumented drift between r04a and the design.

## Decision

For the Phase 1 vertical slice, completion of the r04 §7 verification/§8 acceptance evidence is decoupled from the definition of "Phase 1 build complete." The running Phase 1 system *is* the mechanism by which that evidence gets produced — the build and the evidence-gathering are the same activity, not two separate ones — but the evidence document itself (the write-up of per-requirement conclusions, reconciliation results, quota accounting, cadence/freshness findings and failure evidence) is treated as a tracked follow-up deliverable, produced once the running build has generated enough real tenant-zero data to write it honestly, rather than a precondition for calling the Phase 1 build itself finished. Reasoning given: this lets real progress continue on the vertical slice while still treating r04's evidence obligations as real and owned, rather than either blocking on them upfront (already superseded by r04a) or quietly dropping them by never producing the document.

## Scope

This does not waive any of r04 §7/§8's substantive content — the same evidence obligations exist as r04 describes; only their relationship to "Phase 1 done" changes. The evidence document remains an explicit, owned, tracked deliverable, not indefinitely deferred: it is expected once the build has run against tenant zero long enough to produce the findings, covering r04 §8's full list (per-requirement supported/unverified/partial/not-observed/not-documented conclusions, reconciliation and residuals, observed quota accounting, cadence/freshness findings, and failure evidence) — with the sole exception of r04 §7.6's physical PV-channel-to-string mapping, which `r04a` already separately and permanently defers as a product-scope decision, not a timing one.

## Files changed

r04 and r04a are unchanged. This addendum (r04b) is the only new file.
