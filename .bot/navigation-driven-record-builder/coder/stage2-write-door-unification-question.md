# Architect Q — should containers self-`Write`? (unify the write door; kill json.Writer's dict/list type-switch + text.Writer's IsLeaf guard)

Branch: `navigation-driven-record-builder`. Raised by Ingi during the formal-render piece (`formal-render-plan.md` step 1). Ingi's instinct: *"shouldn't the item know how to write itself — just `v.Write(this)`, leaf or container?"* He wants an architect ruling before touching the write-door contract.

## The current shape (traced, HEAD 15f5d432e)

```
text.@this.Write(w)  => w.String(_value)          // a LEAF owns its bare wire form
number/bool/date/…                                 //   likewise
dict.@this                                          // NO Write override — only Output(IWriter, mode, context): async, view-aware
list.@this                                          //   likewise
item.@this.Write (base) => throw "no bare wire form — not a leaf value"   // base throws for containers, on purpose
```

Base `Write`'s own doc: *"Only leaves are asked; Normalize routes non-leaves through their own branches; the default throws so a missing override is loud."*

So `Write(IWriter)` is **deliberately leaf-only**. Containers are rendered **structurally by the writer**:

- `json.Writer.Value` (`json/writer.cs:153-189`) has explicit `case dict.@this` → `{}` and `case list.@this` → `[]` arms **before** its `case item.@this v: v.Write(this)` — a type-switch per container type.
- `text.Writer.Value` reaches containers via `default → Structural()` (the json delegate). During the formal piece I added a leaf-only arm to `text.Writer.Value` (`case item.@this { IsLeaf: true } v: v.Write(this)`) so a top-level *leaf* renders bare while containers keep going to the json delegate — an **IsLeaf guard** that only exists to dodge the base `Write` throw.

## The smell Ingi is naming
Two write mechanisms split by leaf-vs-container: leaves own `Write`, containers own `Output` **and** get inline-rendered by each writer's type-switch. The value does NOT uniformly own its render — the writer knows how to render dict/list. My `IsLeaf` guard is the tell.

## The clean shape (Ingi's instinct)
Give `dict`/`list` a `Write(IWriter)` mirroring what `json.Writer` already inlines:
```csharp
// dict.@this.Write
w.BeginObject(); foreach (var e in Entries) { w.Name(e.Name); w.Value(e.Peek()); } w.EndObject();
// list.@this.Write
w.BeginArray(CountRaw); foreach (var i in Items) w.Value(i); w.EndArray();
```
Then **both** writers collapse to one arm — `case item.@this v: v.Write(this)` — dropping `json.Writer`'s `dict`/`list` cases and `text.Writer`'s `IsLeaf` guard. `BeginObject`/`Value`/`EndObject` already route correctly through either writer (`text.Writer.BeginObject → Structural`), so `dict.Write(w)` works for json AND text uniformly. One door; the value owns its render; no writer type-switch.

## The one thing I need you to rule on — the wire path
`json.Writer`'s dict/list arms are already **sync + viewless** (`foreach entry: Value(entry.Peek())`, no `mode`, no context). So a sync viewless `dict.Write` reproduces the *bare-json* behavior exactly. The open question is the **plang WIRE** (`application/plang`, envelope + `[Out]` filtering + signing):

- Does the wire render a container through **`dict.Output(IWriter, mode, context)`** (view-aware, async) — in which case `Write` is purely the bare/preview door and unifying it is safe — **or** does the wire reach containers via **`Value(dict)`** (the same viewless arm), meaning a `Write` unification must preserve whatever `[Out]`/envelope handling that path relies on?
- Put differently: is `dict.Output` (async, view) genuinely a *different* render from `Value(dict)` (sync, viewless), or has the viewless arm already been the container render everywhere and `Output` is vestigial for containers?

If the wire routes containers through `Output`, unification is a clean, contained refactor (dict/list gain `Write`; two writers lose their special-casing; `Output` stays for the wire). If the wire routes containers through `Value`, I need the ruling on how `Write` preserves `[Out]`/signing before I touch it.

## Scope / sequencing
- Independent of the formal piece's correctness — the `| formal` filter drives `Value(item)`, which yields the right output under **either** the guard or the unified door. So the formal piece continues with the `IsLeaf` guard as the interim; if you rule "unify," I drop the guard + json.Writer's dict/list cases as a small contained change (its own commit, own test that `Value(dict)`/`dict.Write(w)`/`dict.Output` agree).
- Files touched by unification: `type/item/dict/this.cs`, `type/item/list/this.cs` (add `Write`), `channel/serializer/json/writer.cs` (drop 2 cases), `channel/serializer/text/writer.cs` (drop the guard), + a courier-consistency test.

Ruling requested: (1) unify or keep the split; (2) the wire-path fact above (Output vs Value for containers) so I don't drop `[Out]`/envelope.
