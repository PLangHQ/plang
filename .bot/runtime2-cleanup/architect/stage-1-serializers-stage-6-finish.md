# Stage 1: `serializers-stage-6-finish`

**Goal:** Delete the two `Serializers` carry-overs that drifted in during the channels work — `Channels.@this.Serializers` and `Channel.Stream.@this.Serializers` — and route all serialization through `App.Serializers`, the canonical app-wide singleton that already exists.

**Scope:**
- *Included:* drop `Serializers` property and ctor parameter on `Channels.@this`; drop `Serializers` property + lazy field on `Channel.Stream.@this`; route the four internal usage sites through `App.Serializers` (or `_app.Serializers` from inside `Channels.@this`).
- *Excluded:* the v1 helpers `WriteAsync(actorName, channelName, ...)` and the `if (channel is Channel.Stream.@this sc)` contentType-override branch — those are stage 2's job. This stage just routes them through `App.Serializers` while they still exist; stage 2 deletes them outright. Same for `ReadAsync<T>(filePath)` — stage 8 moves it off `Channels` entirely; stage 1 just routes its serializer call through `app.Serializers`.

**Deliverables:**
- `PLang/App/Channels/this.cs` — `Serializers` property removed; ctor parameter removed; the three internal call sites (lines 75, 176, 204 today) use `_app.Serializers`. Class shrinks by ~5 lines.
- `PLang/App/Channels/Channel/Stream/this.cs` — `_serializers` field and `Serializers` property removed; `WriteCore` and `ReadCore` (and any other call site inside this file) use `App.Serializers` (the inherited `Channel.@this.App` reference). Class shrinks by ~10 lines.
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` from a clean rebuild.

**Dependencies:** None. This stage is the first one carved on this branch and runs against trunk-as-of-fork.

## Design

### The smell this closes

Smell #3 from the OBP checklist: *same logical thing stored twice across types*. `App.Serializers` is the canonical serializer registry — content-type → serializer routing. `Channels.@this.Serializers` is a *second* registry instance allocated as a fallback (`new Serializers.@this()`) inside the Channels ctor. `Channel.Stream.@this.Serializers` is a *third* registry, lazily allocated per Stream channel.

Three instances of the same registry across three types, with no synchronization: a write through one Stream channel could resolve a different serializer than a write through `app.Serializers` directly. Today this is invisible because the three instances happen to register the same defaults — but the shape is wrong, and any future "register a custom serializer" call would silently apply to only one of the three.

The Stage 6 channels-work that introduced these carry-overs already promoted the canonical home to `App.Serializers`. The two carry-overs are the leftover scaffolding — same content, wrong owner, three places.

### The ownership realignment

Serializers' single owner is `App.@this`. `Channels.@this` and `Channel.Stream.@this` *use* the registry; they don't *own* it. The fix:

- `App.@this.Serializers` (already exists at `App.this.cs:154`) is the only registry instance.
- `Channels.@this` reaches `_app.Serializers` (the private `_app` field already wired).
- `Channel.Stream.@this` reaches `App.Serializers` via the inherited `Channel.@this.App` property (declared at `Channel/this.cs:75`, set during channel registration on the parent App).

No new types. No new fields. Two property removals, one ctor-parameter removal, four call-site updates.

### The new shape

**`Channels.@this`** — the property removal is straightforward:

```csharp
// Today (lines 28-32):
/// <summary>
/// The serializer registry — content-type routing for I/O.
/// Stage 6 promotes this to App.Serializers (app-wide, not per-actor).
/// </summary>
public Serializers.@this Serializers { get; }

// After: removed entirely.
```

Constructor (today, line 48):
```csharp
public @this(App.@this app, Serializers.@this? serializers = null)
{
    _app = app;
    Serializers = serializers ?? new Serializers.@this();
}

// After:
public @this(App.@this app)
{
    _app = app;
}
```

Internal call sites in `Channels.this.cs` change from `Serializers.X(...)` to `_app.Serializers.X(...)`:
- Line 75 (in `ReadAsync<T>(filePath)`): `Serializers.Deserialize<T>(...)` → `_app.Serializers.Deserialize<T>(...)`.
- Line 176 (in `WriteAsync(channelName, data, contentType)`, the v1 overload): `sc.Serializers.SerializeAsync(...)` → `_app.Serializers.SerializeAsync(...)` (the `sc.` is the contentType-override branch; `sc` is a `Channel.Stream.@this` whose `Serializers` we're removing).
- Line 204 (in `ReadChannelAsync<T>`): `sc.Serializers.DeserializeAsync<T>(...)` → `_app.Serializers.DeserializeAsync<T>(...)`.

**`Channel.Stream.@this`** — the property and field removal:

```csharp
// Today (lines 14-29):
private readonly bool _ownsStream;
private Serializers.@this? _serializers;

public Serializers.@this Serializers
{
    get => _serializers ??= new Serializers.@this();
    init => _serializers = value;
}

