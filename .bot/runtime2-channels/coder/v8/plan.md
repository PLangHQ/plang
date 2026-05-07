# v8 Plan — tester v7 missing-coverage probes

## Scope
Add two regression tests to `PLang.Tests/App/ChannelsTests/Stage8_ChannelEventsTests.cs` so future refactors can't silently revert v7's B1/L1 fixes.

## Tests

### 1. `Enter_OnOneInstance_DoesNotLeakToAnother` (B1 probe)
```csharp
var evA = new global::App.Channels.Channel.Events.@this();
var evB = new global::App.Channels.Channel.Events.@this();
using var _ = evA.Enter("X");
await Assert.That(evA.IsActive("X")).IsTrue();
await Assert.That(evB.IsActive("X")).IsFalse();
```
If `_active` becomes `static` again, evB sees evA's active set.

### 2. `Enter_FromConcurrentChild_DoesNotLeakToParent` (L1 probe)
Tester's empirically validated shape — the naive `Task.WhenAll` version is a false green:
```csharp
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
var leaked = ev.IsActive("B");
release.SetResult();
await t;
await Assert.That(leaked).IsFalse();
```
If `Enter` mutates the parent's set in place (no copy-on-write), the parent flow sees "B" while the child is still inside its scope.

## Verification
1. Build: `dotnet build PlangConsole`
2. Run: `dotnet run --project PLang.Tests` — expect 2762/2762.
3. Sanity-check coverage: revert each fix in working tree, confirm the matching probe goes red, restore.

## Files
- `PLang.Tests/App/ChannelsTests/Stage8_ChannelEventsTests.cs` — append two `[Test]` methods.

No production code change. No PLang-side change.
