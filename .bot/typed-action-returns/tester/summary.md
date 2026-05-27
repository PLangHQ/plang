# Tester — typed-action-returns

## Version
v1 — first tester pass on this branch.

## What this is

The coder shipped typed `Run()` returns across the action surface (Stages 0–4) plus two bonus refactors (every `ISerializer` method returns `Data`; `http` body dispatch flows through `Serializers.GetByContentType` with a `TextFallback` for parse failures). Goal of this pass: confirm the full suite is green and that the tests actually verify the new contracts rather than rubber-stamp them.

## What was done

- Clean rebuild (per CLAUDE.md "Stale-binary trap").
- Full C# suite (`PLang.Tests` TUnit binary): **3124/3124 PASS**.
- Full PLang suite (`Tests` via fresh `plang --test`): **221/221 PASS**, 0 fail, 0 stale. (Coder's handoff mentioned 12 "stale placeholders"; all 12 + 1 were implemented in commit `2553dd7f2`.)
- Audited the 13 `Tests/TestModule/TypedReturns/` `.test.goal` files + their `.pr.json`. No text-vs-action drift; all step `text` semantically matches `actions[0].module.action`. The Stage 2 goals exercise `goal.getTypes`, which now reflects on `Run()` signatures — so they're effectively static metadata checks (correct for the stage's contract).
- Audited the C# Stage 0–4 test files (`PLang.Tests/App/TypedReturnsTests/`). Stage 4 Build() inference tests are particularly strong: they exercise `RunBuildPass` end-to-end with real `PrAction`s and assert against the stamped `Type=` parameter on the terminal `variable.set`. Three precedence cases covered (user hint wins, build wins when no hint, explicit `Type="object"` preserved).
- Spot-checked the Serializers Data refactor and http TextFallback paths for error-path coverage. Found two missing-coverage gaps (see findings #2, #3 in `test-report.json`).

## Verdict

**PASS.** Test report at `.bot/typed-action-returns/test-report.json`. 4 minor + 1 info findings; none blocking.

### Key findings (full detail in test-report.json)

1. (minor) `FileRead_Build_LiteralMissingFile_WritesBuildWarning` only verifies the builder channel is non-noop — it never reads what was written. A regression that emits no warning at all would still pass.
2. (minor) **Serializer Data refactor error paths are entirely unverified.** Every `Deserialize_*` test in `JsonStreamSerializerTests` / `TextStreamSerializerTests` uses `.Value!` on happy-path input. The try/catch over `JsonException` / `NotSupportedException` / `IOException` that the refactor explicitly added is not exercised by a single test.
3. (minor) **http TextFallback parse-failure path uncovered.** No test sends `Content-Type: application/json` with malformed body to confirm the fallback kicks in — yet that's the headline new behavior of `ParseResponseAsync` post-refactor.
4. (minor) `CompileLlm_Kernel_ContainsTypeHintRule` is a string-presence check; behavior-level coverage already exists in `BuilderValidate_UserHintWinsOverBuildInference`, making this one redundant rather than wrong.
5. (info) **Process miss:** coder did not write `.bot/typed-action-returns/coder/v1/baseline-tests.md`. With a fully-green pre-state this didn't bite, but the workflow expects it.

## Code example (the strongest test pattern in this branch)

`Stage4_TypeHintPrecedenceTests.BuilderValidate_DistinguishesExplicitObject_FromDefaultObject` — the kind of test the rest of the branch should aspire to:

```csharp
// Explicit Type="object" already set on the variable.set → Build()'s "csv"
// must NOT overwrite. Catches the subtle "explicit object got promoted by
// the inference layer" bug that earlier draft Build() versions hit.
var setAction = Make("variable", "set",
    ("Name", "x"), ("Value", "%!data%"), ("Type", "object"));
var actions = ActionsOf(Make("file", "read", ("Path", "foo.csv")), setAction);
await RunBuildPass(actions, _app);

var typeParam = setAction.Parameters.First(p =>
    string.Equals(p.Name, "Type", StringComparison.OrdinalIgnoreCase));
await Assert.That(typeParam.Value).IsEqualTo("object");
```

End-to-end through `RunBuildPass`, asserts on the actual mutated parameter rather than a return code. If the precedence rule were reversed, this catches it. The pattern is repeatable for the missing-coverage gaps in findings #2 and #3.
