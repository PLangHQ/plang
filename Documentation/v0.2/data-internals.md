# Data Internals & Source Generator

> Part of the App architecture notes — index in [`good_to_know.md`](good_to_know.md).

## Data.Compare — Structural JSON Diff

`Data.Compare(other)` compares two Data objects by serializing both to JSON and walking the tree. Returns a Data whose Value is a dictionary with:
- `match` (bool) — whether the two objects are structurally equal
- `fields` — per-field comparison results (for objects)
- `items` — per-element comparison results (for arrays)
- `missingFields` / `extraFields` — fields present in one but not the other

Comparison rules:
- Numbers compared as `decimal` to avoid int/long/double boxing mismatches
- Keys are case-insensitive
- Null and missing (Undefined) are treated as equivalent
- Strings compared with `StringComparison.Ordinal`

Used by the builder eval runner to compare `.pr` output against `.golden` files.

---

## IdentityData — Data Subclass

`IdentityData` extends `Data` directly — a pure data record with typed properties (`PublicKey`, `PrivateKey`, `IsDefault`, `IsArchived`, `Created`). It lives on `Actor.Identity` as a property. No lazy resolution, no sync-over-async.

Handlers update `Actor.Identity` directly after mutations (e.g., `setDefault`, `rename`). The `DefaultIdentityProvider.Get()` refreshes `app.System.Identity` when resolving the default identity. `IdentityData.ToString()` returns the public key, so `%MyIdentity%` in a string context gives the public key.

See `PLang/app/module/identity/type/identity.cs` for the class definition.

---

## Source Generator — OBP shape and incremental cache

`PLang.Generators/` mirrors the per-folder `@this` convention used by the runtime. Entry point is `PLang.Generators/this.cs` (`IIncrementalGenerator`); below it the work splits into Discovery (Roslyn boundary) and Emission (string output):

```
PLang.Generators/this.cs                — IIncrementalGenerator entry, source-output stage
  ├ Discovery/this.cs                   — IsActionPartialClass predicate, GetActionClassInfo, BuildProperty factory
  └ Emission/
      ├ Action/this.cs                  — per-handler emitter (shell + ExecuteAsync + __SnapshotParams)
      └ Property/
          ├ this.cs                     — abstract record (EmitProperty, EmitSnapshotEntry)
          ├ Data/this.cs                — Data<T> / plain Data
          ├ Code/this.cs                — [Code]
          └ Legacy/this.cs              — raw-scalar (transitional)
```

**Per-property polymorphism.** `Discovery.BuildProperty` picks one of the three Property leaves per declared property and packs primitive fields into the leaf's record. `Emission.Action.@this` consumes `ActionClassInfo` and dispatches via `ActionProperty.EmitProperty(sb)` / `EmitSnapshotEntry(sb)` — the leaves know their own emission shape.

**Incremental cache stability.** Roslyn's `IIncrementalGenerator` caches by **structural** equality on pipeline outputs. `List<T>` uses reference equality, so two lists with identical contents miss the cache on every recompile. `EquatableArray<T>` (in `PLang.Generators/EquatableArray.cs`) wraps `T[]` with element-wise `Equals`/`GetHashCode`. `ActionClassInfo` is a `record` with `EquatableArray<PropertyBase>`, `EquatableArray<string>`, `EquatableArray<RawScalarValidation>`, `EquatableArray<DiagnosticInfo>` — **no `IPropertySymbol` references leak in**, all fields are primitives. Result: if two compilations produce semantically identical class info, Roslyn reuses cached emission output.

Tracking-name constants (`ActionInfoTrackingName`, `ActionInfoFilteredTrackingName`) on `PLang.Generators.@this` exist so `IncrementalCacheTests` can drive `CSharpGeneratorDriver.WithTrackingName(...)` and assert pre-Where vs post-Where step reuse — a regression of "ActionClassInfo no longer value-equal" is caught by the test.

**Test alias clash with namespace generation.** `PLang.Tests/GlobalUsings.cs` declares heavily-used type aliases:

```csharp
global using Data = global::app.data.@this;
global using Variables = app.variable.@this;  // alias name kept for `%var%`-bag readability
```

