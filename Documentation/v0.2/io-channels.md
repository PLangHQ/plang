# Channels

Channels are PLang's per-actor I/O primitive. Every read, write, and prompt at runtime flows through a named channel, and channels are **redirectable** — a user can re-register `output` to a file, point `debug` at an in-memory buffer in a test, or back any name with a goal that runs on every write.

> **Predecessor:** an earlier `app.io.IO` / `app.io.Channel` shape was replaced on `runtime2-channels`. Naming and structure below reflect the current code.

## Shape at a glance

```
App
├── System (Actor)        ─┬── Channels  (app.channels.@this — registry per actor)
├── Service (Actor)        │     ├── "output"   → Channel.Stream.@this   (stdout)
└── User (Actor)           │     ├── "error"    → Channel.Stream.@this   (stderr)
                           │     ├── "input"    → Channel.Stream.@this   (stdin)
                           │     ├── "debug"    → Channel.Stream.@this   (Debug.Write target)
                           │     └── "logger"   → Channel.Goal.@this     (user-registered)
                           └── Serializers (content-type → serializer)
```

Three actors, three independent registries. The entry point (PlangConsole today; future PlangWeb) wires the four default-named channels on each actor before user code runs. `environment.run` enforces this — boot fails with `MissingRequiredChannelAtBoot` if any of `output`/`error`/`input` is absent.

## The registry: `app.channels.@this`

Per-actor. Pure registry — Register / Get / Remove / Resolve. **Choreography (the actual write/read/ask paths) lives on `Channel.@this`**, not here.

```csharp
public sealed class @this : IAsyncDisposable
{
    public const string Output = "output";
    public const string Error  = "error";
    public const string Input  = "input";
    public const string Debug  = "debug";
    public static readonly string[] Defaults = [Output, Error, Input];

    public Channel.@this? Get(string name);
    public Channel.@this? Resolve(string? name);  // null/empty → "output"
    public void Register(Channel.@this channel);
    public Task<bool> RemoveAsync(string name);
    public Channel.@this GetOrCreate(string name, Func<Channel.@this> factory);
    public bool Contains(string name);
    public IEnumerable<string> ChannelNames { get; }
    public IEnumerable<Channel.@this> All { get; }

    public Data.@this Verify();              // boot invariant: Defaults all registered
    public @this Snapshot();                 // shallow copy — same instances, new dict

    // Convenience (most callers prefer the typed surface on Channel.Stream.@this directly):
    public Task<Data.@this> WriteAsync(string channelName, object? data, string? contentType = null, CancellationToken ct = default);
    public Task<Data.@this> WriteTextAsync(string channelName, string text, CancellationToken ct = default);
    public Task<Data.@this> ReadChannelAsync<T>(string channelName, CancellationToken ct = default);
    public Task<Data.@this> ReadTextAsync(string channelName, CancellationToken ct = default);
    public Channel.@this CreateMemoryChannel(string name, ChannelDirection dir = ChannelDirection.Bidirectional);  // tests
}
```

Reach the registry from an actor: `actor.Channels`. Every channel registered is stamped with the owning actor — events bound to that channel run with that actor's `Context`.

### Direction enforcement

`Resolve` / `Get` return the channel; the registry's `WriteAsync` / `ReadChannelAsync` overloads gate on direction (write fails on `Input`-only, read fails on `Output`-only) and surface `ChannelReadOnly` / `ChannelWriteOnly` Data errors. The default console pair is intentionally direction-split: `output` is write-only, `input` is read-only. See [Interactive prompts](#interactive-prompts) below.

## The channel: `app.channels.Channel.@this`

Abstract base. Carries config (Buffer, Timeout, Mime, Encoding, Encryption, Signing), wires the public `WriteAsync` / `ReadAsync` / `Ask` to the abstract `WriteCore` / `ReadCore` / `AskCore` that concretes implement, and runs the channel-event lifecycle (Before/After Read/Write, OnAsk).

```csharp
public abstract class @this : IAsyncDisposable, IDisposable
{
    public string Name { get; init; }
    public ChannelDirection Direction { get; init; }     // Input | Output | Bidirectional
    public long Buffer { get; init; } = 4096;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public string Mime { get; init; } = "text/plain";
    public string Encoding { get; init; } = "utf-8";
    public string? Encryption { get; init; }
    public string? Signing { get; init; } = "auto";
    public bool IsOpen { get; protected set; }
    public DateTime Created { get; }
    public IDictionary<string, object> Metadata { get; }
    public Channel.Events.@this Events { get; }          // per-channel bindings + recursion guard

    public virtual bool CanRead  { get; }                // IsOpen && Direction != Output
    public virtual bool CanWrite { get; }                // IsOpen && Direction != Input

    public abstract Task<Data.@this> WriteCore(Data.@this data, CancellationToken ct = default);
    public abstract Task<Data.@this> ReadCore(CancellationToken ct = default);
    public abstract Task<Data.@this> AskCore(Data.@this prompt, CancellationToken ct = default);

    public virtual Task<Data.@this> WriteAsync(Data.@this data, CancellationToken ct = default);
    public virtual Task<Data.@this> ReadAsync(CancellationToken ct = default);
    public virtual Task<Data.@this> Ask(Data.@this prompt, CancellationToken ct = default);
}
```

