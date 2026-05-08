# codeanalyzer — runtime2-cleanup

## Version

v1 — review of coder v1 (stage 1: per-actor `Channels.Serializers` as single home).

## What this is

First codeanalyzer pass on this branch. Coder just landed stage 1 (commit `c74be34e`) — three surfaces holding/aliasing serializer registries collapsed to one canonical home (per-actor `Channels.@this.Serializers`):

- `App.@this.Serializers` deleted (the App-root shortcut bypassed actors).
- `Channel.Stream.@this._serializers` field + lazy property deleted (third copy per Stream).
- `Channel.@this.Channels` back-ref added; `Channels.Register` is the single point that stamps it.
- 5 production caller sites + the test-side sweep done.

Architect's brief: `.bot/runtime2-cleanup/architect/stage-1-serializers-single-home.md`.
Coder's summary: `.bot/runtime2-cleanup/coder/summary.md`.

## What was done

Five-pass review on every changed file. Findings live in `v1/report.md`; verdict in `v1/verdict.json`.

**Verdict: PASS for stage-1 scope.**

The diff matches the brief line-for-line; residual greps (`app\.Serializers\b`, `_serializers` in `PLang/App/Channels/`) come back empty; both test suites green.

### Findings (none block stage 1)

1. **Snapshot back-ref aliasing** (`PLang/App/Channels/this.cs:138–144`) — `Channels.Snapshot()` calls `copy.Register(ch)` on the same channel instances, which mutates `ch.Channels = copy`. After Snapshot, the original Channels still contains `ch` in its dict but `ch.Channels` points to the snapshot. Functionally invisible today (shared `Serializers`/`App`/`Actor`), but the new back-ref is the first one *meant* to discriminate between foundational and overlay scopes — the aliasing wasn't observable before and is now. Belongs to a later stage's design call (Snapshot copies the channel wrapper, or Register stops setting the back-ref). Not a stage-1 fix.
2. **Stale `// Stage 1:` comment** (`PLang/App/Channels/this.cs:53`) — refers to runtime2-channels' Stage 1, confusing on this branch. Trivial.
3. **Per-actor invariant under-asserted** (`PLang.Tests/App/ChannelsTests/Stage6_EntryPointWiringTests.cs` — `AppThis_SerializersExists_PerActor`) — asserts both User and System Serializers non-null, but not that they're distinct instances. A regression that re-introduced a shared singleton would slip past. Trivial; add `IsNotEqualTo`.

### Code example — the snapshot finding

```csharp
public @this Snapshot()
{
    var copy = new @this(_app, Serializers) { Actor = Actor };
    foreach (var ch in _channels.Values)
        copy.Register(ch);   // ch.Channels = copy. ch is also in original._channels.
    return copy;
}
```

After this call the original Channels and the snapshot share the same channel instances, but every `ch.Channels` points at the most-recently-registered owner. Last-write-wins back-ref aliasing. Today: shared `Serializers` instance hides it. Tomorrow: any read of `channel.Channels` to scope a foundational-vs-overlay distinction silently gets the wrong answer.

## Files

- `v1/plan.md` — review approach.
- `v1/report.md` — full per-file analysis.
- `v1/verdict.json` — `{status: "pass"}`.

## Next

```
run.ps1 tester stage-1 "Review the code on branch runtime2-cleanup" -b runtime2-cleanup
```

The coder may want to squash findings #2 and #3 in a quick follow-up before stage 2 starts. Finding #1 is for the architect to thread into the right future stage's brief.
