# v4 — Verifying coder commit `65555d3e`

## What this is

v4 verifies the coder's response to v3's 8 findings. Coder closed 5 of 8, deferred 3 with reasons. v4's job: confirm each closure is correct (not just textually but behaviorally), confirm the deferrals are reasonable, and surface any second-order issues the fixes themselves introduced.

## What was done

Read the diff between v3-review (`d8d39be1`) and coder's fix (`65555d3e`). Verified each of the 5 closures against the v3 finding text and traced behavioral consequences for the two structural fixes (#4 NormalizeParameterTypes surfacing reaches LlmFixer; #5 PromoteGroups error reaches structured error pipeline). Re-read the 5 changed files in context (not just patched lines). Re-checked v3's deferred carryover items.

**Verdict: CLEAN.** All prioritized closures are correct. The 3 deferrals are defensible. One second-order issue surfaced (the locale fix is asymmetric — see Pass 3) but it was already known from v3's cross-cutting section and isn't a regression.

## Code example — pattern of the closures

The five bare-catch fixes all follow the same shape — the one v2 introduced for similar sites:

```csharp
// Before
try { _grepRegex = new Regex(Grep, RegexOptions.IgnoreCase); }
catch { _grepRegex = new Regex(Regex.Escape(Grep), RegexOptions.IgnoreCase); }

// After
try { _grepRegex = new Regex(Grep, RegexOptions.IgnoreCase); }
catch (ArgumentException) { _grepRegex = new Regex(Regex.Escape(Grep), RegexOptions.IgnoreCase); }
```

Narrow filter to exactly what the framework throws for the recoverable case; let everything else propagate. Mechanical, identical at all 5 sites with the appropriate exception set per call.

## What v4 escalates for the next round

The locale fix at `TypeConverter.cs:325` is correct on the parse side but creates an asymmetry against three FORMAT sites that still use Thread.CurrentCulture (`FormatValue` × 2, `ExampleRenderer`). On European locales, the @known round-trip path can produce `"3,14"` in the formal string (current culture format) and then fail to parse it back (Invariant culture parse). Before the fix both ends used current culture and round-tripped consistently within a locale; after the fix, the asymmetry undoes the value of the fix for the locales it was meant to help.

The next coder round should propagate `CultureInfo.InvariantCulture` to the format sites — preferably by extracting a single helper that addresses v1 #9 (renderer consolidation) and v1 #10 (culture-sensitive ToString) at the same time.

## Sub-finding

`promoteGroups` is unreachable from any current goal or .pr file. The new `ActionError("PromoteGroupsImmutableStep")` code path therefore has zero test coverage. The action exists in the module registry (LLM-routable from a step like "promote groups"), but no builder goal currently routes to it. Either delete the module or write a goal-test that exercises the new error path.
