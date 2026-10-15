# codeanalyzer v1 — kind-is-type (stages 0–3 + 767 + 789/790)

## Scope (set by the architect, 2026-10-15)

Review the **landed** stages 0–3 of decision 774 (`a kind is a type`), plus:
- **767** — a goal call's name never climbs out of its folder.
- **789/790** — a setting reads itself back from its stored record; no reader is kept for the old shape.

Diff base is the parent tip **runtime2 `cf66e2d01`**. Stages 4–6 are mid-flight (coder still on stage 4's born-unread/navigator work, builder on stage 5) and are **out of scope** until the architect sends the go.

**Known and accepted — do NOT re-raise unless found worse than recorded:**
- `type.Construct` beside `Make` (the second make door; on branch A's "one make door" list).
- bool's closed pair living on a kind (backlog 782).
- the record reader's json round-trip in `Step.Set` (798).
- `template` left on its base, a facet not a `kind<T>` (782).

## Method (character passes, in order)

1. **Pass 1a — OBP rules** against `CLAUDE.md ## OBP Shape Smells` + `object_pattern_formal.md`. Run `Tools/ObpScan` on the core types so no member is skipped.
2. **Pass 1b — named shape smells**, explicit yes/no per smell, on every file in scope.
3. **Pass 1c — parked list** (`Documentation/Runtime2/obp-cleanup.md`): cite, don't re-raise.
4. **Pass 2 — simplification**; **Pass 3 — readability**; **Pass 4 — behavioural reasoning** (trace origins, full type surface, catch sites, clone/copy, rehydration); **Pass 4.5 — root cause vs symptom**; **Pass 5 — deletion test**.

## The OBP checks the plan itself names (verify these specifically)

| new surface | check |
|---|---|
| `kind<T> : kind : type` | what's different about a kind lives on the kind class (it registers under T); no `is kind` check outside it |
| a kind falls back to its parent | **one** place passes a question up (`kind<T>`); a kind overrides only what is its own; the parent never asks its kind back |
| names on two levels | a kind is asked of its parent, never of the app; the app-wide kind search (`type/list/this.cs:51,57–69`) is gone; no second lookup by kind name |
| a settings row read by its key | one fact names the class (the key); the row doesn't store it a second time (789/790: `setting.Create` from `declared.ClrType`; old-shape reader gone; unreadable row loud) |
| `[Kinds]` + the base-class walk gone | `type/item/this.cs:528–533`; `type.format` gone (`type/this.cs:117`, `type/format/this.cs`) |
| 767 | a name with a `..` segment refused before resolve (`goal/list/this.cs` `Find`) |

## Review order (highest value first — locked core, new surface)

1. **The kind model** — `type/kind/{this.cs, this.Generic.cs, empty/this.cs, subclass/this.cs, format/this.cs}`, `type/this.cs`, `type/this.Generic.cs`, `type/item/this.cs` base virtuals. Check the 776 "every hide became an override" ruling (no CS0108 member hides left).
2. **The app-wide search removal + registry** — `type/list/this.cs`, `type/list/Registry.cs`.
3. **The record read (789/790)** — `type/reader/this.cs`, `type/reader/Record.cs` (new), `type/item/ICreate.cs`, `type/item/setting/this.cs`, `app/this.cs` (the deleted start-up loop).
4. **Settings as types** — module `*/setting/this.cs` classes, `goal/step/this.Scope.cs` (step.Setting), `goal/list/setting/this.cs`, deleted `setting/kind/` + `setting/serializer/Reader.cs`.
5. **Stage 1** — `type/item/number/kind/`.
6. **Stage 3 family kinds** (mechanical `kind<T>`) — duration, size, hash, archive, conversation, junit, wire, path/scheme, separator (as `kind<text>`), list/kind, choice/set. Sample for a uniform pattern; one divergence = one finding.
7. **767** — `goal/list/this.cs` `Find`.

## Output

- `v1/report.md` — per-file findings, by smell name, with file:line; verdict per file.
- `v1/verdict.json` — PASS/FAIL one-liner.
- `summary.md` — overwrite at bot root.
- Reply to the architect: Done / HASH / CHECK / OPEN.

Read-only. No source edits. Announced, reverted mutation probes only if needed to confirm a finding.
