# kind-is-type

Traces to: the conversations with Ingi on 2026-10-14 and 2026-10-15, logged in `.bot/kind-is-type/architect/summary.md` (decisions 743–749, 768, 773, 774; and 711, 717 in `.bot/app-systems/architect/summary.md`). Chain: idea → conversation → summary → this plan → code (`start.goal` and the tests it runs).

## Why

Half of the plang suite's failures come from one root: **a kind isn't a type.** The builder's Type question offers only top-level types, so it can't offer `list<path>`, json, decimal or an iso duration. The builder's measurement on head (`.bot/kind-is-type/builder/baseline-kinds.md`):

| step | saved as today |
|---|---|
| `set %n% = "1.5" as decimal` | `number` 5/5 |
| `set %files% = [...] as list of path` | `list` 2/5, `path` 3/5 |
| `set %x% = '{"a":1}' as json` | `dict` 5/5 |
| `read 'notes.md' as text` | `{file, kind: md}` 5/5 |
| `set %d% = "PT30S" as iso duration` | `duration` 5/5 |

At run the same root loses a kind or can't check one (kind/strict ×4, DataTypeReadsEntity, BareLiteralJudgedByForm, Plane_DataKeyVsProperty, DoublePlusDecimal_Errors), and parsed json is a C# `JsonElement`, not a plang value, so `%cfg% is dict` answers false (Cut2, Narrow).

Ingi: "a strong type is information for the LLM": a slot typed `separator` tells the builder what goes there; a slot typed `text` doesn't. Today the information is in the code and the builder never sees it.

## What the developer gets

- `as json`, `as decimal`, `as iso duration`, `as list of path` keep what the step said, at build and at run.
- A json file is read only when it's used, and parses into a plang dict or list: `%cfg% is dict` is true, and `%cfg.db%` reads a plang value.
- Every type keeps the name it has today; nothing a developer has written or saved changes.
- A goal moved with its `.build/` folder runs where it is; an app's goal can never pass as one of plang's own.

## The model (Ingi, 2026-10-15, decision 774)

1. **A kind is a type:** `kind<T> : kind : type`, where T is the type it sits under (`decimal : kind<number>`). What makes a kind different lives on the kind class: it registers under T, never at the top. "What is a kind?" has an answer: `%!app.type["number"].kind.list%`.
2. **Names on two levels, like files in folders.** A top-level type's name is unique in the app: its `[PlangType]` word, else its namespace, as today; two classes claiming one name are refused at load. A kind's name is unique only under its parent, and a kind is found only through its parent (`%!app.type["number"].kind["decimal"]%`); there is no app-wide search for a kind by name. MIME and extension lookups (`.md`, `application/json`) stay app-wide, each owned by one kind.
3. **A kind is its parent except where it says otherwise.** A value's own operations (add, round, split) stay on its value class: a decimal is a number value, an md file a text value. On the type, a kind answers what is its own (how "1.5" reads, its MIME); every other question it asks its parent, written once in `kind<T>`. Ingi: "can kind use the parent type implementation of a method?", "yes".
4. **The written form stays `{name, kind}`** (Ingi: "yes 1.1"): `{number, kind: decimal}`, `{item, kind: json}`, `{list, kind: path}`; it nests, `{list, kind: {list, kind: path}}`. `kind` is one of the type's kinds, or for list and dict what they hold (any type: each parent answers what it accepts). `%n!type.name%` is `number`, `%n!type.kind.name%` is `decimal`.
5. **Which kinds:** formats, each under the type its content becomes (773; Ingi: "json is not text"): item's json (it holds any value), text's md and xml, table's csv, image's png and jpg; sub-sorts: number's int, long, double, decimal; path's file and http; duration's iso, short, dotnet; size's si, iec; template's plang. A list's or dict's element is any type.
6. **Named values aren't kinds.** A type carries its own list of named values: closed (choice's sets, bool's true and false) or open (separator's line, comma, tab, space, semicolon, while any characters are allowed). A closed set is a C# enum, which can't inherit, so it stays choice's own set: one generic set class, a `kind<choice>`, holding the enum. separator is a `kind<text>` with its named values ("a strong type is information for the LLM").
7. **Settings are settings, not kinds.** Each settings class is its own top-level type, named like any type without a word, by its namespace (`app.module.llm.setting`); a class declared inside another reads through its owner (`app.module.llm.decider.setting`). Its address stays what a program writes and the builder teaches (`%!llm.decider.setting%`). A saved row is read into its class by its key (`<actor>!<path>`), which already says which class; old rows read as they are.
8. **A format owns its work:** its MIME, its extension, reading and writing its bytes (Ingi: "it own it self then, it will know how to (de)parse it self"), and it holds its raw bytes until first use (Ingi: "only read/parsed on first usage, until then it stays bytes"); passing it on writes the bytes unread. So json stays its raw bytes until read into (decision 401 changed by 773).
9. **The C# navigators go where they only served json.** Parsed json becomes a plang dict or list (717), so item's json navigator and its own count/first/last go. The C# host kinds (`kind/reflection`, `kind/dict`, `kind/list` over a C# object) stay until 673.
10. **The builder offers each type with its kinds under it** ("number: int, long, double, decimal"), and a list asks "a list of what?" when the step names one. It writes `{number, kind: decimal}`; it never needs `decimal` as an app-wide name.
11. **A goal's path is where its `.pr` is found** (745, landed).

