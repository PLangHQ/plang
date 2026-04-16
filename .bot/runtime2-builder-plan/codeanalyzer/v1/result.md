# Code Analysis: runtime2-builder-plan

**Branch**: runtime2-builder-plan
**Analyzer**: codeanalyzer v1
**Scope**: ~200 changed files — Data<T> composition, return removal, condition orchestration, foreach inline, builder improvements, source generator, new modules

---

## Critical Findings (Action Required)

### 1. Foreach Dictionary Support Silently Dropped

**File**: `PLang/App/modules/loop/foreach.cs`
**Pass**: 4 (Behavioral)

The old foreach had explicit `IDictionary<string, object?>` and `IDictionary` handling with key/value pairs. The new version uses `Collection.AsEnumerable()` which for dictionaries yields `KeyValuePair<,>` objects — the user gets the raw KVP struct as their `%item%`, not the value.

Old:
```csharp
if (collection is IDictionary<string, object?> dict)
    foreach (var kvp in dict)
        yield return (kvp.Key, kvp.Value);
```

New:
```csharp
foreach (var item in Collection.AsEnumerable())
    Context.Variables.Set(variableName, item);
```

PLang developers iterating dictionaries now get `KeyValuePair<string, object?>` objects. `%item%` is the struct, not the value. `%key%` was the dict key, now it's the loop index (count). This is a **silent breaking change** — no error, just different runtime behavior.

**Also**: `KeyName` was the dictionary key before. Now it's always the numeric loop index (`count`). This breaks any PLang code using `foreach %dict%, call X key=%k%` where `%k%` was expected to be the dictionary key.

**Severity**: HIGH — silent data change, no error

---

### 2. Condition Orchestration Re-Entrant Guard Uses Variables

**File**: `PLang/App/modules/condition/if.cs`
**Pass**: 4 (Behavioral)

The orchestration uses `__condition_orchestrating__` stored in `Context.Variables` to prevent re-entrance. But Variables is a `ConcurrentDictionary` shared within the context. If an inner condition runs (e.g., an elseif branch calls a goal that has its own conditions), the guard prevents the inner condition from orchestrating its own step.

```csharp
var alreadyOrchestrating = Context.Variables.Get("__condition_orchestrating__")?.Value is true;
```

