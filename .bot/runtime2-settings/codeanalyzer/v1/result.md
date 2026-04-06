# Code Analyzer v1 — Full Analysis

Branch: `runtime2-settings` vs `runtime2` merge base.
Three passes per file: OBP compliance, simplification, readability.

---

## PLang/App/Engine/Settings/ISettings.cs

### OBP Violations
None. Marker interface — no behavior to misplace.

### Simplifications
None needed. 18 lines, 3 of which are the interface itself.

### Readability
Good XML doc explaining the source generator contract (read side, write side, manifest).

### Verdict: CLEAN

---

## PLang/App/Engine/Settings/this.cs

### OBP Violations

1. **Line 52-54: Module prefix derivation via namespace string parsing**
   - Current:
     ```csharp
     var fullName = typeof(T).Namespace ?? "";
     var lastDot = fullName.LastIndexOf('.');
     var modulePrefix = lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
     ```
   - This derives the module name from the CLR namespace. `App.actions.archive` → `"archive"`. The module identity is extracted from a string rather than being a property on the type itself.
   - **OBP perspective:** This is "extracting a field" from the type system rather than asking the object. If `ISettings` had a `string Module { get; }` property, each settings class would declare its own module name.
   - **However:** The namespace-based convention is consistent with how the handler registry works (`[Action]` types use namespace-based module discovery in `Library.Discover`). Adding a `Module` property to `ISettings` would duplicate what the namespace already provides.
   - **Verdict: Acceptable convention.** Same pattern used elsewhere in the codebase. If the convention ever breaks (settings class in a non-standard namespace), it would need a fix. Consider a comment noting this assumption.

### Simplifications

1. **Lines 25-43: `Resolve<T>` — clean scope chain walk**
   - while loop walks context.Parent chain, then falls back to Defaults, then classDefault. Three-level resolution in 18 lines.
   - **Observation:** The cast `(T)value` (lines 34, 40) will throw InvalidCastException if the stored value isn't actually T. No defensive check.
   - **Recommendation:** Consider using `value is T typed ? typed : classDefault` instead of a hard cast. If a settings key gets written with the wrong type (e.g., int instead of long), the hard cast will produce a confusing runtime error.

2. **Lines 63-74: `Set` — simple dispatch**
   - Clean. Two paths: isDefault → engine Defaults, else → context scope with lazy init.

### Readability

1. **Lines 9-11: Navigation example in XML doc**
   - `engine.Settings.For<archive.Settings>(context).Max` — excellent. Shows the full navigation path.

2. **Line 71: Lazy init `context.SettingsScope ??= new Scope()`**
   - Clean pattern. Settings scope only allocates when written to.

### Verdict: CLEAN
Well-structured. One defensive improvement opportunity (hard cast in Resolve).

---

## PLang/App/Engine/Settings/Scope.cs

### OBP Violations
None. Simple key-value store, owns its own data.

### Simplifications
None. 29 lines, three methods. Minimal.

### Readability
Clean. ConcurrentDictionary with case-insensitive keys. Consistent with Variables's approach.

### Verdict: CLEAN

---

## PLang/App/Engine/Settings/ModuleView.cs

### OBP Violations
None.

### Simplifications

1. **Lines 15-26: ModuleView stores `_settings`, `_context`, `_modulePrefix`**
   - Three fields. This is a lightweight view object — created per `For<T>()` call. No caching, no state mutation.
   - The view delegates to `_settings.Resolve` with the prefixed key. Clean single-responsibility.

2. **Line 34: Key construction `$"{_modulePrefix}.{propertyName}"`**
   - String interpolation on every Resolve call. For hot paths this allocates. But settings resolution is not a hot path — it's called once per handler execution at most.
   - **Verdict: Fine.**

### Readability
Clean. XML doc shows usage example.

### Verdict: CLEAN

---

## PLang/App/actions/archive/Settings.cs

### OBP Violations
None.

### Simplifications
None needed. Two properties with defaults. The class is `partial` — source generator will add the scope-chain resolution.

### Readability

1. **Lines 17-27: Properties with PLang syntax examples**
   - `/// PLang: "set max gzip size to 20mb"` — good. Shows how the PLang developer will use this setting.

2. **Line 21: Default value `100 * 1024 * 1024`**
   - Readable calculation instead of magic number `104857600`. Good.

### Verdict: CLEAN

---

## PLang/App/actions/archive/types.cs

### OBP Violations
None.

### Simplifications
None.

### Readability

1. **Record `settingsResult` — lowercase name**
   - Follows the handler types convention (lowercase action names). Consistent.

### Verdict: CLEAN

---

## PLang/App/Engine/this.cs (diff only)

### OBP Violations
None.

### Simplifications
None. Clean addition: `Settings` property + constructor init.

### Readability
Follows established Engine property pattern. Good XML doc with navigation example.

### Verdict: CLEAN

---

## PLang/App/Engine/Context/PLangContext.cs (diff only)

### OBP Violations

1. **`SettingsScope` is a nullable mutable property on PLangContext**
   - `public Settings.Scope? SettingsScope { get; set; }`
   - PLangContext is per-request, so this is per-request state as a property — which is correct per Rule 4. The scope is lazy-initialized when written to.
   - **Verdict: Not a violation.** PLangContext IS the per-request state container.