`WriteAsync` is the public entry — it fires `BeforeWrite` (a throw aborts the write; `AfterWrite` is suppressed), invokes `WriteCore`, then fires `AfterWrite` (always — handler throws are swallowed; original outcome stands). Same shape for `ReadAsync` / `Ask`.

### Subtypes

```
Channel.@this (abstract)
├── Channel.Session.@this (abstract)    kept-open, stateful: Ask blocks until answer
│   ├── Channel.Stream.@this            wraps System.IO.Stream (stdin/stdout/stderr/file/memory)
│   └── Channel.Goal.@this              writes invoke a goal; %!data% available inside
└── Channel.Message.@this (abstract)    one-shot: AskCore returns Suspend, callback resumes
                                        (Web channel will extend Message when shipped)
```

External developers picking a base for a custom transport choose **Session** for kept-open connections and **Message** for one-shot (suspend/resume via callback). The split is structural — there is no `Channel.Web` yet, but its base is reserved.

#### `Channel.Stream.@this`

Wraps a `System.IO.Stream`. Concretes for stdin/stdout/stderr (handed in at boot, `ownsStream: false`), HTTP response bodies, file streams, and `MemoryStream` for tests.

```csharp
public sealed class @this : Session.@this
{
    public System.IO.Stream Stream { get; }
    public Serializers.@this Serializers { get; init; }

    public static @this Input(string name, Stream stream, bool ownsStream = false);
    public static @this Output(string name, Stream stream, bool ownsStream = false);
    public static @this Memory(string name, ChannelDirection direction = Bidirectional);

    public Task<byte[]> ReadAllBytesAsync(CancellationToken ct = default);
    public Task<string> ReadAllTextAsync(CancellationToken ct = default);
    public Task         WriteBytesAsync(byte[] data, CancellationToken ct = default);
    public Task         WriteTextAsync(string text, CancellationToken ct = default);
}
```

`AskCore` writes the prompt then `await reader.ReadLineAsync()` with `ResolveEncoding()` and `leaveOpen: true`, gated by per-channel `Timeout`. Works on bidirectional streams (memory, HTTP session). Does **not** work across the split console pair — see below.

#### `Channel.Goal.@this`

Writes invoke a PLang goal. The Data envelope lands in the goal as `%!data%`. The channel captures the registering actor; for the duration of each goal call it pushes an AsyncLocal channel-resolution overlay onto the actor — the actor's `FoundationalChannels` (the snapshot taken at boot, before any user-registered overlays). So a goal-channel body like

```plang
Logger
- write out %!data% to log.txt
```

reaches the original entry-point streams, not the overlay that fired the goal-channel — preventing infinite recursion if the same name is later re-registered, and giving fan-out via composition for free (a `logger` goal-channel can write through `output` and also append to a file).

#### `Channel.Events.@this`

Per-channel binding list with its own lock and an AsyncLocal "this binding is already firing" recursion guard. The cross-source firing orchestration (per-channel → per-actor → app-level) lives on `Channel.@this`; this type owns the per-channel slice. Same shape spirit as `Goal.Events` / `Step.Events`: the type that owns the data also owns its access rules.

## The four default-named channels

```
"output"   write-only by default  user-facing program output
"error"    write-only by default  user-visible errors; Debug.Write fallback
"input"    read-only  by default  stdin / interactive answers
"debug"    bidirectional          --debug diagnostic stream (Debug.Write)
```

`output` / `error` / `input` are in `Defaults` and **cannot be removed** — `channel.remove "output"` returns `ChannelInvariantViolation`. To redirect them, use `channel.set` to replace the backing. `debug` is not in `Defaults`; it can be added or removed freely.

`app.modules.debug.Write(...)` resolves the System actor's `debug` channel falling back to `error`. It is gated on `IsEnabled` (set by `--debug`); production code calls it freely without checking. Full rule on when to use which surface: see `good_to_know.md` "Console.* Is Banned in Production C#".

## Actor channel resolution & overrides

```csharp
public sealed class app.actor.@this
{
    public AppChannels Channels => _channelsOverride.Value ?? _channels;
    public AppChannels FoundationalChannels { get; }                 // boot snapshot, lazy
    public IDisposable PushChannelsOverride(AppChannels overlay);    // AsyncLocal scope
    public void FreezeFoundational();
}
```

