# codeanalyzer v1 — runtime2-data-share-state

## What this is

Code review of coder/v1's Data identity-preservation rewrite (architect/v1
phases 1–4 + 5a spot-check tests). Standard 5-pass codeanalyzer review **plus**
Ingi's special lifecycle audit: trace where Data is created, where `.Value`
is unwrapped (must be only in module.action), no Data-in-Data wrapping, and
no redundant copies beyond legitimate cross-type casts.

## What was done

Read the five rewrite files end-to-end:
1. `PLang/App/Data/this.cs` — events→Lists, `WrapAs<T>`, `AsCanonical`,
   `IsPlangIterable`/`IsPlangAssignable`, `TryFullVarMatch`, AsT_Impl rewrite.
2. `PLang/App/Variables/this.cs` — Set comment cleanup; Remove fires OnDelete.
3. `PLang/App/Debug/this.cs` — `+=` → `.Add(...)` (4 sites).
4. `PLang/App/modules/variable/set.cs` — full rewrite: `MintTyped` if-chain,
   `CarryStateFromSource`, `SnapshotClone`.
5. `PLang.Generators/Emission/Property/Data/this.cs` — plain-Data emission
   uses `AsCanonical`.

Then traced Data lifecycle across the wider codebase: greppped 79 `new
Data.@this` sites, `.Value` reads in `Goals/`, `Actor/`, `PLang.Generators`.
Build verified clean (0 errors, only existing warnings).

### Findings — 4 concrete cleanups, no blocking issues

**1. Dead conditional in `AsCanonical` full-match branch** (`Data/this.cs:471–473`).
   `if (!resolved.Success) return resolved; return resolved;` — both branches
   identical. Collapse to `return resolved;`.

**2. Redundant transient Data allocation in `WrapAs<T>` IEnumerable branch**
   (`Data/this.cs:642–647`). Constructs a throwaway Data just to call its
   `AsEnumerable()` method. Inline the body:
   ```csharp
   object convertedEnum = IsPlangIterable(value) ? value : new[] { value };
   return ConstructWrap<T>((T?)convertedEnum, ctx);
   ```
   Saves one allocation per cross-type IEnumerable wrap. Exactly the
   "zero-overhead" signal Ingi asked for.

**3. Dead-but-side-effecting Type mutation in `Variables.Set`**
   (`Variables/this.cs:71`). `if (type != null) dv.Type = type;` mutates
   the caller's Data — but **no caller in the codebase passes `type+Data`**.
   The branch is unreachable AND violates Rule 7 ("relay, don't repackage")
   the moment a future caller exercises it. Delete.

**4. JSON-roundtrip clone duplicated three times** — `Variables/this.cs:150–172`,
   `modules/list/add.cs:63–69`, `modules/variable/set.cs:158–168`. Same
   options, same shape. Extract as `Data.SnapshotClone(object)` static method.

### Lifecycle audit — verdict CLEAN

- **Data creation**: 79 sites all justified — variable.set is the sole
  binding-mint site for variables; navigators yield child Data with proper
  parent; modules construct fresh domain values at boundaries; cross-type
  casts in WrapAs/ConstructWrap. Only redundant alloc on the new path is
  finding #2.
- **`.Value` unwrap**: clean inside the new As/AsCanonical path. The two
  pre-existing violations (`Steps:161` reading `result.Value is bool` in the
  engine; Legacy emitter `__Resolve<T>`/`__StripPercent`) are documented as
  out-of-scope or Phase 6 deferred.
- **Data-in-Data**: none in production code. The closest pattern
  (`Data.@this<@this>("", this)`) is Goal/Step/Action self-reference, where
  the inner `@this` is the entity class, not a `Data`.
- **Redundant copies**: As<T>'s four rules each justify their allocs (zero,
  one, two-but-fixable per #2, one). AsCanonical follows the same discipline.

## Code example — the finding that best illustrates the pattern

Finding #2 (the IEnumerable transient) shows what Ingi's lifecycle lens
catches that the standard pass doesn't:

```csharp
// Current — TWO allocations: transient Data, then ConstructWrap.
if (typeof(T) == typeof(System.Collections.IEnumerable))
{
    var transient = new @this("", value, _type) { Context = ctx };
    object? convertedEnum = transient.AsEnumerable();
    return ConstructWrap<T>((T?)convertedEnum, ctx);
}

// After — ONE allocation. value!=null already guaranteed at line 634.
if (typeof(T) == typeof(System.Collections.IEnumerable))
{
    object convertedEnum = IsPlangIterable(value) ? value : new[] { value };
    return ConstructWrap<T>((T?)convertedEnum, ctx);
}
```

The transient existed only to call an instance method on data the wrap had
already extracted. Inlining is straightforward and matches the same
predicates `AsEnumerable` uses internally.

## Verdict

**FAIL — NEEDS WORK** (minor). Architecturally clean, lifecycle-faithful, all
findings are 5-minute fixes. None block correctness.

## Next

**coder** for the four findings; then **tester** to verify the existing 2524
C# tests still pass and no behavioural drift slipped in.