Do NOT create test namespaces matching these alias names — `PLang.Tests.app.data` or `PLang.Tests.app.variables` namespaces shadow the type alias for all sibling test files (`CS0118: 'Data' is a namespace but is used like a type`). File-level `using Data = ...` cannot override (CS1537 against the global, and the namespace still wins at sibling scope). Convention: when a test folder mirrors `PLang/app/data/` or `PLang/app/variable/`, use the `*Tests` suffix on the folder/namespace (`PLang.Tests/app/DataTests/`, `PLang.Tests/app/VariablesTests/`). Same applies to any future global alias whose name is also a directory under `PLang/app/`.

---

## Action property kinds (PLNG001 build-time gate)

Action handler properties are constrained at build time. `Discovery.IsValidActionProperty` accepts only:

- **`Data<T>` / `Data`** — the standard form. Resolution flows through `Action.GetParameter(name, context).As<T>(Context)` lazily on first read.
- **`[Code] T`** — eagerly populated from `app.Code.Get<T>()` at the start of `ExecuteAsync`. Used for pluggable infrastructure (HTTP, signing, LLM).

Anything else fails the build with `PLNG001: Property '{0}' on action '{1}' must be Data<T> or [Code]. Raw scalars are not permitted.` The diagnostic carries the full identifier span so IDE squiggles underline the property name, not a one-character mark.

**Why the gate exists.** The pre-v4 generator handled raw `partial string` / `partial int` / etc. with bespoke logic per kind — 700 lines of conditionals, hard to extend, easy to break. PLNG001 narrows the surface so emission lives on two Property leaves with one shape each (`Emission/Property/Data` and `Emission/Property/Code`). The Legacy emitter and `[VariableName]` attribute that bridged this in v4–v6 are gone as of `runtime2-generator-obp` v7.

---

## `app.variable.Variable` — the variable-name carrier

`Variable` is a record (`Name`, `RawValue`, `WasPercentWrapped`) used as the wrapped type in `Data<Variable>` for action parameters that *name* a variable rather than carry its value (write targets, read-by-name lookups: `variable.set`, every `list.*`, `loop.foreach` ItemName/KeyName). It implements `IRawNameResolvable`, a marker that tells `Data.AsT_Impl` to skip its `%var%` substitution branch and call `Variable.Resolve(raw, ctx)` directly. Both `value="%x%"` and bare `value="x"` slot forms collapse to `Variable { Name = "x" }` — symmetric, and works even when the named variable doesn't yet exist (e.g., `set %x% = 5` creating x for the first time).

**Why it exists.** Before this change, `[VariableName] string` was a transitional carve-out for slots whose value the source generator strips `%` from rather than resolving. `Data<Variable>` is the typed form: same payload, but lives in the same OBP shape as every other handler property (`Data<T>`), and provenance attaches at the wrapper level (`Data<Variable>.Signature`) for future signing without a third API shape.

**Implicit string conversion gotcha.** `Variable` defines `static implicit operator string(Variable v) => v.Name`, so `string s = name.Value` works. But `var s = name.Value` infers `Variable`, not `string`. If you need a string-typed local, write `string s = name.Value;` (or extract `.Name` explicitly). The implicit conversion fires at method-call boundaries — `Variables.Get(name.Value)` and `Variables.Set(name.Value, …)` read naturally. For string interpolation, `ToString() => Name` covers it: `$"variable '{name.Value}' missing"` prints the canonical name, not the synthesized record format.

**Nullable variant.** `loop/foreach.cs` `ItemName` / `KeyName` are intentionally nullable. Use `name?.Value?.Name ?? default` because `?.Value` returns `Variable?` which doesn't chain through the implicit operator.

**`WasPercentWrapped`.** Records whether the slot was `%x%` or bare `x` on the wire. Not load-bearing today — surfaces the LLM-emission shape for a future build-time validator that warns on bare-name slot values.

