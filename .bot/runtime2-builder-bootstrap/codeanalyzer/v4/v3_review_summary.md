# v3 review summary

v3 (NEEDS WORK) flagged 8 items. Coder's commit `65555d3e` closed 5 mechanically; deferred 3 with reasons.

## Closed (verified at code level)

| # | Finding | Fix shape | Verified |
|---|---------|-----------|----------|
| 2 | 5 bare-catch sites still in diff | Narrow catches added: `UnauthorizedAccessException` (test/discover:48), `JsonException ‖ NotSupportedException` (list/add:71, Debug:614, DefaultBuilderProvider.FormatValue:440), `ArgumentException` (Debug:218), `not (NRE ‖ OOM ‖ SOE)` (Debug:672). list/add adds Debug.Write surface. | ✓ each site read |
| 3 | TypeConverter Convert.ChangeType locale | `CultureInfo.InvariantCulture` arg added at line 325 | ✓ |
| 4 | NormalizeParameterTypes silent error discard | Now returns `List<string>`, caller folds into `validationErrors` (line 240) so LlmFixer re-prompts | ✓ |
| 5 | PromoteGroups SetValue stderr-only | `SetValue` returns bool; caller returns structured `ActionError("PromoteGroupsImmutableStep")` | ✓ |
| 6 | Debug.Apply not idempotent | `_applied` bool guard at top of Apply (lines 80, 120-121) | ✓ |

## Deferred (with coder reasons)

- **#1 Step.Clone() deletion** — coder kept; my recommendation was delete (zero production callers).
- **#7 Data.Clone _rawValue propagation** — coder noted no current caller relies on it.
- **#8 Debug LLM tracing decoupled from OpenAiProvider** — coder will do when 2nd provider lands.

All three are defensible — deletion of Step.Clone is the only one where the deletion-test argument still applies, but the coder may want to preserve it for future use.

## What v4 should add

The diff is small (5 files, +45/-17). Each fix is itself new code that needs Pass 4 reasoning:

1. **Verify exception filters match what the runtime actually throws.** A reflection getter wraps user exceptions in `TargetInvocationException` — does the `not (NRE ‖ OOM ‖ SOE)` filter at Debug:677 catch that correctly? Yes (TargetInvocationException isn't NRE/OOM/SOE), so OK.
2. **Verify the surfacing reaches the right place.** NormalizeParameterTypes errors land in `validationErrors` — does that list reach LlmFixer? Trace it.
3. **Verify nothing else needs a similar fix.** The `_applied` guard on Apply is good — but anywhere else in the diff with subscribe-once semantics?
4. **Look at what v3 deferred to v4 anyway.** v1's #9 (three formal-syntax renderers consolidation) and #10 (culture-sensitive ToStrings) are still open. Are they regressions or new sites?
5. **Fresh-eyes pass on the 5 changed files** — every fix introduces new code. That code is itself diff and itself reviewable.
