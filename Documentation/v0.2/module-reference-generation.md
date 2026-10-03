# Module Reference Generation

The learner-facing module pages (`system/modules/<module>/start.md`) are **generated** from
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
| Learner page (`system/modules/<module>/start.md`) | `name`, `prose`, `say` |
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
Variables — fill in the %variables% written inside the file's text before returning · say: load vars, fill in the variables, with variables · builder: true when the step says to load or fill the file's variables ("load vars", "fill in the variables", "with variables"); a plain read says none of these and leaves it false
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
land together. `module.guide.md` may also end with a `## Examples` section of
module-level worked programs (authored by whoever owns the learner programs); it
renders as part of the guide, so the golden includes it too.

## Golden output — file module

This is the exact literal the generator must produce for the `file` module —
actions in catalog order, `*.description.md` verbatim (no added periods, second
sentences kept), one line per source passage. The `ModulePageTests` render diffs
against this. (Sources per part: see the fact-ownership table above.)

```markdown
# File Module
Read, write, copy, move, delete, list, and check whether files exist through the configured filesystem abstraction. A `%!x.setting%` is a setting, never a file: `save %!llm.setting%` saves a setting, which is the setting module.

## Paths can be URLs

The `Path` on every action in this module is polymorphic. Anything that looks like a URL is routed to the matching scheme handler; a bare path or `file://` goes to the local filesystem.

- read 'config.json', write to %config%
- read 'https://api.example.com/users.json', write to %users%
- read %source%, write to %content%

The last one works whether `%source%` holds a local path or a URL — the program doesn't care which. The registered schemes are `file://` (and bare paths) and `http(s)://`:

| Action | `file://` (local) | `http(s)://` |
|--------|-------------------|-------------|
| read | open and read | GET |
| save | write to disk | POST (the server decides; 405 surfaces as `MethodNotAllowed`) |
| exists | stat | HEAD (2xx means it exists) |
| delete | remove | DELETE |
| list | directory entries | server-defined, usually unsupported |
| copy / move | filesystem copy / rename | read the source, then write the destination |

**Consent applies to any path.** The first time your program touches an HTTPS URL, the runtime asks the same way it does for a local file — `Allow worker to read https://api.example.com/users.json? (y/n/a)`; answering `a` remembers the grant. Grants are scoped per actor, path, and verb, so the same URL read by two actors is asked of each. For an HTTP URL the path is canonicalized first — scheme and host lowercased, the default port (80 for http, 443 for https) dropped, query parameters sorted by key — so `HTTPS://API.example.com/u.json?b=2&a=1` and `https://api.example.com/u.json?a=1&b=2` count as the same resource.

**Errors come back as data.** A non-2xx response is not an exception — it arrives the way a permission denial or a disk-full error does, and you handle it with `on error`. The status maps to an error key: 404 is `NotFound`, 405 is `MethodNotAllowed`, and a network failure is `NetworkError`. See the [http](http.md) module for the full mapping.

**When to use which.** `read %url%` is the shorthand for "GET this and give me the body." Reach for the [http](http.md) module (`- get %url%, write to %x%`) when you need to set the method, headers, or a request body — it exposes the full verb surface; this module is the one-liner.

## Examples

### Read, modify, save

```plang
Start
- read 'data.json', write to %data%
- set %data.processed% = true
- save %data% to file 'data.json'
- write out "processed: %data.processed%"
```

`data.json` was `{"name": "orders", "processed": false}`. Printed `processed: true`; the file is now `{"name":"orders","processed":true}`.

### Copy with backup

```plang
Start
- copy 'config.json' to 'config.backup.json', overwrite
- set %newConfig% = {"version": 2}
- save %newConfig% to file 'config.json'
- read 'config.backup.json', write to %old%
- write out "backup is version %old.version%, config is now version %newConfig.version%"
```

`config.json` was `{"version": 1}`. Printed `backup is version 1, config is now version 2`.

### List and process files

```plang
Start
- list files in 'inbox' matching "*.csv", write to %files%
- foreach %files%, call ProcessFile file=%item%

ProcessFile
- read %file%, write to %content%
- write out "Processing: %file%"
```