This is fine for same-step re-entrance (the guard's purpose), but if an action in a branch body calls a goal that has a condition step with multiple actions, that inner step won't orchestrate. The inner step would need a child context (which `RunGoalAsync` creates), so this may actually be safe — **but only if `RunGoalAsync` always uses a child context with fresh Variables**. Need to verify.

**Mitigation**: If `RunGoalAsync` creates a child context with cloned variables, this is safe. If it shares the same context, inner conditions break silently.

**Severity**: MEDIUM — depends on context isolation

---

### 3. Foreach `result.Handled = true` Suppresses Error Propagation

**File**: `PLang/App/modules/loop/foreach.cs:55`
**Pass**: 4 (Behavioral)

```csharp
var loopResult = Data(new types.loop { itemCount = count, completed = true });
if (bodyActions.Count > 0)
    loopResult.Handled = true;
```

This marks the result as Handled so the Step runner breaks out of the action loop (Step:174 `if (!result.Success || result.Handled) break;`). That's correct for skipping remaining actions that are part of the loop body. But `Handled = true` combined with the step's own check means the step itself won't check for errors on the loop result — it'll trust the Handled flag.

If a body action succeeds but leaves an error condition that the step-level error handler should catch, the `Handled = true` mask prevents it.

**Severity**: MEDIUM — error masking in edge cases

---

### 4. `Data.Value` Getter Has Side Effects (NeedsResolution + Factory)

**File**: `PLang/App/Data/this.cs:177-192`
**Pass**: 2 (Simplification) + 4 (Behavioral)

The Value getter now does three things:
1. Invokes `_valueFactory` (lazy initialization)
2. Calls `_context.Variables.ResolveDeep` (if NeedsResolution is set and value is list/dict)
3. Returns `_value`

This means accessing `.Value` twice on the same Data may return different objects (if ResolveDeep creates new collections). Serialization, comparison, and any code that assumes Value is stable between reads will behave incorrectly.

```csharp
get
{
    if (_valueFactory != null)
    {
        _value = _valueFactory();
        _valueFactory = null;
    }
    if (NeedsResolution && _value != null && _context?.Variables != null
        && (_value is IList || _value is IDictionary))
        return _context.Variables.ResolveDeep(_value);
    return _value;
}
```

**Note**: ResolveDeep for `IDictionary` creates a **new dictionary** every call. For `IList`, it creates a new list. So `data.Value != data.Value` (reference inequality) when NeedsResolution is true.

**Severity**: MEDIUM-HIGH — subtle instability in value identity

---

### 5. ResolveDeep Mutates Typed Object Properties In Place

**File**: `PLang/App/Variables/this.cs:433-447`
**Pass**: 4 (Behavioral)

The new typed-object resolution in ResolveDeep mutates string properties on CLR objects:

```csharp
foreach (var prop in type.GetProperties(...))
{
    if (prop.PropertyType != typeof(string)) continue;
    var strValue = prop.GetValue(value) as string;
    if (strValue == null || !strValue.Contains('%')) continue;
    var resolved = ResolveDeep(strValue);
    if (!ReferenceEquals(resolved, strValue))
        prop.SetValue(value, resolved);
}
```

This modifies the original object's properties. If that object is shared (e.g., an Action's Parameters list that's part of the .pr data), the mutation contaminates the template. Next execution sees already-resolved values where it expected `%var%` references.

The guard `if (value is Data.@this) return value` prevents mutating Data objects, but doesn't protect plain CLR objects referenced by Data values.

**Severity**: HIGH — shared mutable state contamination

---

### 6. `implicit operator @this<T>(T value)` Is Dangerous

**File**: `PLang/App/Data/this.cs:546`
**Pass**: 4 (Behavioral)

```csharp
public static implicit operator @this<T>(T value) => new("", value);
```

This creates a Data<T> with empty Name and no Type. Implicit conversions are silent — a method returning `Data<string>` can now accidentally return just a `string` and it'll compile fine. The resulting Data has no Name, no Type, no Context. Any code that relies on Name for variable storage or Type for conversion will silently fail.

**Severity**: MEDIUM — correctness risk from silent implicit conversion

---

### 7. `_buildTimer` Is Static — Race Condition for Concurrent Builds

**File**: `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:15`
**Pass**: 4 (Behavioral)

```csharp
private static readonly Stopwatch _buildTimer = new();
```

A single static Stopwatch for all builds. If two builds run concurrently (unlikely but possible with `--build` in parallel), the timer is shared and the elapsed times are meaningless.

**Severity**: LOW — cosmetic timing only, not logic

---

## OBP Violations

### 8. `DefaultBuilderProvider.PromoteGroups` — Helper Methods Decompose Objects

**File**: `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:305-370`
**Pass**: 1 (OBP)

`GetString(object step, string key)` and `SetValue(object step, string key, string value)` work with untyped `object` by checking if it's `IDictionary` or `JsonElement`. This is classic OBP violation — the object should own its own property access.

The step objects here come from LLM JSON responses, so they're not domain objects. This is a serialization boundary, which makes the dictionary/JsonElement handling acceptable. But `SetValue` only handles `IDictionary` and silently does nothing for `JsonElement` — an LLM response that comes as JsonElement will have its level never promoted.

```csharp
private static void SetValue(object step, string key, string value)
{
    if (step is IDictionary<string, object?> dict)
        dict[key] = value;
    // JsonElement silently dropped — can't mutate JsonElement
}
```

**Severity**: MEDIUM — silent no-op on JsonElement steps

---

### 9. `list.any` and `list.group` — ExtractProperty Doesn't Handle CLR Objects

**Files**: `PLang/App/modules/list/any.cs:40-54`, `PLang/App/modules/list/group.cs:41-48`
**Pass**: 1 (OBP) + 4 (Behavioral)

Both `ExtractProperty` and `ExtractKey` handle `IDictionary<string, object?>` and `JsonElement` but not CLR objects. If a list contains POCOs (which happens when goal.call returns typed objects), the property extraction silently returns null. For `list.any`, this means the condition always fails. For `list.group`, all items group under key "".

**Severity**: MEDIUM — silent failure for POCO lists

---

### 10. Goal No Longer Inherits Data — `Value` Property Semantics Changed

**File**: `PLang/App/Goals/Goal/this.cs`
**Pass**: 1 (OBP)

Goal changed from `Data.@this<@this>` to `IDataWrappable`. This is the correct OBP direction (composition over inheritance, per data-generic-design.md). However, any code that previously accessed `goal.Name` through the Data.Name property now gets the Goal.Name property directly. This should be fine because `Name` was marked `new` before — but any code that cast a Goal to `Data` and accessed `.Value` will now fail at compile time, which is the right kind of failure (loud, not silent).

The `AsData()` method is correct OBP: `Goal is responsible for its own Data representation`.

**Verdict**: CLEAN — correct architectural change

---

## Simplifications

### 11. `Data.ToBoolean()` — Consider Using a Switch Expression

**File**: `PLang/App/Data/this.cs:401-412`
**Pass**: 2

The cascading if-chain for type checking could be a pattern-match switch. Minor readability improvement:

```csharp
// Current: 12 lines of if/return
// Could be:
return val switch {
    bool b => b,
    string s => s.Length > 0,
    int i => i != 0,
    long l => l != 0,
    // ...
    _ => true
};
```

**Severity**: LOW — style only

---

### 12. `Data.ShallowClone()` Duplicates Clone Logic

**File**: `PLang/App/Data/this.cs:420-435`
**Pass**: 2

`ShallowClone()` and `Clone()` both set the same metadata fields (Error, Handled, Returned, ReturnDepth, Warnings, Signature, Properties, Context, NeedsResolution). The difference is only in value handling (`_value` direct vs `_value.DeepClone()`). Consider a shared `CopyMetadata(from, to)` helper.

But: this is a known PLang pattern — Clone methods are intentionally explicit to avoid missing fields (the "clone family" bug pattern). Keeping them explicit is safer than abstracting.

**Verdict**: Acceptable as-is. Explicit > abstract for clone methods.

---

### 13. `validateResponse.Run()` — Heavy JSON Parsing Inline

**File**: `PLang/App/modules/builder/validateResponse.cs`
**Pass**: 2

The entire method is inline JSON navigation with `JsonElement.TryGetProperty`, `JsonSerializer.Deserialize<List<object>>`, dictionary checks, etc. This is working at the serialization boundary so it's acceptable, but the dual JsonElement/IDictionary handling is repeated per field.

**Severity**: LOW — boundary code, acceptable complexity

---

## Readability

### 14. Variable `__condition_orchestrating__` Is Magic String

**File**: `PLang/App/modules/condition/if.cs`
**Pass**: 3

`"__condition_orchestrating__"` is a magic string in Variables. Consider using `ReservedKeywords` constant.

**Severity**: LOW

---

### 15. Foreach Body Actions Discovery Uses Linear Search

**File**: `PLang/App/modules/loop/foreach.cs:62-73`
**Pass**: 3

```csharp
int myIndex = -1;
for (int i = 0; i < actions.Count; i++)
{
    if (ReferenceEquals(actions[i], __action))
    {
        myIndex = i;
        break;
    }
}
```

This is fine for small action lists (typical: 2-5 actions). But the pattern is duplicated in `condition/if.cs:73-81` (finding myIndex in the action list). Could be a shared method on Actions. Not urgent.

**Severity**: LOW — minor duplication

---

## Behavioral Reasoning (Additional)

### 16. NeedsResolution on Parameter Data — Double Resolution Risk

**File**: `PLang.Generators/LazyParamsGenerator.cs:615` (`__ResolveData`)
**Pass**: 4

```csharp
data.NeedsResolution = true;
return data;
```

The `__ResolveData` method sets `NeedsResolution = true` on the original parameter Data from `__action.Parameters`. This mutates the shared .pr Data object. If the same action is executed twice (retry, loop), the second execution's `__ResolveData` sees the same Data with NeedsResolution already set — but the execution starts by resetting backing fields, so it re-resolves. The mutation of the shared parameter Data is technically a write to shared state.

However, `NeedsResolution` is a boolean — setting it to true twice is idempotent. And the Data.Value getter checks both the flag and the context before resolving. So this is safe in practice.

**Severity**: LOW — idempotent mutation, safe

---

### 17. `Data.As<T>()` Uses Reflection for Resolve Method

**File**: `PLang/App/Data/this.cs:320-330`
**Pass**: 4

```csharp
var resolveMethod = typeof(T).GetMethod("Resolve",
    BindingFlags.Public | BindingFlags.Static,
    null, new[] { typeof(string), typeof(Actor.Context.@this) }, null);
```

This reflection runs on every `As<T>()` call. For hot paths (every parameter resolution), this is expensive. The result should be cached per type.

**Severity**: MEDIUM — performance on hot path

---

### 18. `App.Start()` — Console.ReadLine() in Build Guard

**File**: `PLang/App/this.cs:393-400`
**Pass**: 4

```csharp
Console.Write($"No app found at {AbsolutePath}. Create new app? (y/n): ");
var answer = Console.ReadLine()?.Trim().ToLowerInvariant();
```

Blocks on Console.ReadLine in a potentially headless/CI environment. No timeout. Will hang forever if stdin is closed.

**Severity**: MEDIUM — CI/headless hang

---

### 19. Action.Return Now `[JsonIgnore]` — Intentional, No Backward Compat Needed

**File**: `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs:40`
**Pass**: 4

`Return` changed from `[Store, LlmBuilder, Debug, Default]` to `[JsonIgnore]`. This is intentional — return is removed by design, replaced by `variable.set` + `%__data__%`. Old .pr files must be rebuilt. No backward compatibility needed.

**Severity**: NOT AN ISSUE — by design

---

### 20. Step Action Execution Now Breaks on Handled

**File**: `PLang/App/Goals/Goal/Steps/Step/this.cs:174`
**Pass**: 4

```csharp
// Old:
if (!result.Success) break;
// New:
if (!result.Success || result.Handled) break;
```

This is the mechanism that makes condition orchestration and foreach body work — they set `Handled = true` to stop processing remaining actions. But it means ANY action that sets `Handled = true` will now prevent subsequent actions in the step from running. If a module handler incorrectly sets Handled, it silently swallows the rest of the step.

The `Handled` flag was previously only consumed by the error system. Now it has broader meaning. Need to audit all places that set `Handled`.

**Severity**: MEDIUM — broadened flag semantics

---

## Deletion Test (Pass 5)

### 21. `Data.ConvertValue()` — No Test Coverage Found

**File**: `PLang/App/Data/this.cs:221-227`
**Pass**: 5

```csharp
public void ConvertValue()
{
    if (_value is not string raw || _type == null) return;
    var converted = _type.Convert(raw);
    if (converted != null) SetValueDirect(converted);
}
```

Called from `Variables.Set()` on dot-path navigation. Converts a string-typed Data to its actual type (e.g., json string → dictionary). If I deleted this method, would a test fail? Need to verify.

**Severity**: LOW-MEDIUM — unverified coverage

---

### 22. `Data.AsEnumerable()` — Used Only by Foreach

**File**: `PLang/App/Data/this.cs:289-300`
**Pass**: 5

Single-value wrapping: `return new[] { _value }`. This is the mechanism that lets `foreach %singleItem%` work by treating a non-collection as a one-element list. If deleted, foreach on non-collections would return empty. Is there a test for foreach on a single (non-list) value?

**Severity**: LOW — narrow usage, should have test

---

### 23. `PromoteGroups` — Builder Feature with No Tests

**File**: `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:285-370`
**Pass**: 5

The entire PromoteGroups method (group-level promotion for builder pass 2) has no C# test coverage. It's called from PLang builder goals. "If I deleted this, would any test fail?" — unclear; depends on PLang test coverage of the builder pipeline.

**Severity**: MEDIUM — untested builder logic

---

## Summary

| Category | Count | Critical |
|----------|-------|----------|
| OBP Violations | 3 | 0 |
| Simplification | 3 | 0 |
| Readability | 2 | 0 |
| Behavioral | 12 | 2 |
| Deletion Test | 3 | 0 |
| **Total** | **23** | **2** |

### Critical (Must Fix Before Merge)

1. **Finding #1**: Foreach dictionary support silently dropped — `%key%` is now loop index, `%item%` is KVP struct
2. **Finding #5**: ResolveDeep mutates shared CLR object properties — template contamination

### Should Fix

4. **Finding #2**: Condition orchestration guard may block inner goals (verify context isolation)
5. **Finding #4**: Data.Value getter returns different objects on repeated reads (NeedsResolution creates new collections)
6. **Finding #6**: Implicit operator creates Data<T> with empty Name and no Type
7. **Finding #8**: PromoteGroups.SetValue is no-op for JsonElement steps
8. **Finding #9**: list.any/group don't handle POCO objects
9. **Finding #17**: As<T>() reflection not cached
10. **Finding #18**: Console.ReadLine in build guard hangs in headless/CI
11. **Finding #20**: Handled flag has broadened semantics — audit all setters

### Acceptable

12-23: Low severity, style, or already-safe patterns.
