# Tester summary — runtime2-channels

## Version
**v7** (latest). Reviews coder v7 fixes for codeanalyzer v3 findings on `Channel.Events.@this`.

## What this is
Coder v7 made a 2-line conceptual change to `PLang/App/Channels/Channel/Events/this.cs`:
- **B1:** `_active` AsyncLocal is no longer `static` — instance scope per `Channel.Events.@this`.
- **L1:** `Enter` is now copy-on-write; `Releaser.Dispose` restores the parent reference instead of mutating an inherited HashSet.

## What was done
- Clean rebuild from scratch; re-ran C# suite: **2760 / 2760 pass**, matches coder baseline.
- PLang results carried from coder baseline (no PLang surface touched in v7).
- Read-only deletion-test reasoning across `Stage8_ChannelEventsTests.cs` and the rest of `ChannelsTests/`.
- Confirmed `Binding.Id` is a random GUID-substring (`Binding/this.cs:89`) — narrows B1's real-world reach.

**Result: pass with two minor missing-coverage findings.** Neither fix is exercised by any existing test; reverting either would not turn the suite red. Both are defensive/structural fixes for hazards that have no current callsite. Suggested ~3-line unit tests for each in the report.

Files written:
- `.bot/runtime2-channels/tester/v7/plan.md`
- `.bot/runtime2-channels/tester/v7/result.md`
- `.bot/runtime2-channels/tester/v7/verdict.json`
- `.bot/runtime2-channels/test-report.json` (branch root, shared)

## Code example (suggested test for F1)
```csharp
[Test]
public async Task ActiveSet_IsScoped_PerChannelEventsInstance()
{
    var evA = new global::App.Channels.Channel.Events.@this();
    var evB = new global::App.Channels.Channel.Events.@this();
    using var _ = evA.Enter("X");
    await Assert.That(evB.IsActive("X")).IsFalse();
}
```
Three lines, fails immediately if `_active` were static again.

## Process note (v7)
Ingi instituted a strict rule mid-session: **tester does not modify source code**, even temporarily for a deletion test. I had begun reverting the B1 fix to run the deletion test and was stopped. The revert was rolled back immediately (working tree clean). Going forward, the deletion test is conducted by reading code and tests carefully and reasoning about which assertions would still hold — not by mutating the source. Saved as feedback memory.

## Next
Suggest **security** next. Consider also picking up the two missing-coverage suggestions as a small follow-up commit before security if you want them locked in alongside the fixes.
