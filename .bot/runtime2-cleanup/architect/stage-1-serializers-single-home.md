# Stage 1: `serializers-single-home`

**Goal:** Consolidate the Serializers registry to a single instance, owned by `Channels.@this`. Today three instances exist (App allocates one, Channels allocates one, each Stream channel lazy-allocates its own); after this stage there is one, and Channels is the owner.

**Scope:**
- *Included:* drop the per-Channels duplicate ctor allocation; drop the per-Stream `_serializers` lazy field and `Serializers` property; route the four internal call sites through the single Channels-owned registry; turn `App.@this.Serializers` into a delegate (`=> Channels.Serializers`) so external callers continue to work without churn.
- *Excluded:* removing the `App.@this.Serializers` delegate property and updating external callers to `app.Channels.Serializers` — that's stage 20's scope. Same for the v1 `WriteAsync(actorName, channelName, ...)` overload and the `if (channel is Channel.Stream.@this sc)` contentType-override branch (stage 2). Same for `ReadAsync<T>(filePath)` relocation (stage 8). Same for any rename inside `Channels/Serializers/` (stage 15).

**Deliverables:**
- `PLang/App/Channels/this.cs` — `Serializers` property and ctor parameter unchanged in shape; the ctor stops allocating a fallback instance (an instance MUST be passed in by App during construction). Class loses the `?? new Serializers.@this()` allocation; gains a `_app.Serializers` reference at construction time. The three internal call sites that currently use `Serializers` continue to work — they reference the property which now points at the canonical instance.
- `PLang/App/Channels/Channel/Stream/this.cs` — `_serializers` field removed; `Serializers` property removed; two internal call sites in `WriteCore` and `ReadCore` replaced with `App!.Channels.Serializers` (using the inherited `Channel.@this.App` back-reference). Class shrinks by ~10 lines.
- `PLang/App/this.cs` — the `public Serializers Serializers { get; } = new Serializers();` initializer removed; replaced with a delegate `public Serializers Serializers => Channels.Serializers;`. (No external caller change in this stage; stage 20 sweeps them.)
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** None. First stage on this branch.

## Design

### The smell this closes

Smell #3 from the OBP checklist: *same logical thing stored twice across types*. Today, the same Serializers registry is allocated three times:

1. `App.@this.Serializers` (App.this.cs:154) — `new Serializers()` at App construction.
2. `Channels.@this.Serializers` (Channels.this.cs:51) — `new Serializers.@this()` if no ctor arg passed.
3. `Channel.Stream.@this.Serializers` (Stream.this.cs:27) — `new Serializers.@this()` lazily, per Stream channel.

Three instances of the same registry across three types, no synchronization. Each instance gets the same defaults today, but a runtime call to register a new content-type would silently apply to only one of them. The shape is wrong.

### Why Channels is the owner

PLang's I/O boundary is `app.Channels`. Serialization happens *only* at I/O boundaries — when bytes/strings cross between the runtime and the outside world. Whether the boundary is a stream channel (HTTP, stdin/stdout), a file system read, or a database load, the act is the same: convert between transport bytes and CLR objects via content-type routing.

