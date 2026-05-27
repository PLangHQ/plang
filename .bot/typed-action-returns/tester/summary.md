# Tester — typed-action-returns

## Version
v2 — mutation-validating coder v1 follow-up to tester v1 critical false-greens.

## What this is

Tester v1 (commit `a8f7390c1`) shipped a FAIL verdict on two confirmed false-greens despite all 3124 C# + 221 PLang tests being green:

1. `FileRead_Build_LiteralMissingFile_WritesBuildWarning` only asserted the builder channel was not noop (trivially true from Setup).
2. The advertised "Data<object> double-wrap" coverage was entirely static — `goal.getTypes` reflection on Stage 2 plang goals and a single C# type-level check. Neither would have caught the coder's stated headline footgun (runtime `Data<object>{ Value = Data<X>{...} }`).

Plus 3 minor-coverage gaps (serializer error paths, http TextFallback parse-failure).

The coder responded with commit `6965c89e4`. This version verifies — including mutation testing — that the new tests are honest.

## What was done

- **Re-ran full suites after clean rebuild.** C# 3136/3136 (+12 new), PLang 221/221. Both green.
- **Mutation-tested both critical fixes.** Commented out the missing-file warning emission in `file/read.cs:76` → `FileRead_Build_LiteralMissingFile_WritesBuildWarning` failed with "Expected to contain definitely-missing-stage4.csv ... but found \"\"". Replaced `Ok(first.Value)` with `Ok(first)` in `list/first.cs:15` to force the implicit-operator double-wrap → `ListFirst_OnPopulatedList_ValueIsRawNotData` failed with the exact "footgun fired" message. Both reverted; source clean.
- **Audited the 3 minor-coverage fixes.** JsonStreamSerializer asserts `Error.Key="JsonDeserializeError"` (not just Success=false). TextStreamSerializer uses a private `ThrowingStream` that raises real IOException, asserts `Error.Key="TextDeserializeError"`/`"TextSerializeError"`. http BodyDispatch test asserts body is the literal malformed string, proving TextFallback fires. All honest.
- **Inspected the `EveryDataObjectRunHandler_IsKnownToThisTest` tripwire.** Reflection sweep enumerates all `Task<Data<object>>` Run() handlers and `IsEquivalentTo`s a frozen list of 18. Any new `Data<object>` handler breaks the test; the failure message tells the author to either narrow T or add a runtime invocation test. Good shape.

## Verdict

**PASS.** Suites green (3136/3136 C# + 221/221 PLang); both tester v1 critical false-greens fixed and mutation-tested; all 3 minor-coverage tests assert behavior, not just success. No new findings.

Test report at `.bot/typed-action-returns/test-report.json`.

## For v2 after review — what the reviewer (tester v1) flagged + what changed

Reviewer flagged two critical false-greens and three minor-coverage gaps. Before/after for the headline fix:

```csharp
// Before (false green — Channels.Register makes this trivially true):
_app.User.Channels.Register(_app.User.Channels.CreateMemoryChannel("builder"));
await Build("file", "read", ("Path", "definitely-missing-stage4.csv"));
await Assert.That(_app.User.Channels.Channel("builder")).IsNotTypeOf<noop.@this>();

// After (mutation-tested honest — reads what Build() actually wrote):
var channel = (stream.@this)_app.User.Channels.CreateMemoryChannel("builder");
const string missing = "definitely-missing-stage4.csv";
await Build("file", "read", ("Path", missing));
channel.Stream.Position = 0;
var written = await channel.ReadAllTextAsync();
await Assert.That(written).Contains(missing)
    .Because("Build() must write a missing-file warning whose message names the offending path.");
```

Commenting out the `WriteAsync` call in `file/read.cs:76` now flips this red. The original version stayed green.
