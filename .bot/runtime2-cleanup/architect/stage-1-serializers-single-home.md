# Stage 1: `serializers-single-home`

**Read first:**
- `plan/principles.md` — the OBP discipline, especially the Context section ("Choosing what back-ref(s) a class holds" and "The smells").
- `plan/scope-map.md` — what's shared (App-level) vs per-actor for the App graph. Channels is per-actor; the per-actor scope is what makes this stage's allocation pattern correct.

**Goal:** Establish per-actor `Channels.@this.Serializers` as the single home for the serializer registry. Delete the `App.@this.Serializers` property (no replacement). Drop the per-Stream `Serializers` field. Stream channels reach their parent Channels' Serializers via a new `Channels` back-ref on the `Channel` base class.

The "single home" is *per actor* — each actor's Channels has its own Serializers instance. Boot-time defaults (Json, Plang, Text) get registered identically per actor; any future runtime registration applies to the actor that registered it. This is not the smell — it's the intended shape.

The smell being closed is **Stream channels each lazy-allocating their own Serializers** (smell #3 — same logical thing stored twice across types: a Stream channel's I/O should resolve through its parent Channels' registry, not a third copy unique to the Stream). Plus **App holding a redundant Serializers property** that bypasses Channels.

**Scope:**
- *Included:* delete `App.@this.Serializers` (App.this.cs:154); drop `_serializers` field and `Serializers` property from `Channel.Stream.@this`; add `Channels` back-ref to `Channel.@this` base class; route Stream's `WriteCore`/`ReadCore` through the new back-ref; update internal Channels.this.cs sites that reach `sc.Serializers`; sweep the 5 external callers of `app.Serializers`.
- *Excluded:* Channels.@this ctor signature (stays `(App app)` — it has an Actor property for per-actor reach, doesn't need Context). v1 helpers (stage 2). `ReadAsync<T>(filePath)` relocation (stage 8). Renames inside `Channels/Serializers/` (stage 15). Any wider Context-conversion of other classes.

**Deliverables:**
- `PLang/App/this.cs` — line 154 (`public Serializers Serializers { get; } = new Serializers();`) deleted.
- `PLang/App/Channels/Channel/this.cs` — gain a `Channels` back-ref alongside the existing `App` back-ref. Property: `public global::App.Channels.@this? Channels { get; internal set; }`. Set during `Channels.Register(channel)`, same point where `App` is set today.
- `PLang/App/Channels/Channel/Stream/this.cs` — `_serializers` field and `Serializers` property removed. `WriteCore` and `ReadCore` use `Channels!.Serializers.X(...)`.
- `PLang/App/Channels/this.cs` — `Register(Channel.@this channel)` at line 110 today contains `channel.App = _app` at line 112. **Add `channel.Channels = this` immediately after.** That's the only registration path; `GetOrCreate(name, factory)` exists at line 104 but has zero callers, and `CreateMemoryChannel` at line 263 routes through `Register`. Verified by grep — no other code paths add channels to `_channels` directly. The two internal sites at lines 176 and 204 that read `sc.Serializers.X` change to `Serializers.X` (the same Channels' own property — `this.Serializers`).
- External caller sweep:
  - `PLang/App/Goals/this.cs:320, 325` → `app.System.Channels.Serializers.Deserialize<...>(...)` (Goals is app-level shared infrastructure; using System actor as the canonical choice for app-level serialization)
  - `PLang/App/Goals/Setup/this.cs:56` → same pattern (`app.System.Channels.Serializers`)
  - `PLang/App/modules/file/providers/DefaultFileProvider.cs:99` → `action.Context.Actor.Channels.Serializers.SerializeAsync(...)` (handler has Context)
  - `PLang/App/Actor/Context/this.cs:172` (the `!serializers` DynamicData) → `() => Actor!.Channels.Serializers` (Context.Actor is set after Context construction, line 128; will be non-null at lambda evaluation time)
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** None. First stage on this branch.

## Design

### Why per-actor Serializers, not shared

Each actor owns its Channels collection (allocated in `Actor.this.cs:129`); each Channels has its own Serializers registry. This is the intended PLang shape — actors can in principle have their own serializer choices, similar to how each actor has its own settings store. Boot-time defaults are registered identically on every actor's Serializers; functionally indistinguishable from a shared singleton when no runtime extension is registered.

The earlier framing of this stage (which I attempted in a prior version of this brief) was "consolidate to a single shared instance on App." That model imposes shared semantics where PLang's design actually wants per-actor. Per-actor is right; the smell isn't the per-actor allocation but the *additional* third allocation site on each Stream channel and the App-root shortcut that bypasses the per-actor model.

### The smells this closes

- **Stream channel allocates its own Serializers** (Stream.this.cs:14–29 — `private Serializers? _serializers; public Serializers Serializers { get => _serializers ??= new Serializers.@this(); init => _serializers = value; }`). A Stream channel's I/O should resolve through its parent Channels — that's where the registry for this actor lives. Each Stream having its own makes "Channels has Serializers" a lie: the Channels.Serializers registry isn't the one Streams are using.
- **App.Serializers as a third surface that bypasses actors entirely.** External callers reach for `app.Serializers` to skip the actor; semantically they're using *some* Serializers but not the one any actor's Channels exposes. With per-actor as the model, every serialization should be associated with an actor (because that's what owns Channels). No actor → wrong path.

### The Channels back-ref on Channel — why this is good shape

`Channel.Stream.@this` needs to reach its parent Channels' Serializers. Today there's no `Channel.Channels` property; only `Channel.App`. Adding the back-ref is the "give the class what it needs" move from the principles file:

- Channel needs to navigate to its parent Channels. A `Channels` property is the natural shape.
- Channel doesn't need Context — it doesn't touch Variables or Trace. `Channels` (which has an Actor property) is sufficient if cross-actor info is ever needed.
- The existing `Channel.App` property stays (used elsewhere). Not removed in this stage; could become redundant later if everything that uses it can navigate via `Channels.App`. Not stage 1's job.

This is the pattern the principles file flags: classes hold whatever back-refs they actually need. Channel previously needed App; now it also needs Channels. Both stay — they earn their keep.

### Files touched + caller propagation

**Files modified (5):**
- `PLang/App/this.cs` — one line deleted.
- `PLang/App/Channels/Channel/this.cs` — one property added.
- `PLang/App/Channels/Channel/Stream/this.cs` — field + property removed; two call sites updated.
- `PLang/App/Channels/this.cs` — `Register(channel)` (or equivalent) sets `channel.Channels = this`; two internal call sites updated.
- (optionally) `PLang/App/Channels/this.cs` constructor's `?? new Serializers.@this()` fallback — keep or simplify; doesn't affect this stage's behaviour. Each Channels still allocates its own Serializers in its ctor; the optional `serializers` ctor parameter exists for tests and stays usable.

**External caller sweep (4 files, 5 sites):**
- `PLang/App/Goals/this.cs:320, 325` (2 sites)
- `PLang/App/Goals/Setup/this.cs:56`
- `PLang/App/modules/file/providers/DefaultFileProvider.cs:99`
- `PLang/App/Actor/Context/this.cs:172`

**Caller verification:** after the sweep, run:
- `grep -rn "app\.Serializers\b\|App\.Serializers\b" PLang/ --include='*.cs'` — should return zero.
- `grep -rn "stream.@this.*\.Serializers\|sc\.Serializers\|_serializers" PLang/App/Channels/ --include='*.cs'` — should return zero.

### Risk + dependencies

**Risk: low-medium.** The mechanical work (delete property, add back-ref, sweep callers) is straightforward. The slight risk is around the *boot ordering* of `Channel.Channels` setting:

- `Channel.@this.Channels` is `internal set` — set when the channel is registered.
- Stream's `WriteCore`/`ReadCore` access `Channels!.Serializers` — null-suppression via `!`.
- If a Stream channel is constructed and used WITHOUT going through `Channels.Register(...)`, `Channels` is null and the `!` throws NRE.
- The Stream factory methods (`Input`, `Output`, `Memory` on lines 42–51) all expect callers to register afterwards. The boot-time wiring at `App.this.cs:351–362` (`WireDefaultConsoleChannels`) does this. Verify no test or production path constructs a Stream and writes to it without registering.

**Dependencies: none.** First stage on this branch.

### Tests

**No new tests required.** Observable behaviour is the same — same serializers resolve through the same registries; the only difference is Stream's resolution path goes through its parent Channels rather than an internal lazy field, and no one is allowed to bypass actors via `app.Serializers`.

**Existing test coverage to verify:**
- `PLang.Tests/App/Channels/` — channel write/read round-trips (verify the new `Channel.Channels` back-ref is set correctly during registration).
- `Tests/` — full suite from a clean rebuild (per CLAUDE.md, the stale-binary trap is real — clean PlangConsole/bin and obj before claiming a green PLang test result).

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (no new failures vs trunk).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild.
- Greps return zero residual `app.Serializers` references and zero `_serializers` references inside Channel/Stream files.

### Watch for (coder eyes-on for things we may not have caught)

The architect has classified scope and back-ref patterns for the files this stage touches, but the code is the source of truth. While working through the touched files, flag anything that doesn't match the scope-map or principles:

- **Multiple overlapping back-refs on a single class** — per `principles.md` "smells", a class with both `_app` and `_actor` and `_context` separately stored is a god-bag. If you see one, note it in the commit message — likely a future cleanup.
- **A `_serializers` lazy field anywhere else in the channels subsystem.** Greps cleared `PLang/App/Channels/`, but verify as you read.
- **A 6th `app.Serializers` reader I missed.** The four-file caller sweep is based on grep; if the build breaks at an unexpected site, that's a tell.
- **Subtle dependency on Stream's old `init Serializers` setter.** Greps showed zero callers using the init-setter form. If you find one, flag it before deleting.
- **`Channel.App` direct readers that aren't yet routed through `Channel.Channels`.** Today's code uses `channel.App` in places (e.g., `Channels.this.cs:176`'s contentType-override branch). Stage 1 leaves `Channel.App` in place; stage 20 (`channel-app-backref-drop`) sweeps the readers later. Don't preempt stage 20 — but if you find a reader that's *only* there because Channels back-ref didn't exist, that's worth a comment for stage 20's brief.
- **Per-actor allocations that should be shared, or shared instances that should be per-actor.** The scope-map covers the major properties; smaller @this types may not be cataloged. If a smaller class allocates per-actor when no per-actor difference exists (or vice versa), flag it.

The cleanup discipline is **don't fix in stage 1; flag for the appropriate later stage**. Stage 1 stays focused on the Serializers consolidation. Anything you find that's not stage 1's job goes in the commit message or as a comment in this branch.

### Stages that follow this one

- **Stage 20** (`channel-app-backref-drop`) — depends on stage 1's `Channel.Channels` back-ref. Once stage 1 lands, stage 20 sweeps the redundant `Channel.App` readers and removes the property. Don't preempt this in stage 1.
- **Stage 21** (`navigators-to-variables`) — independent, not touched by stage 1.

### Out of scope

- The v1 `WriteAsync(actorName, channelName, ...)` overload on `Channels.@this` — stage 2.
- The contentType-override branch `if (channel is Channel.Stream.@this sc)` in `Channels.WriteAsync` and `Channels.ReadChannelAsync` — stage 2 (Stage 1 leaves the branch in place but updates `sc.Serializers` to `Serializers` since the smell is on Stream's property, not the v1 helper).
- Moving `ReadAsync<T>(filePath)` off `Channels` — stage 8.
- Renames inside `Channels/Serializers/` (`*PropertyFilter.cs` → `Filters/`, `TimeSpanIso8601Converter` → `TimeSpanIso8601`, `TypeJsonConverter` → `Data/Json.cs`, `JsonStreamSerializer` → `Json`, etc.) — stage 15.
- Removing the `Channel.App` back-ref (made redundant by the new `Channel.Channels` back-ref, since `Channels.App` is reachable through the new path) — its own future cleanup, not stage 1.
- Converting `Channels.@this` to take Context — Channels doesn't need Context for what it does today. If a future stage gives Channels a per-actor responsibility that requires Context, that stage adds it.

If you find yourself reaching for any of the above while doing this stage, stop and let the appropriate later stage handle it.

## Commit plan

One commit:

```
runtime2-cleanup stage 1: per-actor Channels.Serializers as single home

Each actor's Channels owns a Serializers registry — that's the intended
per-actor shape. The smell isn't the per-actor allocation; it's the two
extra surfaces that bypass it: App.@this.Serializers (an app-root
shortcut that skips actors entirely) and Channel.Stream.@this.Serializers
(a third copy lazily allocated per stream that doesn't share with its
parent Channels).

Deletes App.@this.Serializers. Drops Stream's _serializers field.
Adds a Channels back-ref to Channel.@this (alongside the existing
App back-ref) so Stream can navigate to its parent Channels' registry.
Sweeps 5 external app.Serializers caller sites to actor.Channels.Serializers.

Channels.@this ctor stays (App app) — Channels has an Actor property
for per-actor reach; doesn't need Context for what it does today.
```
