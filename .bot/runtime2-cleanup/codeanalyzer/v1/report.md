# codeanalyzer v1 — runtime2-cleanup stage 1

Reviewing commit `c74be34e — runtime2-cleanup stage 1: per-actor Channels.Serializers as single home` against the architect's brief at `.bot/runtime2-cleanup/architect/stage-1-serializers-single-home.md`.

## Summary

Stage 1 lands the shape it was scoped to land. The brief's deliverables match the diff line-for-line: `App.Serializers` deleted; `Channel.@this.Channels` back-ref added with `internal set`; `Channel.Stream.@this._serializers` and its lazy property gone; `Channels.Register` is the single point that stamps both `App` and `Channels` on a registered channel; the five-site external caller sweep is exactly the five sites the brief enumerated; the residual greps for `app\.Serializers\b` and `_serializers` come back empty. Tests pass on both the C# and PLang sides.

I have **one substantive finding** (latent footgun in `Channels.Snapshot`) and **three minor notes**. None of them block the stage-1 verdict, but the snapshot finding is the kind of thing that gets harder to spot later — flagging it now is the cheap moment.

---

## PLang/App/this.cs

### Pass 1a / 1b — OBP shape

Property deletion only — no shape change. The line that used to read

```csharp
public Serializers Serializers { get; } = new Serializers();
```

is gone. Nothing in this file references it. Clean.

### Verdict: CLEAN

---

## PLang/App/Channels/Channel/this.cs

### Pass 1a — OBP rules

The new property:

```csharp
public global::App.Channels.@this? Channels { get; internal set; }
```

is the right shape. `internal set` means only `App.Channels.@this.Register` can set it; consumers read. That matches the discipline the principles file flags: back-refs are write-controlled by the owner that registers the relationship.

Two back-refs now coexist on `Channel.@this`: `App` (line 75) and `Channels` (line 83). That's smell #3 in the spirit ("same logical thing stored twice across types" — `App` is reachable from `Channels._app`). The architect *explicitly* deferred this to **stage 20** in the brief's "Stages that follow this one" section, and stage 20 needs `Channels.@this` to expose a public `App` accessor first (today `_app` is private — line 19 of `Channels/this.cs`). So `Channel.App` cannot be removed yet without that extra step, which stage 20 will own. This is correctly out of stage-1 scope; flagging as confirmation that stage 20 has real work to do.

### Pass 1b — shape smells

| # | Smell | Hit? |
|---|------|------|
| 1 | Public `List<T>`/`Dict`/`HashSet` with rules from outside | No (`Metadata` is pre-existing and out of scope) |
| 2 | `lock (other.X)` from outside | No |
| 3 | Two collections of the same logical thing | App/Channels back-ref overlap — stage-20 deferred (architect-acknowledged) |
| 4 | Allocate-here / mutate-there / clean-up-elsewhere | No (Register stamps both back-refs in one place) |

### Pass 4 — behavioural reasoning

`MatchingBindings` (lines 179–200) reads `App.Events` and `Actor.Context.Events` independently. With `Channels` now also set in `Register`, the brief notes that future `App` reads here could route through `Channels?.App` — but **only after stage 20 exposes `Channels.App`**. Today `App` and `Channels` are stamped at the same moment (lines 113–114), so there's no execution-ordering window where one is null but the other isn't. Safe.

### Verdict: CLEAN

---

## PLang/App/Channels/Channel/Stream/this.cs

### Pass 1a — OBP rules

Field + lazy property deletion was the smell-killer. `WriteCore` (line 53) now reads `Channels!.Serializers.SerializeAsync(...)` — going through the parent registry. That's the canonical "navigate through your owner" shape.

### Pass 4 — behavioural reasoning (the `Channels!` null-suppression)

`Channels!` at line 53 throws NRE if `Channels` is null. The brief acknowledged the boot-ordering risk and the coder updated 7 unit tests that constructed a Stream channel directly and called `WriteCore` without going through `Register`. Production paths all go through `Channels.Register` before any write, and `CreateMemoryChannel` (Channels.this.cs:268) routes through `Register`. So today's call graph is safe.