**Missing-name guard (v8).** Non-nullable `Data<Variable>` slots (and any future `Data<T>` where `T : IRawNameResolvable`) get a generator-emitted pre-`Run()` validation that fires `MissingRequiredParameter` ServiceError when the parameter is absent or its `.Value == null`. Plumbed Discovery → `ActionClassInfo.RequiresRawNameResolvable` → `Emission/Action/this.cs` (mirrors `[IsNotNull]`). Closes the silent-NRE path the implicit `string` operator would otherwise take. The `foreach` ItemName/KeyName are skipped via the `!p.IsNullable` filter — those slots are intentionally permissive. Empty-string slot values (`Name = ""`) currently pass the guard (`?.Value == null` is the literal check); pre-v7 surfaced this via `string.IsNullOrEmpty(...)`. Tightening is a noted optional follow-up — not blocking, sits inside the signed-`.pr` trust boundary.

---

## `Data.As<T>` — cycle, depth, ServiceError contract

`Data.As<T>(context)` is the v4 resolution entry point. Three guards plus a `ServiceError` contract; both halves matter for handler correctness.

### The two ServiceError keys

| Key | Status | Trigger | Source |
|-----|--------|---------|--------|
| `VariableResolutionCycle` | 400 | A `%var%` references itself transitively (e.g. `%a%="%b%", %b%="%a%"`) | `[ThreadStatic] HashSet<string>` exact-match cycle detection in `AsT_Impl` |
| `ResolveDepthExceeded` | 400 | An *expanding* chain produces a new string at each level past `ResolveDepthLimit = 32` | Depth check inside the cycle's try/finally |

The HashSet alone misses expanding cycles — `%a%="X-%b%", %b%="Y-%a%"` produces a fresh string each level (`"X-Y-X-Y-..."`), so HashSet membership never trips. Real handler chains go 1–5 levels deep; matrix tests exercise 5 (see `AsT_DeepChain_5Levels_ResolvesCorrectly`). The cap is well above any legitimate use.

### The dual capture pattern (don't break either half)

Generated `Data<T>` getters resolve lazily on first read. When `As<T>` returns `FromError(ServiceError)` for a cycle/depth trip, the FromError-Data lives on the backing field with `.Value = default(T)`. A handler `Run()` body that reads `.Value` proceeds with a default, **masking the resolution error**.

The fix is two-part. Generated emission carries both:

```csharp
// (1) In each Data<T> getter — capture the error as the property is touched:
get {
    if (__Body_backing == null) {
        __Body_backing = __ResolveData("body").As<string>(Context);
        if (!__Body_backing.Success) __resolutionError = __Body_backing;
        __Body_set = true;
    }
    return __Body_backing!;
}

// (2) In ExecuteAsync — surface AFTER Run() completes:
if (__resolutionError != null) return __resolutionError;
var __runResult = await Run();
if (__resolutionError != null) return __resolutionError;
return __runResult;
```

The pre-Run check catches eager-validated raw scalars (Legacy emission writes `__resolutionError` before Run too). The post-Run check catches Data<T> getters that fired *during* Run — which is the common case. **Removing either half re-introduces the silent-default bug.** The auditor's first attempt at this fix proposed (1) only; that was dead code without (2).

### Action-destination carve-out

When `T` is `Action.@this` or `IEnumerable<Action.@this>`, `AsT_Impl` skips the variable walk entirely. Sub-actions hold raw `%var%` strings for *deferred* resolution at their own dispatch — resolving them at outer dispatch would prematurely substitute everything inside the action graph.

### `.Value` is raw

