# Codeanalyzer v4 — Verification of coder commit `65555d3e`

Branch: `runtime2-builder-bootstrap`
Date: 2026-04-28
Scope: 5 files touched in commit `65555d3e` (+45/-17). Pass-4 behavioral reasoning on the fixes themselves; verify each surfacing reaches the right consumer.

## TL;DR

All 5 closed findings verified at code level. Catch filters are narrow and correct. Surfacing for #4 (NormalizeParameterTypes) reaches LlmFixer through `Validate` → `HandleValidationError` → `BuildStep`. Idempotency guard is correct. The 3 deferred items are defensible.

One asymmetry surfaces from fix #3 (Convert.ChangeType InvariantCulture): the parse side is now locale-safe, but the format side at three rendering sites still uses Thread.CurrentCulture. On European locales this introduces a new round-trip risk via the `@known` mapping in BuildGoal.llm. Not a blocker — v3 only prioritized the parse side — but it should be the next priority.

**Verdict: CLEAN.** All prioritized fixes verified. One carryover (the format-side InvariantCulture) is escalated for the next round.

---

## Per-fix verification

### #2 — Five bare-catch sites narrowed (CLOSED)

| Site | Filter | Verdict |
|------|--------|---------|
| `test/discover.cs:48` | `catch (UnauthorizedAccessException)` | ✓ matches what `ValidatePath` throws for traversal-outside-root (PLangFileSystem.cs:233). Other ValidatePath errors (empty path, fs not init) are raw `Exception` and correctly propagate per the comment. |
| `list/add.cs:71` | `catch (... ex) when (ex is JsonException ‖ NotSupportedException)` + `Debug.Write` | ✓ correct exception set for `JsonSerializer.Serialize` of arbitrary object graphs. Surfaces via fire-and-forget `_ = Debug.Write(...)`; safe because Debug.Write short-circuits on `IsEnabled = false`. |
| `Debug/this.cs:223` | `catch (ArgumentException)` | ✓ exact set for `new Regex(invalid)`. |
| `Debug/this.cs:619` | `catch (... ex) when (ex is JsonException ‖ NotSupportedException)` | ✓ correct set for `JsonSerializer.Serialize` of dict/list. |
| `Debug/this.cs:677` | `catch (... ex) when (ex is not (NRE ‖ OOM ‖ SOE))` | ✓ standard codebase shape. Note: in modern .NET (Core 2.1+) reflection no longer wraps in TargetInvocationException, so the underlying NRE/OOM/SOE propagates correctly through this filter. |
| `DefaultBuilderProvider.FormatValue:440` | `catch (... ex) when (ex is JsonException ‖ NotSupportedException)` | ✓ correct set for `JsonSerializer.Serialize` of structured values. |

All 6 follow the same shape as the v2 fixes. No over- or under-catching.

### #3 — TypeConverter Convert.ChangeType locale fix (CLOSED)

`TypeConverter.cs:325` now passes `CultureInfo.InvariantCulture`:

```csharp
return (System.Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture), null);
```

This correctly fixes the parse side. LLM-emitted `"3.14"` will now parse to 3.14 on it-IT/de-DE locales where it previously threw FormatException.

**Carryover concern (escalated, see Pass 3 below):** the FORMAT side at three sites still uses Thread.CurrentCulture. The asymmetry is observable through the `@known` round-trip path on European locales.

### #4 — NormalizeParameterTypes silent error surface (CLOSED, traced end-to-end)

`DefaultBuilderProvider.cs:571` now returns `List<string>` of conversion errors. Caller at line 240 folds them into `validationErrors`. Caller flow:

```
NormalizeParameterTypes (returns errors)
  → validationErrors list (line 240)
  → BuildValidation ActionError (line 327)
  → returned to validate handler caller
  → ApplyStep.goal: builder.validate ... on error call HandleValidationError
  → HandleValidationError sets %goal.Steps[idx].validationError% and calls BuildStep
  → LLM re-prompted with the validation error
```

Confirmed at `os/system/builder/ApplyStep.goal:17-25`. Surfacing reaches LlmFixer correctly.

### #5 — PromoteGroups SetValue surfaced as ActionError (CLOSED with caveat)

`SetValue` now returns `bool`; caller returns structured `ActionError("PromoteGroupsImmutableStep")` instead of stderr-only `Console.Error.WriteLine`.

