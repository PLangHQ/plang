# Code Analyzer — v1 — `singular-namespaces`

**Scope analyzed:** the landed structural code of the singular-namespace rename at **HEAD `f7790b3a6`** (not the coder v1 report's commit `aa1b3796d` — work continued past it; the report is stale, see below). Focus on the genuinely *new* shapes — the `X/list/this.cs` accessor registries, `app/this.cs` accessor wiring, and `app/type/this.cs` (the Stage-4 type entity + Entry fold). The ~700 mechanical rename/move files were spot-checked, not line-audited.

**Build:** clean — `dotnet build PlangConsole` → 0 errors, 510 warnings (all pre-existing nullability from the deferred Stage-2 non-null work; no new warning classes).

---

## Stale-report note (read first)

The coder v1 `report.md` describes Stages 4-fold-dissolve and the property sweep as **deferred**. They are **not** — four commits land after the report was written:

```
f7790b3a6 coder: wire 5 PLang test contracts + refresh Cut4 TypeProvider.dll fixture
fd6e4e367 coder: stage 2 — architectural decision, ?. defensiveness stays
a94d03a54 coder: stage 4 Entry dissolve — fold complete, no discriminator enum
560931074 coder: Variable record → @this — Roslyn rename + file relocation
ed6f09d48 coder: stage 3c — Roslyn-driven property renames on app/actor/context
```

Verified against HEAD source, not the report:
- **`Entry` / `EntryKind` are gone.** `grep "record Entry|class Entry|enum EntryKind"` over `PLang/app/` → 0 hits. `BuildTypeEntries` now constructs `app.type.@this` instances directly (`type/list/this.cs:438`). Only `Field` survives (`type/Field.cs`) — legitimate sub-entity, not a parallel struct. **The central OBP duplication smell this refactor targeted (two parallel views of type-catalog data) is genuinely closed.** Good.
- **Plural `App.Goals`/`Channels`/`Types`/… properties are gone** from `app/this.cs` and `actor/this.cs`. The Stage-3c symbolic rename completed; no plural aliases linger beside the singulars. Build-clean confirms no half-renamed member sites remain.
- **`Variable` is now `@this`** (`variable/this.cs`), record→class flip done.

So the partial state the report warns about is mostly resolved. My findings below are against what actually shipped.

---

## What's clean (stated so it isn't re-litigated)

- **No type-switching in any registry.** I surveyed all 16 `**/list/this.cs` registries + `module/this.cs` + `type/this.cs`. Zero `is X.` downcasts or `switch`-on-element-kind. The architect's worry (channel I/O type-switching on `channel.stream.@this` inside the registry) is resolved — the switch moved onto the element as a virtual `Write`/`Read`. This was the load-bearing OBP risk of the reshape and it landed correctly.
- **Index-miss hard-throw is consistent across selection registries.** `goal`, `channel`, `format`, `type`, `module` all throw `KeyNotFoundException` on `["nope"]` (goal/list:234,245; channel/list:146; format/list:367; type/list:166; module:144). `variable` and `variable/navigator` return soft defaults — but that is *correct*: reading an undefined variable yielding `NotFound`, and an unknown CLR type falling to a default navigator, are intended PLang semantics, not selection-by-registered-name. The "uniform hard error" rule holds where it should.
- **Entry-fold dissolve is real**, as noted above.

---

## PLang/app/type/this.cs + PLang/app/type/list/this.cs

### Pass 4 — Behavioral · Pass 4.5 — Root-cause smell  ⟶ **NEEDS WORK**

**The two type-entity doors are not equivalent. `app.Type[name]` returns a bare, contextless entity; `data.Type` returns a stamped one. The doc comment claims they're the same, and the test masks the gap by stamping Context by hand.**

- **Symptom / the false promise:** `type/this.cs:13–16` —
  > "Both doors — `data.Type` and `app.Type[name]` — return the same entity shape; `app.Type` resolves names through the registry and **stamps `Context`** …"

- **What the code actually does:** the indexer does **not** stamp Context.
  ```csharp
  // type/list/this.cs:161
  public app.type.@this this[string typeName] {
      get {
          if (Get(typeName) == null)
              throw new KeyNotFoundException($"No PLang type registered under name '{typeName}'.");
          return new app.type.@this(typeName);   // ← no Context, not the catalog-built entity
      }
  }
  public app.type.@this of<T>() => new app.type.@this(GetTypeName(typeof(T)));  // :172 — same
  ```
  Every catalog property routes through `Context`:
  ```csharp
  // type/this.cs
  public IReadOnlyList<Field>? Fields { get => Promote()._fields; ... }   // Promote() bails if Context==null
  private @this Promote() { ... if (Context?.App?.Type == null) return this; ... }   // :≈140
  public System.Type? ClrType => _clrType ?? Context?.App.Type.Clr(Value) ?? AppTypes.GetPrimitiveOrMime(Value);
  public string? Kind => Context?.App.Format.KindOf(Value);
  public ... Scheme => Value == "path" ? Context?.App?.Type?.Scheme : null;
  ```
  So for an entity from the indexer (`Context == null`): `Fields`/`Values`/`Properties`/`Shape`/`Example`/`Description`/`Kinds`/`Scheme`/`Kind`/`Compressible` are **all silently null**, and `ClrType` silently falls to the static `GetPrimitiveOrMime` map (works for primitives; returns **null for any DLL-loaded / user-registered type** like the Cut4 `Money` fixture, where the `data.Type` door would have resolved it through the registry).

- **The test asserts the workaround** (Pass 4.5 witness tell #13 — "the test asserts the workaround"). `PLang.Tests/.../TypeAccessorTests.cs` — every fold-property case manually re-stamps:
  ```csharp
  var t = app.Type["int"];   t.Context = app.User.Context;   // :20-21 (comment: "wire Context to enable")
  var t = app.Type[enum];    t.Context = app.User.Context;   // :48-49
  var p = app.Type["path"];  p.Context = app.User.Context;   // :57-58
  var g = app.Type["goal"];  g.Context = app.User.Context;   // :65-66
  var p = app.Type["path"];  p.Context = app.User.Context;   // :74-75
  var t = app.Type["string"];t.Context = app.User.Context;   // :83-84
  ```
  The "9/9 passing, including 5 Entry-fold properties" in the coder report is true *only because the test does the stamping the production door doesn't*. A production caller writing `app.Type["money"].Fields` gets `null`, not the fields.

- **Why this is a finding, not polish:** no production caller reads catalog props off the indexer door **today** (`grep` confirms: only tests touch `app.Type[...]`/`of<T>()`). But this is precisely the "same-shape second site, silent divergence" case — two doors documented as equivalent, one silently degraded. The first production caller that reaches for `app.Type[name].Description` (e.g. a builder/LLM-catalog path) will get null and not know why, and the test suite won't catch it because the tests stamp by hand. Silent inconsistency is a major even when green.

- **Why it's structural (the root), Pass 4.5 asymmetry tell #14:** `data.@this` stamps the entity on the way out (`data/this.cs:81` `if (_type != null) _type.Context = value;`); the registry **cannot** — `type.list.@this` holds no `App`/`Context` anchor (`BuildTypeEntries` even takes `modules` as a parameter precisely because it has none). The deeper smell is that *catalog/fold data is App-global and actor-independent*, yet it is reached through an `actor.context.@this Context`. Routing global type metadata through an actor context is the over-coupling; the indexer can't satisfy it, so it ships a half-entity.

- **Root-level fix would look like:** one of —
  - (a) the indexer returns the **already-built** entity from the cached `BuildTypeEntries` catalog (matches "first loaded instance" intent; fold props come for free) instead of `new @this(typeName)`; or
  - (b) give the type entity a back-reference to its registry/`App` and resolve catalog/fold off that, reserving `Context` only for genuinely actor-scoped queries — then no door needs a manual stamp and the test's six `.Context =` lines delete.

  (b) is the real fix; (a) is the smaller one that still closes the silent-null. Either way the doc comment becomes true and the test stops asserting the workaround.

### Pass 2 — Simplification (Low)

**`type/this.cs` `Promote()` re-walks the entire action catalog + linear-scans for self.**
```csharp
var entries = Context.App.Type.BuildTypeEntries(Context.App.Module);   // full catalog walk
var match = entries.FirstOrDefault(e => string.Equals(e.Value, Value, OrdinalIgnoreCase));  // O(n) self-find
```
Every *stamped* entity that wasn't itself produced by `BuildTypeEntries` triggers a complete catalog rebuild + scan the first time any fold property is read. `BuildTypeEntries` isn't memoized on the registry (`type/list/this.cs:585` re-invokes it too). For a hot path (data flowing through typed values, each reading `.Fields`/`.Description`) this is a full catalog walk per entity. This is the re-derive-what-upstream-knew smell (#8) in time rather than in storage — the Entry *struct* is gone but the Entry *computation* still runs per-entity-on-demand. Fix rides along with the door fix above: if the indexer/registry hand back cached built entities, `Promote()`'s rebuild path largely disappears. Recommend the registry memoize `BuildTypeEntries`.

---

## Redundant enumeration surfaces (Low — deletion test)

The Stage-3 accessor surface added a canonical `list` enumerator, but two registries now carry a second, apparently-dead enumerator beside it:

1. **`goal/list/this.cs`** — `list` (`IEnumerable<goal.@this>`, :252) **and** `Value` (`IReadOnlyList<goal.@this>`, :299, `.ToList()` — allocates). `Value` predates the branch (Phase-5 origin); `list` is the new canonical surface. No production reader of the goal-list `Value` found (the `Goal.Value` hits in `module/**` are all `Data<…>.Value` lazy-param accessors, a different `.Value`). **Deletion test: removing `Value` breaks no test I can find.** Two enumerators returning the same set, one lazy one eager, is a pick-the-wrong-one footgun.

2. **`channel/list/this.cs`** — `list` (:149) **and** `All` (:154), *both added in the same Stage-3 commit* `273f51ad4`. Tests use `Channel.list` (`ChannelAccessorTests.cs:26`); `All` has no callers in source or tests. Born redundant.

Neither is a behavior bug; both are lines that don't earn their place. Leave the choice of which name survives to the coder, but one of each pair should go.

---

## Verdict: **NEEDS WORK**

The rename itself is well-executed — Entry struct genuinely dissolved, plural aliases gone, no registry type-switching, index-miss consistent, build clean. The blocker is one undisclosed behavioral gap (not on the coder's deferral list): **`app.Type[name]`/`of<T>()` return a contextless half-entity whose catalog properties are silently null, while the doc comment promises door-equivalence and the test masks it with a manual `Context` stamp.** That is a silent same-shape divergence with a latent null trap for the first production catalog-reader, and the test asserts the workaround rather than the contract. Fix the door (return the catalog-built entity, or back the entity by App not actor-Context), then drop the two dead enumerators.