// After:
private readonly bool _ownsStream;
// (no _serializers field, no Serializers property)
```

Internal call sites in `Channel/Stream/this.cs` change from `Serializers.X(...)` to `App.Serializers.X(...)` (using the inherited `Channel.@this.App` property):
- `WriteCore` (around line 64): `Serializers.SerializeAsync(...)` → `App!.Serializers.SerializeAsync(...)`.
- `ReadCore` (around line 95–115): same pattern for the deserialize call.

The `App` property on the base `Channel.@this` class is `App.@this? App { get; internal set; }` — nullable, set by the registration path. By the time `WriteCore`/`ReadCore` runs, the channel has been registered and `App` is non-null. The `!` suppresses the nullable warning at the call site (or — see the testing note below — wrap in a guard if you prefer). A null `App` here would mean a Stream channel was constructed and used without registration, which is itself a bug — fail fast with `NullReferenceException` is acceptable for now (we're not adding new error paths in this stage).

### Why not pass Serializers to the Stream channel at construction

Tempting, because Stream channels currently have an `init`-only `Serializers` setter. But that's exactly the multiple-registries shape we're closing. The whole point of this stage is "one registry, one owner." Passing it in at construction means each Stream channel could carry a *different* registry — same smell, different mechanism. The fix is structural: navigate to the canonical owner via the parent reference; don't hand carry-overs around.

### Files touched + caller propagation

**Files modified (2):**
- `PLang/App/Channels/this.cs`
- `PLang/App/Channels/Channel/Stream/this.cs`

**Caller sweep needed:**
- Inside `Channels.this.cs` itself: 4 sites (lines 32, 48, 51, 75, 138, 176, 204 in today's numbering — line 138 is the `Copy()` method that passes `Serializers` to a new instance; that constructor call needs the new signature).
- External callers of `channels.Serializers`: scan `grep -rn "channels?\.Serializers\b" PLang/ --include='*.cs'`. Today's grep shows the external callers are already on `app.Serializers` (Goals/this.cs:320, Goals/Setup/this.cs:56, modules/file/providers/DefaultFileProvider.cs:99). No external migration needed. Confirm with the grep before commit.
- External callers of `streamChannel.Serializers`: scan `grep -rn "Stream.@this.*\.Serializers\|sc\.Serializers" PLang/ --include='*.cs'`. Today's hits are inside Channels.this.cs (the contentType-override branch, lines 176 and 204) — already in our touch list. Confirm with grep before commit.
- The `Serializers.@this? serializers = null` ctor parameter on `Channels.@this`: find every `new Channels.@this(...)` call site and verify none passes a non-null serializer. Most likely zero hits today.

**Test impact:**
- Channels-touching C# tests in `PLang.Tests/App/Channels/` — verify they still pass. They shouldn't depend on the carry-over registries; if any do, the test was riding the smell.
- PLang tests under `Tests/` that exercise channel I/O — same.

### Risk + dependencies

**Risk: low.** This is purely an ownership realignment over an already-existing canonical type. No new classes, no new behaviour. The most likely failure mode is missing a call site (a place that reaches `channels.Serializers` or `streamChannel.Serializers`) — caught by the build break (the property is gone) and by the tests.

**One real risk** — if `App` is null on a Stream channel during a write (channel constructed but never registered), today's code falls back to a lazy `new Serializers.@this()` and the write succeeds. After this stage, the same scenario throws `NullReferenceException`. Verify there's no test or production path that constructs an unregistered Stream channel and writes to it. The Stream factory methods (`Input`, `Output`, `Memory` on lines 42–51) all require registration after construction, so this should be fine — but worth a sweep.

**Dependencies: none.** First stage on this branch.

### Tests

**No new tests required.** This stage doesn't change observable behaviour — the same serializers resolve, the same write/read paths execute, just through a single registry instance instead of three.

**Existing test coverage to verify:**
- `PLang.Tests/App/Channels/` — channel write/read round-trips through Stream.
- `PLang.Tests/App/Channels/Serializers/` — serializer registry behaviour. (If this directory doesn't exist, that's a separate test-coverage question, not a blocker for this stage.)
- `Tests/` — any PLang tests that read/write to console channels exercise the Stream path. Run the full suite.

**Definition of done — same as the plan's:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (no new failures vs trunk).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (per CLAUDE.md, the stale-binary trap is real).
- The grep sweeps above return zero residual references to `channels.Serializers` or `stream.Serializers`.

### Out of scope

- The v1 `WriteAsync(actorName, channelName, ...)` overload on `Channels.@this` — stage 2 deletes it.
- The contentType-override branch `if (channel is Channel.Stream.@this sc)` in `Channels.WriteAsync` and `Channels.ReadChannelAsync` — stage 2 deletes both.
- Moving `ReadAsync<T>(filePath)` off `Channels` (it doesn't read from a channel) — stage 8.
- Any rename of the `Serializers` folder, files, or types — stage 15 (compound-name-rename) handles those (`JsonStreamSerializer` → `Json`, etc.).
- Renames inside `Channels/Serializers/` (`*PropertyFilter.cs` → `Filters/*.cs`, `TimeSpanIso8601Converter` → `TimeSpanIso8601`, `TypeJsonConverter` → `Data/Json.cs`) — stage 15.

If you find yourself reaching for any of the above while doing this stage, stop. Make a note and let the appropriate later stage handle it.

## Commit plan

One commit:

```
runtime2-cleanup stage 1: drop Serializers carry-overs on Channels and Stream

Stage 6 of the channels work introduced two duplicate Serializers
registries — one on Channels.@this, one lazy-initialized per
Channel.Stream.@this — alongside the canonical app.Serializers.
Same registry, three places: smell #3 (same logical thing stored twice
across types).

Removes both carry-overs. Channels.@this and Channel.Stream.@this now
reach app.Serializers via _app and the inherited Channel.App property.
External callers were already on app.Serializers; no migration needed.

The v1 WriteAsync helpers and the contentType-override branch in
Channels stay (stage 2 deletes them); they route through app.Serializers
in the meantime.
```