`Data.Value` returns the raw stored value (factory-resolved if any, but never `%var%`-substituted). Substitution happens only inside `As<T>(context)`. Each `As<T>` call resolves freshly against the current variable store — there is nothing to cache and nothing to invalidate. Caching, if any, lives on the caller (e.g. the generator's per-property backing field).

---

## `Action.GetParameter` — pure parameter lookup

```csharp
public Data GetParameter(string name, Actor.Context context);
```

Walks `Parameters` first, falls back to `Defaults`, returns `Data.NotFound(name)` when missing. **Pure lookup — no resolution side effects.** Resolution lives in `Data.As<T>(context)`.

Why the `context` parameter even though the lookup is context-free today: contract symmetry with `As<T>(context)`. Both names "reach into the parameter graph" — a future variant that resolves on lookup (e.g. for handlers that want the resolved Data immediately) keeps the same signature. The hook is cheap; renaming the API later is not.

**Within the source generator**, handlers call `__ResolveData(name)` which delegates to `GetParameter` and stamps the Data's `Context`. From outside, callers (e.g. tests composing actions directly) call `GetParameter` themselves and pipe through `As<T>`.

---

## `ICodeGenerated.SnapshotParams` — default-impl interface method

`ICodeGenerated` declares `List<ParamSnapshot> SnapshotParams() => new();` with an interface default impl. The generator emits a per-handler override that walks each declared property and produces a `ParamSnapshot` (delegating to `EmitSnapshotEntry` on the corresponding `Property` leaf).

**Don't implement `SnapshotParams` by hand.** Same reason handlers don't write `: ICodeGenerated` — the generator owns this surface. The default-impl exists so handlers without parameter properties (e.g. simple infrastructure actions) compile cleanly without a generated override.

`App.Run` calls `handler.SnapshotParams()` from its catch block (and from the success-with-error path) and stamps the result onto `Error.Params` if not already populated. The generator no longer attaches snapshots inside generated `ExecuteAsync` — that responsibility moved to `App.Run` in v4 Phase 3 so all dispatch paths get consistent error context.

---

## Data identity preservation — `As<T>` four wrap rules

`Data.As<T>(context)` does not always allocate. It applies four rules (architect/v1/plan.md §Phase 2; `app/data/this.cs` `WrapAs<T>`):

1. **Same-type fast path** — `this is Data<T>` and `.Value is T` → return `this`. No allocation.
2. **Variance fast path** — `value is T` and `IsPlangAssignable(T, value.GetType())` → new `Data<T>` whose `.Value` is the same reference (cast-only). `Properties`, `OnCreate`, `OnChange`, `OnDelete` aliased from `this`.
3. **Cross-type with conversion** — converted `.Value`, state aliased. `T == IEnumerable` delegates to `Data.AsEnumerable()` so the string-not-iterable rule has one source of truth.
4. **Conversion failure** — `Data<T>.FromError(error)` sentinel; nothing aliased. The post-Run resolution check (see *Resolution semantics* in `data-generic-design.md`) surfaces it.

Aliasing means the four state slots are list refs shared between source and view: `wrapped.Properties.Set(...)` is visible through `source.Properties`; subscribers added to either side fire from either side. Removing any of the four alias assignments in `ConstructWrap<T>` is a silent regression — `--debug={"variables":[...]}` watches and `condition.if`'s `branchIndex` (attached to the result Data via `Properties`) both depend on this.

**Where it lives**: `app/data/this.cs` — `WrapAs<T>` is the dispatch; `ConstructWrap<T>` is the per-rule constructor. Plain `Data` slots bypass `As<T>` entirely via `AsCanonical` (next entry).

---

## `AsCanonical` — plain `Data` slots return the live variable

Handler properties typed as plain `Data` (not `Data<T>`) operate on the *live variable*. The generator emits `__ResolveData(name).AsCanonical(Context)` instead of `As<object>(Context)`. `AsCanonical`:

- **Full match `%var%`** → returns the LIVE variable Data from `Variables.Get(name)`. Mutations to `.Value` on the result IS the variable. `list.add` reads `List.Value as List<object?>`, calls `.Add(...)`, and the live variable sees the change without any explicit write-back.
- **Literal value (no `%`)** → returns `this` (the parameter Data). Same ref.
- **Partial interpolation** (`"hello %x%"`) → fresh `Data` over the interpolated string with the slot Name; state aliased from `this`.
- **Container with nested `%var%`** (list/dict) → walked via `WalkContainerVars`; fresh `Data` with substituted values, state aliased from `this`.
- **Unset `%var%`** → not-initialized `Data` carrying the variable name (so handler diagnostics see "missing %x%", not "missing slot").

The walker is shared with `AsT_Impl` so plain `Data` and `Data<T>` resolve nested vars by the same rule. Drift between the two paths bit `set ... type=json` over a list-of-dicts (coder/v2 fix) — the typed path walked containers, the plain path didn't, so handlers reading `Value.Value` saw literal `"%var%"` strings inside.

`Properties` and event lists are aliased on the partial/container paths so subscribers attached to the slot survive the wrap. The four alias lines on the container-walk branch (`transient.Properties = ...; transient.OnCreate = ...; ...`) are unpinned by tests as of this branch (auditor F2 carryover) — preserve them when refactoring.

---

## `Variables.Set` — events follow the name, Properties stay with the Data

When `Variables.Set(dv)` replaces an existing binding under the same name (`app/variable/this.cs:78-87`):

```csharp
if (_variables.TryGetValue(name, out var prev) && !ReferenceEquals(prev, dv))
{
    dv.OnCreate = prev.OnCreate;   // alias — same list refs
    dv.OnChange = prev.OnChange;
    dv.OnDelete = prev.OnDelete;
    prev.FireOnChange(dv);
}
```

**Events follow the name.** Each `Data` under a name shares the *same* event-list refs as the prev binding. Subscribers added at any point — to source, to any view, before or after replacement — are visible from every alias because they share the same list. This is what makes `--debug={"variables":[{"name":"x","event":"onchange"}]}` see every assignment to `%x%`, not just the first; pinned by `Set_Replace_AliasesPrevOnChangeOntoDv` and the regression test `DebugWatch_OnChange_FiresOnEveryReplacement` in `SubscriberSurvivalTests`.

**Properties stay with the `Data` instance.** They're metadata about the *value* (e.g. `condition.if`'s `branchIndex`, attached to a step's `!data` Data). A new binding starts with its own `Properties` so stale metadata doesn't bleed across re-bindings.

**Idempotent Set.** The `!ReferenceEquals(prev, dv)` guard means setting the same instance twice is a no-op (no double-fire of `OnChange`).

**Inconsistency on the non-Data path.** `Variables.Set(string, object?, Type?)` for a non-Data value mutates the existing Data in place via `existing.Value = value`; the `Value` setter fires `OnChange(this, this)` — same instance for both args. The replacement path fires `(prev, dv)` as two distinct Data; the in-place path fires `(this, this)`. `OnTypeChange` watches via the non-Data path therefore never fire (auditor v2 N1) — but user-visible `set %x% = ...` always goes through `variable.set` → `MintTyped` → Data path, so user variable watches work correctly. Engine paths (`!data` rebinding, `list.add` write-back, settings vars) hit the non-Data path; OnTypeChange on those is best-effort.

---

## `variable.set` is the sole binding-mint site

`app/module/variable/set.cs` owns type inference for user-visible variables. `MintTyped(name, raw, ctx)` switches on the runtime type of the bound value and constructs the right `Data<T>` directly. Hot types (string, bool, int, long, double, decimal, float, DateTime, DateTimeOffset, Guid, byte[], `List<object?>`, `Dictionary<string, object?>`) take the if-chain; cold types fall through to a reflection construction (`typeof(Data<>).MakeGenericType`).

**Mutable refs are snapshot-cloned via JSON roundtrip.** `set %x% = %y%` where `y` is a list/dict mints a `Data<List<object?>>` (or `Data<Dict>`) over a *fresh* container — later mutations of the source do not bleed through. The clone runs through `Data.UnwrapJsonElement` to recursively normalize `List<JsonElement>` (which `JsonSerializer.Deserialize<List<object?>>` produces) into primitives.

**Forced type via `[Type]`** — `set %x% = "42", type=int` calls `TypeMapping.TryConvertTo(value, targetType, ctx)`; conversion failure surfaces as `Data.FromError`, `Variables.Set` is not called, and the binding stays whatever it was. For `type=json`, the value flows through `JsonNode` (`JsonObject` implements `IDictionary<string, JsonNode?>`, NOT `IDictionary<string, object?>`, so it has its own dispatch arm in `TypeConverter`); see *JsonNode in TypeConverter* below.

**Other `Variables.Set` callers exist but don't mint user-named bindings:** `Action.RunAsync` rebinds `!data` per step; `list.add` falls back to `Variables.Set(ListName, list)` on the convert-non-list-to-list path; `cache/wrap.cs` restores cached `!data`. None of these are slots a user would `set %x% = ...` on, so the "sole" framing holds at the user-visible layer.

---

## String-not-iterable — `IsPlangIterable` / `IsPlangAssignable`

C# treats `string` as `IEnumerable<char>`. Plang treats strings as atomic. Two helpers in `app/data/this.cs` enforce this:

```csharp
internal static bool IsPlangIterable(object? value) =>
    value is IEnumerable && value is not string;

internal static bool IsPlangAssignable(Type target, Type source) {
    if (typeof(IEnumerable).IsAssignableFrom(target) && source == typeof(string))
        return false;
    return target.IsAssignableFrom(source);
}
```

Three call sites: `As<T>` variance fast path (so `Data<string>` doesn't variance-cast to `Data<IEnumerable>`), `Data.AsEnumerable()` (single-element wrap), and `Data.EnumerateItems()` (foreach). All route through these helpers so the rule has one source of truth.

**User-visible behaviour**: `foreach %s%` where `s = "hello"` runs the body **once**, with `%item% = "hello"` — not five times with each char. Pinned by `ForeachStringNotIterableTests` (C#) and the deferred `Modules/Loop/Foreach/StringNotIterable.test.goal2` (PLang). `As<IEnumerable>()` on a `Data<string>` falls into Rule 3 and produces a single-element list `["hello"]`.

---

## JsonNode / JsonArray dispatch in `TypeConverter`

`set ... type=json` mints a `Data<JsonNode>` (TypeMapping maps `"json"` → `typeof(JsonNode)`). Downstream typed handlers want concrete types (`LlmMessage`, `List<LlmMessage>`, etc.) so the converter must roundtrip. Caveat: `JsonObject` implements `IDictionary<string, JsonNode?>` (NOT `IDictionary<string, object?>`) and `JsonArray` implements `IList<JsonNode?>` (NOT non-generic `IList`) — neither matches the existing `IDictionary<string, object?>` / `IList` arms in `app/Utils/TypeConverter.cs`'s complex-source dispatch.

Fix lives at `TypeConverter.cs` (~line 336): `JsonNode` joins `IDictionary<string, object?>`, `JsonElement`, `IList` in the complex-source check (so the JSON-roundtrip serialize→deserialize-to-target arm picks it up); `JsonArray` gets its own element-iteration arm parallel to `JsonElement`-array. Pinned by `TypeMappingDictConversionTests`.

**Why this matters cross-cuttingly**: the LLM builder pipeline does `set %messages% = [...], type=json` then passes `%messages%` to a typed handler expecting `List<LlmMessage>`. Without the JsonNode/JsonArray arms, `JsonObject` slipped past every dispatch arm and landed at `TypeMismatch`, which surfaced as an NRE further down in `OpenAi.Query`. Anyone touching the type-conversion dispatch should keep these four arms (`IDictionary`, `JsonElement`, `JsonNode`, `IList`) symmetric — adding a new complex source means adding a parallel arm.

---

## Lazy `Data.Signature` is ICallback-only — the carve-out

When you read `data.Signature`, the getter populates lazily **only if the wrapped value is an `ICallback`**. Plain `Data<T>` keeps `Signature == null` until something explicitly calls `EnsureSigned()` on it.

The carve-out is deliberate. A fully lazy populate would silently break every existing `if (data.Signature == null)` site across the verify path — they'd start succeeding (signature populated) where they previously needed an explicit sign. Restricting auto-populate to `ICallback` keeps the change behavioural-minimum: callbacks cross security boundaries, so they always seal; everything else keeps the explicit-`EnsureSigned` discipline.

`RawSignature` (an internal accessor on `Data.@this`) is the verify-path's hatch: it returns the underlying field without triggering populate, so verify can ask "is there already a signature here?" without changing state.

**If you're tempted to widen the carve-out, audit every `data.Signature == null` site first.** The trip-wires are subtle.

See `Documentation/v0.2/callbacks.md` for the seal-then-verify gate that depends on this discipline.

---

## `RestoredFrame` is a surrogate, not a `Call.@this`

`PLang/app/callstack/RestoredFrame.cs` is the position record callbacks use to identify their resume point. It carries the resolved live `Action` (linked to its Step → Goal in the live `app.goals` registry) plus the positional triple `(StepIndex, ActionIndex, Id)` captured at issue time.

**It is not a `Call.@this`.** It cannot be Pushed into `CallStack`'s AsyncLocal `Current`. It has no Stopwatch, no `OnSet`, no lifecycle. Restoring into a real `Call.@this` would tear up its invariants because Call's ctor is internal and lifecycle-coupled.

The dispatch path is: callbacks read `RestoredFrame` to identify the resume `Position`; `callback.Run` dispatches the bottom frame's Action through `App.Run`, which Pushes a *fresh* live `Call`. The surrogate exists so the snapshot wire shape stays a pure data record — restoring does not mean reconstructing the AsyncLocal frame in place.

---

## `Errors.Push` sets `error.App = this.App` for callback materialisation

`Error.Callback` is a property that materialises an `ErrorCallback` on demand by calling `app.Snapshot()`. For that call to land, the `Error` instance needs a path to the live App tree at the point the callback is asked for — which is later than the point the error was raised.

`PLang/app/errors/this.cs` solves this by setting `error.App = this.App` inside `Push`. Errors plumb through code that doesn't know about App; the back-ref means recovery callbacks can materialise without re-threading App through the throw site. If you reorganise error handling, preserve this assignment — `Error.Callback` reads `App` via this property and silently returning `null` would mask the recovery path.

---

## Action `Run()` returns are typed — and the `Data<T>` implicit-operator footgun

Action handlers declare their return shape on the method signature, not via a separate attribute:

- `Task<Data<T>>` for a concrete T (`Data<string>`, `Data<bool>`, `Data<byte[]>`, `Data<path>`, `Data<List<path>>`, …). Renders in the action catalog as `→ returns T`.
- `Task<Data<object>>` for genuinely polymorphic actions (`goal.call`, `environment.run`, `llm.query`, `callback.run`, `mock.verify`, `loop.foreach`, the `list.*` cascade, `math.*`, `condition.*` operators returning bool/string/…). Renders as `→ returns data`.
- Bare `Task<Data>` for actions that produce no value (`output.write`, `error.throw`, side-effect-only writes). The catalog omits the `→ returns` line and the compile LLM rule treats `write to %x%` after such a step as invalid.

`Modules.Describe()` reads the signature by reflection; `action.@this.ReturnTypeName` carries T's PLang name; the per-step template (`stepActionDetails.template`) renders the return line; the compile LLM uses T as the type-annotation on the trailing `variable.set`'s `Value`. There is no separate `Type=` parameter — the `Data<T>` wrapper carries it.

**The footgun.** `data.@this<T>` defines an implicit operator `@this<T>(T value)` (`PLang/app/data/this.cs`). When `T = object` and the source value is itself a `Data` subtype, the operator silently wraps it — you get `Data<object>{ Value = Data<bool>{ Value = true } }` instead of the inner `Data<bool>` passing through. Bites every method declared `Task<Data<object>>` whose body returns a base `Data` or `Data<U>` it received from another call. Symptom: downstream `Value.As<bool>()` sees a `Data<bool>` where it expected a `bool` and either resolves to `default(bool)` or throws.

Mitigations:

- **For polymorphic forwarding actions** (the body genuinely returns a `Data` produced elsewhere — `goal.call`, `llm.query`, `condition.if` evaluators), stay on bare `Task<Data>` until a `Data.As<T>` passthrough or a `Data<T>.From(Data source)` helper lands. The convention is captured in `todos.md` under *Provider typing follow-ups*.
- **For owned-construction actions** (the body produces the value), explicit factory: `data.@this<object>.Ok(value)`. Never `return innerDataInstance;` from a `Task<Data<object>>` method.

Migration status as of branch `path-polymorphism`: ~60% of handlers typed (73 `Task<data.@this<…>>` vs 48 bare `Task<data.@this>`). The remaining bare handlers are either the polymorphic-forwarding carve-out above or genuinely void; both are correct shapes, not pending work. Audit before flipping a bare signature.

## Truthiness — `IBooleanResolvable` and async condition evaluation

A value's boolean meaning belongs to the value, not to `Data`. `Data.ToBoolean()` is the sync fallback (null/false/0/"" falsy, everything else truthy); **do not** add type-specific cases to it. A type that knows its own truthiness implements `app.data.IBooleanResolvable`:

```csharp
public interface IBooleanResolvable
{
    Task<bool> AsBooleanAsync();
}
```

`path` implements it — truthiness means "does the resource exist". For `FilePath` that's a stat; for `HttpPath` it's a HEAD request. Because the probe can be I/O, the entire condition-evaluation pipeline is **async**:

- `IEvaluator.Evaluate` returns `Task<data.@this>` (`PLang/app/module/condition/code/IEvaluator.cs`).
- `Operator.Evaluate` is `Func<data.@this?, data.@this?, Task<bool>>` (`PLang/app/module/condition/Operator.cs`).
- `assert.IsTrue` / `assert.IsFalse` are async (`PLang/app/module/assert/code/Default.cs:138`).
- `Data.ToBooleanAsync()` dispatches to `IBooleanResolvable` when present and falls back to `ToBoolean()` otherwise (`PLang/app/data/this.cs:896`).

A new operator or evaluator must `await`. A new type that wants scheme-defined truthiness implements `IBooleanResolvable` — never edit `Data.ToBoolean()` to special-case it.

## Recursion guards belong on the value, not on a parallel context layer

When a type's instance can re-enter itself through its own body (a goal
channel whose body writes to channels, a step orchestrator that may
evaluate sub-steps, an event binding that fires on a hook the binding
itself triggers), the question is *where the "I'm already running" bit
lives*. Two shapes appear:

1. **Push a parallel view onto the owning context, resolve against it.**
   Snapshot the registry at boot, override it during the call, swap it
   back. The lookup site asks "which view am I in?" and walks the
   resulting collection.
2. **Put the bit on the instance itself, branch at the lookup site.**
   One `AsyncLocal<bool> _executing` (private) on the instance, one
   public `IsExecuting` getter, one `if … return null` at the registry
   lookup. The registry stays single-view; the instance carries its
   own discipline.

Default to **#2**. It is the OBP-aligned shape: the type that owns the
data owns the rule that guards it.

`channels` on `builder-ergonomics` migrated from #1 to #2:
- **What was deleted** — `Actor.FoundationalChannels`,
  `FreezeFoundational`, `PushChannelsOverride`,
  `AsyncLocal<AppChannels?> _channelsOverride`, `AppChannels.Snapshot`,
  the foundational-lazy-init in the actor.
- **What replaced it** — `Channel.Goal.@this._executing` +
  `IsExecuting` (private field, public getter), one branch in
  `AppChannels.Get`:
  `if (channel is channel.goal.@this g && g.IsExecuting) return null;`

The shipped bug that forced the migration is the canonical failure mode
of shape #1: a foundational snapshot taken on first access froze the
registry to whatever was registered at that moment, making everything
registered later invisible to writes from inside a goal-channel body.
The "boot-snapshot" approach also conflated "guard against
self-recursion" with "see only the boot view of the world", which are
unrelated concerns. The repro is `.bot/builder-ergonomics/foundational-channels-snapshot-bug.md`.

### Tells that you're drifting back into shape #1

- "The lookup needs to know which view it's in" → add a flag on the
  resolved object, branch at the lookup instead.
- "I need an AsyncLocal stack of registries / channels / contexts to
  swap during the call" → if the *only* reason the stack exists is to
  hide the calling instance, an AsyncLocal `bool` on the instance is
  enough.
- "The snapshot is lazy on first access" → a lazy snapshot ties the
  view to whatever wall-clock event happened to fire first. Future
  registrations are silently invisible.

### When shape #1 IS the right answer

If the override needs to expose a **different set of values** (different
channels, different bindings) — not just hide the calling instance —
then a real overlay layer earns its keep. The smell-test: can you
describe the override as "everything stays the same except instance X
acts as if it weren't here"? If yes, shape #2. If you actually need to
substitute X with a different X (mocking, scoped-fixture tests, a real
permission override), shape #1.

Cycle A→B→A under shape #2: A is executing (its `_executing` is true),
A's body writes to B (B is free), B is executing (its `_executing` is
true), B's body writes to A — A is now executing on the same async
context, lookup returns null, `ChannelNotFound` surfaces. The bit flows
down the await chain; no central registry view to swap.
