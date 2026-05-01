# codeanalyzer — runtime2-data-share-state

## v1 — Data identity-preservation review (NEEDS WORK)

Reviewed coder/v1's rewrite (architect/v1 phases 1–4 + 5a tests) against the
standard 5-pass review **plus** Ingi's lifecycle audit. Architecture is
clean: the four As<T> rules + AsCanonical correctly implement
"every plang variable IS Data" with state aliasing.

Four concrete cleanups found (none blocking):
1. Dead conditional in `AsCanonical` full-match branch (`Data/this.cs:471–473`).
2. Redundant transient Data alloc in `WrapAs<T>` IEnumerable branch
   (`Data/this.cs:642–647`) — exactly the zero-overhead signal Ingi flagged.
3. Dead-but-side-effecting Type mutation in `Variables.Set` (`:71`) — no
   caller passes type+Data; delete.
4. JSON-roundtrip clone duplicated 3× (Variables.Set, list.add,
   variable.set) — extract as `Data.SnapshotClone(object)`.

Lifecycle audit: 79 `new Data.@this` sites all justified; `.Value` reads
clean on the new path (pre-existing `Steps:161` engine peek and Legacy
emitter helpers documented as out-of-scope / Phase 6 deferred); no
Data-in-Data in production; copies match the architect's "1 alloc per
cross-type cast" contract except finding #2.

See [v1/summary.md](v1/summary.md) for details and
[v1/result.md](v1/result.md) for full per-file findings.

## v2 — review of coder/v1 review-response (CLEAN — pass)

Coder pushed `60b8d1f3` addressing all four v1 findings: `AsCanonical`
dead branch collapsed, `WrapAs` transient gone, `Variables.Set` mutation
deleted, three JSON-roundtrip clones extracted to `Data.SnapshotClone`
+ `Json.SnapshotClone` options.

All four fixes verified clean. No new OBP violations, no new dead code.
Pass 4 (behavioral) caught a quiet behavior unification: two of three
old call sites lacked `UnwrapJsonElement`; the new helper applies it
everywhere (the right shape — no `JsonElement` leaks downstream). The
commit message framed the change as pure dedup; in reality it's
dedup + unification. Worth the note, not a fix request.

Two cosmetic carryovers from v1 sub-findings remain (`??` defensive
fallback in `set.cs:117–118`; redundant `global::` qualification in
three callsites) — non-blocking, can ride the next unrelated touch.

See [v2/summary.md](v2/summary.md) for the behavior-unification snippet
and [v2/result.md](v2/result.md) for full pass-by-pass.

**Suggested next step: tester** — pin the now-Dictionary shape at
`list.add` snapshots and `Variables.Set` dot-path; confirm 9 stub C#
tests stay properly stubbed.

## v3 — review of coder/v2 (CLEAN — pass)

Coder pushed `24cba238` fixing two value-resolution bugs that blocked
the LLM builder: (1) `AsCanonical` only walked strings — list/dict
parameters with nested `%vars%` from `.pr` were never resolved on the
plain-`Data` path, while `AsT_Impl` did walk them, so plain `Data` and
`Data<T>` had drifted; (2) `TypeConverter` had no `JsonNode` arm — a
`Data<JsonNode>` from `set ... type=json` couldn't reach typed handlers.

Fix shape: `WalkContainerVars` + `IsWalkableContainer` extracted as
shared helpers; both `AsCanonical` and `AsT_Impl` route through them.
`JsonNode` added to dispatch + parallel `JsonArray` element-iteration
arm. 6 new tests directly exercise the bug shapes.

User asked for extra weight on Pass 4 — the var-walk through the
`json → LlmMessage` path. End-to-end trace verified: 10-step flow
from `.pr` parse → AsCanonical walk → resolved list → JsonNode storage
→ `As<List<LlmMessage>>` → JsonArray arm → typed elements. Symmetry
holds. Action-template carve-out works at the per-element level via
`SubstitutePrimitive`'s guards.

Three minor items, none blocking:
1. `AsCanonical`'s partial-interp + container-walk branches duplicate
   "build transient + alias state" 6 lines each — extract `BuildTransient`.
2. New `JsonArray` arm silently drops convert errors per element,
   matching the existing `JsonElement` arm but diverging from the
   regular `IList` arm — informational.
3. **Test gap (durable)** — none of the 4 new `AsTIdentityTests`
   container tests assert state-aliasing on the new transient. Deleting
   the four aliasing lines wouldn't be caught. One additional
   `ReferenceEquals` test would pin it.

See [v3/summary.md](v3/summary.md) for the end-to-end trace and
[v3/result.md](v3/result.md) for full per-file findings.

**Suggested next step: tester** — verify the 6 new tests, optionally
add the missing aliasing test, re-run the LLM smoke path now that
`Start.goal` builds past the NRE.
