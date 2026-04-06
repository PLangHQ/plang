# Code Analysis v3 — Fresh-Eyes Review

This is a fresh-eyes re-analysis of the builder module. The v1/v2 analysis found 5 minor findings (4 resolved, 1 deferred). This review starts from scratch and asks: **what did I miss?**

---

## Finding 1: `Describe()` leaks `[Provider]` properties into LLM builder prompt

**File**: `PLang/App/Modules/this.cs`, lines 151-175
**Severity**: Medium (affects builder quality for ALL modules)

### The problem

`Describe()` scans all public instance properties of action types but only filters out `EqualityContract` and `Context`:

```csharp
foreach (var prop in parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
{
    if (prop.Name == "EqualityContract" || prop.Name == "Context") continue;
    // ... adds prop to parameter list
}
```

Every action handler has a `[Provider]` property (e.g., `public partial IBuilderProvider Builder { get; }`). This property is auto-resolved by the source generator at runtime — it is **not a user-facing parameter**.

But `Describe()` includes it in the output. The LLM builder prompt sees something like:

```
builder.goals: Path (string?), Builder (IBuilderProvider)
builder.validate: Actions (Actions), Builder (IBuilderProvider)
condition.if: ... Assert (IAssertProvider)
http.request: ... Http (IHttpProvider)
```

### Why it matters

The LLM sees `Builder: IBuilderProvider` as a parameter it should map from the user's step text. This is noise at best, and could cause wrong .pr output at worst. This affects **every module**, not just builder — every handler with `[Provider]` leaks its provider property.

### Fix

Add `[Provider]` to the skip list:

```csharp
if (prop.Name == "EqualityContract" || prop.Name == "Context") continue;
if (prop.GetCustomAttribute<modules.ProviderAttribute>() != null) continue;
```

### Deletion test

The `Describe()` output feeds `BuildGoal.llm`. No C# test verifies that `[Provider]` properties are excluded. A test like `Describe_ExcludesProviderProperties` would prove this.

---

## Finding 2: `Step.Clone()` drops Action.Defaults, Errors, Warnings

**File**: `PLang/App/Goals/Goal/Steps/Step/this.cs`, lines 77-83
**Severity**: Minor (latent — no current callers, but public method on core entity)

### The problem

`Step.Clone()` deep-copies Actions but only copies 4 of 7 `[Store]` properties:

```csharp
Actions = new Actions.@this(Actions.Select(a => new Action
{
    Module = a.Module,        // copied
    ActionName = a.ActionName, // copied
    Parameters = new List<Data>(a.Parameters), // copied
    Return = a.Return != null ? new List<Data>(a.Return) : null // copied
})),
```

**Missing from clone:**
| Property | Attribute | Severity |
|----------|-----------|----------|
| `Defaults` | `[Store]` | Dropped — lost after Validate fills them |
| `Errors` | `[Store]` | Dropped — errors set by builder lost |
| `Warnings` | `[Store]` | Dropped — warnings lost |
| `ParameterSchema` | `[JsonIgnore]` | Acceptable — runtime-only |
| `Cacheable` | `[JsonIgnore]` | Acceptable — defaults to `true` |

### Why it matters

This is the **clone family audit pattern** that has appeared on 3+ previous branches. If `Step.Clone()` is called after the builder's `Validate` action fills `Defaults`, the cloned step loses all default values. The builder would need to re-validate, but nothing enforces that.

### Fix

Add the missing fields:

```csharp
new Action
{
    Module = a.Module,
    ActionName = a.ActionName,
    Parameters = new List<Data>(a.Parameters),
    Return = a.Return != null ? new List<Data>(a.Return) : null,
    Defaults = a.Defaults != null ? new List<Data>(a.Defaults) : null,
    Errors = new List<Info>(a.Errors),
    Warnings = new List<Info>(a.Warnings)
}
```

### Deletion test

No test calls `Step.Clone()`. Could delete the entire method and no test fails. However, it's a public method on a core entity — it WILL be called.

---

## Finding 3: Dead code in `Parse()` — tab check after tab replacement

**File**: `PLang/App/Goals/Goal/this.cs`, line 314
**Severity**: Nit

### The problem

Line 203 replaces all tabs with spaces:
```csharp
text = text.Replace("\t", "    ");
```

Line 314 checks for tabs in continuation line detection:
```csharp
if (currentStep != null && raw.Length > 0 && (raw[0] == ' ' || raw[0] == '\t'))
```

The `|| raw[0] == '\t'` branch is dead code — by the time we check individual lines, all tabs are already spaces. It's harmless but technically unreachable.

---

## v1/v2 Assessment

The original 5 findings were all correct:
1. Implicit Start goal — **correctly identified, fixed with test**
2. Bare dash — **correctly identified, fixed with test**
3. Activator.CreateInstance — **correctly identified, fixed with try/catch**
4. IConfigure<T> untested — **correctly identified, fixed with test**
5. Runtime1 type reference — **correctly identified, deferred**

The v2 re-review was accurate. No false positives.

---

## Summary of New Findings

| # | File | Line(s) | Finding | Severity |
|---|------|---------|---------|----------|
| 1 | Modules/this.cs | 151-175 | `Describe()` leaks `[Provider]` properties into LLM prompt — affects ALL modules | medium |
| 2 | Step/this.cs | 77-83 | `Step.Clone()` drops Action.Defaults/Errors/Warnings | minor |
| 3 | Goal/this.cs | 314 | Dead `'\t'` check after tab replacement | nit |

---

## Overall Verdict: NEEDS WORK

Finding #1 is medium severity — it pollutes the LLM builder prompt for every module. Finding #2 is the recurring clone family pattern. Both should be fixed before this module ships.
