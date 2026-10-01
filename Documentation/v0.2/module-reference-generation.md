# Module Reference Generation

The learner-facing module pages (`docs/modules/<module>.md`) are **generated** from
the same source the builder already uses — never hand-written. Every fact on a page
has exactly one home, so a page can never disagree with the code.

This document is the spec: the source format, where each fact comes from, the
generation rules, and a golden output sample for the `file` module.

## Why generated

Hand-written module tables drift. Real examples found on `doc-tree-app-obp`:
`condition` documented `Condition / GoalIfTrue / GoalIfFalse` while the handler is
`Left / Operator / Right / Negate`; `error handle` documented `Goal` while the
param is `Actions`; `output write` documented an `Actor` param that does not exist.
A table generated from the handler cannot drift from the handler.

## One home per fact

| Fact on the page | Single source |
|---|---|
| Module intro | `<module>/module.description.md` + optional `module.guide.md` (learner-only) |
| Action summary line | `<module>/<action>.description.md` |
| Example steps | `<module>/<action>.examples.md` (the `Step text:` lines) |
| Property name, "what it changes", "how you say it" | `<module>/<action>.notes.md` |
| Type / Required / Default | handler C# attributes (`data.@this<T>`, `[Default]`, nullability / `[IsNotNull]`) |
| Returns (type) | handler `Start()` / `Run()` signature — `Data<T>` → T |
| Returns (meaning) | a `Returns —` line in `<action>.notes.md` |

The generator reads all of this off the descriptor `app.goal.step.action.@this`,
which already exposes `.Description` / `.Notes` / `.Examples` (lazy file items,
`action/this.Schema.cs:69-86`) and the handler type; property shape and the return
plang-type come from the same catalog metadata the builder uses
(`action/this.Schema.cs:44-66`).

## Source format: `notes.md`, one line per property, tagged

One line per property. The prose before the first `·` is the learner description.
Optional tags follow, each introduced by `·`:

- `· say:` — how you type it in a step (fills the "How you say it" column).
- `· builder:` — the compile-only directive (when the planner should emit it).

A line whose property name is `Returns` documents the return value's meaning.

```
Pattern — which files come back, as a glob · say: `matching '<glob>'` · builder: only when the step names one
```

**Parsed once, not stripped twice.** A `notes.md` file is read as its lines, each
parsed into `{name, prose, say, builder}`. Each template then shows only the fields
its audience needs — nothing is rendered raw, so no tag can leak:

| Consumer | Shows |
|---|---|
| Learner page (`docs/modules/*.md`) | `name`, `prose`, `say` |
| Compile teaching (build prompt) | `name`, `prose`, `builder` |

> Because the compile teaching reads `notes.md` today, the tagged `notes.md` files
> and the parse-into-fields change land **atomically** — do not commit tagged
> `notes.md` ahead of the parse, or the tag text reaches build prompts.

## Type column

The Type column shows **the plang type name straight from the catalog** — no second
naming scheme. Property types and the return type are already plang types on the
descriptor (`action/this.Schema.cs:44-66`): `path`, `text`, `bool`, `number`,
`list`, `item` (the base type — a bare `Data` return is `item`, not "object"),
`variable` (a variable *name*, not its value), `duration`, `dictionary`.

**Required** = non-nullable `data.@this<T>` with no `[Default]` (the generator emits a
missing-parameter guard for these), or an explicit `[IsNotNull]`. **Default** = the
`[Default(x)]` value, or `—` when required.

## Enriched `notes.md` — file module (ready to apply with the loader)

