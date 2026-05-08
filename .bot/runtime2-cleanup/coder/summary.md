# coder — runtime2-cleanup

## Version

v2 — Stage 2 (`channels-v1-helpers-drop`).
Stage 1 (`serializers-single-home`) summary kept below as a v1 entry.

## v2 — Stage 2 (`channels-v1-helpers-drop`)

### What this is

Dead-code deletion stage. Two surfaces on `Channels.@this` had outlived
their purpose and were retained only by self-references and stale doc
comments referring to a "Stage 4 / Stage 6" of the prior channels plan
(not this cleanup plan). Stage 2 deletes them.

1. `WriteAsync(string actorName, string channelName, object? data, ...)` —
   v1 actor-routing helper. Zero external callers.
2. The `contentType` override branch inside the single-string
   `WriteAsync(channelName, data, contentType, ...)`. Zero callers ever
   passed `contentType`. The parameter goes too.

### What was done

Single file edited: `PLang/App/Channels/this.cs`.

- Deleted the two-string `WriteAsync` overload (10 lines incl. comment).
- Dropped the `string? contentType = null` parameter from the surviving
  `WriteAsync(channelName, data, ...)`.
- Removed the ~20-line `if (!string.IsNullOrEmpty(contentType) && channel is Channel.Stream.@this sc) { ... }` block that re-serialised through the parent Channels' Serializers using the cast Stream.
- Replaced the surviving WriteAsync's doc-comment with a per-actor
  description that matches stage 1's reality (no "Stage 4" reference).

Resulting surviving body is ~5 lines: `GetChannel` → wrap envelope →
delegate to `channel.WriteAsync(envelope, ct)` (which fires Channel
events and routes through `WriteCore`).

### Verification

- `grep -n "WriteAsync(string actorName" PLang/App/Channels/this.cs` → 0.
- `grep -n "string? contentType" PLang/App/Channels/this.cs` → 0.
- `grep -n "channel is Channel.Stream.@this sc" PLang/App/Channels/this.cs` → 3 (the three remaining out-of-scope `sc` casts the brief flagged: `WriteTextAsync`, `ReadChannelAsync`, `ReadTextAsync`).
- `dotnet build PlangConsole` clean (0 errors, 449 warnings — unchanged from trunk).
- `dotnet run --project PLang.Tests` → **2755/2755 pass**.
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` → **199/199 pass**.

### Code example — the shrink

Before (~32 lines including doc comment, contentType branch, error path):

```csharp
public async Task<Data.@this> WriteAsync(string channelName, object? data,
    string? contentType = null, CancellationToken cancellationToken = default)
{
    var (channel, error) = GetChannel(channelName, requireWrite: true);
    if (error != null) return error;

    var envelope = data is Data.@this d ? d : Data.@this.Ok(data);
    if (!string.IsNullOrEmpty(contentType) && channel is Channel.Stream.@this sc)
    {
        try
        {
            await Serializers.SerializeAsync(new SerializeOptions
            {
                Stream = sc.Stream, Data = envelope.Value,
                ContentType = contentType, CancellationToken = cancellationToken
            });
            return App.Data.@this.Ok();
        }
        catch (Exception ex) when (...) { return ServiceError(...); }
    }
    return await channel!.WriteAsync(envelope, cancellationToken);
}
```

After (5-line body):

```csharp
public async Task<Data.@this> WriteAsync(string channelName, object? data,
    CancellationToken cancellationToken = default)
{
    var (channel, error) = GetChannel(channelName, requireWrite: true);
    if (error != null) return error;

    var envelope = data is Data.@this d ? d : Data.@this.Ok(data);
    return await channel!.WriteAsync(envelope, cancellationToken);
}
```

The two-string overload deletion is mechanical — the method block goes
entirely.

### Notes for next stages

The brief explicitly flagged the three remaining `is Channel.Stream.@this sc`
casts — verified in place at `WriteTextAsync` (line 169 post-edit),
`ReadChannelAsync` (line 198), `ReadTextAsync` (line 218). The first
reaches into `sc.Stream` + `sc.Mime` (cross-class internal reach, same
shape as the contentType branch); the latter two dispatch to
Stream-specific public methods. None on stage 2; flagged for whichever
future stage carves them.

DefaultHttpProvider's two callers at `app.System.Channels.WriteAsync(...)`
(lines 852, 907) verified — both pass two positional args, none pass
`contentType`. Removing the parameter was source-compatible.

---

## v1 — Stage 1 (`serializers-single-home`)

(Original v1 summary kept for context.)

First commit on `runtime2-cleanup` after the architect carved Stage 1.
Established per-actor `Channels.@this.Serializers` as the single home for
the serializer registry by deleting the two surfaces that bypassed it:

- `App.@this.Serializers` — App-root shortcut that bypassed the actor entirely.
- `Channel.Stream.@this._serializers` — third copy lazily allocated per stream.

Added `Channels` back-ref on `Channel.@this` (alongside `App`); set in
`Channels.Register(channel)`. Stream's `WriteCore` routes through
`Channels!.Serializers.SerializeAsync(...)`.

Caller sweep covered 5 production sites + 6 test files + 7 unit tests
that needed the new boot-ordering (construct-then-write was migrated to
register-via-Channels.Register).

Both suites passed: C# 2755/2755, PLang 199/199.
