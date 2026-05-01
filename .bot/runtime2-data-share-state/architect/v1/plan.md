# Plan v1 — Data Identity Preservation + [VariableName] Migration

## What this is

The original todo (`Documentation/Runtime2/todos.md:61`) asked to delete the Legacy emission path and migrate ~25 handlers off `[VariableName]` to typed `Data<T>`. Design discussion uncovered that this can't be done meaningfully without a deeper change first: today's `Data.As<T>()` *always* allocates a fresh wrapper via `ConvertAndWrap`, even on the same-type fast path, and silently drops the source's `Properties`, event subscribers, and memory identity. That violates the "everything is Data" principle. Handlers reading `.Properties` on a typed parameter see an empty bag; subscribers wired through `--debug={"variables":[...]}` only survive replacements via a snapshot copy that doesn't propagate later subscribes.

This branch fixes the identity model first, then does the surface migration as a consequence.

## Principle (the architectural anchor)

Every plang variable IS `Data`. The engine/runtime/emission never touches `.Value` — only modules unwrap, at the leaves. The "source" for identity rules is the **canonical** Data — the live variable when the slot resolves a `%var%`, the parameter Data when the slot is literal.

| Source                            | Target          | Result                                                          |
|-----------------------------------|-----------------|-----------------------------------------------------------------|
| `Data<T>` matches T exactly       | `Data<T>`       | source as-is. Same instance.                                    |
| `Data<U>` where `U : T`           | `Data<T>`       | new `Data<T>` aliasing all state; `.Value` same ref. One alloc. |
| `Data<U>` `U` not assignable to T, or plain `Data` | `Data<T>` | new `Data<T>` aliasing state, **converted** `.Value`.        |
| anything                          | plain `Data`    | source as-is.                                                   |

variable.set is the **only** site that mints fresh independent state — new Properties, new event lists, snapshot-cloned mutable Value refs.

### Three property shapes by intent

Every property on a handler picks one of three shapes based on what the handler reads:

- **plain `Data`** for *variable references* — the handler operates on the live variable. `.Name` = var name (propagated from resolution), `.Value` = live ref. Used by `list.add/get/set/contains/...`, `foreach.Collection`, etc. Mutations to a list/dict in `.Value` are visible because the ref is shared.
- **`Data<string>`** for *literal name slots* — the slot value IS a name string. `.Value` = name. Used by `variable.set.Variable`, `foreach.Item/Key`. The .pr stores the bare name (no percents); the LLM examples teach the bare-name convention. `[VariableName]` and `__StripPercent` go away.
- **`Data<T>`** for *typed value slots* — standard typed wrapper. Used for `list.set.Index` (`Data<int>`), `variable.set.Type` (`Data<string>?`), etc.

## Phases

### Phase 1 — Convert events from C# delegates to Lists

`PLang/App/Data/this.cs` lines 96-119 currently use C# `event Action<...>`. These are immutable multicast delegates and can't be reference-shared between two Data instances. Replace:

```csharp
public List<Action<@this>>       OnCreate { get; set; } = new();
public List<Action<@this, @this>> OnChange { get; set; } = new();   // (oldData, newData)
public List<Action<@this>>       OnDelete { get; set; } = new();
```

`FireOnChange/Create/Delete` bodies become `foreach` over the lists. `CopyEventsFrom` (line 105-110) is **deleted** — superseded by alias-by-assignment.

**Callsites to update** (only two outside Data itself):
- `PLang/App/Debug/this.cs:147,149,151,153` — change `+=` to `.Add(...)`. Pure syntactic.
- `PLang/App/Variables/this.cs:76` — `dv.CopyEventsFrom(prev)` becomes alias-by-assignment (Phase 3).

**Why lists, not a sub-object (`DataState`)**: a wrapper-class-for-fields smells of overengineering and isn't OBP. List<T> is already a reference type — both source and wrapper hold the same ref, `.Add()` on either is visible through both. Same pattern Properties already follows.

### Phase 2 — Rewrite `As<T>` / `ConvertAndWrap` for identity preservation

`PLang/App/Data/this.cs:383` and `:502-510`. Two concerns:

**(2a) Name propagation on full-match resolution.** When the parameter Data's `.Value` is exactly `%var%`, the resulting Data must carry the variable's `.Name`, not the slot's. Today (`Data/this.cs:441-447`) the resolution path fetches `Variables.Get(varName)` then *recurses on `.Value`* (line 447) — the recursion stays on `this` (the slot-named parameter Data), so `ConvertAndWrap` ends up using the slot Name. Refactor so the live variable's Data becomes the canonical source for the wrap, propagating its Name into any constructed wrapper. For partial interpolation (`"hello %x%!"`) and unset `%var%`, fall back to the slot Name.

