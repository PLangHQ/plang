# kind-is-type

Traces to: the conversation with Ingi on 2026-10-14, logged in `.bot/kind-is-type/architect/summary.md` as decisions 743–749 (and 711, 717 in `.bot/app-systems/architect/summary.md`). Chain: idea → conversation → summary (this file) → code (`start.goal` and the tests it runs).

## Why

Half of the plang suite's failures come from one root: **a kind isn't a type.** The builder's Type question offers only top-level types, so it can't offer `list<path>`, json, decimal or an iso duration. The builder's measurement on head (`.bot/kind-is-type/builder/baseline-kinds.md`):

| step | saved as today |
|---|---|
| `set %n% = "1.5" as decimal` | `number` 5/5 |
| `set %files% = [...] as list of path` | `list` 2/5, `path` 3/5 |
| `set %x% = '{"a":1}' as json` | `dict` 5/5 |
| `read 'notes.md' as text` | `{file, kind: md}` 5/5 |
| `set %d% = "PT30S" as iso duration` | `duration` 5/5 |

At run the same root loses a kind or can't check one (kind/strict ×4, DataTypeReadsEntity, BareLiteralJudgedByForm, Plane_DataKeyVsProperty, DoublePlusDecimal_Errors), and parsed json is a C# `JsonElement`, not a plang value, so `%cfg% is dict` answers false (Cut2, Narrow). That's 10 of the 16 failing tests and 1 unbuilt one.

Ingi: "a strong type is information for the LLM": a slot typed `separator` tells the builder what goes there; a slot typed `text` doesn't. Today the information is in the code and the builder never sees it.

## What the developer gets

- `as json`, `as decimal`, `as iso duration`, `as list of path` keep what the step said, at build and at run.
- A json file is read only when it's used, and parses into a plang dict or list: `%cfg% is dict` is true, and `%cfg.db%` reads a plang value.
- A type is found by its name the way a path is: `text`, `llm.setting`, one segment more where two types share a last word.
- A goal moved with its `.build/` folder runs where it is; an app's goal can never pass as one of plang's own.

## The rulings (Ingi, 2026-10-14)

