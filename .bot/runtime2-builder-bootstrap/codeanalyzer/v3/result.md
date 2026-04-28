# Codeanalyzer v3 — Fresh-eyes findings

Branch: `runtime2-builder-bootstrap`
Date: 2026-04-28
Scope: pattern sweeps across all 167 changed C# files + deep reads on what v1 deferred (Debug/this.cs, TypeMapping.cs, builder modules, test infrastructure).

## TL;DR

v1's bare-catch sweep was triaged — it caught Tier 1/2 sites and missed several. **5 bare catches remain in the diff**, including in files the user hits constantly (`test/discover`, `list/add`, `Debug/this.cs` ×3, `DefaultBuilderProvider.FormatValue`). Two silent-fail paths in the builder swallow conversion errors. `Step.Clone()` is missing 7 of its own properties (the LLM-derived metadata added this branch). `Convert.ChangeType` is locale-sensitive — JSON-shaped numbers will break on European locales.

Architecture is solid; what's left is the same anti-patterns v1 flagged, just at sites v1 didn't open.

**Verdict: NEEDS WORK.**

---

## Half A — Pattern sweeps

### 1. Bare-catch sites still in the diff

v1+v2 fixed 6 sites. v3 found 5 more.

| File:line | Catches | What it does | Fix |
|-----------|---------|--------------|-----|
| `PLang/App/modules/test/discover.cs:48` | `catch` (no filter) | Treats any exception from `fs.ValidatePath` as "traversal outside root → empty list" | `catch (UnauthorizedAccessException)` — that's the only one ValidatePath throws that's recoverable |
| `PLang/App/modules/list/add.cs:71` | `catch` (no filter) | Falls back to alias when JSON round-trip clone fails | `catch (JsonException ‖ NotSupportedException)` + Debug.Write — same pattern as v2 fixed in `Variables/this.cs:162` |
| `PLang/App/Debug/this.cs:218` | `catch` (no filter) | Falls back to a literal-escaped Regex if user's Grep pattern is invalid | `catch (ArgumentException)` — that's the only thing `new Regex(...)` throws |
| `PLang/App/Debug/this.cs:614` | `catch { /* fall through to preview */ }` | Falls through to FormatPreviewValue when JSON serialize fails on dict/list | `catch (JsonException ‖ NotSupportedException)` — same pattern as v2 fixed in `FluidProvider.cs:143`, `Errors/Error.cs:292` |
| `PLang/App/Debug/this.cs:672` | `catch` (no filter) | Falls back to `?` when reflection prop.GetValue throws | `catch (Exception ex) when (ex is not (NullRef ‖ OOM ‖ StackOverflow))` — reflection getters can throw TargetInvocationException, also the standard codebase shape |
| `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs:440` | `catch` (no filter) | Falls back to ToString() when JSON serialize fails on a structured value | `catch (JsonException ‖ NotSupportedException)` — already part of v1's #9 deferred (three formal-syntax renderers) but worth listing here as a still-bare site |

**Per memory `feedback_silent_error_critical.md`** these are CRITICAL severity, not medium. Same fix shape v2 applied at the other 5 sites.

### 2. Locale-sensitive primitive conversion

**`PLang/App/Utils/TypeConverter.cs:322`** — `System.Convert.ChangeType(value, targetType)` uses `Thread.CurrentCulture` by default. For an Italian/Spanish/German locale, `"3.14"` (a JSON-shaped number) → `double` raises `FormatException` because the locale expects `"3,14"`.

PLang values cross JSON boundaries constantly (`.pr` files, settings, LLM responses). Any string-encoded number resolved through this path is a locale bug waiting for a non-en-US user.

Fix: `System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture)`.

Same concern as v1's #10 (deferred culture-sensitive `ToString` in renderers) — different end (parse vs format), same root cause: missing InvariantCulture.

### 3. Clone-family divergence — Step.Clone() missing 7 properties

**`PLang/App/Goals/Goal/Steps/Step/this.cs:183`**

Step.@this property surface (12-249) vs Clone() body (185-215):

| Property              | In Clone? | Notes |
|-----------------------|:---------:|-------|
| Index                 | ✓         | |
| Text                  | ✓         | |
| LineNumber            | ✓         | |
| Indent                | ✓         | |
| Comment               | ✓         | |
| Actions               | ✓         | (deep — also clones Modifiers/Parameters/Defaults) |
| WaitForExecution      | ✓         | |
| Goal                  | ✓         | (reference) |
| Intent                | ✓         | |
| Errors                | ✓         | |
| Warnings              | ✓         | |
| **PriorText**         | ✗         | **NEW this branch** |
| **Guidance**          | ✗         | NEW this branch |
| **Level**             | ✗         | NEW this branch |
| **Confidence**        | ✗         | NEW this branch |
| **Formal**            | ✗         | NEW this branch |
| **Source**            | ✗         | NEW this branch |
| **Keep**              | ✗         | NEW this branch |
| Disabled (computed)   | n/a       | derived from Actions |
| Hash (computed)       | n/a       | |
| HasSubSteps (computed)| n/a       | |