## Stages

Each stage ends with its tests built and run; each test goes red without its change. Each stage reads: why; the plang a developer writes, with `→` what happens; what goes away. Each stage's shape comes to the architect before it's built.

### 0. A goal's path is where its `.pr` is found (landed, fcaf1055d)

```
myapp/Start.goal + .build/start.pr, moved to myapp2/
- (run myapp2's Start)                 → it runs as /Start.goal in myapp2, and is found held on its second load
- read 'notes.txt'                     → myapp2/notes.txt, not the old folder's
myapp/.build/start.pr saying "path": "/system/Start.goal"
- start program 'sh'                   → asked: its folder is the app's, so no os trust
```

### 1. The probe: number's kinds are `kind<number>` (rulings 1–4)

One family first, to find what the change really costs before every family takes it: today's kind class has verbs that clash with item's (Set, Output, Read, Convert, Clr, IsSequence, Names), and with `kind : type : item` those clash directly.

```
- variable.set(Name=%n%, Value="1.5", Type={name:"number", kind:"decimal"})   → %n!type.kind.name% is "decimal", %n!type.name% is "number"
- set %m% = %n% + 1                    → number's add; the kind didn't need one
- %!app.type["number"].kind.list%      → int, long, double, decimal, …
```

Reported to the architect before stage 2: each clash and how it was settled; each question decimal answers itself and each it passes to number; that number never asks its kind back.

### 2. Settings are settings (ruling 7)

```
- %!llm.setting!type.name%             → app.module.llm.setting, a type of its own
- %!llm.decider.setting%               → its own class, app.module.llm.decider.setting; no clash with llm's
- (a row saved before this stage)      → still reads, its class taken from its key
- set %!llm.setting.cache% = …         → saved and read back as before
```

Goes: the `setting` family (`type/item/setting/kind/`, `Registry.cs:330–334`); a row's kind as the thing that picks its class.

### 3. Every family's kinds are types (rulings 1–6)

```
- {path, kind: file}, {duration, kind: iso}, {text, kind: md}, {image, kind: png}, {table, kind: csv}, {item, kind: json}
- set %x% = '{"a":1}' as json (formal) → %x!type% is {item, kind: json}; %x% is text is false
- set %files% = ["a.txt"] as list of path (formal)   → %files[0]% is born a path
- {list, kind: {list, kind: path}}     → a kind nests
- two types that each have a kind named the same   → each found through its own parent, no clash
- split %x% by ","                     → "comma", one of separator's named values; "with ' | '" keeps any characters
- set %img% = %bytes% as image/gif, strict   → refused when the bytes aren't a gif
```

Goes: each family's codec kinds as classes beside their type (they become `kind<T>`); the app-wide kind search; `[Kinds]` on parents; `type.format`.

### 4. A format owns its bytes (rulings 8–9)

```
- read 'config.json', write to %cfg%   → {item, kind: json}: the file's bytes, unread
- write %cfg% to 'copy.json'           → the bytes pass through, byte for byte, still unread
- write out %cfg.db%                   → json parses itself: "plang"
- if %cfg% is dict, …                  → true after the read: parsed json is a plang dict
- read 'list.json', write to %l%       → after the first read into it, %l% is a plang list
- read 'report.csv', write to %r%      → the same through csv: a table at first use
- (an action whose dict slot is given %cfg%, unread)   → still unread when the action answers; the first read goes through json (decision 758)
```

Goes: item's json navigator (walking a `JsonElement`, its own count, first and last); text's json wrapper kind.

### 5. The builder offers kinds (ruling 10)

```
- set %n% = "1.5" as decimal           → Type={number, kind: decimal}          (today: number 5/5)
- set %files% = [...] as list of path  → Type=list, "a list of what?" → path: {list, kind: path}   (today: list 2/5, path 3/5)
- set %x% = '{"a":1}' as json          → Type={item, kind: json}               (today: dict 5/5)
- read 'notes.md' as text              → Type=text                             (today: {file, kind: md} 5/5)
- set %d% = "PT30S" as iso duration    → Type={duration, kind: iso}            (today: duration 5/5)
- set %n% = 5                          → no Type, as before (guard)
- set %s% = "hi" as text               → text, as before (guard)
```

