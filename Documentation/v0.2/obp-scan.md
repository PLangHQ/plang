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

## Cross-file forks — the census sweeps

The triage above is diff-scoped and body-local — it cannot see a fork whose two halves are two *clean* methods in two files. The born-native lift is the worked example (found 2026-07-14, `.bot/navigation-driven-record-builder/`): `type.@this.Create` and `type.list.@this.Create` were two rung-ladders for one operation, mutually recursive, each individually "navigated, not switched". Five reasons it survived every sweep, each inverted into a scan:

1. **Body-local audits miss inter-method forks** → **signature census**: for one operation, grep every public method sharing its shape; more than one non-delegating implementation = fork. `grep -rn "public.*item.@this Create(object" --include="*.cs"` found exactly the two doors, zero noise. Run one census per value-model operation (born, convert, compare, write, read, navigate) — the "one selection door" rule generalized.
2. **Uniform naming camouflages** (everything named `Create` reads as one door family; *one name for two things* is invisible to a rename-hunting eye) → the census above keys on signature, not intent — it sees through the shared name.
3. **Mutual delegation makes every trace look cooperative** (each door's fallback calls the other, so any single trace terminates correctly; agreeing forks produce rot, never bugs) → **mutual-recursion scan**: for each dispatcher, grep its body for the other candidate's accessor; an A→B fallback with a B→A dispatch is one operation split across two homes.
4. **Registries acquire behavior silently** → **registry-constructs scan**: `grep -n "new global::app" app/*/list/this.cs` (and kin), keep hits that construct *domain values* (identity descriptors are a registry's legitimate mint). A `*/list/` class building values is a factory on the wrong owner.
5. **The claim is often already written down, in prose** → **prose-claims scan**: grep doc comments for `THE `, "the one owner", "one home", "the single"; group claims by responsibility; more than one claimant per responsibility = fork or doc drift. Noisy (per-type "THE door" on `Value` overrides is legitimately plural) but the born lift had three claimants sitting in plain sight.

These sweeps are whole-tree, not diff-scoped — run them at stage ends, when a placement smell surfaces, or when a worklist (analyzer sweep, rename) forces a visit inside old dispatch code. A hit is a candidate, not a verdict: declared-vs-inferred, sync-vs-async seams can legitimately be two doors — the test is whether the two bodies contain the *same rungs*.

---

**Last scanned:** `a8dc3a109` (cli-app-property-override Stages 4-10 + follow-ups: CallStack→Actor + plang-typed knobs, run-state internal-set, Test.Apply→walk + ConvertIntoPlangList, Debug Apply→Activate + Level choice, CurrentActor removal, builder→build rename, app.builder.type dissolve → app.type.spec/render + app.type.catalog.view, build-mode-inversion Case A — 2026-07-07). ALARMS CLEAN. Two borderline: (1) `ConvertIntoPlangList` is Verb+Noun but matches the sibling `ConvertElementsInto` — consistent with the converter file's convention, not a new divergence. (2) Case A `Setting.Set(InMemory,...).GetAwaiter().GetResult()` reads as sync-over-async but is a completed ValueTask (InMemory has no I/O) — commented at the site. No forks (the if/switches are config-reads + converter target-dispatch, not behavioral forks); all new API surface was planned/approved; no new .Clr, no construct-then-stamp; the CallStack-knob `.Value`/`.ToInt32()` reads are value-face reads, not smell-#5 transforms. Update after each scan.