**Caller audit**:
- Production callers in `PLang/`: zero (verified by grep).
- Test callers: one — `PLang.Tests/App/Modules/modifier/ModifierRegistryTests.cs:78`. Asserts modifier-copy behavior only; doesn't exercise the new properties.

Two equally-good fixes:
1. **Delete Step.Clone()**. Pure deletion-test win — 30 lines vanish, only behavior lost is "deep-clone modifiers", which the one test asserts but nothing in production needs. Update the test to construct two steps with shared modifiers and assert directly, or drop the test.
2. **Fix Step.Clone()** to propagate all 18 properties. Worth doing only if a future feature needs deep step copies; today it's dead weight.

The recommendation is **delete**. Per project memory's clone-family pattern, every property addition forces every copy method to update — Step.Clone() will rot at the same speed forever.

### 4. Data.Clone() — `_rawValue` not propagated

**`PLang/App/Data/this.cs:494`** — Clone() builds the new instance via the public constructor and copies Error/Handled/Returned/ReturnDepth/Warnings/Signature/Properties/Context/NeedsResolution. The private `_rawValue` field (preserved pre-resolution snapshot) is **not** copied.

Practical impact: a clone of a resolved Data carries the resolved value but loses the snapshot needed for ResetResolution() to roll back. Today this is harmless because ResetResolution guards on `_rawValue != null`. But anyone who relies on `clone.ResetResolution()` to re-resolve from scratch silently doesn't.

