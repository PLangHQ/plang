# Stage 2: `channels-v1-helpers-drop`

**Read first:**
- `plan/principles.md` — OBP discipline, especially the "Choosing what back-ref(s) a class holds" and "smells" sections.
- `plan/scope-map.md` — Channels is per-actor; this stage doesn't change scope, only deletes dead helpers.

**Goal:** Delete two dead surfaces on `Channels.@this`:
1. The `WriteAsync(string actorName, string channelName, ...)` overload — a v1 helper that takes an actor name and routes via `_app.GetActor(actorName)`. Zero external callers.
2. The `contentType` override branch inside `WriteAsync(string channelName, object? data, string? contentType, ...)` — a special-case path that re-serializes through the Channels' Serializers when a non-null `contentType` is passed and the channel is a Stream. Zero callers pass `contentType`. The parameter goes too.

Both are documented in their own doc-comments as "kept for v1 callers" and "stage 4 cleans this up." That referenced "stage 4" is the prior channels plan, not this cleanup plan — the helpers are dead today and this is the cleanup plan's stage 2.

**Scope:**
- *Included:* delete the two-string `WriteAsync` overload entirely; delete the contentType override branch in the single-string `WriteAsync`; drop the `contentType` parameter; verify DefaultHttpProvider's two callers still work without it.
- *Excluded:* the other `is Channel.Stream.@this sc` branches in `ReadChannelAsync` (line 202), `WriteTextAsync` (line 231), `ReadTextAsync` (line 251). They have the same shape smell but aren't in this stage's plan-stated scope. Flag for a future stage if anything else touches them.

**Deliverables:**
- `PLang/App/Channels/this.cs` — two deletions:
  1. The `WriteAsync(string actorName, string channelName, object? data, CancellationToken ct = default)` method at lines 60–66 (~7 lines).
  2. Lines 174–189 in the single-string `WriteAsync` — the entire `if (!string.IsNullOrEmpty(contentType) && channel is Channel.Stream.@this sc) { try { Serializers.SerializeAsync(...) } catch ... }` block. Plus drop the `string? contentType = null` parameter from the method signature.
  3. Update the doc-comment on the remaining `WriteAsync(string channelName, object? data, ...)` — drop the "Stage 4 moves this responsibility" note and the contentType-related text.
- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** Stage 1 landed; the `Channels.Serializers` per-actor home is established. Stage 2 is independent of that — it doesn't touch Serializers — but builds on a clean trunk.

## Design

### The smell this closes

**Smell #4** — *allocate-here / mutate-there / clean-up-elsewhere*, at the API-surface level. The contentType-override branch had `Channels` reaching into Stream's internal `System.IO.Stream` property and re-doing serializer work that should belong to the channel itself. Today it's dead; nobody invokes it. Removing it closes both the cross-class internal-reach AND the dead code.

The `WriteAsync(actorName, channelName, ...)` helper is a different smell — **dead code**. The doc-comment promises "kept for v1 callers (DefaultHttpProvider etc.)" but a grep of the codebase finds no callers. DefaultHttpProvider uses the per-actor `app.System.Channels.WriteAsync(name, data)` form, which doesn't go through this overload.

### The new shape

**`Channels.@this.WriteAsync` after stage 2:**

```csharp
// Today (lines 60–66):
public async Task<Data.@this> WriteAsync(string actorName, string channelName, object? data, CancellationToken ct = default)
{
    var (actor, error) = _app.GetActor(actorName);
    if (error != null) return App.Data.@this.FromError(error);
    return await actor!.Channels.WriteAsync(channelName, data, cancellationToken: ct);
}

// After: deleted entirely.
```

```csharp
// Today (lines 164–192):
public async Task<Data.@this> WriteAsync(string channelName, object? data, string? contentType = null, CancellationToken cancellationToken = default)
{
    var (channel, error) = GetChannel(channelName, requireWrite: true);
    if (error != null) return error;

    var envelope = data is Data.@this d ? d : Data.@this.Ok(data);
    if (!string.IsNullOrEmpty(contentType) && channel is Channel.Stream.@this sc)
    {
        try {
            await Serializers.SerializeAsync(new SerializeOptions {
                Stream = sc.Stream, Data = envelope.Value,
                ContentType = contentType, CancellationToken = cancellationToken
            });
            return App.Data.@this.Ok();
        }
        catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException)) {
            return App.Data.@this.FromError(new ServiceError($"Failed to write to channel '{channelName}': {ex.Message}", "WriteError") { Exception = ex });
        }
    }
    return await channel!.WriteAsync(envelope, cancellationToken);
}

// After:
public async Task<Data.@this> WriteAsync(string channelName, object? data, CancellationToken cancellationToken = default)
{
    var (channel, error) = GetChannel(channelName, requireWrite: true);
    if (error != null) return error;

    var envelope = data is Data.@this d ? d : Data.@this.Ok(data);
    return await channel!.WriteAsync(envelope, cancellationToken);
}
```

Five lines of body. The serializer routing happens inside `channel.WriteAsync(envelope)` (which fires events, then `WriteCore`, which on Stream uses the parent Channels' Serializers per stage 1's wiring).

### Files touched + caller propagation

**Files modified (1):**
- `PLang/App/Channels/this.cs` — two methods touched, one deleted, one shrunk by ~17 lines.

