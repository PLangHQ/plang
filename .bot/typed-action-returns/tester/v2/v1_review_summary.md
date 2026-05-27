# Tester v1 review — what came back

Tester v1 verdict was **FAIL** on two confirmed false-greens (suites all green, but the tests didn't verify what they claimed):

1. **(critical) `FileRead_Build_LiteralMissingFile_WritesBuildWarning`** — asserted only that the "builder" channel was not the noop sink. Channel is registered unconditionally in Setup(), so the assertion was trivially true regardless of what Build() actually wrote.
2. **(critical) Data<object> double-wrap coverage was entirely static** — Stage 2 plang goals used `goal.getTypes` (reflection on the Run() signature) and the C# `DataValueFromTypedRun_NotDoubleWrapped` test also operated at the type level. Neither would have caught the runtime owned-construction footgun the coder warned about (`Data<object>{ Value = Data<X>{...} }`).

Plus three minor-coverage gaps (serializer Data.Fail paths, http TextFallback parse-failure path, weak CompileLlm presence check) and an info note about missing baseline-tests.md.

## What the coder did

Commit `6965c89e4` "coder: address tester v1 false-greens + codeanalyzer v3 N1":

- **#1 fix:** Rewrote the FileRead test to construct a `MemoryChannel` via `Channels.CreateMemoryChannel("builder")`, drain `channel.Stream` after Build(), and assert `written.Contains(missing-path-string)`. The assertion now reads what was actually written to the channel rather than the channel's type.
- **#6 fix:** New file `PLang.Tests/App/TypedReturnsTests/RuntimeDoubleWrapTests.cs` invokes `list.first`/`list.get`/`list.last`/`math.add` Run() handlers and asserts `result.Value is Data` is false — catching the implicit-operator double-wrap at runtime, not statically. A 5th `EveryDataObjectRunHandler_IsKnownToThisTest` test uses reflection to pin the full set of 18 `Task<Data<object>>` handlers; the tripwire forces any new `Data<object>` handler to either narrow T or add a runtime invocation test below.
- **#2 fix:** 4 malformed-input tests in `JsonStreamSerializerTests` across DeserializeAsync/DeserializeAsync\<T\>/Deserialize/Deserialize\<T\> asserting `Success=false` AND `Error.Key="JsonDeserializeError"`.
- **#2 (text):** `ThrowingStream` private class in `TextStreamSerializerTests` raises real `IOException` on read/write; tests assert `Error.Key="TextDeserializeError"`/`"TextSerializeError"`.
- **#3 fix:** `BodyDispatch_BrokenJsonContentType_FallsBackToString` in Stage3 http tests sends Content-Type `application/json` with body `"{not json"` and asserts `resp.Body is string` AND equals the literal malformed text (proves TextFallback fires).
- #4 weak-assertion left as-is (acceptable; downstream behavior test exists).
- #5 baseline-tests.md still not produced (process-only).
- Codeanalyzer v3 N1 (3 stale `%__data__%` → `%!data%` comment updates in `goal/getTypes.cs`) folded into the same commit.

## Mutation-test results (this version)

Both critical fixes confirmed honest:

- **FileRead fix:** commented out `Context.Actor.Channels.Channel("builder").WriteAsync(...)` in `file/read.cs:76`. Test failed: `Expected to contain "definitely-missing-stage4.csv", because Build() must write a missing-file warning whose message names the offending path. but found ""`. Reverted.
- **Double-wrap fix:** replaced `Ok(first.Value)` with `Ok(first)` in `list/first.cs:15` to force the implicit-operator footgun. `ListFirst_OnPopulatedList_ValueIsRawNotData` failed with the exact "list.first double-wrapped: result.Value is itself a Data — the Data<object> implicit-operator footgun fired" message. Reverted.

Source clean post-mutation (verified via `git status`).
