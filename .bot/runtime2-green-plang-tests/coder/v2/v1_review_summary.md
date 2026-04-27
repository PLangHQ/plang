# v1 Review Summary (from tester v2)

Tester re-baselined coder v1's Waves 1–4. Verdict: **needs-fixes**.

## Headline

- Zero regressions vs v1 baseline. +18 wins (128/35/5).
- C#: 2273/2274 (pre-existing flake).
- Three **major** W3 gaps + one **critical** dormant-prompt issue.

## What tester flagged

| ID | Sev | Wave | Location | What's missing |
|---|---|---|---|---|
| F3-1 | major | W3 | `PLang.Tests/App/Modules/variable/settests.cs:39` | `Set_ReturnsOk` never asserts `result.Value`; revert `variable.set` to empty `Data.Ok()` — test still passes. |
| F3-2 | major | W3 | action-execution tests | `Action.RunAsync` no-mutation contract: `context.Variables.Set("__data__", result)` aliases without renaming. No C# test guards it. Reintroducing `result.Name = "__data__"` → all C# tests pass. |
| F3-3 | major | W3 | `PLang.Tests/App/Memory/VariablesTests.cs` | `Variables.Set(name, Data)` aliasing-without-clone: old code `ShallowClone` + rename; new code aliases same reference and leaves `Data.Name` advisory. Revert to old clone-if-names-differ branch → all C# tests pass. |
| F4c-1 | critical | W4c | builder prompt | Five prompt rules in `BuildGoal.llm` landed but dormant — no `.pr` rebuild shipped. Loop/ForeachDictionary/Signing Expired/TimedOut/ConditionCompound still fail as before. Coder-or-architect call. |
| F2-1 | minor | W2 | — | `On_InvalidType_ReturnsError` was removed; runtime `TypeMapping.TryConvertTo` path for bad enum string no longer exercised. |
| F3-4 | minor | W3 | `RenderTests` | `FluidProvider` kvp.Key path untested — tests use `Set(new Data("x", ...))` where key == Data.Name. |
| F4b-1 | minor | W4b | pre-existing | MaxDownloadSize/OnProgress/signed download untested — not W4b-introduced. |

## My scope for v2

Address the three **major** findings (F3-1, F3-2, F3-3) per user's instruction.
The minor findings and F4c-1 (prompt rebuild) are out of scope for this version.

## Pattern tester wants

All three findings share the same shape: semantic change was made in code, but
no C# test fails if someone reverts it. Deletion-test passes silently.

Example (F3-1):
```csharp
// current:
await Assert.That(context.Variables.GetValue("testVar")).IsEqualTo("testValue");
// missing:
await Assert.That(result.Value).IsEqualTo("testValue");
```

Fix is one assertion per test where the contract lives.
