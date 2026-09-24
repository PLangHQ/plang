# Remove context from item types

With Ingi, 2026-09-24. **Draft, being designed with Ingi. Not for coder yet.**

## Why

Every problem in the births pass came from an item that **stores** a context, nullable or not. A stored context goes stale as soon as the item is shared or outlives the run that made it. Getting one in also takes a late stamp, and that stamp writes on whatever is shared:

- the program's parameter rows (step 1: two actors running one goal read each other's variables and permissions)
- a value's type object (`Promote()`, the stamps on `%x!type%`)
- a list's stored elements (navigation stamps the running context onto the shared program's step list)

Ingi's spider sense throughout: we were fixing this the wrong way, by making stored contexts correct instead of not storing them.

## The rule

**An item never stores a context. When it needs one, the caller passes it.** The `Data` box carries the context; the item inside it never does. An item stores only facts about itself, for example a path's location as typed and its absolute location.

**Always the context, never the App.** `app.Format` and `context.App.Format` may not be the same thing, so an item never holds or receives an App on its own.

## Why it holds — from plang's side

A value needs a context only while it is being **used**. Every use happens during execution, inside a step, and every step has a context. Two lines carry it:

1. Everything that enters through I/O arrives as a `Data`: channel reads (`channel/this.cs:291`), file reads (`path/file/this.Operations.cs:78, :97`), `.pr` rows. Handlers return `Data`; variables store `Data`.
2. Every variable get and set goes through the memory stack (`variable/list/this.cs`), which is born with its context (`:81`).

The item's doors already receive the context from whoever asks: `Value(data)`, `Get(parent, key)` (navigation), `Output(writer, mode, context)`.

| plang | value | needs | the context comes from | status |
|---|---|---|---|---|
| `set %g% = "Hi %name%"` | text template | variables, to render `%name%` | `Value(data)`: `data.Context` (`text/this.cs:102-120`, stores none today) | done |
| `set %l% = [1, %x%]` | list template | variables | `Value(data)` | render done; element hand-out open (below) |
| `read file.txt` | path | create: the running goal's folder and the root | `Resolve(raw, context)` | settled |
| | | I/O: permission (actor), result `Data`, root, formats | the caller passes it to the verb | settled |
| `read data.json` | file | formats (json → dict) | `Value(data)`, and the file's type via `Data.Type` | settled |
| `if file.txt` | path | "does it exist" | `Data.ToBooleanAsync()` passes its context to `AsBooleanAsync` (`data/this.cs:679`) | settled |
| `get https://…` | url / http path | permission, result `Data`, formats | same as path: verbs get it passed; `Value(data)`; `Data.Type` | settled |
| `%x!type.Description%` | type | the type registry | navigation, with the asker's context (births 3.5, landed) | done |
| `write out %price%` | number | formats (culture later) | `Output(writer, mode, context)` | nothing stored today |

## The path, settled with Ingi

**When it's needed:** the path is created at execution. The instruction becomes an execution at the generator's run copy plus the handler's `await Path.Value()`, and not before. At `.pr` load the row holds only the raw text in a `wire`. `path.Resolve(raw, context)` runs at execution and is the only creation door. Every C# infra caller also passes a context: most pass their running one, and some pass `App.System.Context` explicitly (`app/this.cs:360, 406, 538`; `goal/list/this.cs:337, 343`; `module/list/this.cs:167`; `build/this.cs:78`).

**What it stores:** facts only, namely `_location` (as typed) and `_absolute`. No context, no App.

**Every use gets the context passed:**

