# Code Analyzer v2 — Re-review of Coder v5 Auditor Fixes

## PLang/App/modules/error/handle.cs

### Fix 1: GoalCall Clone (Auditor F1 — major)

**Before:** `goalCall.Parameters = parameters; goalCall.Action ??= ...` — mutated shared singleton.
**After (lines 115-123):** Creates a new `GoalCall { Name, Description, Parallel, Parameters, PrPath, Action }`.

**Verification:**
- GoalCall has 7 properties: Event (JsonIgnore/event-only), Name, Description, Parallel, Parameters, PrPath, Action
- Clone copies all 6 runtime-relevant properties. Event is correctly excluded — error goals are not event bindings.
- Original GoalCall is never touched. Fix is correct.

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. The comment on line 114 ("Clone — never mutate the shared deserialized GoalCall singleton") explains the *why*, which is appropriate.

### Behavioral Reasoning
- **Concurrent safety:** Under parallel execution, each invocation creates its own GoalCall — no shared state. Fix addresses the root cause.
- **Action stamping (auditor nit F5):** Line 122 still uses `context.Step?.Actions.FirstOrDefault()`. This remains a nit — in multi-action steps it stamps the first action, not the failing one. Not addressed by v5 (was a nit, not requested). Not a regression.

### Deletion Test
Lines 126-132 (PushError to callstack) — if deleted, error history would be lost but execution unaffected. This is intentional diagnostic infrastructure, not dead code.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Steps/Step/this.cs (lines 168-176)

### Fix 2: Modifier Clone Asymmetry (Auditor F2 — minor)

**Before:** Modifier clones copied only Module, ActionName, Parameters.
**After (lines 168-176):**
```csharp
Modifiers = new ActionModifiers(a.Modifiers.Select(m => new Action
{
    Module = m.Module,
    ActionName = m.ActionName,
    Parameters = new List<Data.@this>(m.Parameters),
    Defaults = m.Defaults != null ? new List<Data.@this>(m.Defaults) : null,
    Errors = new List<Info>(m.Errors),
    Warnings = new List<Info>(m.Warnings)
}))
```

**Verification:**
- Parent action clone (lines 160-167) copies: Module, ActionName, Parameters, Defaults, Errors, Warnings.
- Modifier clone now mirrors exactly. Symmetry restored.
- Both use `new List<T>(source)` for shallow copy of collections — consistent pattern.

### OBP Violations
None.

### Simplifications
None.

### Readability
The parallel structure between parent and modifier clone makes the pattern self-documenting.

### Verdict: CLEAN

---

## PLang/App/modules/cache/wrap.cs

### Fix 3: Cached Data Mutation (Auditor F3 — minor)

**Before:** `cached.Name = "__data__"` — mutated cached object by reference.
**After (lines 33-36):**
```csharp
var hit = cached.ShallowClone();
hit.Name = "__data__";
context.Variables.Put(hit);
return hit;
```

**Verification:**
- `ShallowClone()` exists on Data (Data/this.cs:452) — returns a new Data with same Value/Properties but independent identity.
- Mutation is now on the clone, cache entry stays pristine.
- `context.Variables.Put(hit)` publishes the clone, not the original. Correct.

### OBP Violations
None.

### Simplifications
None.

### Readability
Clean. Variable name `hit` clearly communicates cache-hit semantics.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Steps/Step/Actions/this.cs (lines 62-69)

### Fix 4: Leading Modifier Warning (Auditor F4 — minor)

**Before:** Leading modifiers (modifier actions with no preceding executable action) were silently dropped.
**After (lines 64-69):**
```csharp
Step?.Warnings.Add(new Info
{
    Key = "DroppedLeadingModifier",
    Message = $"Modifier '{action.Module}.{action.ActionName}' has no preceding action and was dropped"
});
```

**Verification:**
- Warning uses `Step?.Warnings` — null-safe if Step not set (shouldn't happen in practice, but defensive).
- Info has Key + Message — consistent with other warning patterns in the codebase.
- The modifier is still dropped (correct behavior — nowhere to attach it). Developer now gets a signal.

### OBP Violations
None.

### Simplifications
None.

### Readability
The message is clear and actionable — identifies exactly which modifier was dropped.

### Verdict: CLEAN

---

## Overall Verdict: PASS

All 4 auditor findings are correctly addressed. No new OBP violations, no regressions, no behavioral concerns introduced by the fixes. The GoalCall clone (F1) is the most important fix — it correctly copies all runtime-relevant properties and eliminates the shared-state mutation.
