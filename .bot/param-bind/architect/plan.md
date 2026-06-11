# param-bind: dispatch becomes assignment

**Why:** the generated dispatch layer is a courier that opens packages. Every action parameter goes through resolve machinery before (or while) `Run()` reads it — on runtime2 today that is lazy getters calling `As<T>(Context)` with `__resolutionError` plumbing; on compare-redesign it became dispatch-time `await Value<T>()` on every parameter whether Run touches it or not. Both shapes do the value's work (resolve, convert) in the wrong place: the doors model (stage 9, compare-redesign) makes values able to resolve/parse/convert themselves, so the only job left at dispatch is putting the right Data in the right slot. The current shape also mutates the shared .pr parameter (`data.Context = Context` inside the generated `__ResolveData`) — a cross-execution race, since actions are shared objects. Settled with Ingi in design chat, 2026-06-11.

## The contract (settled)

**Dispatch = assignment.** Binding a parameter to a handler slot is one statement. All logic lives in two runtime members — real C#, written once, debuggable — not in emitted strings.

### `context.Variable<T>(name)` — typed ask on the variable store

```
context.Variable<Path>("path")
        │
        ▼
 look up "path" in variable memory
        │
 already a path? ──yes──► slot = the variable's own Data (same object)
        │                  (binding identity: aliasing, shared sample,
        no                  narrowing all preserved)
        ▼
 Path.Create(value)
        │
 ok? ──yes──► slot = NEW Data<Path>
        │      (%path% in memory unchanged — conversion never rebinds)
        no
        ▼
 slot = Data with Error
```

### `__Bind<T>(paramName)` — one runtime method, used by generated code

```
__Bind<T>("path")
      │
      ▼
 .pr parameter "path"
      │
 full-match %var%? ──yes──► context.Variable<T>(varName)
      │
      no (literal)
      ▼
 T.Create(literal) → Data<T>
```

Absent optional slot → `Uninitialized` (non-null model, as today). `[Default]` → fallback argument on `__Bind`.

### Generated `ExecuteAsync` shape

```csharp
// bind section — one line per slot
Path = await __Bind<path>("path");
Mime = await __Bind<text>("mime", fallback);

if (!Path.Success) return __Fail(Path);     // shape errors stop before Run()
if (!Mime.Success) return __Fail(Mime);

global::app.data.@this result;
try { result = await Run(); }
catch (Exception ex) { return __Fail(ex); }  // bare exception → "module.action: ..." wrap, as today

if (result.Success && __UnobservedParamError() is { } err)
    return err;                              // ruling-8 epilogue: unobserved param error can't be swallowed

return result;
```

Return mapping, `%!data%`, events, modifiers stay in `Action.RunAsync` — untouched by this branch.

### The error split this produces

```
bind time (before Run)             inside Run (the doors)
─ missing required param           ─ load fails (file gone)
─ conversion declines              ─ parse fails (bad json)
  → return, Run never starts,        → Data.Error at first use
    no partial side effects          → handler observes it, or the
                                       ruling-8 epilogue surfaces it
```

Shape errors at bind, content errors at use. The bind line itself calls no door: the identity tier is a lookup, the conversion tier is T.Create on what is in memory (it may await `Value()` only when conversion genuinely needs content — rare, and work the handler would have paid at first use anyway).

## Leaf trace — incumbent and call sites

The incumbent is the resolve machinery in `PLang.Generators`, two shapes depending on what is merged when work starts:

- **runtime2 today** (`Emission/Property/Data/this.cs`, 88 lines): lazy getter bodies, four branches (plain `AsCanonical` / nullable / `[Default]` / required), each calling `__ResolveData(name).As<T>(Context)` sync inside the getter, setting `__resolutionError`.
- **compare-redesign** (same file, 136 lines): `EmitDispatchResolve` — same four branches moved to dispatch time, `await Value<T>()` per parameter inside `__ResolveParameters`, getters reduced to backing reads with fallback.

Call sites of the machinery:

