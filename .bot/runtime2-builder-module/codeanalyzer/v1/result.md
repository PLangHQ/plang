# Code Analysis v1 — Builder Module (Piece 8)

## Architecture Overview

The builder module follows the established handler → provider delegation pattern. 8 thin action handlers delegate to `IBuilderProvider` / `DefaultBuilderProvider`. `Goal.Parse()` is a static method on Goal itself (OBP-correct — Goal owns its own text format). `Step.Merge()` and `Goal.MergeFrom()` are instance methods on their respective entities (OBP-correct — behavior belongs to the owner).

---

## PLang/Runtime2/modules/builder/providers/DefaultBuilderProvider.cs

### OBP Violations

1. **Line 93: Rule 1 — Behavior belongs to the owner**
   - `Goals()` iterates `parsedGoals` and calls `MergePrData()` for each. This is fine because the merge loop is builder pipeline orchestration, not Goal-internal logic. Goal owns `MergeFrom()` which is correctly delegated to. **Not a violation.**

No OBP violations found.

### Simplifications

1. **Lines 77-100: `Goals()` silently skips failed reads (line 81: `if (!readResult.Success) continue`)**
   This drops file read errors without trace. If a `.goal` file exists but can't be read (permission error, locked file), the builder silently ignores it. This is a design choice, but consider adding these to `allErrors` as warnings — the existing `allErrors` list is there for exactly this purpose.

2. **Lines 15-20: `BuildingGuard` is called on every action entry (7 call sites)**
   The guard pattern works, but it's duplicated across all 8 provider methods. Consider moving the guard to the action handler level (each handler's `Run()` method) or to the provider resolution path, so the provider itself doesn't need to guard. However, this is consistent with how it works now and the duplication is low-cost. **Minor — no change needed.**

### Readability

1. **Lines 288-337: `ResolveGoalCallPaths` is 50 lines of nested loops**
   Three levels of nesting: `foreach action` → `foreach param` → type check + goal resolution. The method is private and does one thing, so it's acceptable, but extracting the inner goal resolution into a named `ResolveGoalCallPath(param)` method would improve readability.

### Behavioral Reasoning

1. **Line 70: `var files = listResult.Value as PLangPath[]` — type assumption**
   The `file.list` action returns `PLangPath[]`. If the file module's return type changes, this silently becomes null and returns an empty list. The cast is correct today but fragile. **Minor — no immediate action needed, but worth noting.**

2. **Lines 265-283: `MergePrData` catch is correct** — catches `JsonException` specifically, returns errors as warnings. Good pattern.

3. **Line 297: GoalCall type check uses string comparison `param.Type?.Value`** — This is correct: Type.Value is the PLang type name string. Not a parsing issue — the type was set by the LLM builder.

### Deletion Test

1. **Lines 288-337: `ResolveGoalCallPaths` — partially tested.** `ValidateActions_GoalCallPath_Resolved` tests the happy path where a `.pr` file exists. `ValidateActions_DynamicNames_Skipped` tests the `%` skip path. **Lines 310-315 (derive goalPath from name, append .goal extension, normalize slashes) have no test that exercises a GoalCall whose Name lacks the `.goal` suffix.** The test at line 85 of `ValidateActionsTests.cs` uses `Name = "DoSomething"` which hits this path, but doesn't assert the derived path. Partial coverage — acceptable.

2. **Lines 247-286: `MergePrData` private helper** — tested indirectly via `GetGoals_MergesExistingPrData` and `GetGoals_CorruptPrFile_IgnoresAndReparses`. Covered.

### Verdict: CLEAN
DefaultBuilderProvider follows the handler → provider delegation pattern correctly. No OBP violations. One minor readability suggestion (extract `ResolveGoalCallPaths` inner loop). No bugs found.

---

## PLang/Runtime2/modules/builder/providers/IBuilderProvider.cs

### Verdict: CLEAN
Standard provider interface. Method names match action names (consistent naming convention confirmed by commit `b0335f94`). All methods take action records — correct OBP.

---

## PLang/Runtime2/modules/builder/ (action handlers)

### actions.cs, app.cs, appSave.cs, goals.cs, goalsSave.cs, merge.cs, validate.cs, types.cs

All 8 handlers are textbook thin delegation:
- `[Action]` attribute with correct name
- `IContext` interface
- `[Provider]` partial property for `IBuilderProvider`
- Single-line `Run()` method delegating to provider
- `[IsNotNull]`, `[Default]` validation attributes used correctly

