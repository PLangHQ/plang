# codeanalyzer — runtime2-cleanup

## Version

v2 — review of coder v2 (stage 2: drop dead Channels v1 helpers).

## What this is

Second codeanalyzer pass on this branch. Coder just landed stage 2 (commit `188205f5`) — pure dead-code deletion in `PLang/App/Channels/this.cs`:

- The `WriteAsync(string actorName, string channelName, ...)` overload (zero callers).
- The `contentType`-override branch + the `string? contentType` parameter on the surviving `WriteAsync` (zero callers ever passed it).

Surviving body shrunk from ~28 lines to ~5: resolve → envelope → delegate to `channel.WriteAsync`. Serializer routing happens inside the channel's own write path (per stage 1's wiring).

## What was done

Five-pass review on the single touched file. Architect's three grep gates all pass:

```
$ grep -c "WriteAsync(string actorName"          → 0
$ grep -c "string? contentType"                  → 0
$ grep -c "channel is Channel.Stream.@this sc"   → 3   (out-of-scope leftovers, expected)
```

DefaultHttpProvider's two callers at lines 852 and 907 use positional `(channelName, data)` form — they bind cleanly to the new signature. Tests green (2755/2755 + 199/199 per coder's summary).

**Verdict: PASS.** No new findings on this stage.

## Carryover from v1 (status check)

| # | v1 finding | Status |
|---|------------|--------|
| 1 | Snapshot back-ref aliasing | Latent, untouched. Out of stage-2 scope. |
| 2 | Stale `// Stage 1:` comment at `Channels/this.cs:53` | **Still present.** Coder didn't touch the ctor. Trivial; will ride along. |
| 3 | `AppThis_SerializersExists_PerActor` under-asserts distinctness | Untouched. Out of stage-2 scope. |

None re-introduced.

## Out-of-scope leftovers (architect-flagged, confirmed)

Three `is Channel.Stream.@this sc` casts remain at `Channels/this.cs:169, 198, 218` in `ReadChannelAsync` / `WriteTextAsync` / `ReadTextAsync`. The first reaches into `sc.Stream` and `sc.Mime` (same shape as the contentType branch just deleted). The latter two dispatch to Stream-specific public methods. A future "Channel polymorphism for text I/O" stage will close them.

## Files

- `v1/` — stage 1 review (PASS, 3 findings).
- `v2/plan.md` — review approach.
- `v2/report.md` — full per-file analysis.
- `v2/verdict.json` — `{status: "pass"}`.

## Next

```
run.ps1 tester stage-2 "Review the code on branch runtime2-cleanup" -b runtime2-cleanup
```
