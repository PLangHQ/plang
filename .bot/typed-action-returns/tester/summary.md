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

**FAIL.** Suites are all green (3124/3124 C# + 221/221 PLang) but two confirmed false-greens block the verdict. Per the strict rule: a test that claims to verify X but provably doesn't = red. Test report at `.bot/typed-action-returns/test-report.json`.

### Confirmed false-greens (blocking)

1. **(critical) `FileRead_Build_LiteralMissingFile_WritesBuildWarning`** asserts `Channels.Channel("builder")` is not the noop sink. The channel is registered in Setup() unconditionally, so the assertion is trivially true regardless of what Build() did. The test name claims warning-emission coverage; it provides none.

2. **(critical) The advertised Data<object> double-wrap coverage doesn't exist.** The coder's handoff calls this the branch's headline footgun ("every typed handler in this branch was checked"). The supposed test layer:
   - Stage 2 plang `Test*DownstreamVariableAnnotatesAs*.test.goal` use `goal.getTypes`, which reflects on `Run()` signatures — never invokes a handler.
   - `Stage2_MechanicalTypings.DataValueFromTypedRun_NotDoubleWrapped` checks `T` at the static type level.
   Neither catches the actual footgun, which is a *runtime* owned-construction bug: T declared as `object`, runtime `.Value` is itself a `Data<X>`. Would ship green.

### Other findings (non-blocking, follow-up)

3. (minor) Serializer Data refactor error paths are entirely unverified — every `Deserialize_*` test uses `.Value!` on happy-path input.
4. (minor) http `TextFallback` parse-failure path uncovered.
5. (minor) `CompileLlm_Kernel_ContainsTypeHintRule` is a redundant string-presence check.
6. (info) Coder skipped `baseline-tests.md`.

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