**(2b) Identity-preserving wrap rules.** Four cases:

1. **Same-type fast path**: if `canonical is @this<T> typed && Type matches`, return `typed`. No allocation.
2. **Variance fast path** (`U : T`): if `canonical is @this<U>` and `IsPlangAssignable(typeof(T), typeof(U))`, construct `new @this<T>(canonical.Name, (T)canonical.Value, canonical.Type, canonical.Parent)` and **alias** Properties/OnCreate/OnChange/OnDelete from canonical. One allocation, `.Value` is the same reference (cast-only, no conversion).
3. **Cross-type wrap with conversion**: source's value can't satisfy T as-is. Construct `new @this<T>(canonical.Name, converted, new Type(typeof(T).PlangName()), canonical.Parent)` aliasing the four state slots. One allocation + conversion of `.Value`.
4. **Plain Data target**: emission for plain `Data` properties skips `As<T>` entirely. The property getter does `__ResolveCanonical(name)` which returns the canonical Data directly (live variable for var ref, parameter Data for literal). No wrap.

**(2c) Plang-specific assignability — the string-not-iterable rule.** `string` implements `IEnumerable<char>` and `IEnumerable`. Under raw C# rules, Rule 2 would treat `Data<string>` as variance-assignable to `Data<IEnumerable>`, letting char iteration happen silently. Plang's rule: **strings are not iterable as collections.** Extract a shared predicate used in three places:

```csharp
internal static bool IsPlangIterable(object? value) =>
    value is IEnumerable && value is not string;

static bool IsPlangAssignable(Type target, Type source) {
    if (typeof(IEnumerable).IsAssignableFrom(target) && source == typeof(string))
        return false;   // string-not-iterable carve-out (matches IsPlangIterable for source string)
    return target.IsAssignableFrom(source);
}
```

The same `and not string` clause lives today inside `Data.AsEnumerable()` (`Data/this.cs:311`) and `Data.EnumerateItems()` (`:345`) — point both at `IsPlangIterable` and `IsPlangAssignable` so the rule has one source of truth.

**(2d) Conversion to IEnumerable — Data owns it (OBP).** When target is `IEnumerable` (Rule 3 cross-type fallback), the wrap doesn't go through generic `TypeMapping.TryConvertTo` — it calls `source.AsEnumerable()` directly. `AsEnumerable` already does the right thing: returns the value as-is if iterable (non-string), wraps in single-element array otherwise (`Data/this.cs:316`), returns empty for null. So Rule 3 for T=IEnumerable becomes:

```csharp
// inside ConvertAndWrap when typeof(T).IsAssignableFrom(typeof(IEnumerable))
var enumerable = source.AsEnumerable();   // OBP — Data owns the conversion
return new @this<T>(source.Name, (T)(object)enumerable, ...) { /* state aliased */ };
```

Data owns its enumeration; the type-resolution layer delegates. No TypeConverter for IEnumerable needed; `Data.AsEnumerable` is the canonical conversion entry point.

For (2) and (3), the wrap construction:

```csharp
var wrapped = new @this<T>(canonical.Name, valueForT, typeForT, canonical.Parent) {
    Context    = ctx,
    Properties = canonical.Properties,   // ALIAS — same ref
    OnCreate   = canonical.OnCreate,     // ALIAS
    OnChange   = canonical.OnChange,     // ALIAS
    OnDelete   = canonical.OnDelete,     // ALIAS
};
```

Requires Data's constructor to *not* unconditionally `Properties = new Properties()`. Lazy-init: `Properties = Properties ?? new Properties()` inside the constructor body, so initializer assignments take precedence. Same for the three event lists.

Conversion failures continue to return `@this<T>.FromError(error)` — sentinel, doesn't alias anything. Today's `__resolutionError` capture path (post-Run check from coder/v6) catches it and surfaces. No change.

### Phase 3 — `Variables.Set` becomes dumb storage; alias on replacement

`PLang/App/Variables/this.cs:38-101`. Today's `Set(string, object?, Type?)` does too much: wraps non-Data values, aliases Data values, fires events, copies events from prev. Strip down:

```csharp
public Data.@this Set(Data.@this dv) {
    var name = CleanName(dv.Name);
    if (_variables.TryGetValue(name, out var prev) && !ReferenceEquals(prev, dv)) {
        // Alias prev's subscribers onto dv — unconditional, every replacement.
        dv.OnCreate = prev.OnCreate;
        dv.OnChange = prev.OnChange;
        dv.OnDelete = prev.OnDelete;
        // Properties? — design call: leaving prev's Properties behind by default, since
        // the new binding is a different value. variable.set is responsible for explicit carry.
        FireOnChange(prev, dv);
    } else if (prev == null) {
        FireOnCreate(dv);
    }
    _variables[name] = dv;
    return dv;
}
```

The non-Data wrapping path (lines 87-93) — modules now construct Data themselves. Drop the overload that takes `(string, object?, Type?)` for the wrap case; or keep it as a thin convenience that calls `Set(new Data(name, value, type))`.

Dot-path support (lines 104+) stays — that's a navigation concern, not a binding-mint concern.

### Phase 4 — variable.set always-types Data<T> via GetType + if-chain

`PLang/App/modules/variable/set.cs`. Handler is the sole site that mints fresh bindings. Steps:

1. Read `Variable.Value` (the user's variable name — string, e.g., `"user"`).
2. Read `Value` (the `Data` parameter holding the value to bind).
3. Determine T:
   - If `Type` parameter is set → use `App.Data.Type.FromName(Type.Value)` and resolve to CLR type (forced/coercion path).
   - Else if-chain on `Value.Value`'s runtime type:
     ```csharp
     bound = value.Value switch {
         string s   => new Data.@this<string>(name, s),
         int i      => new Data.@this<int>(name, i),
         long l     => new Data.@this<long>(name, l),
         double d   => new Data.@this<double>(name, d),
         bool b     => new Data.@this<bool>(name, b),
         decimal m  => new Data.@this<decimal>(name, m),
         DateTime t => new Data.@this<DateTime>(name, t),
         Guid g     => new Data.@this<Guid>(name, g),
         byte[] ba  => new Data.@this<byte[]>(name, ba),
         List<object?> list => new Data.@this<List<object?>>(name, SnapshotClone(list)),
         Dictionary<string, object?> dict => new Data.@this<Dictionary<string, object?>>(name, SnapshotClone(dict)),
         null       => new Data.@this(name, null),
         _          => ConstructViaReflection(name, Value.Value)   // typeof(Data.@this<>).MakeGenericType
     };
     ```
4. Mutable refs (List, Dict): snapshot-clone via JSON roundtrip (already-existing pattern at `list/add.cs:67-69` and `Variables/this.cs:147`).
5. Pass to `Context.Variables.Set(bound)`.

`AsDefault` semantics preserved: check `Variables.Get(Variable.Value).IsInitialized` before constructing/setting.

### Phase 5 — Handler migration to the new shapes

The 25 handlers move off `[VariableName] partial string Foo` + raw scalars onto the **three property shapes** declared in the Principle section. Each handler picks per-slot based on what it actually reads. Touch list:

- **modules/variable/**: clear, exists, get, remove, set
- **modules/list/**: add, any, contains, count, first, flatten, get, group, indexof, join, last, range, remove, reverse, set, sort, split, unique
- **modules/loop/**: foreach (`ItemName`, `KeyName`)

**Pattern A — variable references → plain `Data`** (handler operates on the live variable):

```csharp
// add %product% to %products%
public partial Data List { get; init; }    // List.Name == "products" (propagated), List.Value == [list]
public partial Data Item { get; init; }    // Item.Name == "product", Item.Value == <product>

public Task<Data> Run() {
    var items = List.Value as List<object?>;   // live ref — mutation visible
    items.Add(Item.Value);
    return Task.FromResult(List);              // return the live variable's Data
}
```

No `Variables.Set(name, list)` write-back needed — the list is the live variable's value reference; mutating it IS mutating the variable.

**Pattern B — literal name slots → `Data<string>`** (slot value is a name string):

```csharp
// foreach %product% in %products%
public partial Data Collection { get; init; }      // Pattern A — live %products%
public partial Data<string>? Item { get; init; }   // Pattern B — Item.Value == "product"
public partial Data<string>? Key { get; init; }    // Pattern B — Key.Value == "key"

// handler:
foreach (var x in Collection.Value as IEnumerable) {
    Context.Variables.Set(new Data(Item.Value, x));   // explicit construction
}
```

**Pattern C — typed value slots → `Data<T>`** (standard, unchanged from current Data<T> shape):

```csharp
// list.set ListName=%products%, Index=2, Value=newval
public partial Data List { get; init; }            // Pattern A
public partial Data<int> Index { get; init; }      // Pattern C
public partial Data Value { get; init; }           // Pattern A or whatever
```

**Builder-prompt change** (small, but real): example syntax for Pattern B slots emits the *bare name* (no percents). Today `variable.set` and similar examples emit `Name([string] %data%)` — change to `Name([string] data)`. `foreach`'s example already emits bare (`ItemName([string] item)`), so it's the variable.set / list-handler examples that update.

For variance (e.g., `Data<IEnumerable>` source from `Data<Table>`): the new As<T> rules handle this in Phase 2 — no handler-side concern. Handlers just declare the shape they want; resolution does the right thing.

**Typed-collection migration** — handlers that *consume* a collection (read-only or transform, returning a new value) move to `Data<IEnumerable>` Collection input. Handlers that *mutate the live variable* must stay on plain `Data` (Pattern A) — variance Rule 2 keeps the ref shared (List<object?> : IEnumerable), so mutation works there too, but for clarity and minimal churn the mutating set stays plain. Classification:

- **Mutate-in-place → plain `Data`**: `list.add`, `list.remove`, `list.set`, `list.reverse`, `list.sort`. The handler reads `List.Value as List<object?>` and mutates; live ref preserved.
- **Read-only / transform → `Data<IEnumerable>`**: `list.any`, `list.contains`, `list.count`, `list.first`, `list.last`, `list.indexof`, `list.join`, `list.flatten`, `list.group`, `list.range`, `list.unique`, `list.split`, **and `loop.foreach.Collection`**. The handler reads `Collection.Value` (already an `IEnumerable`, properly converted under Rule 2 or Rule 3).

Once `loop.foreach` declares `Data<IEnumerable> Collection`, the `Collection.EnumerateItems()` path (`foreach.cs:37`) still works — `EnumerateItems` is a Data method that walks dict/list/single-value semantics and isn't displaced by the type change.

**Both `AsEnumerable` and `EnumerateItems` stay** — they're distinct OBP methods serving different consumers:
- **`AsEnumerable()` → `IEnumerable`** — the canonical "convert me to enumerable" method. Used by handlers that just iterate values (count, any, first, last, contains, indexof, join, flatten, group, range, unique, split). After this branch lands, this is also what `As<T>` calls when T=IEnumerable (Phase 2d).
- **`EnumerateItems()` → `IEnumerable<(Data key, Data value)>`** — pair iterator with index/dict-key. Used by `foreach` because the loop binds `Item` AND `Key` per iteration.

Both reference the shared `IsPlangIterable` predicate (Phase 2c) so the string-not-iterable rule has one source of truth.

### Phase 6 — Generator + attribute deletions

- **Delete** `PLang.Generators/Emission/Property/Legacy/this.cs` (~105 lines).
- **Delete** `[VariableName]` attribute and references.
- **Delete** `__StripPercent`, `__Resolve<T>`, `__HasParam`, `RawScalarValidations` from `PLang.Generators/Emission/Action/this.cs:250-298`.
- **Delete** the pre-Run resolution check at `PLang.Generators/Emission/Action/this.cs:232` — once Legacy is gone, `__resolutionError` is only populated during Run by Data<T> getters, so the pre-Run check can never trip. Post-Run check (coder/v6) stays.
- **Update PLNG001 build-time gate** in `PLang.Generators/Discovery/this.cs` to allow only `Data<T>`, plain `Data`, `[Provider] T`. Remove `[VariableName]` from the allowed shapes.
- **Property/Data emission** (`PLang.Generators/Emission/Property/Data/this.cs`): no functional change needed — it already emits `__ResolveData(...).As<T>(Context)` which becomes identity-preserving once Phase 2 lands.

## Test plan

New tests:
- **Identity tests** in `PLang.Tests/App/DataTests/AsTIdentityTests.cs`:
  - **Same-type fast path**: `As<T>()` on Data<T> matching T returns same instance.
  - **Plain Data**: plain Data emission returns canonical (live variable for var ref, parameter Data for literal) as-is — no wrap.
  - **Variance**: `As<IEnumerable>()` on `Data<List<int>>` returns one wrapper instance; `wrapped.Value` IS `source.Value` (ref equality on the underlying list); `wrapped.Properties === source.Properties` (ref equality).
  - **Cross-type wrap**: source.Properties === wrapped.Properties (ref equality); `source.Properties.Add(...)` visible through wrapped.Properties.
  - **Event live propagation**: subscribe to wrapped.OnChange; fire on source; wrapped's subscriber sees it.
- **Name propagation tests** in `PLang.Tests/App/DataTests/NamePropagationTests.cs`:
  - Parameter Data `{name:"List", value:"%products%"}` resolved → result.Name == "products".
  - Parameter Data `{name:"Variable", value:"user"}` (literal) resolved → result.Name == "Variable" (slot, no propagation).
  - Parameter Data `{name:"Greeting", value:"hello %name%!"}` (partial) → result.Name == "Greeting" (slot, partial doesn't propagate).
  - Parameter Data `{name:"X", value:"%missing%"}` (unset var) → result.Name == "missing" (still propagates), result.IsInitialized == false.
- **Replacement-survival tests** in `PLang.Tests/App/VariablesTests/SubscriberSurvivalTests.cs`:
  - Subscribe handlers to placeholder; replace; new Data has same OnChange list ref.
  - Add subscriber AFTER replacement (to either old or new ref) → both refs see it.
- **variable.set type-inference tests** in `PLang.Tests/App/Modules/variable/SetTypeInferenceTests.cs`:
  - `set %x% = "hello"` → variable is `Data<string>`.
  - `set %x% = 42` → variable is `Data<int>` (or `Data<long>` per type rules).
  - `set %x% = %y%` where `y` is List → x is `Data<List<object?>>` AND `x.Value !== y.Value` (snapshot-cloned).
  - `set %x% = %y% (string)` → forced Data<string>; conversion error if y can't convert.
- **Plang-assignability tests** in `PLang.Tests/App/DataTests/PlangAssignabilityTests.cs`:
  - `Data<List<object?>>` → `Data<IEnumerable>`: variance fast path, `wrapped.Value === source.Value`.
  - `Data<string>` → `Data<IEnumerable>`: NOT variance fast path (string-not-iterable rule). Conversion produces single-element list `["the string"]`, `wrapped.Value` is the new array, NOT the original string.
  - `Data<int>` (`42`) → `Data<IEnumerable>`: conversion produces single-element list `[42]`.
  - `foreach %s%` over `s = "hello"` → loop body runs once with the string itself, NOT five times with each char.

Migrated tests:
- All existing tests using `[VariableName]` or raw-scalar handlers update to expect Data<string>-shaped properties.
- Matrix tests under `PLang.Tests/Generator/Matrix/` — verify they still pass (no Legacy path = no Legacy/Modifier/Plain paths firing).
- Existing `DataResolutionTests` and `DataAsTResolutionTests` — verify identity preservation on existing fixtures.

End-to-end:
- `plang '--debug={"variables":[{"name":"x","event":"onchange"}]}'` over a goal that sets x — debug output appears, replacement-survival works.
- `plang p build` runs clean across the system goals.

## Order of execution (commits)

1. Phase 1 (events → Lists) + Phase 2 (As<T> rewrite) — foundational, must land together with green tests. Big single commit, but coherent.
2. Phase 3 (Variables.Set dumb storage) — depends on phase 1 (alias replaces CopyEventsFrom).
3. Phase 4 (variable.set always-typed) — depends on phase 3 (dumb Set means handler constructs Data).
4. Phase 5 (handler migration) — handler-by-handler is fine, each is independent. Can be one big commit or chunked by module folder.
5. Phase 6 (generator + attribute deletes) — last, after all handlers off Legacy. PLNG001 verifies no stragglers.

## Risk / scope notes

- **Sweeping change**: ~30 files touched. Single PR is large but each phase is internally coherent.
- **Identity tests are load-bearing**: the test additions in Phase 1+2 are what prove the principle holds. Without them, regressions are silent.
- **Properties carry on Set replacement**: I left this as "default behavior preserves prev's subscribers but NOT prev's Properties." That's a design call to confirm — alternative is "alias all four (Properties + 3 event lists) on replacement," meaning the new binding inherits the old binding's Properties metadata. Worth a Ingi-check before Phase 3 lands.
- **Reflection cost in Phase 4**: only on the cold path (uncommon types). Hot types (string, int, bool, list, dict) handled by if-chain. Should profile if action-result writes show up in flame graphs.
- **`event.on` plang action for Data CRUD** (the `add on create on %user%, call MyGoal` shape): explicitly **deferred** to a follow-up branch. This branch lands the infrastructure (List<Action<Data>> on Data, alias-on-replacement). The plang-level binding action is its own design pass.

## Open question for Ingi

On Phase 3 (`Variables.Set` replacement): should `Properties` also alias from prev to dv unconditionally (matching the OnCreate/OnChange/OnDelete lists), or stay separate (new binding starts with its own Properties)? My read is **stay separate** — Properties is metadata about the *value*, and a replacement binding has different metadata. But events are about the *name* (subscribers want to track that name across reassignments). Separating them clarifies their roles. Confirm before Phase 3 implementation begins.