**Caveat (sub-finding, not a blocker):** `promoteGroups` is unreachable from any current goal or .pr file in the repo. It's an action available via the module registry — the LLM could route to it from a step like "promote sub-step groups" — but no current builder goal invokes it. The new error code path therefore has no test coverage. The fix itself is correct; the action is just dormant.

### #6 — Debug.Apply idempotency (CLOSED)

`_applied` bool guard at line 80 + early-return at line 120-121. Single caller today (`Executor.cs:53`), so the guard is defensive insurance against future re-callers. Pre-`_applied = true` is set BEFORE subscription work — if any subscription throws, the guard stays set and re-Apply is a no-op. Acceptable: Apply throwing crashes the process anyway (no try/catch around it), so the guard's interaction with mid-flight throws is moot.

The new `_applied` field is `private` and not marked `[JsonIgnore]` (unlike `_grepRegex` at line 78). System.Text.Json doesn't serialize private fields by default, so the absence of the attribute is benign — but inconsistent with the sibling field. Minor stylistic nit, not a finding.

---

## Deferred items (coder explicitly listed)

| v3 # | Item | Coder reason | Verdict |
|------|------|--------------|---------|
| 1 | Step.Clone() deletion (still missing 7 props) | "deferred per coder" | Defensible. The deletion-test argument still applies — zero production callers — but coder may want to preserve for future use. Clone-family hazard remains, but isn't a regression. |
| 7 | Data.Clone _rawValue propagation | "no current caller relies on it" | Verified — only `ResetResolution` reads `_rawValue` and it guards on `_rawValue != null`. A clone would silently no-op on `ResetResolution`. Acceptable to defer since no production path exercises clone-then-reset. |
| 8 | Debug LLM tracing decoupling from OpenAiProvider | "do when 2nd provider lands" | Acceptable architectural debt. `--debug={"llm":{...}}` silently no-ops for non-OpenAI providers today, but OpenAI is the only LLM provider on this branch. |

All three deferrals are reasonable. None should block the v4 verdict.

---

## New / escalated findings

### A. Locale format/parse asymmetry — ESCALATED carryover

Fix #3 (parse with InvariantCulture) is correct but creates an asymmetry against three FORMAT sites that still use Thread.CurrentCulture:

| Site | Code | Effect on it-IT |
|------|------|-----------------|
| `DefaultBuilderProvider.FormatValue:437` | `if (v is IConvertible) return v.ToString() ?? "";` | `3.14` (double) → `"3,14"` |
| `FluidProvider.FormatFormalValue:140` | `if (v is IConvertible) return v.ToString() ?? "";` | same |
| `ExampleRenderer:103` | `sb.Append(value);` (uses ToString) | same |

The end-to-end break path:

1. it-IT user runs build. LLM emits `"3.14"`. NormalizeParameterTypes parses with InvariantCulture → `3.14` (double). ✓
2. `step.Actions[*].Parameters[*].Value = 3.14`. .pr saved via `JsonSerializer.Serialize` (InvariantCulture) → JSON contains `3.14`. ✓
3. Same build or next pass: `RenderFormal(prior.Actions)` → `FormatValue(3.14)` → `"3,14"` (Thread.CurrentCulture). step.Formal saved as a string literal containing `"3,14"`.
4. NEXT build with @known: BuildGoal.llm shows the prior `formal` to the LLM as a canonical example (per `BuildGoal.llm:91-110` "treat @known entries as canonical examples"). LLM mimics → emits `"3,14"`.
5. NormalizeParameterTypes parses `"3,14"` with InvariantCulture → either FormatException OR `Convert.ToDouble("3,14", InvariantCulture) = 314` (treating "," as thousands separator). Either way, wrong value.

**Why this matters now and not before:** before fix #3, both ends used Thread.CurrentCulture. The round-trip was internally consistent within a single locale (it-IT round-tripped "3,14" correctly; en-US round-tripped "3.14" correctly). The asymmetry from the half-fix turns "consistent within a locale" into "broken on every locale that isn't en-US."

**Why this wasn't fixed in #3:** v3 only prioritized the parse side (priority #3). The format side appeared in the cross-cutting section D ("Locale-sensitive code is now pervasive") with a follow-up suggestion to build a single `InvariantString.Format(value)` helper. Coder addressed exactly what was prioritized.