### Verdict: CLEAN
These are the cleanest handlers in the codebase. No findings.

---

## PLang/Runtime2/modules/builder/BuilderTypeInfo.cs

### Verdict: CLEAN
Single-line record. Carries builder type metadata.

---

## PLang/Runtime2/Engine/Goals/Goal/this.cs (Parse, MergeFrom, ToText)

### OBP Violations
None. `Parse()` is static on Goal — Goal owns its text format. `MergeFrom()` is an instance method — Goal owns step matching. Both correct.

### Simplifications
None needed.

### Readability

1. **Lines 198-360: `Parse()` is 162 lines** — This is the longest method on this branch. It handles goal headers, step lines, continuation lines, block comments, line comments, blank lines, and tab conversion. The method does one thing (parse) and the state machine is linear (top-to-bottom). Breaking it up would scatter the state machine across multiple methods without improving clarity. **Acceptable for a parser.**

### Behavioral Reasoning

1. **Line 203: `text = text.Replace("\t", "    ")` — tab normalization happens before line splitting.** This means a tab in a step's text content is also replaced. If a user writes `- set %x% = "tab\there"`, the literal tab in the string value gets replaced with 4 spaces. However, since `\t` in PLang strings would be handled by the runtime's string interpolation (not the raw .goal text), this is a non-issue for real-world usage. **Minor — no action needed.**

2. **Lines 333-346: Goal header — `IsSetup` and `IsSystem` detection**
   - `IsSetup` checks if goal name is "Setup" OR path starts with `setup/`. The path is normalized with leading `/` stripped. This is correct.
   - `IsSystem` checks if path starts with `system/`. Same normalization. Correct.
   - **Note**: The path normalization at line 333 uses `path?.Replace('\\', '/').TrimStart('/')`. But `path` is the parameter passed to `Parse()`, which at the call site in `DefaultBuilderProvider.Goals()` (line 86-88) is constructed as `"/" + relativePath`. So the leading `/` is always present and gets trimmed. Correct behavior.

3. **Lines 171-185: `MergeFrom` — O(n×m) step matching**
   Steps are matched by `Text` with a consumed-index set. For typical goal sizes (1-20 steps), this is fine. No performance concern.

### Deletion Test

1. **`Parse()` continuation lines (lines 314-326)**: Tested by `GoalFileTests.Parse_ContinuationLines_AppendsToStepText`. Covered.

2. **`Parse()` block comments (lines 219-258)**: Tested by `GoalFileTests.Parse_MultiLineComments_HandledCorrectly`. Covered.

3. **`Parse()` implicit "Start" goal (lines 281-291)**: When a step appears before any goal header, a "Start" goal is auto-created. **No test covers this path.** Could delete lines 281-291 and no test would fail. **Finding: untested implicit Start goal.**

4. **`Parse()` empty step text (line 279: `trimmed == "-"`)**: A bare `-` with no text creates a step with `Text = ""`. **No test for this edge case.** Could delete `|| trimmed == "-"` and no test fails. **Finding: untested bare dash.**

### Verdict: NEEDS WORK
Two untested `Parse()` paths: implicit "Start" goal (step before any header) and bare dash step. No bugs, but unproven.

---

## PLang/Runtime2/Engine/Goals/Goal/Methods.cs (FormatForLlm)

### OBP Violations
None.

### Simplifications

1. **Lines 106-139: `BuildFormatData()` and lines 141-189: `FormatForLlmFallback()`** — These two methods both construct the same anonymous object shape (step → actions → parameters) with slight formatting differences. The action projection is duplicated at lines 124-131 and 160-167. **Minor — the duplication is within one file and the methods serve different output targets (Scriban template vs string). Tolerable.**

### Readability

1. **Lines 108-109: `var jsonOpts = new JsonSerializerOptions()` in `BuildFormatData()`** — Creates empty options but never configures them. This is equivalent to `JsonSerializerOptions.Default`. Should use `Json.CamelCaseIndented` for consistency, or remove the local variable and use default serialization. Same issue at line 144 in `FormatForLlmFallback()`. **Minor nit.**

### Behavioral Reasoning