Fix: also copy `_rawValue` in Clone() (plus probably `_resolved` so the clone doesn't redundantly re-resolve).

Lower priority than Step.Clone — Data is in active use, and the failure mode is a silent miss of an uncommon code path rather than a wrong value.

---

## Half B — Files v1 deferred / under-examined

### `PLang/App/Debug/this.cs` — NEEDS WORK

Three bare catches (covered in Half A above), plus:

#### Apply() is not idempotent

**Lines 115-260**: Each call subscribes new event bindings (`events.Register(...)` × 3-5 depending on Level), wires `OpenAiProvider` callbacks (`oai.OnBeforeRequest +=`, `OnAfterResponse +=`), and mutates `_engine.User.Context.Variables.OnResolveTrace +=`. Nothing tracks subscriptions.

A second `Apply()` call doubles every event handler. Today there's one caller (`RegisterStartupParameters` parses `--debug` once) but the class doesn't enforce single-shot. A test or future feature that re-applies (e.g., applying debug after CLI args via API) would silently double output.

Fix: an `_applied` guard at the top of `Apply()`, or expose a `Reset()` that unsubscribes before re-applying.

#### LLM tracing hardcodes OpenAiProvider

**Lines 170-204**: `if (provider.Value is OpenAiProvider oai)`. Other providers — `PlangProvider`, future Anthropic/Vertex/Bedrock — have no `OnBeforeRequest`/`OnAfterResponse`, so `--debug={"llm":{...}}` silently does nothing for them.

The right shape is a marker interface (`ILlmProviderTraceable` with the two events) that any provider can implement. Then debug subscribes generically. Today's hardcoding works because OpenAi is the only provider; rate it as "infrastructure debt that becomes a real bug the moment a second provider lands."

#### Reflection-by-name in ResolveLlmFilePath

**Line 425**: `goalData.Value.GetType().GetProperty("Name")` — string-name reflection on the resolved %goal% value. If `Goal.@this.Name` is renamed (or %goal% holds a different shape — e.g., a wrapped GoalCall), this silently picks up nothing and the path becomes `unknown_…txt`. The variable is documented to hold a Goal object; a typed cast `as Goal.@this` would fail loudly when the contract drifts.

Lower priority — reflection is contained within the diagnostic path.

### `PLang/App/Utils/TypeMapping.cs` — CLEAN-ish

The 708-line file is well-structured. Existing v1 sub-findings still stand (forwarders to TypeConverter at 318-328, generic-type mega-list at 165-184, IsScalarPlangType heuristic at 276-300) — none are regressions. Convert.ChangeType locale issue is in TypeConverter.cs, not here.

### `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — NEEDS WORK

Beyond the bare catch at FormatValue:440 and v1's already-deleted DiagGoal:

#### PromoteGroups silent fail on JsonElement input

**Lines 544-551**: When a step is a `JsonElement` (immutable), `SetValue` writes a stderr warning and returns without setting. The validation pipeline doesn't see the failure. The LLM's retry loop never knows promotion was skipped. Per the project's "silent error swallowing is critical" rule, this should be a structured `Errors.ActionError`.

The current shape is also self-defeating — the comment says "expected IDictionary" which means the JsonElement path is "shouldn't happen". If it shouldn't happen, throw / return Error to surface the assumption violation. If it can happen, handle it (deserialize the JsonElement to a dict and write).

#### NormalizeParameterTypes silently drops conversion errors

**Line 607**: `var (converted, _) = TypeMapping.TryConvertTo(p.Value, targetType, context);` — error discarded. If LLM emits a value that can't convert to the schema-declared type (e.g., a record-shape for a string-typed param), the param keeps the wrong-typed value. Validation later may or may not catch it; the runtime gets a typed mismatch.

The fix is small: collect errors and add them to `validationErrors` list (already used in the surrounding method).

#### Static `_buildTimer` ✱ shared state

**Line 16**: `private static readonly Stopwatch _buildTimer = new();` — single instance across all builds. The builder is sequential today (one goal at a time), so safe. If parallel build is ever introduced, this races. A comment marking the assumption would help future contributors not regret it.

### `PLang/App/modules/test/discover.cs` — NEEDS WORK

Bare catch at line 48 (covered in Half A). The other catches in the file (lines 113, 128) are properly filtered — so this catch was a clear oversight, not a pattern.

### `PLang/App/modules/test/run.cs` — CLEAN

Test-runner discipline is solid: filtered catches at 142 (`OperationCanceledException` for timeout), 146 (`not (OOM ‖ StackOverflow)`), proper SemaphoreSlim-throttled parallelism, CancellationToken plumbed through PushCancellation. This file is a model for the patterns the rest of the diff doesn't always follow.

### `PLang/App/Catalog/this.cs` — CLEAN

`TypeSchemas` getter recomputes per access (v1 sub-finding, lower priority). Build/ToJson are simple. No new findings.

---

## Cross-cutting v3 observations

### A. v1's bare-catch sweep was triaged, not exhaustive

v1 flagged 5-6 sites; v2 fixed those plus 1 new wrap. v3 found 5 more bare catches in files v1 didn't open. The fix shape is identical at every site — `catch (JsonException ‖ NotSupportedException)` for serialize/deserialize, `catch when (ex is not (NullRef ‖ OOM ‖ StackOverflow))` elsewhere. The delta isn't difficulty; it's coverage.

A linter or a `git grep -E '^\s*catch\s*(\(\s*\)|\{)$'` step in the build would prevent the next bare catch from landing.

### B. The "silent error swallow" pattern keeps recurring

v3 surfaced four more silent-failure sites: list/add catch, discover catch, Debug FormatValue catch, NormalizeParameterTypes error discard, PromoteGroups SetValue stderr-only. Per project memory: silent error swallowing is ALWAYS critical, never medium. The systemic issue is that `var (_, error) = ...` and `try { } catch { }` are the path of least resistance; every reviewer has to push back individually.

A heavier lift but worth considering: a `[NoSilentDiscard]` analyzer that errors on `(_, error) = TryConvertTo(...)` patterns where the throwaway is the second return. Today the pattern is everywhere.

### C. Clone-family hazard is real and recurring

Step.Clone() is the third instance of "new property added, copy method not updated" the team's hit on this branch (after Context.Clone/CreateChild and Variables.Clone). The standing project-memory pattern is correct; what's missing is a place to enforce it. Options:
- A test that diffs a class's properties against its Clone() body (reflection-based).
- A code generator that emits Clone() from `[Cloneable]` markers.
- A convention that all clones go through serialization round-trip (already used in Variables.Set/list.add — but with the bare-catch fallback that re-introduces the alias bug they were preventing).

For now: just delete Step.Clone (zero production callers).

### D. Locale-sensitive code is now pervasive

Three sites (`Convert.ChangeType` at TypeConverter:322, plus the two deferred renderer ToStrings at ExampleRenderer:103 and FluidProvider:140) all use Thread.CurrentCulture implicitly. Plus `DefaultBuilderProvider.FormatValue:437` (the third renderer). Build a single `InvariantString.Format(value)` helper and replace all four sites in one PR.

---

## Priority list for the coder

1. **Delete `Step.@this.Clone()`** — zero production callers. Update the one test (`ModifierRegistryTests.StepClone_ClonesActionModifiers`) to test modifier construction directly, or drop the test. Removes 30 lines of code that would otherwise rot every time a Step property is added. (Deletion test win.)

2. **Fix the 5 bare catches** at the file:line listed in Half A #1. Mechanical, identical to v2's fixes elsewhere.

3. **Fix `TypeConverter:322` Convert.ChangeType** to pass `CultureInfo.InvariantCulture`. One-line fix.

4. **Surface conversion errors in `NormalizeParameterTypes`** at DefaultBuilderProvider:607. Collect into validationErrors so LlmFixer can re-prompt.

5. **Surface PromoteGroups failures** at DefaultBuilderProvider:550 — return structured Error instead of stderr-only warning.

6. **Add idempotency guard to `Debug.Apply()`** — single-line bool field check.

7. **Document or fix `Data.@this.Clone()` `_rawValue` propagation** at Data/this.cs:494. Lower priority.

8. **Decouple Debug LLM tracing from OpenAiProvider** — extract `ILlmProviderTraceable` marker interface. Architectural; do this when a second LLM provider lands.

Plus the still-open v1 deferreds (#9 three formal-syntax renderers consolidation, #10 culture-sensitive ToStrings) and the v2 carryover sub-findings (silent first-element-of-array, null→0 default).