1. **A kind is a type.** A type's kind is another type: `{list, kind: path}` (the element), `{text, kind: json}` (a format), `{number, kind: decimal}` (a sort). It nests: `list<list<path>>`. The written form stays `{name, kind}` (Ingi: "why would you suggest changing the form?"); what changes is that `kind` names a type, not a codec.
2. **Which kinds become types:** formats (text's json, md, csv; image's png; duration's short, iso, dotnet; size's iec, si; template's plang), sub-sorts (number's int, long, double, decimal; path's file, http) and generic elements. (747, 748.)
3. **Named values aren't kinds.** A type carries its own list of named values: closed (choice, bool's true and false) or open (separator's line, comma, tab, space, semicolon, while any characters are allowed). separator stays a type under text, because the names are information for the LLM ("do we want to strong type it so llm gets info").
4. **The C# navigators go where they only served json.** Parsed json becomes a plang dict or list (717), so item's json kind (`type/item/kind/json/`) and its own copy of count/first/last go. The C# host kinds for reflection (`kind/reflection`, `kind/dict`, `kind/list` over a C# object) stay until "avoid reflection; one door to a type's members" (673, the backlog).
5. **A format's work belongs to its type:** its mime, its extension, reading and writing its bytes (Ingi: "it own it self then, it will know how to (de)parse it self").
6. **A format value holds its raw bytes until first use** (Ingi: "only read/parsed on first usage, until then it stays bytes"). Passing it on writes the bytes unread.
7. **A type is named by its namespace, read from the root it lives under** (Ingi: "yes, name types by namespace", then "why isn't list = app.type.item.list, and app.goal.list=app.goal.list", "yes"; decision 752). Like a file path, no search for a unique tail, so no two types can share a name: plang's own types drop `app.type.item.` (`list`, `text`, `path`), a module's drop `app.module.` and their `type.` folder (`window.window`, `screen.window`, `llm.setting`), everything else drops `app.` (`goal.list`, `type.list`). The `[PlangType("…")]` words go; each settings class is its own type (the `setting` family of decision 48 existed only to dodge the name clash).
8. **The builder offers kinded types by name, and a generic asks for its element** (Ingi: "yes, that is the shape"). The Type question lists every type by its short name, kinds included; a list asks "a list of what?" when the step names one.
9. **A goal's path is where its `.pr` is found** (745). The `.pr` stores only the goal file's name (`"file"`); `"prPath"` goes; an old `.pr`'s name is read from the end of its `"path"`. An app's goal never carries `/system/` (Ingi: "it should NOT have system in it").

## Stages

Each stage ends with its tests built and run; each test goes red without its change. Each stage reads: why, in a sentence or two; the plang a developer writes, with `→` what happens; what goes away. (The shape is the coder's, from its 2026-09-28 "a timer is a value" proposal; Ingi: "I think that would help with the plan we are giving out".)

### 0. A goal's path is where its `.pr` is found (ruling 9; landed, fcaf1055d)

A `.pr` stored its own path, and plang believed it: a moved goal ran its old path, and an app's `.pr` could claim to be one of plang's own.

```
myapp/Start.goal + .build/start.pr, moved to myapp2/
- (run myapp2's Start)                 → it runs as /Start.goal in myapp2, and is found held on its second load
- read 'notes.txt'                     → myapp2/notes.txt, not the old folder's
myapp/.build/start.pr saying "path": "/system/Start.goal"
- start program 'sh'                   → asked: its folder is the app's, so no os trust
/system/error/Show.goal (plang's own)  → still /system/error/Show.goal, and trusted
```

Goes: the stored `"path"` and `"prPath"`; an old `.pr`'s name is read from the end of its `"path"`.

### 1. A type is named by its path from a root (ruling 7)

A type's name is its namespace, read from the root it lives under, so no two types can share a name and the settings classes need no family of their own.

```
- set %s% = "hi" as text               → text, plang's own (app.type.item.text), as before
- set %l% = [1, 2] as list             → list, plang's own, though goal.list, type.list, … also end in list
- write out %!app.type["llm.setting"].name%     → llm.setting: the llm settings class is a type of its own
- write out %!app.type["window.window"].name%   → window.window; and screen.window finds the screen's
- write out %!app.type["window"]%      → nothing: a module's type is named by its module
- save %!llm.setting%                  → saved and read back as before
```

Goes: the `[PlangType("…")]` words; the `setting` family as a kind (decision 48).

### 2. A kind is a type (rulings 1–3)

A type's kind is another type, written `{name, kind}` as today. The builder and the run can then name and keep `decimal`, `json`, `list<path>`.

```
- set %n% = "1.5" as decimal           → %n!type% is {number, kind: decimal}; decimal is a type under number
- if %x% is text, …                    → true for a json value: json is under text
- set %files% = ["a.txt"] as list of path   → %files[0]% is born a path, at its first read
- set %grid% = [[...]] as list of list of path   → a kind nests
- split %x% by ","                     → Separator is "comma", one of separator's named values; "with ' | '" keeps any characters
- set %img% = %bytes% as image/gif, strict  → refused when the bytes aren't a gif
```

Goes: each family's kind classes as codecs beside their type (number, duration, size, template, separator, bool, path's schemes), as the coder's trace finds them.

### 3. A format type owns its work (rulings 4–6)

A format type keeps its raw bytes until first use, then parses itself into plang values. No C# object travels in their place.

```
- read 'config.json', write to %cfg%   → %cfg% holds the file's bytes, unread
- write %cfg% to 'copy.json'           → the bytes pass through, byte for byte, still unread
- write out %cfg.db%                   → the json type parses itself: "plang"
- if %cfg% is dict, …                  → true: parsed json is a plang dict
- read 'list.json', write to %l%       → after the first read into it, %l% is a plang list
- read 'report.csv', write to %r%      → the same through the csv type: a table at first use
- (an action whose dict slot is given %cfg%, unread)   → still unread when the action answers; the first read goes through the json type (today the made value loses json and can't read: decision 758)
```

Goes: item's json navigator (walking a `JsonElement`, its own count, first and last).

### 4. The builder offers kinded types (ruling 8)

The Type question lists every type by its name, kinds included, and a generic asks for its element. The baseline's five wrong rows build what the step says.

```
- set %n% = "1.5" as decimal           → Type={number, kind: decimal}          (today: number 5/5)
- set %files% = [...] as list of path  → Type=list, and "a list of what?" → path, so {list, kind: path}   (today: list 2/5, path 3/5)
- set %x% = '{"a":1}' as json          → Type={text, kind: json}               (today: dict 5/5)
- read 'notes.md' as text              → Type=text                             (today: {file, kind: md} 5/5)
- set %d% = "PT30S" as iso duration    → Type={duration, kind: iso}            (today: duration 5/5)
- set %n% = 5                          → no Type, as before (guard)
- set %s% = "hi" as text               → text, as before (guard)
```

Measured by the builder: 5 builds each, fresh, `"cache":"skip"`, counted with jq. The teaching-edit rule applies (the decider fixtures and page goldens re-pinned, diffs read).

### 5. The leftovers go

The demolition list below, each checked by grep.

## Demolition (what must not survive)

| goes | where | when |
|---|---|---|
| a goal's stored `"path"` and `"prPath"` on write, `PrPath`'s `[Store]` | `goal/this.Item.cs:148–149`, `goal/this.cs:84–89` | 0 |
| `[PlangType("…")]` words as type names | every `[PlangType]` attribute; `NameOf`'s first branch (`type/item/this.cs:539–541`) | 1 |
| the `setting` family as a kind (each class a kind named by its path) | `type/item/setting/kind/this.cs` | 1 |
| a kind as a separate codec class beside its type | each family's `kind/` folder (number, duration, size, template, separator, bool, path's `scheme`), as the coder's trace finds them | 2 |
| the `kind` class itself: a type's kind is a type (decision 768) | `type/kind/this.cs` and its `empty`, `list`, `subclass` | 2 |
| `type.format`, a filter over the kinds with a MIME or an extension, read only by its own tests (768) | `type/this.cs:117`, `type/format/this.cs`; `FormatKindTests` asks the types under a type instead | 2 |
| item's json navigator: walking a `JsonElement`, its own `count`/`first`/`last` | `type/item/kind/json/this.cs` (`:64–84`, `:89–91`) | 3 |
| "is this an action slot?" asked by name | `goal/step/action/this.Validate.cs:54–55`, `formal/reader.cs:221` and `:349`, `action/serializer/Reader.cs:153–154` (F8.1) | 5 |
| `ToString` writing `name<kind>` only for list, dict and choice | `type/this.cs:547` (F4.2) | 5 |
| the stopgap where `%x!type.kind%` answers a clr carrier or its name | 80e4c9e4e (the type's `kind` member) | 5 |

**Stays:** the written form `{name, kind}`; `text`, `number` and the other short names a developer writes; the reflection and C# host kinds (`kind/reflection`, `kind/dict`, `kind/list` over C# objects) until 673; decision 401 (json stays text until read into).

## What tests can't show (checks the coder runs and reports)

- **Stage 1:** no `[PlangType]` left (grep); the surface golden re-pinned with decision 747 named.
- **Stage 2:** every kind found by the trace is one of: a type under its parent, a named value, or a C# host kind kept for 673. None left unclassified (application/plang's `wire` kind included).
- **Stage 4:** the eval by name: each baseline sentence 5/5, no golden case regresses.
- **Every stage:** the gate by name; PLNG001/002 at 0; the plang suite no worse than 245/16/12/3 except the tests this stage fixes.

## OBP validation

| new surface | check | result |
|---|---|---|
| a kind is a type | one concept, one class: no codec class beside the type it codes | to verify at stage 2 |
| a type's name is its namespace | the three paths agree: plang name = C# namespace = folder | by construction |
| a format type's raw bytes | the value owns its bytes and its parse; no reader outside it parses for it | to verify at stage 3 |
| a generic's element question | the type answers what it offers; no `is list` in the builder | to verify at stage 4 |
| a goal's path from where it's found | one location fact; nothing stored that can disagree with the file's place | by construction |

The coder owns the code shape; the sketches here are intent. Each stage's shape comes to the architect before it's built.
