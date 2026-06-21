# Docs v1 — template-stamping-at-read — PASS

**Next: Ingi** (merge-gate decision).

## Verdict

PASS. Five documentation gaps found and filled across four files. CLAUDE.md proposal filed for two new cross-cutting rules. No C# docstring drift on changed files. Merge gate carries from auditor PASS (`728e1ba2e`).

## Changes made

### 1. `wire-serialization.md` — Wire auto-verify contract (updated)

Replaced the stale bullet: `Wire.Read does not auto-verify. A reconstructed Data carries its Signature populated-but-unverified.`

This was accurate before `commit 50963ed18` (signature-as-layer integration). Now:
- Transport (`View.Out`): `@schema:signature` layers are auto-verified inline; context-less Wire fails closed with `SignatureVerifyContextMissing`.
- At-rest (`View.Store`): context-less Wire trusts on read (local-FS threat model; SettingsStore refactor will add verify-with-context).
- Regression test pointer: `Deserialize_SignatureLayer_NoActorContext_FailsClosed` in `WireConverterSigningTests.cs`.

### 2. `wire-serialization.md` — Template-stamping at read (new section)

New section added before `Multi-segment serializer extension matching`:
- The problem: post-parse `Authored()` walk stamped all values regardless of origin (http bodies got stamped).
- The fix: `Wire` gains a `template` constructor param; goal deserialization uses the dedicated authored Wire (`Template="plang"`); all other Wires have `template=null` → literal reads.
- Types own the holes-decision: `text.@this` stamps only when `HasHoles`.
- Trust boundary: only two code paths reach the authored Wire — `goal.list` and `GoalCall`. Runtime ingest never does.

### 3. `variables.md` — Born-typed decline + GetValue cleanup (updated)

- Added **Born-typed variable decline** bullet to Behavior & Rules.
- Removed `GetValue(string name)` from the API surface table (deleted in codeanalyzer session B-series handoff).
- Removed `GetValue<T>()` from the Data properties list (never existed on `Data`; stale entry). Replaced with `Clr<T>()`.
- Removed the `memory.GetValue("age")` code example.

### 4. `type-system.md` — datetime navigable members + Data.Clr<T>(fallback) (new section)

New section at the end: `datetime` navigable members — `.Date` (→`date`), `.TimeOfDay` (→`time`), `.Offset` (→`duration`), `.Ticks`/`.Millisecond`/`.DayOfYear`/`.DayOfWeek` (→`number`). How dot-navigation reaches them. `Data.Clr<T>(fallback)` as the async typed CLR extraction utility.

### 5. `good_to_know.md` — five new index entries

- Wire auto-verify
- Template-stamping at read
- Born-typed variable decline
- `datetime` navigable members
- `Data.Clr<T>(fallback)`

### 6. CLAUDE.md proposal filed

`.bot/template-stamping-at-read/claude-md-proposals.md` — two bullets: Wire auto-verify posture, born-typed variable decline.

## What I did NOT find

- No System.IO reach in changed doc files (n/a — docs only).
- No stale type names in docs for renamed types (checked `GetValue`, `EnsureInnerSigned`, `_authored` — all removed from both code and docs).
- No module catalog gaps (`variables.md` / `type-system.md` actions unchanged in name/signature).