1. **Line 84: `FormatForLlm` takes `PLang.Interfaces.PLangContext?`** — This references `PLang.Interfaces`, which is a Runtime1 namespace. The rest of this branch is pure Runtime2. **Finding: Runtime1 type reference in Runtime2 code.** Check if this method existed before this branch or was added here.

### Deletion Test

1. **`FormatForLlm` and `FormatForLlmFallback`** — No tests exist for these methods on this branch. Could delete the entire `Methods.cs` file and no builder module test would fail. However, these may be tested elsewhere or used by the builder goals at runtime. **Flag but don't block on this.**

### Verdict: NEEDS WORK
Runtime1 type reference (`PLang.Interfaces.PLangContext`) in `FormatForLlm`. Empty `JsonSerializerOptions` should be removed or replaced with centralized options.

---

## PLang/Runtime2/Engine/Goals/Goal/Steps/Step/this.cs (Merge, Clone)

### OBP Violations
None. Step owns both `Merge()` and `Clone()`.

### Simplifications
None.

### Behavioral Reasoning

1. **Clone() (lines 68-96) — complete field audit:**

   | Field | In Clone? | How |
   |-------|-----------|-----|
   | Index | ✓ | explicit |
   | Text | ✓ | explicit |
   | LineNumber | ✓ | explicit |
   | Indent | ✓ | explicit |
   | Comment | ✓ | explicit |
   | Actions | ✓ | deep copy (new list + new Action) |
   | OnErrorGoal | ✓ | explicit |
   | Hash | ✓ | explicit |
   | PreviousHash | ✓ | explicit |
   | Intent | ✓ | explicit |
   | OnError | ✓ | ref copy (OK — immutable record-like) |
   | Cache | ✓ | ref copy (OK — immutable record-like) |
   | Timeout | ✓ | explicit |
   | Errors | ✓ | new List copy |
   | Warnings | ✓ | new List copy |
   | WaitForExecution | ✓ | explicit |
   | Goal | ✓ | ref copy (back-reference, correct) |
   | _stepCache | — | derived from Cache, correct to skip |

   **All fields covered.** Clone is complete.

2. **Merge() (lines 102-127)** — Merges Actions, Cache, OnError, Errors, Warnings. Does NOT merge Hash, PreviousHash, Intent, Timeout, OnErrorGoal, WaitForExecution. This is intentional: those are either structural (from parse) or derived (Hash). **Correct design.**

### Verdict: CLEAN

---

## PLang/Runtime2/Engine/Modules/this.cs (Describe, GetDefaults)

### OBP Violations

1. **Lines 137-194: `Describe()` is on EngineModules** — This is correct OBP: the module registry owns its own description. It navigates its internal state (`Names`, `GetActions`, `GetActionType`). No violation.

### Simplifications

1. **Lines 200-237: `GetDefaults()` — reflection-heavy**
   Two paths: IConfigure<T> instantiation + property scanning, then [Default] attribute scanning. Both use `Activator.CreateInstance` and reflection. This is builder-time code (not runtime hot path), so performance doesn't matter. The logic is clear. **No simplification needed.**

### Readability

1. **Lines 151-175: `Describe()` inner loop** — 25 lines of property reflection per action type. Dense but clear. Each line does one thing. Acceptable.

### Behavioral Reasoning

1. **Line 211: `Activator.CreateInstance(configType)` — could throw** if configType has no parameterless constructor. This would propagate as an unhandled exception from `GetDefaults()`, which is called from `DefaultBuilderProvider.Validate()` line 164. The validate action would crash instead of returning an error. **Finding: unguarded `Activator.CreateInstance` in `GetDefaults()`.** Should wrap in try-catch or guard with a constructor check.

### Deletion Test

1. **`GetDefaults()` IConfigure<T> path (lines 206-224)**: This path is only exercised if an action type implements `IConfigure<T>`. **No test exercises this path.** The `ValidateActions_DefaultsFilled` test uses `file.list` which uses `[Default]` attributes (the second path), not `IConfigure<T>`. Could delete lines 206-224 and no test fails. **Finding: untested IConfigure<T> defaults path.**

### Verdict: NEEDS WORK
`Activator.CreateInstance` in `GetDefaults()` can throw unhandled. `IConfigure<T>` defaults path is untested.

---

## PLang/Runtime2/Engine/Utility/Json.cs

### Verdict: CLEAN
Two static readonly options. Clean consolidation. Used consistently across the builder module.

---