**Caller verification:**
- `Channels.WriteAsync(actor, channel, data)` — zero callers. Confirmed by `grep -rn "Channels\.WriteAsync(\".*\",\s*\"" PLang/ --include='*.cs'` returning empty plus `grep -rn "Channels\.WriteAsync\b" PLang/` showing only the overload's own internal redirect at line 65 and the two DefaultHttpProvider callers (which use the *single-string* form).
- `Channels.WriteAsync(channel, data, contentType)` — the two DefaultHttpProvider callers at lines 852 and 907 don't pass `contentType` (verified by reading both call sites). Removing the parameter is binary-compatible-shaped — they continue working unchanged.
- Test side — grep `Channels.WriteAsync` in `PLang.Tests/` returned zero hits; no test exercises either of the deleted surfaces.

### Risk + dependencies

**Risk: very low.** This is dead-code deletion + one parameter removal. No new types, no new behaviour, no scope changes.

Possible failure modes:
1. **A grep miss** — a caller of `Channels.WriteAsync(actor, channel, data)` somewhere I didn't scan. Build break catches this immediately (the overload is gone).
2. **A caller of `Channels.WriteAsync(channel, data, contentType: someValue)`** — same verification: build break catches it. None found.
3. **Doc comment drift** — the existing comment on the surviving WriteAsync references "Stage 4" of the channels work. Update or remove that to avoid confusing the next reader.

**Dependencies: stage 1 landed.** Otherwise independent.

### Tests

**No new tests required.** Observable behaviour is unchanged — the contentType-override path was dead; the actor-name helper had no callers.

**Existing test coverage to verify:**
- `PLang.Tests/App/Channels/` — channel write/read round-trips.
- `PLang.Tests/App/Core/EngineTests.cs` — boot wiring.
- `Tests/` — full PLang suite from a clean rebuild.

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (no new failures vs trunk; baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `grep -n "WriteAsync(string actorName" PLang/App/Channels/this.cs` — zero hits.
- `grep -n "string? contentType" PLang/App/Channels/this.cs` — zero hits.
- `grep -n "channel is Channel.Stream.@this sc" PLang/App/Channels/this.cs` — three hits remain (lines that are out of scope for this stage), the contentType-override one is gone.

### Watch for (coder eyes-on)

The cleanup discipline: **flag, don't fix**. If you see something that smells but isn't stage 2, note it in the commit message or as a comment for the future stage.

- **The other `is Channel.Stream.@this sc` branches in this same file** (`ReadChannelAsync` at line 202, `WriteTextAsync` at line 231, `ReadTextAsync` at line 251). These are out of scope for stage 2. The first one (ReadChannelAsync) reaches into `sc.Stream` and `sc.Mime` — same kind of cross-class internal-reach as the contentType-override we're deleting. The latter two (WriteTextAsync/ReadTextAsync) dispatch to Stream-specific public methods (`sc.WriteTextAsync(text)`, `sc.ReadAllTextAsync(...)`) — different shape, polymorphism on Channel could replace them. None of these in stage 2; flag if you have an opinion on which future stage should close them.
- **Stale doc comments referencing the prior channels plan's "Stage 4" or "Stage 6"** — the codebase has several. Out of scope to rewrite all of them, but if you touch a method whose comment is now wrong (e.g., the surviving `WriteAsync` whose doc says "Stage 4 cleans this up"), fix that one comment in your edit.
- **An unexpected build break** — the deletions are mechanical, but if something fails to compile, the missed-caller is the most likely cause. Don't paper over by re-adding the method; trace the actual call site and decide whether it needs the per-actor form (`app.GetActor(name).Channels.WriteAsync(...)`) or one of the surviving Channels methods.

### Stages that follow this one

- Several future stages may want to close the remaining `is Channel.Stream.@this sc` casts in `Channels.this.cs`. None are formally scheduled today. If they earn a stage, this brief's "Watch for" note is the breadcrumb.
- Stage 4 (`dispose-self-owns`) is the next ownership-realignment stage and is independent of stage 2.

### Out of scope

- The remaining `is Channel.Stream.@this sc` casts in `ReadChannelAsync`, `WriteTextAsync`, `ReadTextAsync` — flag for future, do not touch.
- Any change to `Channel.@this` base class virtuals (e.g., adding abstract `WriteTextAsync`/`ReadTextAsync` to make the dispatch polymorphic) — design decision, not stage 2's scope.
- Renames inside `Channels/` (folder, files, types) — stage 15 (`compound-name-rename`) territory.
- `ReadAsync<T>(string filePath)` relocation — stage 8.

If you find yourself reaching for any of the above, stop and let the appropriate later stage handle it.

## Commit plan

One commit:

```
runtime2-cleanup stage 2: drop dead Channels v1 helpers

Two surfaces on Channels.@this had outlived their purpose:

1. WriteAsync(actorName, channelName, ...) — a v1 routing helper kept
   "for DefaultHttpProvider etc." per its doc comment. Zero external
   callers; only its own internal redirect line referenced it.

2. The contentType-override branch in WriteAsync(channelName, data,
   contentType, ...) — special-case path where a non-null contentType
   reached into Stream's underlying System.IO.Stream to re-serialize
   directly. Zero callers ever pass contentType; the contentType
   parameter is dead too.

Deletes the two-string overload and the contentType-override branch +
parameter. Single-string WriteAsync shrinks to ~5 lines of body — name
resolution + envelope wrap + delegate to channel.WriteAsync(envelope).

DefaultHttpProvider's two callers at app.System.Channels.WriteAsync
(lines 852, 907) keep working — they never passed contentType.

Out of scope: the three remaining `is Channel.Stream.@this sc` casts
in ReadChannelAsync / WriteTextAsync / ReadTextAsync. Same shape smell;
flagged for a future stage.
```
