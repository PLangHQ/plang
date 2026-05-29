# Security summary — builder-ergonomics

**Version:** v1
**Verdict:** PASS (1 Low, latent)

## What this is

`builder-ergonomics` shipped a per-channel `IsExecuting` AsyncLocal recursion
guard, replacing the previous `FreezeFoundational` / `Snapshot` /
`PushChannelsOverride` mechanism. Also: builder-channel routing, P4 root-cause
error chaining in `Conversion.cs`, P6 confidence-per-step, planner verb-rule
tweaks. Tester v2 PASS with mutation-confirmed guard coverage.

## What I checked

- **Channel recursion guard** (`channel.goal.@this`, `AppChannels.Get`,
  `Actor.@this`). AsyncLocal flow into the awaited goal body, finally restore,
  cycle A→B→A containment, concurrency across goal channels, Task.Run inheritance
  of the flag, private/get-only enforcement. Sound.
- **Removed-API straggler check.** `FreezeFoundational` / `FoundationalChannels` /
  `PushChannelsOverride` / `Snapshot` — source-tree grep clean (only stale `obj/`
  metadata).
- **`Conversion.cs` error chaining.** Inbound-Error mutation only, message format
  unchanged, no new sensitive egress.
- **`Tagged.cs`.** Test-only `ClearCacheForTests` / `CacheSize` removed; no
  production callers; unbounded `_cache` is pre-existing.
- **Builder pipeline.** Build-time orchestration only; BuilderChannel.goal body
  doesn't close any cycle.
- **Semgrep.** 15 hits, all known baseline (serializer-hygiene).

## Findings

- **F1 (Low, open).** `AppChannels.Channel(string name)` opportunistic-write
  variant bypasses `IsExecuting`. Today only `file/read.cs:76` calls it (for
  "builder" warnings) and `BuilderChannel.goal` body is `- write out %!data%`, so
  no recursion path closes. Latent — any future `Channel(name)` caller whose name
  overlaps an executing goal-channel and triggers IO arms the loop. Two-line fix
  mirroring the `Get`-side guard. Not a blocker.

## Code example — the new guard

```csharp
// PLang/app/channels/this.cs
public channel.@this? Get(string name)
{
    if (!_channels.TryGetValue(name, out var channel)) return null;
    if (channel is channel.goal.@this g && g.IsExecuting) return null;  // recursion isolation
    return channel;
}

// PLang/app/channels/channel/goal/this.cs
var prev = _executing.Value;
_executing.Value = true;
try   { return await Actor.App.RunGoalAsync(Goal, ctx, ct); }
finally { _executing.Value = prev; }
```

## Next

Branch is clean. Auditor should review the broader merge story; F1 can ride a
later channel-routing pass.

VERDICT: PASS
Next: run.ps1 auditor builder-ergonomics "Review the code on branch builder-ergonomics" -b builder-ergonomics