`inbox/` held `a.csv`, `b.csv` and `notes.txt`. Printed `Processing: inbox/a.csv`, `Processing: inbox/b.csv` — `notes.txt` is skipped, so you can see `matching "*.csv"` doing its work.

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
| Subfolder | (on by default) | choice<subfolder> | no | include | when copying a folder, copy its sub-folders too |

**Returns:** the destination path.

## read
Read a file's content; optionally resolve %var% patterns in the text before returning

- read file.txt, write to %content%
- read 'config/settings.json'
- read 'receipt.txt', load vars, write to %receipt%

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Path | the path, inline | path | yes | — | the file to read |
| Template | load vars, fill in the variables, with variables | choice<template> | no | — | the kind of template the file's text is: plang fills its %variables% from memory |

**Returns:** the file's content. A JSON file is navigable; it is parsed when first navigated.

### Reading as a template

With `load vars`, the read treats the file's text as a template: each `%variable%` in it is filled with that variable's current value. While the text still holds `%variables%` to fill, the read stays lazy — re-read and re-filled at each use, so it reflects the latest values; text with no variables left to fill is kept once opened.

Infrastructure variables are never filled. A `%!…%` name — engine internals like `%!app%`, `%!trace%`, `%!fileSystem%` — is left exactly as written, because a file's content may be untrusted; only the program's own `%variables%` resolve. To put an `%!…%` value into a string, build the string in `.goal` code rather than reading it from a file.

For example, a `greeting.txt` of:

```text
Hello, %name%!
Trace: %!trace.id%
```

read with `load vars`:

```plang
Start
- set %name% = "World"
- read 'greeting.txt', load vars, write to %greeting%
- write out %greeting%
```

prints:

```text
Hello, World!
Trace: %!trace.id%
```

`%name%` is filled; `%!trace.id%` is left exactly as written.

## exists
Check whether a file or directory exists at Path — what "if '<file>' exists" or "when the file is there" asks — returning the path, whose truthiness is its existence, to feed a condition

- check if file.txt exists, write to %fileInfo%
- if 'list.json' exists, write out "there"

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

## Golden output — condition module

```markdown
# Condition Module
Comparisons: ask whether something holds — equal, greater, contains, starts with, empty — and either branch on the answer (if/elseif/else) or keep it. Any yes/no question belongs here, whatever it is about: whether a list holds an item, whether text starts with something, whether a number is bigger. The subject's own module is not involved just because the question is about its contents. What follows the comparison is an ordinary action — call a goal, return from the goal (`if %done%, return`), write out, keep the answer in a variable — and is that action's own module.

## Branching: if, elseif, else

`if` runs what follows it when its condition is true. To choose among several cases, chain `elseif` (or `or if`) and `else` (or `otherwise`) after it — the first branch whose condition holds runs, and `else` runs when none do:

```plang
- if %total% > 20, write out "big", elseif %total% > 15, write out "medium", else write out "small"
```

For a `%total%` of 18 this prints `medium`. What follows each branch is that branch's body. To ask a yes/no question and keep the answer rather than branch on it, use [compare](#compare).

## Combining conditions with and / or

Join two conditions with `and` (both must hold) or `or` (either):

```plang
- if %a% > 1 and %b% < 10, write out "both hold"
- if %a% > 10 or %b% < 10, write out "one holds"
```

## Testing whether a file exists

A path is true when it exists, so a condition can test one directly — `if '<file>' exists`, or a bare `if %path%`. It stats a local file, or sends an HTTP HEAD for a URL, behind the same consent prompt as any path access; a denied prompt reads as false.

```plang
- if 'here.txt' exists, write out "here.txt is there"
```

A common shape is to check first, then branch — load a file when it is there, fall back to defaults when it is not:

```plang
- check if 'config.json' exists, write to %found%
- if %found%, call LoadConfig, else call UseDefaults
```

`check if … exists` writes the path to `%found%`; because a path is true when it exists, `if %found%` is the existence test.

## compare
Compare two values with an operator and write the boolean result to a variable

- compare %a% > %b%, write to %isGreater%
- check if %myList% contains 20, write to %has20%
- check if %name% starts with "plang", write to %isPlang%
- check if %content% is empty, write out "nothing here"

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Left | the value before the comparison | item | no | — | the first value compared |
| Operator | `>`, `is`, `contains`, `is empty`, … (as in condition.if) | choice<operator> | yes | — | the comparison, a choice<operator> |
| Right | the value after the comparison | item | no | — | the second value |

**Returns:** a `bool`.

## if
Evaluate a condition and execute the then-branch actions; pair with elseif/else for full branching

- if %count% > 0, call ProcessItems
- if %content% is not empty
- if %flag% is true, call Go
- if %done%, return %result%

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Left | the value right after `if` | item | yes | — | the value tested |
| Operator | `>`, `is`, `is not`, `contains`, `starts with`, `is empty`, `is in […]`, `is a <type>`, … | choice<operator> | no | — | the comparison, a choice<operator> |
| Right | the value after the operator | item | no | — | what Left is compared to |

**Returns:** a `bool`.

## elseif
Additional condition branch evaluated when the preceding if condition is false

- else if %a% > 5, write 'mid'

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Left | the value right after `else if` | item | yes | — | the value tested, the same as condition.if's |
| Operator | `>`, `is`, `contains`, `is empty`, … (as in if) | choice<operator> | no | — | the comparison, the same as condition.if's |
| Right | the value after the operator | item | no | — | what Left is compared to, the same as condition.if's |

**Returns:** a `bool`.

## else
Fallback branch that executes when all preceding if/elseif conditions are false

**Returns:** a `bool`.
```

