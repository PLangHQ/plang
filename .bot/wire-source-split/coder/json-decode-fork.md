# Two json decode paths — the fork Ingi wants collapsed

**Status:** for architect. Ingi: *"I am not happy with the fork, having two read paths, so I want to discuss."*

**Trigger:** designing `wire.Clr` (lower an undecoded wire to CLR). Whichever decoder I route it through picks a side of a fork that already exists in the shipped 6c implementation. Rather than pick, surface the fork.

---

## The two decoders — different OUTPUT shapes, both live

For one json payload there are two decode paths that land on **different representations**:

| path | entry | produces |
|---|---|---|
| **A — kind** | `Kind[json].Load(raw)` → `await loaded.Value()` | **`clr(json)`** — opaque `JsonElement`-backed carrier |
| **B — typeReader** | `item.serializer.json.Parse(raw)` (registered `(item,json)`, + direct callers) | **native item tree** — `dict`/`list`/`text`/`number`/`bool` |

This is not two code paths onto one result. It's two results. A is lazy/opaque; B is the eager native tree.

### Where each is reached

**A (kind → clr)** — `source.Value()` kind-first rung, `source.cs:135`:
```csharp
if (_type.Kind is { } kind
    && await Context.App.Type.Kind[kind.Name].Load(_value, Context) is { } loaded)
{
    var decoded = await loaded.Value();   // json → clr(json)
    ...
}
var item = Read();                        // ← B, only reached when the kind DECLINES
```
So for **json**, Value() always takes A; it never falls through to `Read()`. B is dormant *for Value()*.

**B (typeReader → native)** is still live everywhere else:
- `wire.Read()` (`item/wire/this.cs:24`) → `plang.Read` (`channel/serializer/plang/this.cs:283`) → `Reader(type.Name, kind)` → the `(item,json)` reader → `Parse` → native tree. Reached by **`wire.Write` into a non-owning writer** (`wire/this.cs:34`, `Read().Write(w)`).
- `item.serializer.json.Parse` direct callers: `data.this.cs:243,349`, `type.this.cs:465`, `list.this.cs:330`, `object/serializer/Reader.cs:21`, `dict` kind `Convert` (`item/kind/dict/this.cs:62`), `dict/serializer/Reader.cs`.

So: **Value() decodes json to `clr`; wire-relay and `as dict` and the object/list readers decode json to a native tree.** Same bytes, two shapes, chosen by which door you enter.

---

## Is this intentional or unfinished?

Two readings, and I can't tell which the architect intended at 6c:

**(1) Intentional — clr-default + native-on-demand.** 6c's rule was *"json stays clr, converts to dict only on explicit `as dict`"*. Under that, A (clr) is the default face and B (native) is only the `as dict` conversion (dict-kind `Convert`). If so, the *other* B call sites (wire-relay decode, object/list readers, the direct `Parse` calls) are leftovers that should also route through the kind, and `item.serializer.json.Parse` should stop being a general decoder.

**(2) Unfinished migration.** 6c added the kind path to `Value()` but left `item.serializer.json.Parse` as the registry decoder and left `plang.Read` routing through the typeReader. The native path was meant to die but 8+ call sites still lean on it.

Either way, **`wire.Clr` has no clean home**: route it through `Read()` (B) and it re-forks (a second json decode beside the kind); route it through the kind (A) and it can't — `Kind.Load` is async, `.Clr` is sync.

---

## What I need ruled

1. **After 6c, is there ONE authoritative json decode?** Is `clr(json)` (A) the sole materialization, with native tree (B) only ever the *explicit* `as dict` conversion — or are both first-class?
2. **If A is authoritative:** `item.serializer.json.Parse` should stop being a decoder — its callers (`data.this`, `type.this`, `list.this`, `object/Reader`, wire-relay) route through the kind. `plang.Read` (the wire read-back) needs to decode via the kind, not the typeReader. That's a real chunk of work — is it in scope for `wire-source-split`, or a follow-up?
3. **`wire.Clr` specifically:** given the sync/async wall, my read is it can't faithfully lower and should **throw "await .Value() first"** (wire-scoped — correct code never hits it, because `Value()` rebinds the Data to the decoded item before any `.Clr`). Agree, or do you want the sync door to survive via a native decode (accepting B)?

## My lean

- One json decode, owned by the **kind** (A). B's native `Parse` becomes an implementation detail *inside* the json kind (it already is — `dict` kind's `Convert` calls it), not a parallel registry decoder.
- `plang.Read`/`wire.Read` route through the kind so a relayed/round-tripped json wire decodes the same way `Value()` decodes it.
- `wire.Clr` throws (materialize first) — no sync decode, no fork.

But collapsing B across its 8+ call sites is the bigger question, and it's yours to scope.
