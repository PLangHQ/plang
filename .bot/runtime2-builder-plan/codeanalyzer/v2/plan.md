# v2 Plan: Re-review of Coder Fixes

## Scope
The coder addressed 10 of 13 v1 findings in commit `db09e0f4` on branch `fix-plang-tests`. This v2 re-reviews each fix for correctness, OBP compliance, and new issues introduced.

## Analysis Targets (per fix)

1. **Data.EnumerateItems()** — new method on Data (this.cs:312-356). Does it handle all dictionary types correctly? Edge cases?
2. **Condition guard → Context._data** — step-scoped key via `GetHashCode()`. Is GetHashCode stable? Is cleanup reliable?
3. **Data.Value caching** — NeedsResolution resolves once, clears flag. Thread safety?
4. **ResolveDeep clone** — MemberwiseClone via reflection. Does it actually prevent mutation?
5. **PromoteGroups JsonElement warning** — Console.Error.WriteLine. Adequate?
6. **List modules rewrite** — 7 files rewritten to use EnumerateItems/GetChild. Data-first, no .Value extraction.
7. **As<T>() cache** — static ConcurrentDictionary for Resolve method lookup. Correct key?
8. **Headless build guard** — Console.IsInputRedirected check. Complete?
9. **Action.Return removal** — all references cleaned? Anything missed?
10. **New tests** — adequate coverage for the fixes?

## Method
5-pass analysis (OBP, Simplification, Readability, Behavioral, Deletion) on changed code only. Focus on behavioral corners of the new implementations.
