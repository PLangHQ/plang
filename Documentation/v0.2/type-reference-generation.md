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

## Golden output — size

The exact literal the generator must produce for the `size` type — a memberless scalar, so description + guide and no member table. `TypePageTests` diffs against this.

```markdown
# size
A count of bytes: a number and its unit, IEC (500 KiB, 95.4 MiB) or SI (512 kB, 100 MB).

# Sizes

A `size` is a count of bytes that writes itself with a unit. Its value is the exact number of bytes; the text rounds for reading.

## Two standards

A size's kind is the standard it is written in:

- `iec` — powers of 1024: `500 KiB`, `95.4 MiB`, `1 GiB`.
- `si` — powers of 1000: `512 kB`, `100 MB`, `1 GB`.

A size read from text keeps the standard its suffix names — `95.4 MiB` is IEC, `100 MB` is SI. A size made from a bare count — a file's length, a download's bytes — is written in the standard the setting names: `%!app.type.size.setting.standard%` (`iec` or `si`, `iec` by default).

## Comparing and testing

A size compares and sorts by its exact bytes, whatever unit each was written in, so `100 MB` and `95.4 MiB` order correctly against each other. A size of zero bytes is falsy, so `if %size%` asks "is there anything".
```

## Golden output — dict

The exact literal the generator must produce for the `dict` type. `TypePageTests` diffs against this.

```markdown
# dict
Named values: each key holds a value of any type.

# Dictionaries

A `dict` is a set of named values — each key holds a value of any type (text, number, a nested dict or list). You write one as JSON: `{"name":"Ada","age":36}`.

## Reading a value

Read a key with a dot: `%person.name%`, `%person.age%`. Keys nest, so `%order.customer.city%` walks in. A key that isn't a plain word reads with brackets: `%row["first name"]%`.

## Count and entries

- `%person.count%` — how many entries, a number. (A key literally named `count` wins over this.)
- `%person.entries%` — the entries in insertion order, to loop over:

```plang
Start
- foreach %person.entries%, call Show entry=%item%
```

## Building and changing

Build one inline, then read or change a key:

```plang
Start
- set %user% = {"name":"Ada","age":36}
- set %user.age% = 37
- write out "%user.name% is %user.age%"
```

## count
How many entries the dict has.

`%person.count%`

**Returns:** a number.
```

## Golden output — number

The exact literal the generator must produce for the `number` type. `TypePageTests` diffs against this.

```markdown
# number
A number, whole or decimal, of any size. Its kind is the precision it is held in (int, long, decimal, double, …).

# Numbers

A `number` is any numeric value — a count, a price, a measurement. Arithmetic uses the operators `+`, `-`, `*`, `/` across steps; the members below reshape a single number.

## Methods

Numbers' methods read with a dot and parentheses, and chain:

- `%delta.abs()%` — drop the sign.
- `%average.floor()%`, `%average.ceiling()%` — round down or up to a whole number.
- `%price.round(2)%` — round to a number of decimal places.
- `%area.sqrt()%` — the square root.
- `%price.min(100)%`, `%score.max(0)%` — the smaller or larger of two numbers.

## abs
The value with its sign removed.

`%delta.abs()%`

**Returns:** the absolute value, a number.

## floor
The value rounded down to a whole number.

`%average.floor()%`

**Returns:** a number.

## ceiling
The value rounded up to a whole number.

`%average.ceiling()%`

**Returns:** a number.

## sqrt
The square root of the value.

`%area.sqrt()%`

**Returns:** a number.

## round
The value rounded to a number of decimal places.

`%price.round(2)%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| decimals | the first argument, a number | number | yes | — | how many decimal places to keep |

**Returns:** the rounded number.

## min
The smaller of this value and another.

`%price.min(100)%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| b | the first argument | number | yes | — | the other number to compare against |

**Returns:** the smaller of the two, a number.

## max
The larger of this value and another.

`%score.max(0)%`

| Argument | How you say it | Type | Required | Default | What it changes |
|----------|----------------|------|----------|---------|-----------------|
| b | the first argument | number | yes | — | the other number to compare against |

**Returns:** the larger of the two, a number.
```

## Golden output — datetime

The exact literal the generator must produce for the `datetime` type. `TypePageTests` diffs against this.

```markdown
# datetime
A date and a time of day, with its offset from UTC.

# Dates and times

A `datetime` is a moment — a date, a time of day, and an offset from UTC. Its parts read with a dot.

## Parts

- `%when.year%`, `%when.month%`, `%when.day%` — the date, as numbers.
- `%when.hour%`, `%when.minute%`, `%when.second%`, `%when.millisecond%` — the time, as numbers.
- `%when.weekday%` — the day name (Monday, …), a text.
- `%when.ticks%` — 100-nanosecond ticks since year 1, a number.

## Whole parts

- `%when.date%` — just the date (a `date`); `%when.time%` — just the time of day (a `time`).
- `%when.offset%` — the offset from UTC (a `duration`).

## year
The year.

`%when.year%`

**Returns:** a number.

## month
The month, 1–12.

`%when.month%`

**Returns:** a number.

## day
The day of the month, 1–31.

`%when.day%`

**Returns:** a number.

## hour
The hour, 0–23.

`%when.hour%`

**Returns:** a number.

## minute
The minute, 0–59.

`%when.minute%`

**Returns:** a number.

## second
The second, 0–59.

`%when.second%`

**Returns:** a number.

## millisecond
The millisecond, 0–999.

`%when.millisecond%`

**Returns:** a number.

## ticks
The number of 100-nanosecond ticks since year 1.

`%when.ticks%`

**Returns:** a number.

## weekday
The day of the week by name (Monday, Tuesday, …).

`%when.weekday%`

**Returns:** the weekday name, a text.

## date
The date part, with no time of day.

`%when.date%`

**Returns:** a date.

## time
The time of day, with no date.

`%when.time%`

**Returns:** a time.

## offset
The offset from UTC.

`%when.offset%`

**Returns:** a duration.
```