**Authoring contract (the generator is pure pass-through):** a line is
`Name — <prose> · say: <say> · builder: <builder>`. The generator strips only the
`Name — ` prefix and the ` · tag:` markers, then emits `<prose>` and `<say>`
**verbatim** — it adds no backticks and strips no punctuation. So:
- Backticks live in the source `say:` — a literal you type (`` `recursive` ``,
  `` `matching '<glob>'` ``) is backticked; a descriptive phrase ("the folder,
  inline") is not.
- Property `<prose>` is a short label with **no** trailing period.
- A `Returns —` line is a full sentence and keeps its terminal period.

`file/list.notes.md`
```
Path — the folder to list · say: the folder, inline
Pattern — which files come back, as a glob · say: `matching '<glob>'` · builder: only when the step names one
Recursive — whether sub-folders are searched too · say: `recursive` · builder: true only when the step says to include sub-folders
Returns — a list of `path` values.
```

`file/read.notes.md`
```
Path — the file to read · say: the path, inline
Template — fill in %variables% inside the file's text before returning · say: `load vars` · builder: true only when the step asks for the file's %variables% to be filled in
Returns — the file's content. A JSON file is navigable; it is parsed when first navigated.
```

`file/save.notes.md`
```
Path — the file to write · say: `to file '<path>'`
Value — what to write · say: the content, inline
Returns — the path that was written.
```

`file/exists.notes.md`
```
Path — the file or folder to check for · say: `check if '<path>' exists`
Returns — the path itself; whether it exists is the value's truthiness, so `if %x% is true` probes it (a filesystem stat, or an HTTP HEAD for a URL) at the moment you test it.
```

`file/copy.notes.md`
```
Source — the file or folder to copy from · say: the first path, inline
Destination — where the copy goes · say: `to '<path>'`
Overwrite — replace the destination if it already exists · say: `overwrite`
Subfolder — when copying a folder, copy its sub-folders too · say: (on by default) · builder: false only when the step says to copy the top folder only
Returns — the destination path.
```

`file/move.notes.md`
```
Source — the file or folder to move from · say: the first path, inline
Destination — where it moves to (this renames it) · say: `to '<path>'`
Overwrite — replace the destination if it already exists · say: `overwrite`
Returns — the destination path.
```

`file/delete.notes.md`
```
Path — the file or folder to delete · say: `file '<path>'`
Recursive — delete a folder's contents too · say: `recursive`
Returns — the deleted path.
```

Module-level learner prose that is not per-action (e.g. "a `Path` can be a URL")
belongs in `<module>/module.guide.md` — a **learner-only** file the builder never
reads (decision 443). It is reached as `module.Guide` and rendered right after
`module.description.md`, before the first action. (`module.notes.md` stays the
builder's module-level teaching, rendered only into Compile prompts.) When the
template renders `module.Guide`, the golden above grows to include the guide block
after the module description — spec golden, template, and `module.Guide` accessor
land together.

## Golden output — generated `docs/modules/file.md`

This is the exact literal the generator must produce for the `file` module —
actions in catalog order, `*.description.md` verbatim (no added periods, second
sentences kept), one line per source passage. The `ModulePageTests` render diffs
against this. (Sources per part: see the fact-ownership table above.)

```markdown
# File Module
Read, write, copy, move, delete, and list files through the configured filesystem abstraction. A `%!x.setting%` is a setting, never a file: `save %!llm.setting%` saves a setting, which is the setting module.

## delete
Delete a file or directory at Path, optionally recursively; nothing there is NotFound

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | `file '<path>'` | path | yes | — | the file or folder to delete |
| Recursive | `recursive` | bool | no | false | delete a folder's contents too |

**Returns:** the deleted path.

## copy
Copy a file or folder from Source to Destination, optionally overwriting and including subfolders

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Source | the first path, inline | path | yes | — | the file or folder to copy from |
| Destination | `to '<path>'` | path | yes | — | where the copy goes |
| Overwrite | `overwrite` | bool | no | false | replace the destination if it already exists |
| Subfolder | (on by default) | bool | no | true | when copying a folder, copy its sub-folders too |

**Returns:** the destination path.

## read
Read a file's content; optionally resolve %var% patterns in the text before returning

- read file.txt, write to %content%
- read 'config/settings.json'

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | the path, inline | path | yes | — | the file to read |
| Template | `load vars` | bool | no | false | fill in %variables% inside the file's text before returning |

**Returns:** the file's content. A JSON file is navigable; it is parsed when first navigated.

## exists
Check whether a file or directory exists at Path and return file info

- check if file.txt exists, write to %fileInfo%

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | `check if '<path>' exists` | path | yes | — | the file or folder to check for |

**Returns:** the path itself; whether it exists is the value's truthiness, so `if %x% is true` probes it (a filesystem stat, or an HTTP HEAD for a URL) at the moment you test it.

## save
Write Value to a file at Path, creating directories as needed. Saving a `%!x.setting%` setting (`save %!llm.setting%`) is not this: that is setting.save.

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | `to file '<path>'` | path | yes | — | the file to write |
| Value | the content, inline | item | yes | — | what to write |

**Returns:** the path that was written.

## list
List files in a directory matching an optional glob pattern, optionally recursing into subdirectories

- list files in docs/ recursive, write to %files%
- list files in %folder%

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | the folder, inline | path | yes | — | the folder to list |
| Pattern | `matching '<glob>'` | text | no | * | which files come back, as a glob |
| Recursive | `recursive` | bool | no | false | whether sub-folders are searched too |

**Returns:** a list of `path` values.

## move
Move or rename a file from Source to Destination, optionally overwriting the target

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Source | the first path, inline | path | yes | — | the file or folder to move from |
| Destination | `to '<path>'` | path | yes | — | where it moves to (this renames it) |
| Overwrite | `overwrite` | bool | no | false | replace the destination if it already exists |

**Returns:** the destination path.
```

## Generation rules

1. **Discover** modules/actions from the module registry (`app.Module`), skipping
   internal-only actions the builder catalog already hides.
2. **Intro** = `module.description.md` then `module.guide.md` (learner-only, reached
   as `module.Guide`) if present.
3. **Per action, in catalog order:** heading = action name; summary =
   `<action>.description.md`; examples = the `Step text:` lines of
   `<action>.examples.md` (drop the `Properties:` mapping lines — those are builder
   data); the parameter table from the handler's properties (`say:` from `notes.md`,
   "what it changes" = the notes prose, Type/Required/Default from attributes);
   `**Returns:**` from the `Returns —` notes line, or a default derived from the
   return type when absent.
4. **Property order** follows the handler declaration order.
5. **Orphans:** a `notes.md` property line with no matching handler property, or a
   handler property with no `notes.md` line, is a warning — **reuse the existing
   orphan scan** (`MarkdownTeaching.ScanOrphans` per CLAUDE.md), do not add a second.

## How it is built

Parallel to the compile teaching, which renders Fluid templates over the catalog
(`os/system/builder/llm/templates/properties.template` reads `action.Notes`
straight off `app.goal.step.action.@this`). The learner page is a **second Fluid
template over the same catalog**, rendered per module by a **plang goal** — not a
C# render pass. The template shows the learner fields (`name`, `prose`, `say`); the
existing compile template shows the builder fields (`name`, `prose`, `builder`).

## Hand-off

- **Docs (owned here):** this spec, the tagged `notes.md` format, the file-module
  enriched prose above, the golden sample. Docs verifies generator output against
  the golden sample.
- **Coder / architect:** parse each `notes.md` line into `{name, prose, say, builder}`;
  the learner Fluid template + the per-module plang goal that emits
  `docs/modules/<module>.md`; the learner fields on the compile side so the existing
  builder template shows `builder` (not raw). The tagged `notes.md` files land in the
  same change as the parse.

## Coordination

- **`file/delete.notes.md` overlaps a pending coder change.** `remove %x%` compiles
  to `file.delete` (destroys the file); the fix waits on an Ingi ruling about pick
  scoring, and the coder holds an uncommitted `delete.notes.md`. The enriched
  `delete.notes.md` here must be **merged** with that change, not landed separately.
- **`read` returns prose** ("a JSON file is navigable; parsed when first navigated",
  decisions 398/401) should be re-checked against the code when the generator runs.
