# test-designer — runtime2-callstack — v1

## What this is

Test contract for the causal callstack refactor designed by the architect. The architect's plan reshapes `App/CallStack/` (frame → `Call.@this`, ConcurrentStack → AsyncLocal-rooted tree, Cause distinct from Caller, recovery via Cause linkage with `Handled` flag, `%!error%` via `app.Errors.Push` AsyncLocal, Variables collection-level events driving Diff capture, `--debug={callstack:{...}}` JSON parse, `tag` action). This v1 produces the C# and PLang test files that pin behavior so the coder has a fixed contract to implement against.

## What was done

- Wrote `v1/plan.md` covering all batches and surfaced two decisions (P8 → C# memory test; P9 → use existing `timeout after` modifier rather than introducing a cancel primitive). User approved both.
- Presented 4 explicit batches (Call shape, CallStack tree, AsyncLocal forking, Cause linkage); user then approved remaining batches in bulk and asked to proceed without further confirmation.
- Wrote 15 C# test files (~95 tests total) under:
  - `PLang.Tests/App/CallStack/` — Call shape, tree mechanics, AsyncLocal forks, Cause linkage, cycle detection, flags, Items extension, Diff capture (incl. memory test), Audit, SnapshotChain.
  - `PLang.Tests/App/Errors/` — `app.Errors.Push` LIFO + AsyncLocal scope; ServiceError chain composition.
  - `PLang.Tests/App/Variables/` — collection-level OnSet/OnCreate/OnRemove + back-compat for per-variable events.
  - `PLang.Tests/App/Modules/debug/` — `tag` action handler.
  - `PLang.Tests/App/Debug/` — `--debug={callstack:...}` JSON parse.
- Wrote 16 PLang `.test.goal` files (one goal per file) under `Tests/App/CallStack/`. Removed the four obsolete goals that targeted the deprecated `%!callStack.Depth%` API (incl. their `.build/` cache).
- All test bodies are stubs (`Assert.Fail("Not implemented")` for C#, `- throw "not implemented"` for PLang). Coder fills them in alongside production code.

## Code example — C# stub pattern

```csharp
[Test]
public async Task Push_WithCause_SetsCauseField()
{
    // stack.Push(action, cause: erroredCall) → call.Cause == erroredCall.
    await Task.Yield();
    Assert.Fail("Not implemented");
}
```

## Code example — PLang stub pattern

```
TestRecoveryActionCauseIsErroredAction
/ Inside a recovery body invoked by error.handle, %!callStack.Current.Cause.Action.Module% resolves to the module of the action that errored (proves Cause is wired to the errored Call).
- throw "not implemented"
```

## Files changed

Created (test stubs):
- `PLang.Tests/App/CallStack/{Call,CallStackTree,AsyncLocalFork,CauseLinkage,CycleDetection,CallStackFlags,ItemsExtension,DiffCapture,CallStackAudit,SnapshotChain}Tests.cs`
- `PLang.Tests/App/Errors/{ErrorsScope,ServiceErrorChain}Tests.cs`
- `PLang.Tests/App/Variables/CollectionEventsTests.cs`
- `PLang.Tests/App/Modules/debug/TagActionTests.cs`
- `PLang.Tests/App/Debug/DebugCallStackParseTests.cs`
- 16 × `Tests/App/CallStack/*.test.goal`

Deleted (obsolete — targeted removed `%!callStack.Depth%` API):
- `Tests/App/CallStack/CallStack.test.goal`, `Start.goal`, `Inner.goal`, `InnerTest.goal`
- `Tests/App/CallStack/.build/` (regenerates on next build)

## Status / what's next

Done. `verdict.json` is `{ "pass": true }`. Suggest **coder** next to implement Phases 1–9 of the architect plan and make these tests pass. Note that `PLang.Tests/App/Core/CallStackTests.cs` (the existing tests against the old `IsEnabled`/`Frame.Parent`/`EventId`/`Phase` shape) is unchanged here — coder will delete/replace it as part of the migration.