## Golden output — duration

The exact literal the generator must produce for the `duration` type. `TypePageTests` diffs against this.

```markdown
# duration
A length of time: a number and its unit (200ms, 30s, 5m, 1h, 1d), or ISO 8601 (PT5M).

# Durations

A `duration` is a length of time — `"2s"`, `"500ms"`, `"1m30s"`, `"PT1H"`. Each member gives the whole span measured in one unit, as a number:

- `%elapsed.seconds%` — the span in seconds (`1m30s` is `90`).
- `%elapsed.minutes%` — in minutes (`1.5`); `%elapsed.hours%` — in hours; `%elapsed.days%` — in days; `%elapsed.milliseconds%` — in milliseconds.

These are totals, not parts: a `1m30s` span is `90` seconds and `1.5` minutes, not `30` seconds and `1` minute.

## days
The whole span measured in days.

`%elapsed.days%`

**Returns:** a number.

## hours
The whole span measured in hours.

`%elapsed.hours%`

**Returns:** a number.

## minutes
The whole span measured in minutes.

`%elapsed.minutes%`

**Returns:** a number.

## seconds
The whole span measured in seconds (1m30s is 90).

`%elapsed.seconds%`

**Returns:** a number.

## milliseconds
The whole span measured in milliseconds.

`%elapsed.milliseconds%`

**Returns:** a number.
```

## Golden output — path

The exact literal the generator must produce for the `path` type. `TypePageTests` diffs against this.

```markdown
# path
Where a file, a folder or a web resource is: a path in the app, an absolute path, or a URL.

# Paths

A `path` is where a file, a folder, or a web resource is — `'data.json'`, `'inbox/'`, or a URL like `'https://api.example.com/x.json'`. The same path works whether it points at disk or the web. Its members read with a dot.

## Parts

- `%file.name%` — the file name with extension (`data.json`); `%file.stem%` — without it (`data`); `%file.extension%` — the extension (`.json`).
- `%file.parent%` — the containing folder, a `path`; `%file.relative%` — the path relative to the app root.
- `%file.mime%` — the MIME type for the extension (`application/json`).

## Existence

- `%file.exists%` — whether something is there (a filesystem stat, or an HTTP HEAD for a URL).
- A path owns its truthiness as existence, so `if %file%` asks the same thing.

## relative
The path written relative to the app's root.

`%file.relative%`

**Returns:** a path.

## extension
The file extension, including the dot (.json).

`%file.extension%`

**Returns:** a text.

## name
The file name with its extension (data.json).

`%file.name%`

**Returns:** a text.

## stem
The file name without its extension (data).

`%file.stem%`

**Returns:** a text.

## mime
The MIME type for the path extension (application/json).

`%file.mime%`

**Returns:** a text.

## parent
The folder that contains it, as a path.

`%file.parent%`

**Returns:** a path.

## exists
Whether something is there at the path (a filesystem check, or an HTTP HEAD for a URL).

`%file.exists%`

**Returns:** true or false (a bool).
```

## Golden output — separator

The exact literal the generator must produce for the `separator` type. `TypePageTests` diffs against this.

```markdown
# separator
What cuts a text into pieces (`split %x% into lines`) or goes between them (`join %list% with comma`): a named separator — `line`, `comma`, `tab`, `space`, `semicolon` — standing for its characters, or any characters as written (`" | "`).

# Separators

A `separator` is what cuts a text into pieces or goes between them — what `split` and `join` use.

## Named separators

Five are named, each standing for its characters:

- `line` — a line break (also `lines`, `newline`).
- `comma` — `,`
- `tab` — a tab.
- `space` — a single space.
- `semicolon` — `;`

```plang
Start
- split %text% into lines, write to %lines%
- join %names% with comma, write to %csv%
```

## Any characters

Anything else is taken as the literal characters to use — `join %parts% with " | "`.

A text that is *exactly* a separator's name means that separator, whether written in the step or held in a variable (`%sep%` holding `comma` splits on `,`). To split or join on the word "comma" itself, write it some other way.
```

## Golden output — parallel

The exact literal the generator must produce for the `parallel` type. `TypePageTests` diffs against this.

```markdown
# parallel
Whether work runs side by side, and how many at once: "in parallel" or "in parallel(cpu: 2)"; left out means not parallel.

# Parallel

`parallel` says whether the items of a loop (or an LLM's tool calls) run side by side, and how many at once.

- `in parallel` — run them together, up to a sensible default (about the machine's cores).
- `in parallel(cpu: 2)` — run at most 2 at a time.
- leave it out — they run one after another.

A program written before `parallel` was a type may say `true` (parallel at the default) or `false` (not parallel); both still read.

A `foreach … in parallel` answers a task: the loop hands back a task you `wait for` to let the parallel work finish.

```plang
Start
- foreach %orders% in parallel(cpu: 4), call Ship order=%item%, write to %tasks%
- wait for %tasks%
```
```

## Golden output — secret

The exact literal the generator must produce for the `secret` type. `TypePageTests` diffs against this.

```markdown
# secret
Characters nobody should see — a key or password; shows as **** everywhere but plang's own store.

# Secrets

A `secret` is characters nobody should see — an API key, a password. You make one by asking secretly:

```plang
Start
- ask "Password?" secretly, write to %password%
```

It shows as `****` in every view — debug output, a trace, a snapshot, a channel — without you having to remember to hide it. Only plang's own settings store keeps it whole, and only code that must send it (a request's key) reads its characters.
```

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