Channels is the subsystem that owns I/O. Therefore Channels owns Serializers. App.Serializers existed as a convenience surface from before the Channels subsystem matured — that ergonomic shortcut stays one stage longer (so external callers don't churn twice), then stage 20 removes it and consumers reach the registry through the navigation path that matches the structure: `app.Channels.Serializers`.

### The new shape

**`Channels.@this`** — already has a `Serializers` property (line 32). The property stays. What changes is *where the instance comes from*: today the ctor allocates a fallback (`new Serializers.@this()`); after this stage, the ctor receives the instance from App (which constructs it once).

```csharp
// Today (lines 48–53):
public @this(App.@this app, Serializers.@this? serializers = null)
{
    _app = app;
    Serializers = serializers ?? new Serializers.@this();
}

// After:
public @this(App.@this app, Serializers.@this serializers)
{
    _app = app;
    Serializers = serializers;
}
```

The four internal call sites that read `Serializers` (line 75 in `ReadAsync<T>(filePath)`, line 138 in `Copy()`, lines 176 and 204 in the v1 contentType-override paths) keep their `Serializers.X(...)` form — the property still exists; it just now points at the canonical instance.

**`App.@this`** — line 154 today is `public Serializers Serializers { get; } = new Serializers();`. After this stage:

```csharp
// Today:
public Serializers Serializers { get; } = new Serializers();

// After (delegate; backing instance lives on Channels):
public Serializers Serializers => Channels.Serializers;
```

The actual `new Serializers()` allocation moves to App's ctor, where the instance is constructed *once* and passed into `Channels.@this` as the new required ctor arg. Sketch:

```csharp
// In App.@this constructor (wherever Channels gets constructed):
var serializers = new Serializers();    // allocated here, once per App
Channels = new Channels.@this(this, serializers);
```

Trace the existing App ctor for the right insertion point. The Channels construction is somewhere in App.this.cs's initializer chain; the `serializers` instance has to be allocated before Channels is.

**`Channel.Stream.@this`** — drops the `_serializers` field and the `Serializers` property entirely:

```csharp
// Today (lines 14–29):
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

The two call sites in this file (`WriteCore` around line 64 and `ReadCore` further down) update from `Serializers.X(...)` to `App!.Channels.Serializers.X(...)`. The `App` property is inherited from `Channel.@this` (declared at `Channel/this.cs:75` as `public global::App.@this? App { get; internal set; }`). It's nullable; `!` suppresses the warning. By the time `WriteCore`/`ReadCore` runs the channel has been registered and `App` is non-null. A null `App` here would mean a Stream channel was constructed and used without registration — itself a bug, fail-fast with `NullReferenceException` is acceptable; we're not adding new error paths in this stage.

### Why not pass Serializers into Stream at construction

Tempting (the `init` setter on Stream's current property hints at it), but it's the same shape as the smell we're closing. Each Stream channel holding its own (or even pointing at the canonical one via init) is "the registry navigates around the codebase as a parameter." The cleaner shape is "navigate to the canonical owner via the parent chain." Stream uses `App.Channels.Serializers` because the registry's owner is reachable from any channel that's been registered — no need to pass the registry itself.

### Files touched + caller propagation

**Files modified (3):**
- `PLang/App/this.cs` — `Serializers` property becomes a delegate; ctor allocates the instance and passes it to Channels.
- `PLang/App/Channels/this.cs` — ctor's `serializers` parameter becomes required; the `?? new Serializers.@this()` fallback removed.
- `PLang/App/Channels/Channel/Stream/this.cs` — `_serializers` field, `Serializers` property removed; two call sites updated.

**Caller sweeps:**
- External callers of `app.Serializers`: untouched. They continue to work via the delegate. (Stage 20 sweeps them.)
- External callers of `channels.Serializers`: should be zero. Confirm via `grep -rn "channels?\.Serializers\b" PLang/ --include='*.cs' | grep -v "App\.Channels\.Serializers\b"`.
- External callers of `streamChannel.Serializers`: should be zero. Confirm via `grep -rn "Stream.@this.*\.Serializers\|sc\.Serializers" PLang/ --include='*.cs'`. Today's hits are inside Channels.this.cs (lines 176, 204) and Stream.this.cs itself — already in the touch list.
- `new Channels.@this(...)` call sites — find every one and pass the App-owned `serializers` instance. Today's ctor allows omitting it (`Serializers.@this? serializers = null`); after this stage it's required. Most likely just the App ctor.

**Test impact:**
- C# tests in `PLang.Tests/App/Channels/` — verify they still pass. They shouldn't depend on the per-instance carry-overs; if any do, the test was riding the smell.
- PLang tests under `Tests/` that exercise channel I/O — same.

### Risk + dependencies

**Risk: low.** Pure ownership realignment over an already-existing canonical type. No new classes, no new behaviour. The most likely failure modes:

1. **Missed call site** — a place that reaches `channels.Serializers` or `streamChannel.Serializers`. Caught by the build break (the property is gone or its meaning changed) and by the tests.
2. **Stream channel used without registration** — today this lazily allocates a serializer registry and writes succeed; after this stage, it throws NRE. The Stream factory methods (`Input`, `Output`, `Memory` on lines 42–51) all require `Channels.Register(...)` after construction, and `Channels.Register` is where the App back-ref gets set. Verify there's no test or production path that constructs an unregistered Stream channel and writes to it.
3. **Channels ctor signature change** — every `new Channels.@this(...)` call site now requires the serializers arg. Likely just the App ctor, but `grep -rn "new Channels.@this(" PLang/` to confirm.

**Dependencies: none.** First stage on this branch.

### Tests

**No new tests required.** This stage doesn't change observable behaviour — the same serializers resolve, the same write/read paths execute, just through a single instance instead of three.

**Existing test coverage to verify:**
- `PLang.Tests/App/Channels/` — channel write/read round-trips through Stream.
- `PLang.Tests/App/Channels/Serializers/` if it exists — serializer registry behaviour.
- `Tests/` — PLang tests that read/write to console channels exercise the Stream path. Run the full suite from a clean rebuild.

**Definition of done — same as the plan's:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (no new failures vs trunk).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (per CLAUDE.md, the stale-binary trap is real — rebuild the bin/obj directories cleanly before claiming a green PLang test run).
- The grep sweeps above return zero residual references to `channels.Serializers` (other than the canonical property declaration) or `stream.Serializers`.

### Out of scope

- The `App.@this.Serializers` delegate property removal and external caller sweep — stage 20 (`serializers-app-shortcut-drop`).
- The v1 `WriteAsync(actorName, channelName, ...)` overload on `Channels.@this` — stage 2.
- The contentType-override branch `if (channel is Channel.Stream.@this sc)` in `Channels.WriteAsync` and `Channels.ReadChannelAsync` — stage 2.
- Moving `ReadAsync<T>(filePath)` off `Channels` — stage 8.
- Renaming inside `Channels/Serializers/` (`*PropertyFilter.cs` → `Filters/`, `TimeSpanIso8601Converter` → `TimeSpanIso8601`, `TypeJsonConverter` → `Data/Json.cs`, `JsonStreamSerializer` → `Json`, etc.) — stage 15.

If you find yourself reaching for any of the above while doing this stage, stop and let the appropriate later stage handle it.

## Commit plan

One commit:

```
runtime2-cleanup stage 1: consolidate Serializers to single Channels-owned instance

Three instances of the Serializers registry today: one allocated by
App.@this, one by Channels.@this, one lazily per Channel.Stream.@this.
Same registry, three places — smell #3 (same logical thing stored twice
across types).

Channels owns the canonical instance. App allocates it once at boot and
passes it into Channels' ctor (now required). Stream channels reach it
via App!.Channels.Serializers using the inherited Channel.App back-ref.
App.@this.Serializers becomes a delegate (=> Channels.Serializers) — kept
as a shortcut for external callers; stage 20 removes both the delegate
and the shortcut by sweeping callers to app.Channels.Serializers.

Internal Channels call sites continue to use this.Serializers; the
property still exists, it just now points at the App-owned instance.
Stream's _serializers field and Serializers property removed entirely.
```