## PLang/Runtime2/Engine/Utility/TypeMapping.cs (ConvertTo, GetBuilderTypeNames, GetComplexTypeSchemas)

### OBP Violations
None. TypeMapping is a utility class (static, stateless).

### Simplifications
None.

### Behavioral Reasoning

1. **Lines 277: `ConvertTo<T>` — `(T?)ConvertTo(value, typeof(T))`**
   If ConvertTo returns null for a value type T, this casts `null` to `T?` which is fine. If T is a non-nullable value type and the caller expects `T`, they'll get `default(T)` not an error. This is consistent with the non-generic version's behavior (line 285: returns `Activator.CreateInstance(targetType)` for null input with value types). **Correct.**

2. **Lines 306-336: GoalCall conversion from various source types** — Handles string, JsonElement, Dictionary. The Dictionary path (lines 321-335) constructs parameters from `IList<object?>` items cast to `IDictionary<string, object?>`. If the list contains non-dictionary items, `OfType` silently skips them. This is a reasonable defensive choice.

### Deletion Test

1. **`GetBuilderTypeNames()` (lines 370-387)** — Tested via `GetTypeInfoTests.GetTypeInfo_ReturnsBuilderTypeNames`. Covered.
2. **`GetComplexTypeSchemas()` (lines 392-413)** — Tested via `GetTypeInfoTests.GetTypeInfo_ReturnsComplexTypeSchemas`. Covered.
3. **`ConvertTo<T>` (line 277)** — Used by `DefaultBuilderProvider.ToGoalCall` (line 342). The GoalCall conversion test at `ValidateActions_GoalCallPath_Resolved` passes a `GoalCall` object directly, hitting the `value is GoalCall gc` fast path at line 341 — **not the ConvertTo path**. Lines 306-336 (GoalCall from JsonElement/Dictionary) are untested by builder tests. However, these are exercised by GoalCall deserialization in the runtime pipeline, so this is shared infrastructure rather than builder-specific. **Note but don't block.**

### Verdict: CLEAN

---

## PLang/Runtime2/Engine/Providers/this.cs

### Change: Line 203 — `"builder" or "ibuilderprovider" => typeof(modules.builder.providers.IBuilderProvider)`
### Change: Line 226 — `Register<modules.builder.providers.IBuilderProvider>(new modules.builder.providers.DefaultBuilderProvider())`

### Verdict: CLEAN
Standard provider registration. Consistent with all other modules.

---

## PLang/Runtime2/modules/http/providers/DefaultHttpProvider.cs

### Changes: Lines 44 and 629 — replaced `new JsonSerializerOptions { PropertyNameCaseInsensitive = true }` with `Json.CaseInsensitiveRead`.

### Verdict: CLEAN
Correct consolidation to centralized JSON options.

---

# Summary of Findings

## Major Findings (0)
None.

## Minor Findings (5)

| # | File | Line(s) | Finding | Severity |
|---|------|---------|---------|----------|
| 1 | Goal/this.cs | 281-291 | `Parse()` implicit "Start" goal (step before header) is untested | minor |
| 2 | Goal/this.cs | 279 | `Parse()` bare dash (`- ` with no text) is untested | minor |
| 3 | Modules/this.cs | 211 | `Activator.CreateInstance` in `GetDefaults()` can throw unhandled | minor |
| 4 | Modules/this.cs | 206-224 | `GetDefaults()` IConfigure<T> path is untested | minor |
| 5 | Goal/Methods.cs | 84 | `FormatForLlm` references Runtime1 type `PLang.Interfaces.PLangContext` | minor |

## Nits (2)

| # | File | Line(s) | Finding |
|---|------|---------|---------|
| 1 | Goal/Methods.cs | 108, 144 | Empty `JsonSerializerOptions()` — use centralized `Json.*` or remove |
| 2 | DefaultBuilderProvider.cs | 81 | Silently skips failed file reads — consider adding to warnings |

---

# Overall Verdict: NEEDS WORK

Five minor findings, no major issues. The builder module is well-structured and follows OBP correctly. The action handlers are the cleanest in the codebase. Main gaps are: two untested `Parse()` edge cases, one unguarded `Activator.CreateInstance`, one untested defaults path, and a Runtime1 type reference. Send back to coder to address findings #1-4 (finding #5 may need architect input on whether `FormatForLlm` should move to Runtime1 or use a Runtime2 context type).
