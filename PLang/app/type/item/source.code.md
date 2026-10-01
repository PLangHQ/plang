# source: a template holds only what its bytes are trusted with

A source is a value still in its raw form (a `.pr` slot, a file's bytes, a wire value), born with its declared type.
When that type is marked a template, the source decides **once, at its birth**, which of the variables written in
its raw it holds. A variable it doesn't hold is never resolved: it stays as written everywhere downstream (text's
render finds nothing at that spot, a whole `%!x%` is no reference).

## The rule (one door)

```
source(value, type, variable, origin, built)                      type/item/source.cs:55
├─ type.Template == null → holds nothing (a string that looks like %x% stays literal)
├─ built (the build's own bytes) → every variable its row lists   :72  (no list: those written in it)
├─ origin set (a file or url read with `load vars`) → own only     :73  written().Where(v => v.IsOwn)
└─ anything else (a peer's value, the app's store) → none          :74
```

`IsOwn` (`variable/this.cs:60` → `code/this.cs:34`): every hop answers for itself, no type-switch.

| hop | own when | file |
|---|---|---|
| root `Variable` | its name has no `!` (`%!app%`, `%!trace%` are the app's) | `code/Variable.cs:16` |
| `Property` | it is no binding (`%x!context%` reaches past the value) | `code/Property.cs:25` |
| `Index` | its key holds only own variables (a literal holds none) | `code/Index.cs:18` |
| `Method` | every value handed to it holds only own variables | `code/Method.cs:25` |

## Who grants `built` (fail closed)

Only the two readers of the build's own bytes, through `ReadContext.IsBuilt` (`type/reader/ReadContext.cs:46`):

- the goal loader, a goal's `.pr` — `goal/this.Item.cs:55` (shortcuts and a resume reload goals through it; a call
  frame holds its goal by address, `callstack/call/this.cs:32`)
- the build reading its own answer — `goal/step/action/formal/reader.cs:379`

It rides to the birth: `type.Read` (`type/this.cs:444–450`) hands it to the source and to `Make(slice, …)` → the
wire (`wire/this.cs:30–35`). A read inside the bytes keeps it: the source's own read (`source.cs:247`), the wire's
(`wire/this.cs:49`), and a row nested in a container (`serializer/json.cs:95`, `:191`; a goal call's parameters
are such rows). Re-declaring keeps it (`source.cs:254`, `wire/this.cs:92`). Every other `ReadContext` is outside.

## A partial template renders through text, then the type makes itself

A source holding variables in its own text (`"%dir%/x.json"`, not a whole `%ref%`) is rendered by text, the one
renderer, and the declared type is made from what it rendered:

```
source.Value(data)                                              source.cs:130
├─ IsVariable (a whole %ref%) → the variable's own door         :148
├─ Text is { } template                                         :168
│    template.Value(data) → a plain text                        text/this.cs:164  (an unset own variable throws
│                                                                 "…is not set" :187; an unset %!x% stays :186)
│    _type.Make(rendered, ctx) → this type from that text       type/this.cs:374  (text: itself, its kind kept;
│                                                                 path: path.Create → Resolve; a decline →
│                                                                 DeclinedException → data.Fail, source.cs:206)
│    made.Value(data)
└─ else: kind-first decode, else the type's reader               :180–203
```

- **`Text` vs `RawText`.** `RawText` (`source.cs:85`) is the raw's CLR string face, for relay and display.
  `Text` (`source.cs:232`) is a **text item** — `text.@this` holding the template and the variables this source
  holds — and text renders it; the source only hands it over. Read off `Peek()`, so a byte raw declared text
  (load vars content) renders the same way. Null for a plain source and a whole `%ref%`.
- **A wire is no text** (`wire/this.cs:44`): a still-encoded slice is decoded and its parts render themselves (a
  `%ref%` leaf is the value it names); rendering the json slice as one text would inline the values as text.