| Site | Disposition |
|---|---|
| Every generated handler (`ExecuteAsync`) | bind section replaces resolve — one `__Bind` line + success check per slot |
| `SetAction` (validate pass, `Build()`) | shell stays; its resolve call becomes the same bind section |
| Direct C# composition (init-supplied slots) | unchanged — init wins, bind respects already-set slots |
| Generated getter fallback (`Uninitialized`/default mint) | dies — slots are plain properties; bind (or init) is the only writer |
| `__ResolveData`'s `data.Context = Context` | dies with `__ResolveData`; fixes the shared-.pr race |

Validation seam stays split from construction: `[IsNotNull]` / `MissingRequiredParameter` remain Peek-based presence checks before the bind section, exactly as today.

## Demolition worklist

**Dies with the core slice** (`PLang.Generators/Emission/Property/Data/this.cs`):

- the four resolve branches (getter-time on runtime2 / `EmitDispatchResolve` on compare-redesign) → one emitted bind line
- `SetFlag` machinery + getter fallback mint → plain property; backing reset per call stays (handler instances are reused)
- `DefaultExpr` cast chain → `[Default]` becomes a `__Bind` fallback argument

**Dies with the core slice** (`PLang.Generators/Emission/Action/this.cs`):

- `__resolutionError` field + the double pre/post-Run checks → per-slot success check before Run + ruling-8 epilogue after Run
- emitted `__ResolveData` helper (`EmitLegacyHelpers`) → `__Bind` in runtime C#, including the `AsCanonical` full-match hop
- `__PrefixActionContext`'s pre-Run use → dies; `__Bind` errors carry param name + module.action context at creation. The catch-wrap keeps its context prefix.
- `__ResolveParameters` as a resolve loop → becomes the bind section; whether it stays async depends only on the conversion tier (coder's call)

**Stays:**

- marker auto-provisions (Context/Channel/Action/Step/Static), channel-name resolve, `[Code]` eager provisioning
- `[IsNotNull]` + `MissingRequiredParameter` presence guards (sync, Peek)
- `__SnapshotParams` (already Peek-based), `Data()`/`Error()` helpers
- backing-field reset per call in `ExecuteAsync`/`SetAction`
- `Action.GetParameter` (pure lookup — `__Bind`'s first step)

**New runtime members:**

- `Variable<T>(name)` on the variable store — two tiers per the diagram; conversion delegates to T.Create (the type's own knowledge, stage-9 home); never rebinds the variable
- `__Bind<T>(paramName, fallback?)` — placement (emitted-once helper vs runtime type) is the coder's call
- `__UnobservedParamError()` — reads the ruling-8 observed flag across the handler's slots (the flag itself ships with stage 9)

## Dependencies and sequencing

- **Hard dependency: stage 9 (compare-redesign) merged into runtime2.** T.Create, the doors, `Value<T>`, the observed-error flag (ruling 8) all live there; this branch is plan-first and rebases onto runtime2 once that merge lands. Code references above name both shapes so the demolition list survives the rebase.
- The contract is independent of who emits the bind lines. This branch keeps the handler-side generator emitting them; a possible future .pr→C# emitter would inline the same assignments — explicitly out of scope here.

## Open questions (for Ingi before coding starts)

1. **Literals born typed in the .pr.** The builder knows the slot's type at build time, so literals could be stored already-typed — conversion failures caught at plang build, runtime bind = pure mint, no T.Create on the literal arm. Raised in chat, not ruled.
2. **`Variable<T>` surface.** Is the typed ask bind-only, or also a public surface for other consumers (goal.call argument mapping, events)?

## You own this

Code shapes in this plan (`__Bind` signature and placement, `__Fail`, the epilogue mechanics, async-or-not on the bind section) are suggestions — the coder owns the final shape. Fixed contracts: the two-tier `Variable<T>` semantics (identity hands over the variable's own Data; conversion never rebinds), one bind statement per slot with no door calls on the identity tier, shape errors return before Run, the ruling-8 epilogue after Run, presence guards stay pre-bind, `RunAsync`'s lifecycle untouched.
