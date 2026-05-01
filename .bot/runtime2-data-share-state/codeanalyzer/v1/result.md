# codeanalyzer v1 — runtime2-data-share-state

Standard 5-pass review **plus** Ingi's lifecycle audit (where Data is created,
where `.Value` is unwrapped, no Data-in-Data, no redundant copies). Branch
landed phases 1–4 + 5a spot-check tests of architect/v1's plan; phases 5b/5c/6
deferred.

---

## Lifecycle audit (Ingi's special lens)

Goal: zero overhead when passing Data between layers. Below is the live trace
of how Data flows through the rewrite.

### Where is Data CREATED?

I grepped `new Data\.@this` across `PLang/App` (79 sites). Every one falls
into one of these categories — the new architecture upholds the "variable.set
is the sole binding-mint site" principle for variables:

| Category                                                  | Sites                                                                                              | OK? |
|-----------------------------------------------------------|----------------------------------------------------------------------------------------------------|-----|
| **variable.set MintTyped** (the one binding-mint site)    | `modules/variable/set.cs:105–119`                                                                  | ✓ |
| **Cross-type cast in WrapAs / ConstructWrap**             | `Data/this.cs:644` (transient — see finding #2), `:662`                                             | ⚠ #2 |
| **Error sentinel via FromError**                          | `Data/this.cs:912`                                                                                 | ✓ |
| **Wraps for unset `%var%` / parameter-Data partials**     | `Data/this.cs:467`, `:477`, `:544`                                                                  | ✓ |
| **Generic `Data.@this<@this>` carriers** (Goal/Step/Action self-reference for handler input)  | `Goal/this.cs:302`, `Step/this.cs:177`, `Action/this.cs:19`                                        | ✓ |
| **Navigators yielding child Data with `parent:`**         | `Data/Navigators/*.cs`                                                                             | ✓ |
| **Modules that produce fresh Data values**                | `file/read.cs:31`, `signing/*`, `http/*` (response props), `llm/*`, `builder/*`, `identity/*`, `setup/this.cs:141`, `Modules/this.cs` (LLM-prompt helpers) | ✓ |
| **TypeConverter at the type-conversion boundary**         | `Utils/TypeConverter.cs:64`, `:308`                                                                | ✓ |
| **Variables.Set wrapping a non-Data value**               | `Variables/this.cs:99`, `:112`                                                                     | ✓ |
| **EnumerateItems pair construction**                      | `Data/this.cs:367, :375, :384, :390, :393` (`WrapItem`)                                            | ✓ |
| **list.add snapshot**                                     | `modules/list/add.cs:69`                                                                            | ✓ (in handler) |
| **list.any operator-comparand wrap**                      | `modules/list/any.cs:23`                                                                            | ⚠ pre-existing — see finding #8 |

**Verdict on creation:** the new code is faithful to the architect's "single
binding-mint site" model. The only redundant new-Data on the new path is
`Data/this.cs:644` (finding #2). Everything else is either a value-producing
boundary (modules creating new domain values) or an iterator/navigator yielding
child Data with the right parent — both legitimate.

### Where is `.Value` UNWRAPPED?

Engine, runtime, and generator-emission code that touches `.Value` is the
litmus test. Grepped across `PLang/App/Goals`, `PLang/App/Actor`,
`PLang.Generators`. Findings:

- **Goals/Goal/Steps/this.cs:161** — `result.Value is bool conditionResult` —
  the Steps runner peeks into the result to decide whether to skip an indented
  block under a falsy condition. Pre-existing; not in this branch's scope, but
  it IS a Rule-7 violation per the architect's principle. Worth a project
  decision: either `result.AsCondition()` lives on Data, or condition steps
  surface a typed property the runner reads instead of `.Value`. **Note for
  Ingi — don't act on this branch.**

- **PLang.Generators/Emission/Action/this.cs:270, :296** — `__Resolve<T>` and
  `__StripPercent` (the Legacy emission helpers) read `.Value`. Architect's
  Phase 6 deletes them. Documented as deferred. ✓

- **PLang.Generators/Emission/Property/Data/this.cs:64, :71** — read `__pr?.Value`
  for the snapshot/error-diagnostics surface. Reading raw input to populate a
  diagnostic record (not for handler logic). ✓

- **All other `.Value` reads** are inside `App/modules/*.cs` (the leaves) or
  inside `Data` itself (the type-resolution machinery is allowed to read its
  own private `_value`). ✓

**Verdict on unwrap:** the new As/AsCanonical path is clean. The pre-existing
`Steps:161` is unchanged by this branch and out of scope.

### Data-in-Data?

No `Data<Data<...>>` or `new Data.@this(otherData)` pattern in production code.
The closest is **Goal/Step/Action's `Data.@this<@this>("", this)`** carriers —
but `@this` there is the Goal/Step/Action class, not a `Data`. ✓

`list/any.cs:23` (pre-existing) does `new Data.@this("", Value.Value)` — that's
an unwrap-and-rewrap of a **value**, not a Data. Still wasteful (could pass
`Value` directly), but not Data-in-Data. Finding #8.

### Redundant copies?

The four As<T> rules are the contract. Walking them:
1. Same-type fast path (`Data/this.cs:623`) — `return sameTyped`. **Zero alloc.** ✓
2. Variance fast path (`:629`) — `ConstructWrap<T>(fast, ctx)`. **One alloc** (the Data<T> wrapper, with `.Value` cast-only and state aliased). ✓
3. T==IEnumerable (`:642`) — **TWO allocs** today: a transient `new @this("", value, _type)` then `ConstructWrap`. **Finding #2.**
4. Cross-type with conversion (`:649`) — `ConstructWrap<T>(converted, ctx)`. **One alloc** + the converted value. ✓

`AsCanonical` also matches the contract: full-match returns the live variable
ref (zero alloc), literal returns `this` (zero alloc), partial allocs one
transient (necessary — the interpolated string is a fresh value).

**Verdict on copies:** clean except finding #2. The IEnumerable branch's
transient is exactly the kind of zero-overhead violation Ingi flagged.

---

## File-by-file findings

## PLang/App/Data/this.cs

### OBP Violations
*None.* The rewrite makes `Data` own its type-resolution and identity-
preservation logic — Rule 1 (behavior on owner) and Rule 7 (relay, don't
repackage) are upheld for the `As<T>` and `AsCanonical` paths.

### Simplifications

1. **Lines 471–473: dead conditional in `AsCanonical` full-match branch**

   ```csharp
   if (resolved == null || !resolved.IsInitialized) { /* notFound */ }
   if (!resolved.Success)
       return resolved;
   return resolved;
   ```

   Both branches return `resolved`. The `if (!resolved.Success)` check has
   no effect. Delete — collapses to a single `return resolved;`. This is
   the literal definition of dead code.

2. **Lines 642–647: redundant transient Data allocation in `WrapAs<T>` IEnumerable branch**

   ```csharp
   if (typeof(T) == typeof(System.Collections.IEnumerable))
   {
       var transient = new @this("", value, _type) { Context = ctx };
       object? convertedEnum = transient.AsEnumerable();
       return ConstructWrap<T>((T?)convertedEnum, ctx);
   }
   ```

   We construct a throwaway Data just to call its `AsEnumerable()` method,
   which only inspects `_value` and uses `IsPlangIterable`. By the time we
   reach this branch, `value != null` is already guaranteed (line 634 catches
   null). Inline the body:

   ```csharp
   if (typeof(T) == typeof(System.Collections.IEnumerable))
   {
       object convertedEnum = IsPlangIterable(value)
           ? value
           : new[] { value };
       return ConstructWrap<T>((T?)convertedEnum, ctx);
   }
   ```

   Saves one allocation per cross-type IEnumerable wrap on the hot path.
   Exactly the "zero-overhead" signal Ingi asked for.

3. **Lines 475–482: AsCanonical's partial-interpolation block re-implements `ConstructWrap`'s "alias state from `this`" pattern**

   ```csharp
   var transient = new @this(Name, interpolated, _type, Parent) { Context = ctx };
   transient.Properties = Properties;
   transient.OnCreate   = OnCreate;
   transient.OnChange   = OnChange;
   transient.OnDelete   = OnDelete;
   ```

   Same shape as `ConstructWrap<T>` minus the generic parameter. Could factor
   into a non-generic `ConstructWrap(value, ctx)` that `ConstructWrap<T>` and
   the partial branch both call. Mild duplication, low priority — the
   awkwardness is that one returns `@this` and the other `@this<T>`, so
   sharing requires either a base helper that the generic wraps or a
   `MemberwiseClone`-style copy. Keep as is unless a third callsite shows up.

### Readability

1. **Lines 619–624: same-type fast path** — `if (this is @this<T> sameTyped && sameTyped._value is T)`. The intent ("fast path: I'm already the right type") is correct, but the second clause `sameTyped._value is T` quietly excludes the case where `_value` is `null` even though `this` is a valid Data<T>. That means unset `Data<int>` calling `As<int>()` allocates a fresh wrapper instead of returning `this`. Acceptable (T is a value type, default(int)=0, so the new wrapper carries the same observable value), but a one-line comment would prevent a future reader from "fixing" this and accidentally returning a default-valued wrapper as the live variable.

### Verdict: **NEEDS WORK** (minor)

Architecturally clean — the identity model is correct and OBP-aligned. Two
real simplifications worth applying (#1 dead code, #2 transient alloc), one
nit. None of these are bugs.

---

## PLang/App/Variables/this.cs

### OBP Violations

1. **Line 71: `Set(name, value, type)` mutates the input Data's Type when value is a Data**

   ```csharp
   if (value is Data.@this dv)
   {
       dv.Context = _context;
       if (type != null) dv.Type = type;   // ← mutates input
       ...
   }
   ```

   `Variables.Set` is supposed to be dumb storage (architect/v1 §Phase 3, the
   stated principle in the comment at line 73-76). Mutating the input Data's
   `Type` is the opposite of dumb — Set takes a side-effect on its caller's
   reference. Coupled with the fact that **no caller in the entire codebase**
   passes a non-null `type` together with a Data `value` (I grepped:
   `PLang/App/**/Variables\.Set([^,]+,[^,]+,`, only `variable/set.cs:82`
   matches and it's single-arg), this branch is effectively dead.

   - **Why it matters:** dead code that mutates a caller's reference is two
     failure modes — first, it implies a contract that Set never honors; second,
     if a caller ever does pass `(name, dv, someType)` it silently mutates dv,
     which violates Rule 7 ("relay, don't repackage").
   - **Fix:** delete line 71. If a caller really needs to set Type, they
     should mutate `dv.Type` themselves before calling Set.

### Simplifications

1. **Lines 150–172: the JSON-roundtrip clone block is duplicated in three places**

   The same pattern (Serialize+Deserialize+UnwrapJsonElement, with CamelCase
   options) lives at:
   - `Variables/this.cs:150–172` (dot-path snapshot)
   - `modules/list/add.cs:63–69` (list-entry snapshot)
   - `modules/variable/set.cs:158–168` (`SnapshotClone` helper)

   Three identical implementations of "deep-clone an arbitrary CLR value via
   JSON roundtrip." This belongs as a static method on `Data` (e.g.
   `Data.SnapshotClone(object) → object?`) so the three callsites collapse
   to one call.

   Concrete payoff: when a fourth handler needs the same pattern (and Phase
   5b/5c will land more such handlers), the helper already exists.

### Readability

*None worth fixing.* The dot-path navigation is intricate but the code is
already commented at the right level.

### Verdict: **NEEDS WORK**

One real OBP violation (#1, deletion-test finding) and one canonical
simplification (#2, three copies of the same JSON-clone helper).

---

## PLang/App/modules/variable/set.cs

### OBP Violations
*None.* This is the architect-defined sole binding-mint site, doing what the
plan asked for. The `MintTyped` if-chain + reflection fallback is the
architect's prescription verbatim.

### Simplifications

1. **Lines 117–118: defensive `??` on snapshot-cloned List/Dict can never fire**

   ```csharp
   List<object?> list                   => new Data.@this<List<object?>>(name,
       (List<object?>?)SnapshotClone(list) ?? new List<object?>()) { Context = ctx },
   Dictionary<string, object?> dict     => new Data.@this<Dictionary<string, object?>>(name,
       (Dictionary<string, object?>?)SnapshotClone(dict) ??
           new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)) { Context = ctx },
   ```

   `SnapshotClone(non-null List/Dict)` cannot return null:
   - `Serialize(non-null)` → non-null JSON string,
   - `Deserialize<object?>(json)` → JsonElement (object/array kind),
   - `UnwrapJsonElement(element)` → Dictionary or List for those kinds.

   The `?? new List<object?>()` and `?? new Dictionary<...>()` fallbacks are
   defensive paranoia. Drop them — the cast suffices. If STJ's behaviour ever
   changes and `SnapshotClone` does return null, a clean NullReferenceException
   surfaces the regression. Better than silent fallback to an empty container.

2. **Three copies of the JSON-roundtrip clone — see Variables.Set finding #2.** Same call. The fix is a `Data.SnapshotClone` static method.

### Readability

1. **Line 42–43: still uses `[VariableName] partial string Name` instead of the new shape.** Documented as deferred per Ingi (the property name `Name` stays, but the shape change to `Data<string>` waits on Phase 5b/6 + a `.pr` rebuild). Leave a one-line comment in the file pointing at architect/v1/plan.md §Phase 5b/6 so the next reader doesn't think it's a missed migration.

### Verdict: **CLEAN** (with one sub-finding noted via Variables.Set #2)

The handler does what it claims. Two cosmetic nits.

---

## PLang/App/Debug/this.cs

### OBP Violations
*None.* The four `+=` → `.Add(...)` switches at `:147,149,151,153` are pure
syntactic translations to the new `List<Action<...>>` shape.

### Simplifications
*None.*

### Readability
*None.*

### Verdict: **CLEAN**

---

## PLang.Generators/Emission/Property/Data/this.cs

### OBP Violations
*None.* The plain-Data slot now emits `__ResolveData("...").AsCanonical(Context)`
instead of `As<object>(Context)` — exactly what architect/v1 §Phase 2 Rule 4
prescribed. Handlers reading `Foo.Value` see the live variable ref for
mutation; literals return the parameter Data; partial interpolations get a
transient with aliased state.

### Simplifications
*None.* The emit logic is already terse.

### Readability
*None.*

### Verdict: **CLEAN**

---

## Pre-existing items observed (NOT in this branch's scope)

1. **`PLang/App/modules/list/any.cs:23`** —
   `var right = Value.Value != null ? new Data.@this("", Value.Value) : null;`
   wraps the unwrapped `.Value` back into a fresh empty-named Data, just to
   pass to `Operator.Value!.Evaluate(left, right)`. `Value` itself is already
   a Data. Could pass `Value` directly. Pre-existing (last touched by the
   builder commit `50351d8b`); not modified by this branch. **Note only.**

2. **`PLang/App/Goals/Goal/Steps/this.cs:161`** — `result.Value is bool conditionResult`
   in the Steps runner. The runner peeks into the result Data to decide
   whether to skip indented sub-steps under a falsy condition. This violates
   the architect's "engine never touches `.Value`" principle. Pre-existing;
   not modified by this branch. Worth a project-level decision: should the
   runner ask `result.AsCondition()` instead, or should condition steps
   surface a typed `IsTruthy` channel? **Note for Ingi.**

3. **`PLang.Generators/Emission/Action/this.cs:270, :296`** — `__Resolve<T>`
   and `__StripPercent` Legacy helpers read `.Value`. Architect's Phase 6
   deletes them. Deferred per coder/v1 (waiting on `.pr` rebuild).

---

## Verdict summary

| File | Verdict |
|---|---|
| `PLang/App/Data/this.cs` | **NEEDS WORK** — 1 dead code, 1 redundant alloc, 1 nit |
| `PLang/App/Variables/this.cs` | **NEEDS WORK** — 1 dead-mutation OBP violation, 1 duplication |
| `PLang/App/modules/variable/set.cs` | CLEAN — 2 nits |
| `PLang/App/Debug/this.cs` | CLEAN |
| `PLang.Generators/Emission/Property/Data/this.cs` | CLEAN |

**Overall:** **NEEDS WORK**. The identity-preservation rewrite is
architecturally correct — Ingi's "every plang variable IS Data" principle is
upheld through the four As<T> rules and AsCanonical. The lifecycle audit
shows the design works: Data is created at the right places, `.Value` is
read at the right places, no Data-in-Data, copies are minimal and justified.

The findings are concrete simplifications that the rewrite missed:

- One dead conditional in `AsCanonical` (literally unreachable code).
- One unnecessary transient `Data` allocation in `WrapAs<T>` for the
  IEnumerable branch (defeats the zero-overhead goal exactly where it
  should shine — the cross-type enumerable wrap).
- One dead-but-side-effecting branch in `Variables.Set` (mutates the
  caller's Data Type while no caller passes type+Data).
- Three copies of the same JSON-roundtrip clone block — should be one
  `Data.SnapshotClone(object)` static method.

None of these block correctness. All five are 5-minute fixes that materially
reduce the surface area of the rewrite.

**Suggested next step:** back to **coder** for the four findings above, then
to **tester** to confirm no behavioural drift.
