# builder-ergonomics — docs summary

**Version:** v1

## What this is

`builder-ergonomics` started from a coder-written friction report on the PLang
builder (`.bot/builder-ergonomics/user-feedback.md`). The branch worked the
list and shipped four landing pieces:

1. **Channel recursion guard rewrite.** The `Actor.FoundationalChannels` /
   `PushChannelsOverride` / `FreezeFoundational` / `AppChannels.Snapshot`
   mechanism was deleted. Replaced by a private `AsyncLocal<bool> _executing`
   on each `Channel.Goal.@this`. The registry's `Get` treats an executing
   goal-channel as not-found. The shipped bug that forced the rewrite was a
   foundational-snapshot taken too early: late-registered channels (like
   `"builder"`) were invisible to writes from inside a goal-channel body.
2. **Root-cause-first error chaining.** When `Conversion.TryConvertTo` is
   handed an `errors.Error` and `Convert.ChangeType` throws, the conversion
   wrapper is appended to the source error's chain and the source is
   returned — so display shows the real cause, not the recovery handler's
   reformatting failure.
3. **Per-step confidence.** All four LLM passes in the builder
   (Plan/Compile/RefineActions/FixValidation) emit `confidence` with
   `Medium`/below carrying an `explanation`. `Low`/`VeryLow` surface as
   `⚠ planner|compiler <level>: …` build-output warnings.
4. **Builder output routing.** Build-time output now flows through a named
   `"builder"` channel registered at the top of `Build.goal` and backed by
   `BuilderChannel.goal`. All write-sites go through `EmitBuildEvent.goal`
   plus a Liquid template. Future redirection (file log, JSON stream, TUI)
   is a one-file swap of `BuilderChannel.goal`.

## What was done in docs v1

Closed five gaps. No XML doc gaps (the new C# surface is already
self-documenting at the source). No PLang `.goal` examples needed (no new
user-facing modules). No claude-md or character proposals on the branch.

**Modified docs:**
- `Documentation/v0.2/io-channels.md` — rewrote `Channel.Goal.@this`
  recursion paragraph, replaced the "Actor channel resolution & overrides"
  block (deleted APIs), and rewrote the "PLang — replace `output`" example
  to a fan-out shape that actually works under `IsExecuting`.
- `Documentation/v0.2/app-tree.md` — dropped the `FreezeFoundational()`
  actor line; added a back-pointer to `io-channels.md`.
- `Documentation/v0.2/scripts/check-app-tree.sh` — removed the
  `FoundationalChannels` skip regex (the canary it skipped is gone).
- `Documentation/v0.2/build.md` — added "Confidence per step" (one
  paragraph + level table + warning shape) and "Builder output routing"
  (architecture, files, why the indirection).
- `Documentation/v0.2/good_to_know.md` — appended "Recursion guards belong
  on the value, not on a parallel context layer" — the OBP lesson the
  branch's load-bearing change codifies.

## Code example — the load-bearing pattern this branch shipped

```csharp
// PLang/app/channels/channel/goal/this.cs
private readonly AsyncLocal<bool> _executing = new();
public bool IsExecuting => _executing.Value;

private async Task<global::app.data.@this> InvokeGoal(...)
{
    var prev = _executing.Value;
    _executing.Value = true;
    try { return await Actor.App.RunGoalAsync(Goal, ctx, ct); }
    finally { _executing.Value = prev; }
}

// PLang/app/channels/this.cs (registry)
public channel.@this? Get(string name)
{
    if (!_channels.TryGetValue(name, out var channel)) return null;
    if (channel is channel.goal.@this g && g.IsExecuting) return null;
    return channel;
}
```

One private AsyncLocal on the instance plus one branch at the registry
lookup. Replaces an entire override-stack mechanism.

## Verdict

PASS — branch ready to merge.