### Simplifications
None. Clean addition.

### Readability
Good XML doc explaining the resolution chain.

### Verdict: CLEAN

---

## PLang/App/Engine/Goals/Goal/Methods.cs (diff only)

### OBP Violations
None.

### Simplifications

1. **Lines 29-30 + 89: Save/restore pattern for SettingsScope**
   - ```csharp
     var savedSettingsScope = context.SettingsScope;
     context.SettingsScope = null; // new goal starts with no local settings
     // ...
     finally { context.SettingsScope = savedSettingsScope; }
     ```
   - This follows the existing save/restore pattern for `context.Goal` and `context.Step`. Consistent.
   - **Observation:** The comment says "new goal starts with no local settings; inherits via Parent chain." But `context.Parent` isn't set here — the SAME context object is reused with its SettingsScope nulled out. The inheritance via Parent chain works because `Resolve` walks `context.Parent`, which was set when the context was created (for child goals via `CreateChild`). For same-context goal calls (like `RunGoalAsync(goal, context)`), there IS no parent chain — the SettingsScope is just nulled.
   - **Question:** If goal A sets a setting, then calls goal B on the same context, does B see A's setting? With this code: no — SettingsScope is nulled. After B returns, A's scope is restored. This is the intended goal-scoping behavior.
   - **Verdict: Correct.** The save/restore correctly scopes settings to individual goal executions.

### Readability
Clean. Follows established pattern.

### Verdict: CLEAN

---

## PLang/App/GlobalUsings.cs (diff only)

### OBP Violations
N/A.

### Readability
Two new aliases: `EngineSettings` and `SettingsScope`. Follow established naming pattern.

### Verdict: CLEAN

---

## PLang.Tests/App/Engine/Settings/SettingsTests.cs

### Readability

1. **Well-structured test hierarchy** — tests the full resolution chain: class default → engine default → goal scope → parent inheritance → child override.

2. **`CreateEngine()` helper** — clean factory, used consistently.

3. **Test names clearly describe the scenario** — `Resolve_ChildGoalScope_OverridesParentGoalScope`.

### Verdict: CLEAN

---

## PLang.Tests/App/Engine/Settings/ScopeTests.cs

### Readability
Straightforward: set/get, null when not set, contains, case-insensitive. Clean.

### Verdict: CLEAN

---

## PLang.Tests/App/Engine/Settings/ModuleViewTests.cs

### Readability

1. **Good isolation test** — `ModuleView_DifferentContextsGetDifferentValues` verifies that concurrent contexts see independent views. Important correctness test.

2. **Uses `ArchiveSettings` alias** — clean per-file alias.

### Verdict: CLEAN

---

## Tests/App/Settings/SetMaxGzipSize/Start.test.goal

### Readability
Three lines: set, get, assert. Good PLang-level integration test.

### Verdict: CLEAN

---

# Cross-Cutting Findings

## Finding 1: Hard cast in Resolve<T> (Medium)
`Settings.Resolve<T>` does `(T)value` which throws InvalidCastException on type mismatch. Should use `value is T typed ? typed : classDefault` for safety. A PLang developer writing `set max gzip size to "twenty"` would produce a confusing cast error instead of falling through to the default.

**File:** `PLang/App/Engine/Settings/this.cs:34,40`

## Finding 2: Module prefix assumption (Info)
`For<T>()` derives module name from namespace. Works because handler discovery uses the same convention. But if a settings class is placed in a non-standard namespace, the prefix will be wrong. A comment noting this assumption would help.

**File:** `PLang/App/Engine/Settings/this.cs:52-54`

## Finding 3: No test for type mismatch in Resolve (Low)
No test verifies what happens when a setting value has the wrong type. Related to Finding 1 — if the cast is changed to a safe pattern, a test should verify the fallback behavior.

**File:** `PLang.Tests/App/Engine/Settings/SettingsTests.cs`

---

# Summary

| File | Verdict |
|------|---------|
| Settings/ISettings.cs | CLEAN |
| Settings/this.cs | CLEAN |
| Settings/Scope.cs | CLEAN |
| Settings/ModuleView.cs | CLEAN |
| archive/Settings.cs | CLEAN |
| archive/types.cs | CLEAN |
| Engine/this.cs (diff) | CLEAN |
| PLangContext.cs (diff) | CLEAN |
| Goal/Methods.cs (diff) | CLEAN |
| GlobalUsings.cs (diff) | CLEAN |
| SettingsTests.cs | CLEAN |
| ScopeTests.cs | CLEAN |
| ModuleViewTests.cs | CLEAN |
| Start.test.goal | CLEAN |

**Overall: 14 CLEAN, 0 NEEDS WORK.** No OBP violations. Three findings (1 medium, 1 low, 1 info).

This is a clean, minimal feature. The settings subsystem follows OBP correctly: Engine owns the settings registry, Scope owns its key-value data, ModuleView is a lightweight navigation view, and the resolution chain walks the context hierarchy. The goal-scoping in Goal/Methods.cs uses the same save/restore pattern as Goal and Step — consistent.

The only actionable item is the hard cast in `Resolve<T>` — a one-line fix that would improve robustness.
