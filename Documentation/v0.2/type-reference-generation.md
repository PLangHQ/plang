# Type Reference Generation

The learner-facing type pages (`os/system/type/<type>/start.md`) are **generated** from
the same source the builder already uses — never hand-written. Every fact on a page has
exactly one home, so a page can never disagree with the code.

This is the sibling of [module-reference-generation.md](module-reference-generation.md):
a type is a module, a member is an action, a method's arguments are an action's
properties. Read that spec first — this one states only what differs for types.

## Why generated

Same reason as modules: a hand-written table of a type's members drifts from the type's
C# the moment a method is renamed or an argument added. A page rendered from the catalog
cannot drift from the catalog.

## One home per fact

| Fact on the page | Single source |
|---|---|
| Type intro | `<type>/type.description.md` + optional `<type>/type.guide.md` (learner-only) |
| Member summary line | the member-name line in `<type>/<member>.notes.md` |
| Member call form (how you type it) | the `say:` tag of that member-name line |
| Argument "what it changes" / "how you say it" | the per-argument lines in `<member>.notes.md` |
| Argument name + type, member return type | the catalog (`app.type.property.@this`: `.Name`, `.Arguments[].Name`/`.Type`, `.Type`) |
| Argument Required / Default | the catalog argument (`.Required`, `.HasDefault`/`.Default`) |
| Returns (meaning) | a `Returns —` line in `<member>.notes.md` |
| Returns (type) | the member's `.Type.Name` |

The generator reads this off the type element `app.type.@this`, which exposes
`.Description` / `.Example` / `.Guide` / `.Notes` (lazy prose/file items,
`PLang/app/type/this.cs:649-666`) and `.Property` — its members, each an
`app.type.property.@this` (`PLang/app/type/property/this.cs`) carrying `.Name`, `.Type`
(the return type entity; read `.Type.Name`), `.Arguments` (null for a property; a list of
`property` for a method, each with `.Name`/`.Type`), `.Required`, `.HasDefault`/`.Default`,
and `.Notes` (the `<member>.notes.md` file item).

## The `.` / `!` read rule is type-level, not per member

How a member is read follows from the type, not from a per-member mark (decision 572):

- A **plain** type — `text`, `path`, `list`, `dict`, `number`, `datetime`, `duration`,
  `size` — reads **every** member with a dot: `%x.length%`, `%p.relative%`,
  `%name.replace("a","b")%`.
- A **reference** type — only `file` and `url` — reads *content* with `.` and its own
  facts with `!`: `%config!path%`, `%url!host%`. Data's own facts (`!type`) are `!`. They
  chain: `%config!path.relative%`.

The member layer carries no read mark; the page's `say:` forms spell out the dot (or `!`)
for each member, so the rule reaches the reader through the examples, not a column.

## Property vs. no-argument method is carried by `say:`, not the catalog

A method whose only C# parameter is the asker's `context` is listed by the catalog with
**no** arguments (`PLang/app/type/this.cs:776-777`) — identical to a real property. So the
catalog cannot tell `toUpper()` (a no-argument method, written with parentheses) from
`length` (a property, written without). The distinction lives in the member note's `say:`
form — `say: %text.toUpper()%` vs `say: %text.length%` — which the page renders verbatim.
**The generator never synthesises a call form; it prints the `say:` the note carries.**

## Source format: `<member>.notes.md`, one line per fact, tagged

Same tagged-line grammar as an action's `notes.md` (`Name — prose · say: … · builder: …`),
with one addition: the member's **own name** is a valid line name, carrying the member's
one-line summary and its call form.

```
replace — The text with every occurrence of one substring swapped for another. · say: %text.replace("old", "new")%
old — the substring to find · say: the first argument
new — what to put in its place · say: the second argument
Returns — the text with every match replaced.
```

- The line named for the member (`replace`) → the `## replace` section's summary + call form.
- Each line named for an argument (`old`, `new`) → a row of the argument table.
- The `Returns —` line → the `**Returns:**` meaning.

A property or a no-argument method has just its own line and (optionally) a `Returns —`
line; no argument lines.

### Orphan warnings

