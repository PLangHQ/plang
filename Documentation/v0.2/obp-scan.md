# OBP scan

The procedure for auditing changed C# against the OBP smells. The smells themselves are named and defined in [`obp-smells.md`](obp-smells.md) — this doc only orders the hunt.

## Procedure — when asked to "obpscan"

1. Find the range: everything changed since the **last scanned commit** (recorded at the bottom of this file). `git diff <last-scanned>..HEAD --stat` for the file list, then read the diffs.
2. Walk every changed/added `.cs` against the triage order below — *fork* first (the loud alarm).
3. Report findings grouped by severity: real violations (fix), borderline (note), clean. Name each finding by its smell.
4. After the scan, update the **Last scanned** marker at the bottom to the current `HEAD`.

The point is to catch shape drift *as it lands*, not at the end of a stage — especially API surface (new classes/methods) that wasn't in the plan.

## Triage order

1. ***fork*** — behavioral `if`/`switch`, generic fallback beside per-type handlers, type-switch in a registry, optional-override branch.
2. **Unplanned API surface** — a new class or method that was NOT in the plan. Every new type/member is a design decision; if the plan didn't call for it, ask *why it exists*: born from usage rather than domain shape → collapse it; a helper that should be a member on an owning type → *stray helper*; a second way to do something that exists → *fork*; a parallel/adapter type to bypass a `[JsonIgnore]`/access problem → fix the owner instead; a public getter or interface the plan didn't intend → flag it.
3. ***verb+noun*** — any compound member name where one half is a verb (`IsX`/`HasX` booleans exempt).
4. **Shape smells** — *naked collection*, *middleman*, *cross-file lock*, *stored twice*, *split lifecycle*, *flat copy*, *raw hand-off*, *stray helper*.
5. **Value smells** — *broken seal* (non-leaf touches `Data.Value`), *opened box* (carrier cracked for a helper), *clr leak* (`.Clr` off-boundary), *late stamp* (construct-then-stamp).
6. **The meta-test** — one line of choreography needing edits in three files = one missing type; coincidental duplication that vanishes when the shape is right — fix the shape, don't extract.

---

**Last scanned:** `a8dc3a109` (cli-app-property-override Stages 4-10 + follow-ups: CallStack→Actor + plang-typed knobs, run-state internal-set, Test.Apply→walk + ConvertIntoPlangList, Debug Apply→Activate + Level choice, CurrentActor removal, builder→build rename, app.builder.type dissolve → app.type.spec/render + app.type.catalog.view, build-mode-inversion Case A — 2026-07-07). ALARMS CLEAN. Two borderline: (1) `ConvertIntoPlangList` is Verb+Noun but matches the sibling `ConvertElementsInto` — consistent with the converter file's convention, not a new divergence. (2) Case A `Setting.Set(InMemory,...).GetAwaiter().GetResult()` reads as sync-over-async but is a completed ValueTask (InMemory has no I/O) — commented at the site. No forks (the if/switches are config-reads + converter target-dispatch, not behavioral forks); all new API surface was planned/approved; no new .Clr, no construct-then-stamp; the CallStack-knob `.Value`/`.ToInt32()` reads are value-face reads, not smell-#5 transforms. Update after each scan.
