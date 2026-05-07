# Coder summary — runtime2-channels

## Version

**v8** (current). v1–v7 history kept in this branch's report.json.

## What this is
Tester v7 PASS but flagged two minor missing-coverage findings on `PLang/App/Channels/Channel/Events/this.cs` — neither v7 fix had a regression test. Tester empirically verified by reverting each in turn (working tree only) that the suite stayed green both times. v8 adds the two probe tests so future refactors can't silently re-introduce either bug.

## What was done

Two new `[Test]` methods appended to `PLang.Tests/App/ChannelsTests/Stage8_ChannelEventsTests.cs`:

- **`EventsActiveSet_IsInstanceScoped_NotShared`** (B1 probe) — instantiates two `Events.@this`, asserts that `evA.Enter("X")` does not bleed into `evB.IsActive("X")`. If `_active` becomes `static` again, this fails.
- **`Enter_FromConcurrentChild_DoesNotLeakChildIdToParentFlow`** (L1 probe) — uses `TaskCompletionSource` to pause a child mid-`Enter`, then asserts the parent flow does **not** observe the child's id. Tester explicitly noted the naive `Task.WhenAll` shape is a false green; this is the empirically-validated form.

No production code change.

### Verification
- Built clean.
- Stage8 filter: 17/17 (was 15).
- Full C# suite: **2762/2762**, 0 fail, 0 skip (was 2760).
- Sanity-checked L1 by reverting it in working tree → L1 probe correctly went red → restored. (B1 revert breaks compilation — `Releaser` accesses `_active` via instance — so the static-reintroduction path is doubly guarded.)

### Files modified
- `PLang.Tests/App/ChannelsTests/Stage8_ChannelEventsTests.cs` — +37 lines, two `[Test]` methods.

### Artifacts
- `.bot/runtime2-channels/coder/v8/plan.md`
- `.bot/runtime2-channels/coder/v8/v7_review_summary.md`

## Code example

```csharp
[Test]
public async Task Enter_FromConcurrentChild_DoesNotLeakChildIdToParentFlow()
{
    var ev = new global::App.Channels.Channel.Events.@this();
    using var _ = ev.Enter("A");
    var inside = new TaskCompletionSource();
    var release = new TaskCompletionSource();
    var t = Task.Run(async () =>
    {
        using var __ = ev.Enter("B");
        inside.SetResult();
        await release.Task;
    });
    await inside.Task;
    var leaked = ev.IsActive("B");   // parent's snapshot must not see child's "B"
    release.SetResult();
    await t;
    await Assert.That(leaked).IsFalse();
}
```

## For v8 after review
This is the response to tester v7. Hand off to **security** next per tester's recommendation.
