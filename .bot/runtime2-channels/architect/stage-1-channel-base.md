# Stage 1: Channel base + Session/Message + Role + Config

**Goal:** Establish the channel contract — abstract base, two pattern abstracts (Session, Message), Role enum, and per-channel config properties. Foundation for every concrete channel that follows.

**Scope:**
- Refactor existing `App/Channels/Channel/this.cs` from concrete Stream-wrapper into an abstract base.
- Introduce `App/Channels/Channel/Session/this.cs` and `App/Channels/Channel/Message/this.cs` as the two abstract pattern types.
- Add `Role` enum (`Output` / `Error` / `Input`) alongside existing `Direction`.
- Add config properties: `Buffer` (long, bytes), `Timeout` (TimeSpan, ISO 8601 in JSON), `Mime` (string), `Encoding` (string), `Encryption` (provider ref?), `Signing` (provider ref?).
- Add abstract methods: `WriteCore`, `ReadCore`, `AskCore`. Sealed wrappers `WriteAsync` / `ReadAsync` / `Ask` will be added in Stage 8 (events) — for now, base just declares the abstracts and concrete subtypes implement.
- **Excluded:** event firing (Stage 8), concrete channels (Stages 2-3), Web channel (deferred).

**Deliverables:**
- `App/Channels/Channel/this.cs` — abstract base. Properties: `Name`, `Role`, `Direction`, `Buffer`, `Timeout`, `Mime`, `Encoding`, `Encryption?`, `Signing?`. Abstract methods: `WriteCore`, `ReadCore`, `AskCore`.
- `App/Channels/Channel/Session/this.cs` — abstract. Documents the contract: kept-open connection, Ask blocks reading from the connection until answer arrives.
- `App/Channels/Channel/Message/this.cs` — abstract. Documents: one-shot exchange, Ask returns Suspend sentinel; resume happens via callback.
- `App/Channels/Channel/Role/this.cs` — `enum { Output, Error, Input }`. (Or inline in Channel base; either works.)
- Custom `JsonConverter` for `TimeSpan` ↔ ISO 8601 string, registered globally so `Timeout` and any other TimeSpan property serialise as `"PT30S"`.
- Channels collection (`App/Channels/this.cs`) drops console-stream-opening from ctor — becomes pure registry. Stage 6 wires console streams via the entry point.

**Dependencies:** None.

## Design

`Channel.@this` is the contract every concrete channel honours. The split into Session / Message gives external developers two structural bases to extend — they pick the one matching their transport's nature.

### The contract

```csharp
public abstract class @this   // App.Channels.Channel.@this
{
    public string Name { get; init; } = "";
    public Role Role { get; init; }
    public Direction Direction { get; init; }

    public long Buffer { get; init; } = 4096;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public string Mime { get; init; } = "text/plain";
    public string Encoding { get; init; } = "utf-8";
    public Encryption? Encryption { get; init; }
    public Signing? Signing { get; init; }

    public abstract Task<Data.@this> WriteCore(Data.@this data, CancellationToken ct);
    public abstract Task<Data.@this> ReadCore(CancellationToken ct);
    public abstract Task<Data.@this> Ask(AskCallback callback);
    
    // Public WriteAsync / ReadAsync wrappers added in Stage 8 (events).
    // For Stage 1, expose simple non-event-wrapping public methods that just call the Core.
}
```

`WriteAsync` takes full `Data.@this` (not `Data.Value`) — Rule 7, relay don't repackage. The channel's serializer (resolved via `Mime`) decides how to render based on Data's type, properties, and signature.

### Session vs. Message

- **`Session`**: kept-open. `AskCore` blocks reading from the connection until an answer arrives, returns the answer Data. Stream-backed and Goal-backed channels both extend Session.
- **`Message`**: one-shot. `AskCore` returns a `Suspend` sentinel (a typed Data marker indicating "engine, suspend the goal; the answer will arrive via callback resume later"). Web extends Message (when shipped).

Both abstracts can supply default `WriteCore`/`ReadCore` if useful, but most concretes override.

### Why ISO 8601 for Timeout

LLM zero-counting risk on int milliseconds. ISO 8601 (`"PT30S"`, `"PT5M"`, `"PT1H30M"`) is well-known to LLMs, structured, no math required. Custom `JsonConverter` reads via `XmlConvert.ToTimeSpan`. Same converter applies anywhere TimeSpan appears — register globally.

### What stays in `App/Channels/this.cs` (the registry)

Today's `Channels.@this` is the per-actor collection. After this stage:
- Stop opening `Console.OpenStandard*` in ctor — entry-point's job (Stage 6).
- Keep `Register` / `Remove` / `Get` / `Contains`. These are used by Stage 5 actions and Stage 6 wiring.
- Remove the `WriteAsync(Write action)` overload entirely (the choreography moves to `Write.Run` in Stage 4).
- `Serializers` moves out — promoted to `App.Serializers` (separate small change in this stage or Stage 6, easy either way).