Measured by the builder: 5 builds each, fresh, `"cache":"skip"`, counted with jq. The teaching-edit rule applies.

### 6. The leftovers go

The demolition list below, each checked by grep.

## Demolition (what must not survive)

| goes | where | when |
|---|---|---|
| a goal's stored `"path"` and `"prPath"` | `goal/this.Item.cs:148–149`, `goal/this.cs:84–89` | 0 (done) |
| number's kinds as codec classes | `type/item/number/kind/*` become `kind<number>` | 1 |
| the `setting` family; a saved row's kind as its class picker | `type/item/setting/kind/`, `Registry.cs:330–334` | 2 |
| every other family's codec kinds | duration, size, template, separator, path's schemes, text's, image's, table's, item's json: each becomes `kind<T>` | 3 |
| the app-wide search for a kind by name | `type/list/this.cs:51` (the fallback), `:57–69` (`Held`) | 3 |
| `[Kinds]` on a parent and the walk up base classes that finds a family | `type/item/this.cs:528–533` and each `[Kinds]` | 3 |
| `type.format`, a filter read only by its own tests | `type/this.cs:117`, `type/format/this.cs`; `FormatKindTests` asks the kinds | 3 |
| item's json navigator | `type/item/kind/json/this.cs` (`:64–84`, `:89–91`) | 4 |
| text's wrapper kind for characters in another format | `type/item/text/kind/format/this.cs` | 4 |
| "is this an action slot?" asked by name | `goal/step/action/this.Validate.cs:54–55`, `formal/reader.cs:221` and `:349`, `action/serializer/Reader.cs:153–154` (F8.1) | 6 |
| `ToString` writing `name<kind>` only for list, dict and choice | `type/this.cs:547` (F4.2) | 6 |
| the stopgap where `%x!type.kind%` answers a clr carrier or its name | 80e4c9e4e | 6 |

**Stays:** every type's name and its `[PlangType]` word; the written form `{name, kind}`; the load-time refusal of two classes claiming one name; every `.pr`, prompt and saved row as it is; the C# host kinds until 673.

## Changed from the first version of this plan (2026-10-15)

The first version renamed every type by its namespace path first (decision 752), to rule out name clashes before kinds became types. It cost a rename of every type name the builder, the `.pr` files, the prompts and the saved rows carry, and it isn't needed: with `{name, kind}`, a kind is found under its parent, so its name only has to be unique there (Ingi: "I feel like a kind cannot be restricted to unique name over the whole codebase only at it's level of type"). Dropped: 752 and what followed from it (762, 769, 770, 772). Postponed: a module's main type as its module folder's class (761). Kept: `error` as `app/error/this.cs`, a goal's name never climbs (767), and table, code and clr under `app/type/item/` (773; their words keep their names).

## What tests can't show (checks the coder runs and reports)

- **Stage 1:** the probe's report (above), before stage 2.
- **Stage 2:** every saved row in `test/.data` and the os store reads after the change; the surface golden moves only where settings change.
- **Stage 3:** every kind found is one of: a `kind<T>`, a named value, or a C# host kind kept for 673; none left unclassified (application/plang's `wire` kind included). No type's name changed (the surface golden and the decider pins name the same types).
- **Stage 5:** the eval by name: each baseline sentence 5/5, no golden case regresses.
- **Every stage:** the gate by name; PLNG001/002 at 0; the plang suite no worse than 249/29/12/3 except the tests this stage fixes.

## OBP validation

| new surface | check | result |
|---|---|---|
| `kind<T> : kind : type` | what is different about a kind lives on the kind class (it registers under T); no `is kind` check outside it | to verify at stage 1 |
| a kind falls back to its parent | one place passes a question up (`kind<T>`); a kind overrides only what is its own; the parent never asks its kind back | to verify at stage 1 |
| names on two levels | a kind is asked of its parent, never of the app; no second lookup by kind name | to verify at stage 3 |
| a settings row read by its key | one fact names the class (the key); the row doesn't store it a second time | to verify at stage 2 |
| a format's raw bytes | the value owns its bytes and its parse; no reader outside it parses for it | to verify at stage 4 |
| a generic's element question | the type answers what it accepts; no `is list` in the builder | to verify at stage 5 |
| a goal's path from where it's found | one location fact | by construction |

The coder owns the code shape; the sketches in the summary are intent.
