# Stage 5 progress — the kind follow-up (decision 812, option b)

The follow-up MECHANISM is built and proven. Two gaps remain: teaching (stage-2 base-type pick) and a pre-existing
stale decider golden. State as of this note — the code is uncommitted WIP pending the architect's call on the golden.

## What's built (WIP, uncommitted)

| file | change |
|---|---|
| `PLang/app/goal/step/pick/question/this.cs` | new `Kind.KindOf`; new `Of` field (the picked type's name for the wording). |
| `PLang/app/goal/step/pick/list/this.cs` | `KindKey = "@kindof."`; `Take` routes a `@kindof.` answer to `Refine`; `Questions()` appends a KindOf question for each Type-slot Option (`property.Type.ClrType == typeof(type.@this)`) whose picked type has an empty kind and offers kinds; `Picked()` resolves the pick (text name → type, or the refined kinded type); `KindsOffered()` (a generic offers the registry's types as its element, else the type's `kind.list` less `k.kind.IsEmpty`/`k.Internal`); `Refine()` folds the answer into `{name, kind}` on `_option`. |
| `os/system/builder/BuildGoal/Decide.goal` | a conditional **stage 3**: render `decider3.template`; `if %questions% contains "@kindof"` → `llm.decider` + `build.pick` (the if's body). |
| `os/system/builder/llm/templates/decider3.template` | renders only KindOf questions as a choice (the picked type's kinds + none; a generic asks its element); `{}` when none, so the stage is skipped. |
| `os/system/builder/BuildGoal/.build/decide.pr` | rebuilt from the **os/ app root** (paths clean: `/system/builder/EmitBuildEvent`; goal moved to the stage-0 `file` field). The if-body nests `llm.decider`+`build.pick` as children (verified). |

**How to rebuild decide.pr** (self-hosting, learned here): build from the **os/ app root**, not the project root —
`cd os && plang '--build={"cache":"skip","files":["system/builder/BuildGoal/Decide.goal"]}'`. Building from
`/workspace/plang` contaminates goal paths with the bin absolute path (`/PlangConsole/bin/.../EmitBuildEvent`).
`os/` needs the decider key: `cp .data/setting/data.sqlite os/.data/setting/data.sqlite` (gitignored). `dev.sh build`
hardlinks `os/system`→bin, so a source rebuild is live in bin automatically.

## Proven (fresh cache:skip builds, then --test)

- `set %n% = "1.5" as decimal` → `{number, kind: decimal}` — **AsDecimalKeepsDecimal PASS**.
- `set %d% = "PT30S" as iso duration` → `{duration, kind: iso}` — **AsIsoDurationKeepsIso PASS**.
- `set %n% = 5` (no as) → `number`, no kind — **NoAsNoType PASS** (guard).
- 3 of 5 + the guard green.

## The two gaps

1. **Stage-2 base-type pick (teaching).** `as json` → stage 2 picks `dict` (want `item`, then json); `as list of path`
   → picks `path` (want `list`, then element). The follow-up is correct but never fires because stage 2 collapses to
   the nearest concrete type (the baseline behaviour). Fix = teaching (prompt C shows each type's kinds under it; the
   Type-question wording) so stage 2 picks `item`/`list`. Pending the architect's confirm on the approach.
2. **Pre-existing stale decider golden.** `PickListTests` (StageOnesAnswer_*) is RED on clean HEAD (confirmed by
   stashing the WIP) — `pick_golden.json` last pinned at `284e61c7a`, before settings-became-types (778). The 1509-line
   diff is that drift, not stage 3. Blocks a clean teaching-edit re-pin. Pending the architect's call: re-pin the
   accumulated drift now, or clear the pre-778 staleness as separate debt first.

## Measurement (5 builds each, fresh cache:skip, jq) — after teaching

| sentence | result 5/5 | vs baseline |
|---|---|---|
| `as decimal` | `{number, kind: decimal}` 5/5 ✓ | baseline number 5/5 — now carries the kind |
| `as list of path` | `{list, kind: path}` 5/5 ✓ | baseline 2/3 flaky — now solid; `[0]` born path |
| `set %n% = 5` (no as) | `number`, no kind 5/5 ✓ | guard holds |
| `as text` | `text` 5/5 ✓ | guard holds |
| `read 'notes.md' as text` | `{file, kind: md}` 5/5 | baseline issue 20 (extension-typed); md, never goal/llm (the architect's check passes) |
| `as json` | `dict` 5/5 ✗ | unchanged — stage 2 picks dict, not item |
| `as iso duration` | `duration` bare 5/5 ✗ | @kindof fires, offers {short, iso, dotnet, none}, names "as iso", but the model answers none |

**Key finding:** editing type DESCRIPTIONS did not move json or iso. Descriptions feed prompt C (the WRITER), not the
decider's stage-2 Type question (criteria names + the ask wording only). So the base-type pick (dict vs item) and the
kind follow-up are driven by the QUESTION wording — `set.notes` `ask:` and `decider3.template` — not descriptions. The
description edits remain correct teaching (writer + type page) but aren't the lever for these two. Reported to the
architect; holding for the go-ahead to tune the ask + follow-up wording.

## Still to do

Teaching for the base-pick; measure the extra-exchange count (how many builds take stage 3); verify `as text` /
`read … as text` answer text or md (never goal/llm — text's machine formats stay offered per the architect); the
element recursion is one level (covers the baseline); re-pin the moved decider/page goldens; 5/5.
