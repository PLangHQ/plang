# coder v3 — stage 3: one set of types

The architect gave the go (plang-40, 2026-09-27). The plan's stage 3 row is in
`.bot/app-systems/architect/plan.md`. This file holds the trace. The findings marked **ask** went back
to plang-40 before any code; stage 3 builds only on what holds.

## What stage 3 is (the architect's go)
- One set of types. Each type owns its name, aliases (`Alias`), C# class and facts, and answers
  `Match(key)` → `ValueTask<TSelf?>` (`IMatch<TSelf>` arrives here).
- Every spelling source goes the same way: `Primitive.Aliases`, `Registry.SeedAliases`, `Precision`,
  the `text/md` and `list<path>` splits (and `Contains`' `/` split).
- `kind`: one member (today's `Kind`), never null. `kind.list` answers full types with their kind
  (`type/kind/list/this.cs`). item's kind classes are today's global store. `Kinds` dies.
- The choice and scheme stores move onto their types.
- `Add` is the one way in. It carries the Loader's checks (sealed names, reserved property names,
  renderer coverage). Lookups that need formats take the caller's context (`context.App.Format`).
- Builder-visible: the prompt twins stay byte-equal, or the change gets one eval run.

## Trace (today's code)

### The registry, `type/list/this.cs` + `Registry.cs`
Seven stores sit side by side:
- `_nameToType`, `_typeToName`, `_runtimeNameToType`, `_clr` (C# class → owning name, from each item's
  `OwnedClrTypes`)
- `_catalogByName` (a Lazy of `BuildTypeEntries(null)`, the only source of the facts)
- `_full` (full types cached per identity)
- `_clrTypeFullNames`

The name door `this[string]` works in this order: catalog hit → the `/` split → the `<…>` split →
`Get(name)` (which resolves generics to System `List<>`/`Dictionary<>`) → the alias → canonical hop →
`Precision` → a bare `new type(name, clr)`.

### Spelling sources (`primitive/this.cs`)
`Aliases` mixes four different things:
| what | entries | where it belongs |
|---|---|---|
| true aliases | string, boolean, dictionary, map, array, integer | the owning type's `Alias` |
| precisions | int, long, float, double, decimal, byte | number's kinds (`number.Kinds` already has them) |
| file extensions | csv, txt, xml, yaml, yml → text (today the kind is **dropped**: "csv" → plain text) | text's kinds? **ask** |
| nullable spellings | int?, long?, double?, bool?, datetime?, guid? | nullability is the slot's fact → drop? **ask** |

Items also spell themselves (datetime, date, time, duration, guid, list, dict, tag, bytes → binary).
`Canonical` (C# class → name) duplicates each item's `OwnedClrTypes`, so it goes into the type's C# class
and ownership.

### Kinds
- The type's `Kind` is a bare token: the ctor does `new kind.@this(kind)`.
- The behaviour comes from the per-App store (`app.Type.Kind[name|clr]`, 10 production uses): a
  static-discovered class per name (json, list, dict, `*`), plus a base instance per unknown name, cached
  **by name** and shared across types.
- `kind.Type` today means "the type this kind decodes to" (md → text), which is not "the type this kind
  belongs to": `{binary, md}` has kind md, whose `.Type` is text. **ask**
- Null checks on `Kind`: about 31 in production, about 78 in tests. The wire writes `kind` only when
  non-null. Equality and the hash read `Kind?.Name`.
- `Kinds` is fed by `hash` only and read by the obsolete view and `Full`. It dies cleanly.

### C# class per type
`type.Create`/`Empty`/`Read`/`Creatable` ask `App.Type[Name]?.ClrType` / `Clr(Name)` because a type
born bare doesn't know its class. Bare births include:
- 33 `new type.@this(...)` sites in 17 production files;
- the JsonConstructor (every type read off the `.pr`/wire);
- the statics `String`/`Int`/….

So "each type owns its C# class, so the lookups go" holds only once every type is born through the set.
The `.pr` type reader is sync (a `ref` reader). The set's lookup can stay sync inside, since the async
`Get` is stage 4. **ask**

### Generics by name
`Get(string, depth)` → `List<T>`/`Dictionary<K,V>` has one production reader: `variable/set.cs:237`
(`targetType`, then `IsInstanceOfType` at `:308`). With `list<path>` → `{list, path}`, `targetType`
becomes `list.@this`. I'll check the conversion at `:308` against the tests.

### Choice, scheme
- **Choice:** `choice.list` holds the sets by C# class; a set's name is the kind. It moves under the
  choice type as choice's kinds, looked up by C# class (like item's by `ClrForm`) or by name.
- **Scheme:** per-App factories, registered in `app/this.cs:292-294` only (plus 26 test uses). They
  become path's kinds (file, http, https).

### Loader
- A static class. It keeps `SealedNames`, `ReservedCore`, `ReservedShadow` and the renderer coverage
  check.
- Its one production caller is `code/load.cs:40`; 15 tests use it.
- It moves into `Add`, which answers a result, not a throw.

### Dead or vestigial
- `IsClrTypeName`: no production caller outside `Registry.cs`.
- `Renderer`: stage 4 note.

## Order (once the asks are settled)
1. The type owns `Alias`, its C# class and its facts. `Add(type)` is the one way in, and startup `Load`
   scans the assembly and adds each type. `Match(key)` + `IMatch<TSelf>`. The seven stores become one
   list of types (private backing, lookups walk it); `_full` and `_catalogByName` go.
2. Spelling sources onto the types; the name door's `/` and `<…>` splits and `Precision` go.
3. `kind` never null; `kind.list`; item's kinds = today's store; `Kinds` dies.
4. The choice and scheme stores move onto choice and path.
5. `Add` carries the Loader's checks; `Loader` dies; `code.load` calls `Add`.
6. Twins: compare prompt C before/after; byte-equal, or one eval run.

Each step compiles, then gets a targeted suite run. The six suites run before each commit.
