# v2 review — what the coder did

## v2 verdict

`needs-fixes` — 10/12 v1 closures verified green, but **F2/F3/F4 PLang clusters were untouched** (25 reds + 1 in lowercase suite). v2 sent it back to the coder for those three.

## Coder's response (commits `bbf982d4..5c917ac5`)

Two commits. Closes F2 + F3, explicitly skips F4. Plus a `tests/` → `Tests/` directory rename completion.

### `6740ebba` — Close F2 and F3

**F2 — catalog skip (`DefaultBuilderProvider.cs` +33 lines).**

Added `IsCatalogDescription(string value, string typeName)` private helper at line 653–664 that recognizes the four shapes produced by `App.Modules.@this.Describe()`:
- `"X"` (e.g. `"int"`)
- `"X?"` (e.g. `"int?"`)
- `"X = default"` (e.g. `"int = 1"`)
- `"%var% X"` (the `%var%` prefix variant)

Anchored on `typeName` from the schema slot — so an LLM-emitted real value can't accidentally be classified as a description (because `"some_real_value"` won't start with `"int"`).

Two skip-guards added that call this helper:
- `NormalizeParameterTypes` at line 616 (the conversion site that produced the `'int = 1'` errors)
- `goal.call` sanity guard at line 263 (the `ToGoalCall` parser that produced the dotted-name errors like `goal.call.Name 'goal.call' looks like a type name`)

**F3 — math examples for the LLM (5 new files: `add.cs`, `subtract.cs`, `multiply.cs`, `divide.cs`, `power.cs`, +15 lines each).**

Added `ExamplesForLlm()` static method to each of the 5 math actions. Each returns 2 `ExampleSpec` entries: a "natural form" example and an "RHS form" example matching `set %x% = %x% + 1`. The catalog feeds these to the LLM via `App.Modules.@this.Describe()` line 248 (`GetMethod("ExamplesForLlm", ...)`).

No runtime evaluator added. The fix lives entirely in the LLM's prompt context. Per `good_to_know.md`, this is the design rule: **don't add a runtime expression evaluator, teach the LLM via examples**.

### `5c917ac5` — directory rename completion

`tests/v7-demo` deleted. Three `docs/modules/*.md` references updated. `os/system/builder/web/index.html` example updated. **Zero behavior change** — purely housekeeping.

## What v3 needs to verify

1. F2 closure — does BuilderValidateValid go green?
2. F3 closure — does Loop.test.goal produce 3? Does the rebuilt `countitem.pr` contain `math.add` instead of literal string concat?
3. False-positive risk on `IsCatalogDescription` — can a real LLM-emitted value accidentally trip the guard?
4. Test quality on the new code — is `IsCatalogDescription` C#-unit-tested? Is `ExamplesForLlm` rendering tested?
5. F4 cluster status — coder explicitly skipped; ~23 reds expected to remain.