- What a variable held is filled once: the rendered text is plain, and the value made from it holds no template
  (a path's location is never one — `path/this.code.md`).
- Still lazy: nothing renders until `Value`, and a source holding variables is never kept (`Cacheable`, `:128`).

## How outside content reaches the door

```
file.read … load vars → path.Read(ctx, template)        module/file/read.cs:18
  Marked() = "plang" — the mark only says "may hold"     type/item/path/this.Operations.cs:67
  → a file/url reference, unread (lazy)                  type/item/content/this.cs:33
  at first use: content.Value → format.Decode(…, template, origin: Path)   content/this.cs:78
    → type.Create(raw, …, origin)                        type/kind/this.cs:231
    → new source(raw, this, origin: origin)              type/this.cs:350   ← own only
a peer's Data (plang wire, http, url/channel application/plang, the store)
  → data.Wire → data.reader (type mark + "variable" list read off the bytes)   data/reader/this.cs:58–106
    → type.Read, not built                               type/this.cs:444–450  ← none (its list ignored)
```

## Kept or rendered each use

- `source.Cacheable => !HasVariable` (`:128`): a source holding variables renders at every use; one holding none is
  parsed once and kept by the holding Data (`data/this.cs:290`).
- The file/url reference decides the same, once decoded: `content.Cacheable` (`content/this.cs:55`) asks the
  decoded value whether it holds variables (`_plain`, `:81`). Content holding only `%!…%` is kept once opened;
  content holding an own variable shows a change between two uses. The read stays lazy: nothing is read at the read
  step, and the content has no variable list of its own, so `Settle` leaves it unopened.

## Tests

- `PLang.Tests/Types/App/Types/SourceTests/LoadVarsTests.cs` — a whole `%!trace.id%` / `%!app.type.list.count%`
  file stays as written; own `%name%` fills beside an app variable (file and url); a binding hop, an index and a
  method handed an app variable stay; json guard; a wire value marked a template holds none, its own list ignored;
  a built `.pr` still resolves `%!…%` (directly and nested in a list); kept once opened / a change shows.
- `PLang.Tests/Data/App/VariablesTests/VariableOwnTests.cs` — `IsOwn` per hop.
- `PLang.Tests/Shared/Make.cs` `Built` — a test authoring what the builder marks births it through the build's door;
  a template made straight from a raw string is outside content and holds nothing.
- `PLang.Tests/Types/App/Types/PathTests/OutsideTextTests.cs` — a listed file, a wire value (marked or not) and a
  string taken as a path stay as written; a developer's `read %dir%/x.txt` fills and reads; what `%dir%` held is
  not filled again; a built path holding `%!…%` renders; an unset variable fails "not set".

## Known faults

- **The grant is copied by hand** at each fresh `ReadContext` made inside a read (`source.cs:231`, `wire/this.cs:46`,
  `serializer/json.cs:95`, `:191`). A place that forgets fails closed (a built template nested there holds nothing),
  never open. Two such places don't copy it and are not the build's path today: the json → type bridge
  (`kind/json/this.cs:181`) and a step's `code` written from rows that aren't actions yet (`goal/step/this.Item.cs:31–49`;
  the builder hands over actions the formal reader made, which are granted).
- **A test making a template from a raw string** (`new Data(name, "%x%", textTemplate)`) makes outside content, which
  holds nothing. Tests birth what the builder marks through `Make.Built`.
- **A plain `.pr` path slot with a bad scheme throws** out of `Value` (the path reader calls `Resolve`, which throws
  `SchemeNotRegistered`; the reader doesn't decline), where a template path's lands on the Data. Logged, not fixed.
- **A json-kinded text template** (a string token typed `text/json` holding a variable) renders as text before it
  is made, so the kind-first decode is skipped; no build writes one today.
- **Json content fills nothing** with `load vars` (its owner is `item`, `kind/json/this.cs:17`, so the raw stays
  bytes and parses as `clr(json)`). A separate ask.
- **The Out view writes the template mark** (`data/this.Output.cs:95–101`) though a template has already rendered
  there; the door ignores it (not built → none).
