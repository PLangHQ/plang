# Plan: Per-action Notes (+ Examples + Description) as Markdown Files

**For:** coder.
**From:** architect.
**Source brief:** `.bot/compile-llm-notes-per-action/builder/notes-per-action.md` — read first; this plan is the design decisions on top of it.

The brief's premise stands: the Compile system prompt carries ~5 KB of action-specific teaching that fires on every step. Move it out, render only when the planner picked the action, and bound drift by what each action's own teaching constrains rather than by global rule density.

The brief asked architect to pick the storage shape. We picked **markdown files alongside the action handlers, file-per-concern** — not a C# attribute. This delivers two things the attribute shape did not: tuning LLM teaching ships without a C# rebuild, and the same file the LLM reads is the file plang devs read when looking up an action.

## Storage layout

Per-action prose lives at `os/system/modules/<module>/`. Three files per concern, action-prefixed for action-specific text, `module.`-prefixed for module-wide text:

```
os/system/modules/
  <module>/
    module.notes.md           # applies to every action in this module
    module.examples.md
    module.description.md
    <action>.notes.md         # applies only to this action
    <action>.examples.md
    <action>.description.md
```

Module folder names are lowercase, matching `PLang/app/modules/<module>/` (`assert/`, `error/`, `output/`, …). Note: `os/system/modules/` already contains legacy PascalCase `*Module` folders (`AiModule`, `OutputModule`, etc.) — those are unrelated; do not place new files inside them and do not rename them in this pass.

`module.` is a reserved action-name prefix inside `os/system/modules/<module>/`. No module may have an action literally named `module`. Document this convention in `Documentation/v0.2/good_to_know.md` so it isn't a surprise.

All three concerns (notes, examples, description) move to markdown. **Do not leave `[Description]` on the C# class** — the split would be arbitrary; all prose moves, C# keeps shape only.

## Merge semantics

When both `module.notes.md` and `<action>.notes.md` exist, **concat — module-level first, then action-specific.** Same rule for examples and description. Override semantics force every action that needs a specific note to re-state the family rule; that's the drift cycle this work exists to kill.

Empty / missing files are fine. An action with no notes file contributes no Notes section to its rendered catalog entry.

## Loader