**Recommended fix shape:**

```csharp
// Replace at DefaultBuilderProvider.FormatValue:437,
// FluidProvider.FormatFormalValue:140, ExampleRenderer:103
if (v is IFormattable f) return f.ToString(null, CultureInfo.InvariantCulture);
if (v is IConvertible) return v.ToString() ?? "";
```

`IFormattable` is the interface that supports culture-aware formatting; primitives implement it. The fallback to `IConvertible.ToString()` covers things like `bool` that are IConvertible but not IFormattable (and bool's ToString is culture-invariant anyway).

Or extract the three identical render bodies into a single `App.Utils.InvariantFormat.Render(value)` helper — addresses v1 #9 (renderer consolidation) and v1 #10 (culture-sensitive ToString) in one PR.

**Severity:** Medium. Only impacts non-en-US locales; only when `@known` round-trip and LLM mimicry both occur. But the fix is small and the asymmetry undoes the value of #3 for the locales it was meant to help.

### B. PromoteGroups is unreachable today — sub-finding

The hardening to `Data.@this.FromError(ActionError("PromoteGroupsImmutableStep"))` is correct, but `promoteGroups` is unreachable from any current goal or .pr file. The action exists in the module registry (LLM-routable), but no builder goal currently invokes it. The new error code path therefore has zero test coverage.

Two options for the next round:
- **Delete** `PLang/App/modules/builder/promoteGroups.cs` + `PromoteGroups`/`SetValue`/`LowestLevel`/`ToStepList`/`GetString` from `DefaultBuilderProvider` if the team confirms no goal will ever route here. (~140 LOC.) Deletion test win.
- **Cover** with a test step like `builder.promoteGroups Steps=%steps%` that includes a JsonElement step in the input — exercises the new ActionError path.

Lower priority than (A); not a regression.

---

## Pass 5 — Deletion test on the new code

For each new line in the diff, would deleting it cause a test to fail?

| New code | If deleted | Verdict |
|----------|-----------|---------|
| `_applied` field + guard | A second Apply call would double-subscribe. No current test. Single caller today. | Defensive — keep, even though no test fails. |
| `CultureInfo.InvariantCulture` arg | en-US tests still pass; non-en-US tests would fail (if they existed). No locale-coverage tests today. | Keep — fixes a real bug even if no test catches its absence. |
| `NormalizeParameterTypes` returns `List<string>` | Conversion failures silently keep wrong-typed values, runtime fails later with a confusing message instead of build-time validation. | Keep — surfaces an error class that LlmFixer can handle. |
| `PromoteGroups` ActionError | Silent stderr-only warning resumes. No current goal exercises this path. | Keep — correctness matters even if no current path triggers. |
| 5× narrow catch filters | OOM/SOE silently swallowed. No current test exercises these paths. | Keep — bare catches are a recurring memory item; consistency matters. |

No new code is dead weight.

---

## Priority list for the next round

1. **Format-side InvariantCulture** at `DefaultBuilderProvider.FormatValue:437`, `FluidProvider.FormatFormalValue:140`, `ExampleRenderer:103`. Use `IFormattable.ToString(null, CultureInfo.InvariantCulture)` or extract a helper. Closes the asymmetry from fix #3 and resolves v1 #9 / #10 in one PR.

2. **Decide on `promoteGroups`** — delete (no caller) or add a test that exercises the new ActionError path.

3. **Optional next-time:** a build-step git hook or analyzer that errors on `^\s*catch\s*(\(\s*\)|\{)$` — would prevent the next bare catch from landing without a reviewer having to spot it. (Cross-cutting v3 observation A; the team has now hit this pattern across three review rounds.)

---

## Note for tester

The locale fix in fix #3 needs **non-en-US culture coverage**. A representative test:

```csharp
[Test]
public void NormalizeParameterTypes_ParsesNumberOnItalianLocale()
{
    using var _ = new CultureScope(new CultureInfo("it-IT"));
    var (converted, error) = TypeConverter.TryConvertTo("3.14", typeof(double));
    Assert.That(converted, Is.EqualTo(3.14));
    Assert.That(error, Is.Null);
}
```

Without this test, the fix could regress silently in any future refactor.
