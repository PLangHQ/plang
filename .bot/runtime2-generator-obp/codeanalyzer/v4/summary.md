# codeanalyzer v4 — review of coder/v7 (Variable + IRawNameResolvable carve-out)

## What this is

coder/v7 retired the `[VariableName]` attribute and its parallel helper
family (`__Resolve<T>`, `__StripPercent`, `__HasParam`,
`Emission/Property/Legacy/this.cs`). The replacement: a
`App.Variables.Variable` record carrying `(Name, RawValue, WasPercentWrapped)`,
declared as `Data<Variable>` on every former `[VariableName] string` slot.
PLNG001 collapses to a two-rule gate (`Data<T>` / `[Provider] T`). 22
handler property declarations migrated; 5 PLNG001PostMigration tests
activated; 8 new `VariableResolveTests` added.

The architect's plan asserted the existing `Data.As<T>` static-Resolve
dispatch (line 612-624) would route `%x%` slots to `Variable.Resolve`. v7
proved that wrong — TryFullVarMatch intercepts first — and added a
20-line **`IRawNameResolvable` carve-out** to `Data.AsT_Impl` that runs
BEFORE `%var%` substitution when `T : IRawNameResolvable`. Path is
unaffected (doesn't implement the marker).

## What was done

Five-pass review per character file:

1. **OBP Compliance** — CLEAN. Variable + IRawNameResolvable as peer files
   in `App/Variables/` is consistent with the `App/Errors/` precedent.
   Generator's two-leaf shape (Data + Provider) matches the post-v5
   contract. PLNG001 message format updated.

2. **Simplification** — Three findings:
   - **MINOR — Stale comment**: `Emission/Property/this.cs:7` still
     references "LegacyScalarProperty" after the Legacy folder was
     deleted.
   - **MINOR DRY**: The new IRawNameResolvable carve-out
     (`Data/this.cs:549-562`) and the existing Path-style static-Resolve
     branch (`Data/this.cs:632-644`) duplicate their reflection-cache
     lookup + `Invoke` + `ConstructWrap` skeleton. Could extract a private
     `TryStaticResolve<T>` helper.
   - **MINOR DRY**: `App/Modules/this.cs::IsVariableNameSlot` and
     `App/Catalog/ExampleRenderer.cs::LookupParamTypeName` reimplement
     the same "unwrap Nullable + Data<T>, check `T : IRawNameResolvable`"
     predicate. Worth a single helper next to `IRawNameResolvable.cs`.

3. **Readability** — Three NITs around `Variable(string)` ctor doc,
   `loop/foreach.cs` line 28-vs-42 nullable-style asymmetry, and a
   carve-out cross-reference to its sibling path-style branch.

4. **Behavioral Reasoning** — One latent contract trap: if a future
   `T : IRawNameResolvable` is declared without a static `Resolve`
   method, the carve-out's reflection lookup returns null silently and
   execution falls through to `%var%` substitution — the exact path the
   marker was supposed to bypass. Today only Variable implements the
   marker so this can't bite. **NIT** — defensive concern only.

5. **Deletion Test** — All Variable + carve-out + IRawNameResolvable code
   earns its place (tests directly hit each). The only borderline
   candidate is `Variable.WasPercentWrapped`: no consumer today, only a
   comment about "future validators." Per Ingi's "don't design for
   hypothetical future requirements," it could be deleted — but it's a
   single bool capturing wire-form information that's expensive to
   recover later, so the cost is essentially zero. Keep, but flag.

## Findings counts

- **MAJOR:** 0
- **MINOR:** 3 — stale comment (Property/this.cs:7), DRY reflection
  (Data/this.cs duplicate Resolve blocks), DRY predicate
  (IsVariableNameSlot in two places).
- **NIT:** 7 — see `result.md` for full list.

## Verdict

**CLEAN — PASS.** Ready for tester. The MINOR DRY findings are optional
simplifications; the stale comment is a one-line fix. None block.

## Code example — the simplification opportunity

The two static-Resolve dispatches in `App/Data/this.cs`:

```csharp
// Before (current v7) — two near-duplicate blocks at lines 549-562 and 632-644

// Carve-out (lines 549-562)
if (raw is string rawNameStr && ctx != null
    && typeof(IRawNameResolvable).IsAssignableFrom(typeof(T)))
{
    var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), t =>
        t.GetMethod("Resolve",
            BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(Actor.Context.@this) }, null));
    if (resolveMethod != null)
    {
        var resolvedObj = resolveMethod.Invoke(null, new object[] { rawNameStr, ctx });
        if (resolvedObj is T result)
            return ConstructWrap<T>(result, ctx);
    }
}

// Path-style (lines 632-644) — same shape
if (raw is string srStr && ctx != null && raw is not T)
{
    var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), t =>
        t.GetMethod("Resolve",
            BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(Actor.Context.@this) }, null));
    if (resolveMethod != null)
    {
        var resolvedObj = resolveMethod.Invoke(null, new object[] { srStr, ctx });
        if (resolvedObj is T result)
            return ConstructWrap<T>(result, ctx);
    }
}
```

```csharp
// After — extract TryStaticResolve, both call sites collapse
private @this<T>? TryStaticResolve<T>(string raw, Actor.Context.@this ctx)
{
    var resolveMethod = ResolveMethodCache.GetOrAdd(typeof(T), t =>
        t.GetMethod("Resolve",
            BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(string), typeof(Actor.Context.@this) }, null));
    if (resolveMethod == null) return null;
    var resolvedObj = resolveMethod.Invoke(null, new object[] { raw, ctx });
    return resolvedObj is T result ? ConstructWrap<T>(result, ctx) : null;
}

// Carve-out
if (raw is string raw1 && ctx != null
    && typeof(IRawNameResolvable).IsAssignableFrom(typeof(T)))
{
    var r = TryStaticResolve<T>(raw1, ctx);
    if (r != null) return r;
}

// Path-style
if (raw is string raw2 && ctx != null && raw is not T)
{
    var r = TryStaticResolve<T>(raw2, ctx);
    if (r != null) return r;
}
```

Saves ~12 lines, makes the symmetry explicit, and keeps the cache shared
by construction.

## Hand-off

Suggest **tester** next — the verdict is PASS and the test surface
(VariableResolveTests + Plng001PostMigrationTests + the migrated handler
tests) is broad enough that the tester's confidence pass should be
mechanical. The two MINOR DRY findings are optional follow-ups and don't
block testing.