As for actions, a named line that matches nothing is a warning — reuse the action note
parser (`PLang/app/goal/step/action/note/this.cs`), generalised so a **member** parses its
`<member>.notes.md` against its `.Arguments`. The allowed non-argument line names are
**`Returns`** and **the member's own name**; any other line naming no argument, or any
argument with no line, is a warning. (See *Coder hand-off* for the one C# change this needs.)

## Page shape

```
# <type>
<type.description.md>

<type.guide.md, if present>

## <member.Name>
<the member-name line's prose>

`<the member-name line's say form>`

| Argument | How you say it | Type | Required | Default | What it changes |   ← methods with arguments only
|----------|----------------|------|----------|---------|-----------------|
| <arg.Name> | <arg line's say> | <arg.Type.Name> | yes/no | <default or —> | <arg line's prose> |

**Returns:** <Returns line's prose, else `a \`<member.Type.Name>\`.`>
```

Members come in catalog order; arguments in the method's declaration order.

## Golden output — text type

The expected `os/system/type/text/start.md`, rendered from `os/system/type/text/*.md` over
the catalog. Byte-exact.

### Golden output — text

```markdown
# text
Textual content. Kind is set from the file extension (md, txt, csv, html, ...). Kind is a hint by default; strict is a no-op for text (plain vs markdown is not detectable from content).

# Working with text

`text` is any textual content — a line you typed, a file you read, the body of a response. Its kind (plain, markdown, csv, html, …) comes from the file extension and is only a hint; it doesn't change how you work with the text.

## Reading its members

Every member of `text` is read with a dot, inside `%…%`:

- `%name.length%` — how many characters it holds (a number).
- `%name.toUpper()%`, `%name.toLower()%`, `%name.trim()%` — the text, transformed.

A method is the type's C# method by name: you write it case-insensitively, with parentheses, and you can chain methods. `%title.trim().toUpper()%` trims both ends and upper-cases the result. `length` is the one property — it reads without parentheses (`%name.length%`); everything else is a method and keeps its `()`.

## Arguments

Methods take their arguments positionally — text in quotes, numbers bare:

- `%greeting.replace("world", "there")%` — swap one substring for another.
- `%summary.maxLength(80)%` — cut to at most 80 characters (`"..."` marks the cut).

## Searching lines

`grep` and `grepCount` treat the text as lines:

- `%log.grep("ERROR")%` — the matching lines, each as `line number: line`, returned as a `list<text>`.
- `%log.grep("ERROR", 2)%` — the same, keeping 2 lines of context around each match.
- `%log.grepCount("ERROR")%` — just the count, a number.

Because `grep` returns a `list<text>`, you can loop over it:

```plang
Start
- read 'app.log', write to %log%
- foreach %log.grep("ERROR")%, call ReportLine line=%item%
```

## length
How many characters the text holds.

`%text.length%`

**Returns:** a number.

## toUpper
The same text with every letter upper-cased.

`%text.toUpper()%`

**Returns:** the upper-cased text.

## toLower
The same text with every letter lower-cased.

`%text.toLower()%`

**Returns:** the lower-cased text.

## trim
The text with whitespace trimmed from both ends.

`%text.trim()%`

**Returns:** the trimmed text.

## replace
The text with every occurrence of one substring swapped for another.

`%text.replace("old", "new")%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| old | the first argument | text | yes | — | the substring to find |
| new | the second argument | text | yes | — | what to put in its place |

**Returns:** the text with every match replaced.

## maxLength
The text cut to at most max characters, with "..." marking the cut.

`%text.maxLength(80)%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| max | the first argument, a number | number | yes | — | the longest the result may be; 0 means no limit |

**Returns:** the text, no longer than max characters (plus "..." when it was cut).

## grep
The lines that match a pattern, each returned as "line number: line", with context lines around each match when asked.

`%text.grep("TODO")%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| pattern | the first argument | text | yes | — | what each line is matched against |
| lines | the second argument, a number (leave it out for none) | number | no | — | how many lines of context to keep around each match; 0 (none) by default |

**Returns:** a list of the matching lines (list<text>).

## grepCount
How many lines match a pattern.

`%text.grepCount("TODO")%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| pattern | the first argument | text | yes | — | what each line is matched against |

**Returns:** a number: the count of matching lines.
```

The golden is fenced as ` ```markdown ` with the guide's inner ` ```plang ` fence kept
real; `TypePageTests` extracts it with the same depth-aware reader `ModulePageTests` uses
(an info-string fence opens, a bare ` ``` ` closes).

## Generation rules

1. **Discover** types from `%!app.type.list%`; skip internal-only types the builder catalog
   already hides. (`text` only, until each type's members are tagged.)
2. **Intro** = `type.description.md`, then `type.guide.md` (learner-only, `type.Guide`) if present.
3. **Per member, in catalog order:** heading = `member.Name`; summary + call form = the
   member-name note line's prose + `say:`; argument table (methods with arguments only) from
   `member.Arguments` (name/type/required/default from the catalog, `say:` and "what it
   changes" from the argument note lines); `**Returns:**` from the `Returns —` note line,
   else `a \`<member.Type.Name>\`.`.
4. **Argument order** follows the method's declaration order.
5. **Orphans:** a note line naming neither `Returns`, the member, nor an argument — or an
   argument with no note line — is a warning (the generalised member note parser; rule above).

## How it is built

A second Fluid template (`docs/templates/type.template`) over the catalog, rendered per
type by a plang goal (`docs/Types.goal`), exactly as `docs/Modules.goal` +
`docs/templates/module.template` write the module pages. The goal saves to
`/system/type/<type>/start.md`, which lands in the os tree by decision 522 (a new
`/system/…` file falls through to `os/system/` when `docs/` has no such folder). Build with
`plang build '--build={"files":"Types.goal","cache":"skip"}'` from `docs/`.

A C# `TypePageTests` (TUnit, mirroring `ModulePageTests`) renders the template in-memory
over the catalog and diffs byte-exact against the `### Golden output — text` block.

## Coder hand-off

One C# change: **a type member exposes a parsed `.Note`** (`Line` / `Returns` / `Warning`)
the way an action does. The action parser (`PLang/app/goal/step/action/note/this.cs`) is the
model — generalise it so it can parse against a **member's `.Arguments`** instead of an
action's `.Property`, and treat the member's own name (alongside `Returns`) as an allowed
non-argument line. Everything else — the type teaching loader, `type.guide.md` / per-member
`<member>.notes.md` as lazy file items — already landed.

Everything above the hand-off (this spec, the tagged notes, `type.guide.md`, the golden,
the template, `Types.goal`) is docs'.
