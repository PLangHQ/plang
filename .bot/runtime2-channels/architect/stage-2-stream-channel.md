# Stage 2: Stream channel

**Goal:** Refactor today's concrete channel into `Channel.Stream` extending `Session`. Wraps a `System.IO.Stream`. The most common channel kind — covers stdin, stdout, stderr, file streams, TCP streams.

**Scope:**
- New `App/Channels/Channel/Stream/this.cs` extending `Channel.Session.@this`.
- Migrate the Stream-wrapping logic from today's `App/Channels/Channel/this.cs` into here.
- **Excluded:** event firing (Stage 8), Goal channel (Stage 3), entry-point wiring (Stage 6).

**Deliverables:**
- `App/Channels/Channel/Stream/this.cs` — concrete, sealed.
  - Constructor takes `(name, stream, role, direction = Output, ownsStream = true)`. Honour ownership for disposal.
  - `WriteCore(Data, ct)` — uses `Mime` + Serializers to write Data's value to the underlying Stream. Honours `Encoding`, `Buffer`, `Timeout`.
  - `ReadCore(ct)` — reads from the underlying Stream, deserialises via Mime + Serializers, returns Data.
  - `Ask(callback)` — Session-style: blocks reading from the Stream until an answer arrives; deserialise; return Data with the answer. Honour `Timeout`.
  - Static factories: `Output(name, stream, role)`, `Input(name, stream, role)`, `Memory(name, role)` for tests.

**Dependencies:** Stage 1 (Channel base, Session abstract).

## Design

`Channel.Stream` is what today's `App/Channels/Channel/this.cs` becomes. The change is structural (now a subtype of Session) and semantic (now operates on `Data.@this` not raw bytes/strings).

### Serializer integration

`WriteCore` doesn't know how to serialise — it asks `App.Serializers` (promoted from `App.Channels.Serializers`) to serialise the Data using the channel's `Mime`. The serializer writes bytes to the channel's Stream. Symmetric for `ReadCore`.

This means `Channel.Stream` is transport (a Stream wrapper); `Mime` + Serializers do format. Decoupled.

### Ask on Session

For a Stream-backed Session (e.g., console stdin), Ask is straightforward:

```csharp
public override async Task<Data.@this> Ask(AskCallback cb)
{
    // Send the question to the channel's output side if applicable
    // (or rely on caller having already written it).
    
    // Block reading from this Stream until an answer arrives.
    var answer = await ReadCore(...timeout...);
    
    // Bind answer to %!ask.answer% — engine resumes the suspended step.
    return answer;
}
```

For console: stdin's `ReadCore` reads a line. Returns it. Caller's resume mechanism takes over.

If the read times out (per `Timeout`), return `Data.Error` with `AskTimeout`.

### Memory factory

Tests will rely on `Memory(name, role)` to create `MemoryStream`-backed channels for assertions ("did handler write 'hi' to output?"). Keep simple — `MemoryStream`, owns it, exposes for `ReadAllBytes` / `ReadAllText` after the test runs.