## Golden output — loop module

```markdown
# Loop Module
Iterate over a collection, executing the remaining step actions once per item

## The work per item is its own action

`foreach` repeats the rest of the step for each element of a collection. What you do with each element is a separate action after it — usually a `call` to a goal:

```plang
- foreach %products% as %product%, call ShowProduct product=%product%
```

`foreach %products% as %product%` binds each element to `%product%`; `call ShowProduct product=%product%` is the per-item work, passing the element on. Without `as`, each element is `%item%`.

## Dicts: binding the key too

Over a dict, each value binds to the item; add `with key` to bind its key:

```plang
- foreach %person% as %value% with key %field%, call ShowEntry field=%field% value=%value%
```

## Counting and running totals

A goal called from the loop updates the caller's variables, so a running total works:

```plang
- set %count% = 0
- foreach %items%, call CountItem
- write out "Total: %count%"
```

With `CountItem` doing `set %count% = %count% + 1`, a three-item list prints `Total: 3`.

## Strings are atomic

A string is one value, not a sequence of characters: `foreach %greeting%` where `%greeting%` is `"hello"` runs once, with the whole string — not once per letter.

## foreach
Iterate over Collection, binding each element to Item (and its key or index to Key) and executing the remaining step actions

- foreach %items%, call ProcessItem item=%item%
- foreach %rows%, write out %item%
- foreach %products% as %product%, call Handle
- foreach %prices% as %price% with key %sku%, write out "%sku%: %price%"

| Property | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| Collection | `foreach %list%`, `for each %order% in %orders%` | item | yes | — | the list or dict to walk |
| Item | `as %product%` (else it is %item%) | variable | no | item | the variable each element is bound to |
| Key | `with key %sku%` | variable | no | — | the variable the key or index is bound to |
| Parallel |  | parallel | no | — |  |

**Returns:** a summary of the loop: `{count, complete}` — how many elements it ran over, and whether it finished (false if cancelled). The work per element is its own action, so there is usually nothing to write the summary to.
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
  `system/modules/<module>/start.md`; the learner fields on the compile side so the existing
  builder template shows `builder` (not raw). The tagged `notes.md` files land in the
  same change as the parse.

## Coordination

- **`file/delete.notes.md` overlaps a pending coder change.** `remove %x%` compiles
  to `file.delete` (destroys the file); the fix waits on an Ingi ruling about pick
  scoring, and the coder holds an uncommitted `delete.notes.md`. The enriched
  `delete.notes.md` here must be **merged** with that change, not landed separately.
- **`read` returns prose** ("a JSON file is navigable; parsed when first navigated",
  decisions 398/401) should be re-checked against the code when the generator runs.