| Use | Where the context comes from |
|---|---|
| Create: goal folder + root (`path/file/this.cs:77-111`) | `Resolve(raw, context)`, used there and not kept |
| Verbs: read, write, exists, list, delete, copy, move (about 110 call sites) | the caller passes its context; handler and infra callers both have one |
| Permission (`this.Authorize.cs:27-98`, `file/this.Operations.cs:362-413`) | the context passed to the verb |
| The result `Data` (`Context.Ok/Error`, about 40 sites) | the context passed to the verb |
| `Relative` (root): writing out a derived path (`Portable`, `this.cs:290-307`) | `Output(writer, mode, context)`. `IWriter` stays context-free: it writes tokens, not who is running |
| `Relative`: comparisons (`IsUnder`, `Owns`) | the verb or handler doing them |
| `MimeType` / kind from extension: `file.read` (`:60, :69, :126`), the file channel (`channel/type/file/this.cs:27`), writing a file (`file/serializer/Default.cs:16`) | the handler, `Value(data)`, `Output(…, context)` |
| A file's or image's type (kind from the extension, `file/this.cs:54`) | `Data.Type` passes its own context. Only file and image use it; the other 34 `Type` getters take the parameter and ignore it |
| Navigation `%x!relative%`, `%x!mimetype%` | the memory stack |
| Derived paths (`Parent`, `Combine`, …, `file/this.Derivation.cs`) | nothing to pass: string math on the absolute; the new path stores its facts |

**Rejected:** storing the root as a string, and storing the App reference (Ingi: pass the reference, never a copied value; and never the App, always the context).

**Found:** the JSON path converter (`channel/serializer/json/converter.cs:26, :78`) has a constructor with no context and passes `_context!` to `Resolve`, so it can throw.

## Truthiness

`IBooleanResolvable.AsBooleanAsync()` (`data/IBooleanResolvable.cs:21`) takes no context. Its caller is `Data.ToBooleanAsync()` (`data/this.cs:679`), and a Data has one, so it passes it: `AsBooleanAsync(context)`. Implementers: item base (`type/item/this.cs:470`), `code` (`type/code/this.cs:30`), path (`path/this.Operations.cs:170`), http (`path/http/this.cs:225`), file (`path/file/this.Operations.cs:132`), image (`image/this.cs:234`). One other caller: `ui/code/Fluid.cs:44`.

## The list, settled with Ingi

**The list itself never uses its context. It only passes it on to its elements.** It never loads, renders or checks a permission with it; the elements do that later, through their own `Data`. The places that pass it on (`type/item/list/this.cs`):

- Reading an element, `Row(i)` (`:127-138`): stamps a stored `Data` element (`:134`), or creates the element's `Data` with it (`:137`).
- Adding an element: `Add` (`:335`), `Insert` (`:345`), `SetAt` (`:503`), `ResetTo` (`:495`), `AddRaw` (`:147`) stamp it onto the element.
- The `Context` setter (`:158-175`) pushes a new context onto every stored element. The navigation stamp (`data/this.Navigation.cs:105-106`) triggers it.
- New lists and wrappers are born with it: `Empty()` (`:665`), chunk split (`:321-322`), the temporary `Data` in `Contains(value)` (`:690`).

