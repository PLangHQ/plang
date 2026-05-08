# codeanalyzer v2 — runtime2-cleanup stage 2

Reviewing commit `188205f5 — runtime2-cleanup stage 2: drop dead Channels v1 helpers` against the architect's brief at `.bot/runtime2-cleanup/architect/stage-2-channels-v1-helpers-drop.md`.

## Summary

Dead-code deletion exactly as scoped. One file (`PLang/App/Channels/this.cs`), two surfaces gone, no new types, no new behaviour. Architect's three grep gates all pass:

```
$ grep -c "WriteAsync(string actorName"          → 0   ✓
$ grep -c "string? contentType"                  → 0   ✓
$ grep -c "channel is Channel.Stream.@this sc"   → 3   ✓ (out-of-scope leftovers, expected)
```

Both `DefaultHttpProvider.cs:852, 907` callers are positional `(AppChannels.Error, App.Data.@this.FromError(...))` — they bind cleanly to the new signature. Tests green per coder's summary (2755/2755 + 199/199).

## Per-file analysis

### PLang/App/Channels/this.cs

#### Pass 1a / 1b — OBP shape

- The surviving `WriteAsync` (lines 154–161) is the right shape: resolve → wrap → delegate to `channel.WriteAsync`. Five lines of body. The serializer routing happens inside `channel.WriteAsync → WriteCore`, which on Stream uses the parent Channels' Serializers per stage 1's wiring. No cross-class internal-reach left in this method.
- Doc-comment rewritten (lines 149–153) — the "Stage 4 moves this responsibility" reference is gone; the new copy describes the actual delegation chain. Matches the brief's deliverable #3.
- Smell #4 (allocate-here / mutate-there / clean-up-elsewhere at the API-surface level) closed for this method: the contentType-override path that reached into `sc.Stream` is gone.

#### Pass 1b shape-smell checklist

| # | Smell | Hit? |
|---|------|------|
| 1 | Public collection with rules from outside | No (unchanged from v1) |
| 2 | Cross-file lock | No |
| 3 | Two collections of same logical thing | Channel.App + Channel.Channels overlap — still deferred to stage 20, unchanged |
| 4 | Allocate / mutate / clean-up split | Closed for `WriteAsync` |

#### Pass 2 — simplification

The whole point of the stage. Body went from ~28 lines (with the contentType branch + try/catch) to ~5. The remaining try/catch around the contentType branch is also gone — `channel.WriteAsync` already does its own exception envelope (Channel/this.cs:124–129), so removing the duplicate catch was the right move.

#### Pass 3 — readability

The new doc-comment names the call chain explicitly: "delegates to the channel's own WriteAsync (which fires events and routes through WriteCore + the per-actor Serializers)". A reader can follow the path from this method without digging into the channel base class.

#### Pass 4 — behavioural reasoning

Two questions for any deletion: was anything reachable, and did anything else compensate?

- The two-string overload had **zero** callers (verified: `grep -rn "Channels\.WriteAsync\b" PLang/` shows only the two DefaultHttpProvider single-string sites). Deleting the method changes no call graph.
- The contentType-override branch was reachable only when (a) caller passed non-null `contentType` AND (b) the resolved channel was a Stream. **Zero** callers ever pass `contentType`. Removing the branch + the parameter is invisible to all callers.
- Removing the parameter is source-compatible because both DefaultHttpProvider sites pass positional args (`channelName, data`); they don't name `contentType:`. Compile verified by the green test suite.

#### Pass 5 — deletion test

The two deleted blocks: zero call graph impact (dead). The surviving lines all earn their place — resolve / envelope / delegate are each load-bearing.

#### Verdict: CLEAN

---

## Out-of-scope leftovers (architect-flagged, confirmed still present)

Three `is Channel.Stream.@this sc` casts remain at lines 169, 198, 218 in `ReadChannelAsync` / `WriteTextAsync` / `ReadTextAsync`. Same shape smell as the contentType-override that just got deleted (especially line 169, which still reaches into `sc.Stream` and `sc.Mime`). The brief explicitly excluded them. Confirming they're untouched, and confirming the breadcrumb is in place for whichever future stage closes them — likely a "Channel polymorphism for text I/O" stage that adds virtual `WriteTextAsync`/`ReadTextAsync` to `Channel.@this`.

## Carryover from v1 (status check, not new findings)

| # | v1 finding | Status after stage 2 |
|---|------------|----------------------|
| 1 | Snapshot back-ref aliasing (`Channels.this.cs:126–132`) | Untouched. Still latent. Not stage-2 scope. |
| 2 | Stale `// Stage 1:` comment at line 53 | **Still present.** Stage 2 didn't touch the ctor. The brief allowed touching it ("if you touch a method whose comment is now wrong"); coder interpreted this as ctor not in scope. Trivial; can ride along on the next file-edit. |
| 3 | `AppThis_SerializersExists_PerActor` under-asserts | Untouched. Not stage-2 scope. |

Stage 2 didn't introduce any new instances of these shapes.

## Verdict

**CLEAN.** Stage 2 lands the exact dead-code deletion the architect scoped. No new findings on this stage; all v1 carryovers are correctly untouched (out of stage-2 scope).