`Modules.Describe()` (C#) gains responsibility for reading these markdown files when assembling each action's catalog entry. For each action:

1. Find the module folder at `os/system/modules/<module>/`.
2. Read `module.{notes,examples,description}.md` if present → module-level text fields on the catalog.
3. Read `<action>.{notes,examples,description}.md` if present → action-level text fields on the catalog.
4. Catalog entry exposes both pairs; renderer concats at render time (not at load time — keeps the two layers visible for debugging).

Cache reads per build. No hot-reload requirement.

**Path resolution.** `os/system/modules/` is resolved relative to the plang root the builder is running against (same root that `os/system/builder/llm/Compile.llm` is read from). Coder: confirm the existing path-resolution helper and reuse it; do not introduce a new convention.

**Do not turn `Describe()` into a plang goal in this pass.** That move (catalog assembly as a `.goal` using `file.read`) is the long-term right answer but has a build-order question — the catalog must exist before any goal can be built that builds the catalog. Park it. The "no rebuild to tune teaching" win is delivered entirely by C# reading the files.

## Validation

At catalog load, fail loud on **orphan markdown files**: any `*.notes.md` / `*.examples.md` / `*.description.md` under `os/system/modules/<module>/` whose stem is not `module` and does not match a registered action. One clear warning per orphan at startup. Don't crash — orphans should not block builds — but make them impossible to miss.

This is the replacement for "the C# compiler catches typos in attribute argument strings" — which it didn't, really, but file-system validation is at least explicit.

## Renderer

In `os/system/builder/llm/Compile.llm` (and any sub-template it includes for per-step action details — coder: locate `stepActionDetails.template` or its equivalent), each rendered action entry gains three blocks **when the corresponding text is present**:

```
## <module>.<action>

Description:
<module.description.md text, then a blank line, then <action>.description.md text>

Notes:
<module.notes.md text, then a blank line, then <action>.notes.md text>

Examples:
<module.examples.md text, then a blank line, then <action>.examples.md text>
```

Omit any block whose concatenated text is empty. Render only for actions in the planner's set — same path the existing per-step action details use today.

Modifiers (`error.handle`, `cache.wrap`, `timeout.after`) render through the same path uniformly. They are entries in the planner's action set; nothing special-cases them.

## What to delete from `Compile.llm`

Once the markdown files carry the rules, delete the corresponding sections from the system prompt. Per the brief's migration map:

| Delete from `Compile.llm` | Move text into |
|---|---|
| `error.handle` recovery semantics (Actions list, no duplicate peer, Key/Message filter) | `os/system/modules/error/handle.notes.md` |
| `"on error call X"` callback-vs-modifier rule | `os/system/modules/error/handle.notes.md` |
| `"is not empty"` operator rule | `os/system/modules/condition/if.notes.md` |
| `foreach` Collection-only, `%item%` auto-bound | `os/system/modules/loop/foreach.notes.md` |
| `call X, name=value` → `GoalName.parameters` | `os/system/modules/goal/call.notes.md` |
| `goal.call` payload `name` is goal identifier | `os/system/modules/goal/call.notes.md` |
| `llm.query` `system=/user=` shorthand | `os/system/modules/llm/query.notes.md` |
| `output.write` channel routing rule | `os/system/modules/output/write.notes.md` |
| `AsDefault` flag / `code.setDefault` distinction | `os/system/modules/variable/set.notes.md` |

**New file (fixes current drift):** `os/system/modules/assert/module.notes.md` containing "omit `Message` from `parameters` and from `formal` unless the step text names a custom error message; `Expected` is taken from the step text literal." Module-level so it applies to every `assert.*` action without re-stating.

What stays in `Compile.llm` is the cross-cutting kernel listed in the brief's "What stays" section — do not move any of that.

## Migration script

One-time, run by coder; not committed as a tool. Walk every action handler under `PLang/app/modules/<module>/<action>.cs`:

1. Extract every `[Example("…")]` argument → append to `os/system/modules/<module>/<action>.examples.md` (one example per paragraph, blank line between).
2. Extract `[Description("…")]` argument → write `os/system/modules/<module>/<action>.description.md`.
3. Delete the `[Example]` and `[Description]` attribute usages from the C# source.
4. Author the Notes files (`<action>.notes.md` / `module.notes.md`) by hand from the migration table above. There is no `[Notes]` attribute to extract — Notes is new.

After the script runs, the C# attribute classes `[Example]` and `[Description]` become unused on action handlers. Coder: decide whether to delete the attribute types entirely or leave them defined-but-unused for other call sites — check whether they're used elsewhere in the codebase first.

## Verification

Two checks from the brief, unchanged:

1. **System prompt size on a `Tests/Simple` step compile drops from ~20.8 KB to ~15 KB.** Trace inspection.
2. **The current drift cases are fixed end-to-end**, repeatedly across 3 fresh-cache builds of `Tests/Simple`:
   - `write out %message%` step → `formal='output.write(Data=%message%)'`. No `channel=%!data%`.
   - `assert %message% equals 'hello plang'` → `Message` omitted from `parameters[]` and from `formal`; `Expected='hello plang'` matches between `formal` and `parameters[]`.

If both hold across 3 fresh-cache builds in a row, the structural fix is working.

## Out of scope

Named explicitly so scope creep is named when it happens:

- Turning `Describe()` into a plang goal. Deferred — separate pass.
- Touching `Plan.llm`. Planner doesn't have the per-action density problem.
- Adding a structural validator for "no extra parameters in `formal` vs `actions`". Separate concern; the formal-mirroring rule stays in the cross-cutting kernel.
- Renaming or relocating any C# action handler files.
- Hot-reload of the markdown files mid-build.

## Order of work (suggested)

1. Loader change: `Describe()` reads `os/system/modules/<module>/{module,<action>}.{notes,examples,description}.md`, attaches text fields to catalog entries.
2. Renderer change: per-action blocks in `stepActionDetails.template`.
3. Migration script: extract `[Example]` and `[Description]` from C# into markdown, delete attributes.
4. Author Notes files from the migration table; author the new `assert/module.notes.md` for the `Message` omission rule.
5. Delete the corresponding sections from `Compile.llm`.
6. Run the two verification checks.
7. Add orphan-file validation at catalog load.

Step 7 last because it is a safety net, not a feature — it catches the next mistake, not this one.