The latent footgun: the public factory methods `Channel.Stream.@this.Input/Output/Memory` (lines 31–40) still let external code construct a Stream channel without going through `Register`. If a future caller does `var s = Channel.Stream.@this.Memory("x"); await s.WriteAsync(...);` without first calling `someChannels.Register(s)`, they NRE. The same was true for `_serializers` before stage 1 — it would lazy-allocate a fresh Serializers; the bug profile changed from "silent third copy" (worse) to "loud NRE" (better). I'd argue the NRE is the correct trade — it forces callers into the single registration path. Worth a sentence in the type's class doc-comment if anyone's expanding the file later, but not a stage-1 finding.

### Verdict: CLEAN

---

## PLang/App/Channels/this.cs

### Pass 1a — OBP rules

`Register` (lines 111–117) is the single registration path and now stamps three back-refs in one place:

```csharp
public void Register(Channel.@this channel)
{
    channel.App = _app;
    channel.Channels = this;
    if (channel.Actor == null) channel.Actor = Actor;
    _channels[channel.Name] = channel;
}
```

This is exactly the shape the principles file describes — the type that owns the data is the type that owns the discipline.

The two internal sites at lines 178 and 206 read `Serializers` (this collection's own) instead of the old `sc.Serializers` (the per-Stream copy). Correct.

### Finding 1 — `Snapshot()` mutates back-refs on shared channel instances (low severity, latent)

**File:** `PLang/App/Channels/this.cs:138–144`

```csharp
public @this Snapshot()
{
    var copy = new @this(_app, Serializers) { Actor = Actor };
    foreach (var ch in _channels.Values)
        copy.Register(ch);
    return copy;
}
```

`copy.Register(ch)` mutates `ch.Channels = copy` (line 114). After `Snapshot()`:

- The original `_channels` dict still contains `ch`.
- `ch.Channels` no longer points to the original — it points to the snapshot.

So the original Channels and the snapshot share the *same* channel instances, but every channel's `Channels` back-ref reflects the most-recently-registered owner (last write wins). This is shape smell #4: allocate-here (original), mutate-there (snapshot), with the back-ref aliasing invisible from any single file.

Today the bug is **functionally invisible** because the snapshot ctor passes the parent's `Serializers` instance (line 140 — `new @this(_app, Serializers)`), so `Channels!.Serializers` resolves to the same registry whichever back-ref a write follows. Same `_app` and same `Actor` on both sides. So `Stream.WriteCore` produces identical output.

It becomes visible the moment any future code reads `channel.Channels` to find a sibling channel, get a per-Channels-instance state, or distinguish snapshot-vs-overlay scope. The new back-ref made this aliasing observable for the first time — `App` and `Actor` were already aliased the same way, but they're shared by design (and aliasing is harmless because the values are equal). `Channels` is *meant* to discriminate between the foundational set and the live overlay (`Actor.this.cs:43–45, 54`); the back-ref on shared channel instances doesn't.

**Why it matters:** the foundational-channels mechanism in `Actor.this.cs` exists precisely so goal channels can write to the original entry-point streams instead of the live overlay. A future feature that reads `channel.Channels` to locate that overlay-vs-foundational distinction would silently get the wrong answer.

**Recommendation (for the coder, not for stage 1):** either Snapshot copies channels (deep-ish — same Stream, new Channel.@this wrappers) or the back-ref isn't set by Register (Register only adds to the dict; back-refs are set somewhere else when the channel is "owned"). I lean toward the second — Register should be cheap registry mutation, not back-ref ownership. But this is a stage 6 / stage 20-adjacent design call, not a stage-1 fix.

**Action:** flag in this report. Don't fix in stage 1.

### Note 2 — stale comment (readability)

**File:** `PLang/App/Channels/this.cs:53`

```csharp
// Stage 1: ctor no longer opens console streams. Entry point wires (Stage 6).
```

This refers to **runtime2-channels' Stage 1**, not runtime2-cleanup's Stage 1. After this branch merges, two different Stage 1s point at this line through git blame. Suggest: drop "Stage 1:" prefix, leave the explanation. Trivial; coder fix.

### Verdict: NEEDS WORK *for the cleanup branch as a whole* (stage 6/20-adjacent latent finding); CLEAN *for stage 1 scope*.

---

## PLang/App/Goals/this.cs · PLang/App/Goals/Setup/this.cs · PLang/App/modules/file/providers/DefaultFileProvider.cs · PLang/App/Actor/Context/this.cs

Caller-sweep diffs only. Each replacement matches the pattern the brief specified:

- `app.Serializers.X` → `app.System.Channels.Serializers.X` (Goals — app-level shared infrastructure routes through System actor)
- `app.Serializers.X` → `action.Context.Actor.Channels.Serializers.X` (DefaultFileProvider — handler has Context, uses the resolving actor)
- DynamicData lambda — `() => Actor!.Channels.Serializers` (Context — Actor is set after Context construction)

The choice of *which* actor at each site is the right judgement call. App-level deserialization (Goals/Setup) routes through System; per-action serialization (DefaultFileProvider) routes through the action's actor. The lambda in `Context.this.cs:172` defers the read so `Actor!` is non-null at evaluation time — verified against the construction order in the surrounding code.

### Verdict: CLEAN

---

## Test-side sweep

`PLang.Tests/App/ChannelsTests/Stage6_EntryPointWiringTests.cs` — renamed `AppThis_SerializersExists_AtAppLevel` → `AppThis_SerializersExists_PerActor`, asserts both `app.User.Channels.Serializers` and `app.System.Channels.Serializers` are non-null.

### Note 3 — per-actor invariant under-asserted (readability)

The renamed test asserts both registries are non-null but does **not** assert they're distinct instances. The per-actor invariant ("each actor's Channels owns its own registry") is the *new contract* of this stage. A regression that re-introduced a shared instance (e.g., Channels ctor falling back to a single static) would slip past this test. Suggest adding:

```csharp
await Assert.That(app.User.Channels.Serializers).IsNotEqualTo(app.System.Channels.Serializers);
```

Trivial; coder fix.

The 7 Stage1/Stage2/Stage8 unit-test updates (registering through `Channels.Register` before exercising `WriteCore`) are exactly the right shape for the new contract — they go through the single registration path.

### Verdict: CLEAN

---

## Pass 5 — deletion test

For each line introduced or modified:

- `Channel.@this.Channels` property — needed by Stream.WriteCore. Deleting it breaks the ban on Stream's local registry. Earns its place.
- `channel.Channels = this` in Register — needed for the back-ref. Earns its place.
- `Channels!.Serializers.SerializeAsync` in Stream.WriteCore — needed (replaces deleted lazy property). Earns its place.
- All five caller-sweep edits — needed (the property they used is deleted). Earn their places.

No dead lines. No introduced abstractions beyond the one the brief asked for.

---

## Verdict

**CLEAN** for stage-1 scope. The diff matches the brief; no shape regressions; behaviour preserved; tests green.

Findings:

1. **Snapshot back-ref aliasing** (latent, low severity) — `Channels.Snapshot` mutates `channel.Channels` on shared channel instances. Functionally invisible today (shared Serializers/App/Actor), but the new back-ref made the aliasing observable. Belongs to a future stage (likely stage 6 or stage 20-adjacent design discussion), not stage 1.
2. **Stale `// Stage 1:` comment** at `Channels/this.cs:53` — refers to runtime2-channels' Stage 1, confusing on this branch. Trivial fix.
3. **Per-actor invariant under-asserted** in `AppThis_SerializersExists_PerActor` — assert distinctness, not just non-null. Trivial fix.

None of these block stage 1. Recommend the coder squashes #2 and #3 in a quick follow-up before stage 2 starts; #1 is a flag for the architect to thread into the right future stage's brief.
