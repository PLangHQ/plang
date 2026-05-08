# Stage 2 — coder plan (`channels-v1-helpers-drop`)

## What

Stage 2 of `runtime2-cleanup`: delete two dead surfaces on `Channels.@this`.

1. The `WriteAsync(string actorName, string channelName, ...)` overload —
   v1 routing helper. Zero external callers (only its own internal redirect
   line referenced it).
2. The contentType-override branch inside the single-string `WriteAsync`,
   plus the `string? contentType = null` parameter. Zero callers ever pass
   contentType.

## File edits

`PLang/App/Channels/this.cs` — only file touched:

- Delete the two-string `WriteAsync(string actorName, string channelName, ...)` overload (10 lines incl. doc-comment).
- Replace the doc-comment on the surviving `WriteAsync(string channelName, object? data, ...)` to describe the new shape (no Stage 4 reference).
- Drop `string? contentType = null` from the signature.
- Delete the contentType-override branch (~20 lines): the `if (!string.IsNullOrEmpty(contentType) && channel is Channel.Stream.@this sc) { ... }` block.

Result: surviving WriteAsync body shrinks to ~5 lines (resolve channel, wrap envelope, delegate to `channel.WriteAsync(envelope, ct)`).

## DefaultHttpProvider verification

`PLang/App/modules/http/providers/DefaultHttpProvider.cs:852, 907` both call
`app.System.Channels.WriteAsync(AppChannels.Error, App.Data.@this.FromError(...))`
— two positional args. Removing the `contentType` parameter is source-compatible
because they never passed it.

## Verification (matches brief's DoD)

- `grep -n "WriteAsync(string actorName" PLang/App/Channels/this.cs` → 0
- `grep -n "string? contentType" PLang/App/Channels/this.cs` → 0
- `grep -n "channel is Channel.Stream.@this sc" PLang/App/Channels/this.cs` → 3 (out of scope: `WriteTextAsync`, `ReadChannelAsync`, `ReadTextAsync`)
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` 2755/2755.
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` 199/199.

## Out of scope (flagged for future stages)

- `is Channel.Stream.@this sc` casts in `ReadChannelAsync` (line 198), `WriteTextAsync` (line 169 after edit), `ReadTextAsync` (line 218) — same shape smell. The first reaches into `sc.Stream` + `sc.Mime`; the latter two dispatch to Stream-specific `WriteTextAsync`/`ReadAllTextAsync`. Polymorphism on `Channel.@this` could replace them.
- The other stale "Stage 4 / Stage 6" comments in unrelated parts of `Channels/`.