`PushChannelsOverride` returns a disposable scope; the override is AsyncLocal so concurrent flows don't collide. Goal channels use this internally to swap to `FoundationalChannels` during goal invocation. External callers normally never touch this — it is the mechanism, not the surface.

## PLang surface

Two actions in the `channel` module:

```plang
- set channel "logger" call Logger
- set channel "audit" call AuditLog, buffer: 65536, timeout: PT30S
- set output channel as MyOutputGoal
- set system input channel as InputGoal
- remove channel "logger"
```

**`channel.set`** registers (or replaces — always upserts) a goal-backed channel on the current actor or an explicitly-named one. Direction precedence: an explicit `direction:` parameter wins; otherwise the channel name `"input"` / `"output"` decides; otherwise `Bidirectional`. Optional config: `buffer`, `timeout` (ISO 8601 like `PT30S`), `mime`, `encoding`, `encryption`, `signing`.

**`channel.remove`** unregisters. Refused for `output`/`error`/`input` (the boot invariant); use `channel.set` to replace their backing instead.

Every PLang `write` step resolves through the channels registry. `write out` resolves the channel named `"output"` on the current actor; `write to "logger"` resolves `"logger"`.

## Interactive prompts

The default console pair is direction-split (`output` write-only, `input` read-only). `Channel.Stream.AskCore` writes-then-reads on a single bidirectional stream — works for memory and HTTP-session channels, **not** across the console pair. Two-call pattern from C#:

```csharp
var output = User.Channels.Get(global::app.channels.@this.Output) as Channel.Stream.@this;
var input  = User.Channels.Get(global::app.channels.@this.Input)  as Channel.Stream.@this;

await output.WriteTextAsync($"Create new app? (y/n): ");
using var reader = new StreamReader(input.Stream, leaveOpen: true);
var answer = (await reader.ReadLineAsync())?.Trim().ToLowerInvariant();
```

`Console.IsInputRedirected` (the TTY gate — *whether* to prompt at all) stays — that's a query, not a write.

## Code examples

### C# — write through the actor's channel

```csharp
// User-facing chatter — output channel
await app.CurrentActor.Channels.WriteTextAsync(
    global::app.channels.@this.Output,
    $"  Saved {goal.Name} ({elapsed.TotalSeconds:F1}s){Environment.NewLine}");

// Diagnostic — Debug.Write (gated on --debug, falls back to error)
await context.App.Debug.Write($"=== Goal '{goalName}' completed ==={Environment.NewLine}");
```

### C# — register a memory channel for tests

The redirection model is exactly how tests capture stderr in the channels world. `Console.SetError(...)` no longer works — the `error` channel was registered with `Console.OpenStandardError()` at boot, capturing that Stream reference. Re-pointing `Console.Error` later doesn't affect the captured Stream.

```csharp
var captured = app.System.Channels.CreateMemoryChannel(
    global::app.channels.@this.Error,
    Channel.ChannelDirection.Bidirectional);
// ... run code under test ...
captured.Stream.Position = 0;
var stderr = await captured.ReadAllTextAsync();
```

### PLang — replace `output` with a goal channel

```plang
Start
- set output channel as Logger

Logger
- write out %!data%
- append %!data% to log.txt
```

Every subsequent `write out` invokes `Logger`. The recursion guard on `Channel.Goal.@this` makes `write out %!data%` inside `Logger` reach the foundational `output` (the original stdout snapshot), not loop back into itself.

## Roadmap

- **Stage 9 — cross-device migration.** A prototyped `Channel.Migrate` / `MigrationEnvelope` surface was removed before merge after security review (the envelope had PKI-shape fields but a keyless integrity hash; receive side was undesigned). The combination is still on the roadmap — see `Documentation/Runtime2/cool.md` "Channels that migrate across devices". Resurrection happens fresh under Stage 9 transport: real Ed25519 envelope, designed-in permission gate on Variables-snapshot exposure, designed-in receive handshake.
- **Web channel.** `Channel.Message.@this` is the reserved base; AskCore returns a Suspend sentinel resumed by callback when the answer arrives.

## Relationships

- The channels registry uses [Serializers](serializers.md) for content-type routing on `Stream` channels.
- All channel I/O returns [`Data`](goal-result.md) — `Success`/`Error`, never throws across the boundary.
- Channel events plug into the same [event](events.md) lifecycle that goal/step events use; bindings live on the per-actor Context Events list and on per-app Events, with per-channel bindings on `Channel.Events`.
- Each [Actor](contexts.md) owns one `Channels` instance; goal-channels capture the actor at register time so events run with the right `Context`.
- The Console.* discipline rule for production C# lives in `Documentation/v0.2/good_to_know.md` "Console.* Is Banned in Production C#".