**The one place list code consumes it:** `Row` turns a raw stored value into an item, `global::app.type.item.@this.Create(raw, _context)` (`:137`), which needs the type registry. That happens when an element is handed out, and someone is always asking at that moment (navigation's `Get(parent, key)`, a handler, `Output`), so it can use their context.

**So the list does not store a context.** An element gets its context from whoever asks for it.

**How the asker's context reaches an element (traced, `%list[0].name%`):**

```
memory stack Get("list") → the list's Data                  (has the context)
 Data.Get("[0].name")                   data/this.Navigation.cs:62
   child = _item.Get(this, "0")         ← `this` = the list's Data: the asker, with the context
     list.Get(parent, "0") → At(0) → Row(0)                  list/this.cs:548-549, :127-138
       a stored Data → returned as-is (by reference, with its own context)
       a raw slot    → new Data("", Create(raw, _context), context: _context)   ← a NEW Data, made on every read
 child.Get(".name")                     :109  ← the element's Data is now the asker
```

- A **stored `Data`** is handed out by reference and keeps its own context. Nothing to do.
- A **raw slot** is the only case that needs a context. `Row` makes a new `Data` for it on every read ("type on read, never cached back", `:38-43`). It takes the context of whoever asks: `Row(i, context)`.
- This replaces both earlier options (a copy per element, or passing the context along the walk). There's no copy and no threading.

**The element doors, and one pass instead of two.** Elements are handed out through navigation `Get(parent, key)` (`:533-553`, has the asker as `parent`), `Items` (`:206`), `At` (`:254`), `First` / `Last` (`:260-263`), `foreach` (`GetEnumerator`, `:27`), and `EnumerateItems` (`:245-251`). Turning rows into elements stays in the list: only the list knows whether a row is a raw value, a stored `Data`, or a chunk. Moving that up would leak its internals.

Today it is O(2N). `Items` loops every row first, creating a `Data` for each into a new `List<Data>` (`:210-216`). Then every caller loops that list again: `Output` (`:239`), `foreach` (`:27`, `Items.GetEnumerator()`), `EnumerateItems` (`:249`). Ingi: no looping over a list twice.

The fix is one pass. The list hands out each element as the caller reaches it, created with the caller's context:

```csharp
// list/this.cs — NEW (name open), replaces Items / GetEnumerator's up-front List<Data>
public IEnumerable<Data> Elements(actor.context.@this context)
{
    for (int r = 0; r < _items.Count; r++)
    {
        if (_items[r] is Chunk chunk)
            foreach (var e in chunk.Items.Elements(context)) yield return e;
        else
            yield return Row(r, context);   // raw slot → a new Data with the caller's context; a stored Data as-is
    }
}
```

`At(i, context)`, `First` and `Last` take the caller's context the same way. Every caller has one: `Output(writer, mode, context)`, handlers (`Context`), navigation (`parent.Context`). The 28 external `.Items` callers pass theirs; the 6 `At` / `First` / `Last` callers too.

**What goes:** `_context` on the list, the `Context` setter's walk (`:158-175`), every stamp on an element (`:134, :147, :335, :345, :495, :503`), the list's context on new lists (`Empty()` `:665`, chunk split `:321-322`, `Contains(value)` `:690`), and the navigation stamp (`data/this.Navigation.cs:105-106`).

## The dict: the same as the list (confirmed)

- `Slot(key)` (`dict/this.cs:118-135`) is the list's `Row`. It stamps a stored `Data` (`:123`) or makes a new `Data` for a raw slot on every read (`:134`, `Create(raw, _context)`). It takes the asker's context.
- `Set(Data)` (`:279`) and `Set(key, value)` (`:291`) stamp added entries; the `Context` setter (`:96-110`) walks them; `Keys` (`:152`) creates a new list with it. All of these go.
- Two small extra spots:
  - `Get<T>(path)` (`:243`) is a typed C# path read (6 callers, e.g. reading an LLM response). It turns the result into an item with `Create(cur, _context)`, so the caller passes its context.
  - `Clr(target)` to a C# record (`:368`) hands `Context` to the reflection kind, which never uses it (`kind/reflection/this.cs:117-134`, `ctx` unused). But it walks `Entries`, which creates the entry `Data`. At the CLR exit a raw slot is already a CLR value, so it can be read without a context.

## The source: stores one, never uses it

`source` (`source.cs:37`, set at `:45`) uses its stored context in one place only: it passes it on to its re-declared copy (`Declared`, `:209`). Everything else gets the context passed in: the load uses the asking `Data`'s (`:114`), `Get(ctx)` (`:31`), `Read(context)` (`:198`). So the source stores none.

## The wire: the parent passes it (settled)

`wire` (a still-encoded `.pr` slice, `type/item/wire/this.cs`, a subclass of source) uses its stored context to decode the slice (`Context.App.Type.Kind[k].Parse(Raw, Context) ?? Read(Context)`) in three doors:

- **`Clr(target)`** (`:58-62`). All three callers hold a `Data` and peek into it: `Data.Clr<T>()` (`data/this.cs:770`, `Peek().Clr<T>()`), the list's CLR exit (`list/this.cs:586`, `row.Peek().Clr(elem)`), the reflection record read (`kind/reflection/this.cs:131`, `slot.Peek().Clr(...)`). A wire is only ever the value of a `Data` that hasn't been loaded yet, and that `Data` has the context. So the parent passes it: either `Clr(target, context)` (the other 20 `Clr` overrides take it and ignore it, like `Type`), or the callers use the `Data`'s own door and the `Data` passes its context.
- **`Write(IWriter)`** (`:31-38`). Only reached through the writers' `Value(item)` branch (`channel/serializer/json/writer.cs:184`, `text/writer.cs:95`), where the writer walks dicts and lists itself (`json/writer.cs:165, :175`) with no context. That branch is already marked as closing (`text/writer.cs:90-94`: "TOMBSTONE… Value(item) is being retired: an item in hand is rendered by asking `item.Output`"). Its replacement is `Output(writer, mode, context)`, where the parent passes the context. When the branch goes, `wire.Write` has no caller.
- **`Output`** (`:46-53`) already receives the context; its fallback to the stored one (`context ?? Context`) goes.
- `Declared` (`:64-66`) only passes it on.

So the wire stores no context.

## computed: the parent passes it (settled)

`computed` (`type/item/computed.cs`) is a system variable (`%Now%`, `%!app%`, …). It uses its context in one place, `Compute()` (`:64-68`), to turn the factory's raw result into an item: `Create(raw, Context)`. `Value(data)` (`:42`) and `Get(parent, key)` (`:47`) already have the asker. `Peek()`, `IsTruthy()` and `ToString()` (`:53, :61, :62`) don't, but a computed is only ever created inside its own `DynamicData` (`data/this.cs:885`), and that `Data` has a context and passes it. So computed stores none.

## clr: the parent passes it (settled)

`clr` (`type/clr/this.cs`), the carrier for a foreign C# object:
- **Creation** (`:57`, `Kind = kind ?? Context.App.Type.Kind[value.GetType()]`) uses the creator's context. There are 6 creators and all have one; nothing is kept.
- **`Get(parent, path)`** (`:103-105`): the parent has it.
- **`Read`, `EnumerateItems`, `Output`** (`:111-113, :134-137, :182`) already receive a context. They use the stored one or fall back to it (`context ?? Context`), and that fallback goes.
- **`Set`, `Enumerate`, `Clr`** (`:123, :128, :154`): their callers are a `Data` or a handler, so they pass it.

So clr stores none.

## error.Error: the exception (Ingi)

`Error` (`error/Error.cs:140`) keeps its context. It is different from the other items: its context records **where the error happened**. It is set when the error is created from the failing context (`:207-213`). It is used by `Callback`, the snapshot taken at the moment of the error (`:115-129`, snapshot is parked), and by the verbose "variables at point of failure" dump (`:388-391`). The context lives on `Error` only, never on the item base class.

Separate item (Ingi: "another thingy"): `Error` stores an `App` (`:102`, set by `Errors.Push`, used by `Callback`). It shouldn't; it can reach the App through its context.

## Inventory — every item type that stores a context today

Found by searching item classes for a context field or property.

| Item | Stores it at | Status |
|---|---|---|
| `path`, and `file` / `url` / `directory` through their path | `path/this.cs:154` | settled: stores none |
| `list` | `list/this.cs:176` | settled: stores none |
| `dict` | `dict/this.cs:136` | settled: stores none |
| `source` | `source.cs:37` | settled: stores none |
| `wire` (source's subclass) | inherits source's | settled: stores none |
| `computed` | `computed.cs:20` | settled: stores none |
| `clr` | `clr/this.cs:24` | settled: stores none |
| `error.Error` | `error/Error.cs:140` | **the exception** (Ingi): keeps where it happened, on `Error` only; its stored `App` (`:102`) is a separate item |
| `snapshot` | `snapshot/this.cs:21` | open, but snapshot is **parked** (Ingi) |
| `actor` | `actor/this.cs:79` (creates its own) | different case: the actor **owns** its context, one per actor, and doesn't hold a copy of someone else's. Confirm with Ingi. |

Not items, and they keep theirs: `Data` (the box that carries the context), the memory stack (`variable/list`), the channel list, serializers, the settings store.

## Relation to work in flight

Births step 3 is landed through 3.5 (`d3ac19588`). Coder holds uncommitted changes for my held rulings 1, 3, 4, 5 (sent before Ingi answered). This design may replace them, especially ruling 1 (list elements).
