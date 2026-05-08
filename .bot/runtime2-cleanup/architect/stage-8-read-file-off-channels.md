# Stage 8: `read-file-off-channels`

**Read first:**
- `plan/principles.md` — OBP discipline, especially the smells section (dead-code shape).
- `plan/scope-map.md` — Channels is per-actor; this stage doesn't change scope.

**Goal:** Delete `Channels.@this.ReadAsync<T>(string filePath)` at Channels.this.cs:59-66. The plan one-liner anticipated relocating it to `app.Serializers` or FileSystem; the actual finding is **zero callers** — it's dead code. No relocation is needed; just delete.

**Scope:**
- *Included:* delete the method (~8 lines including doc-comment) at Channels.this.cs:59-66.
- *Excluded:* anything else. Pure dead-code removal.

**Deliverables:**
- `PLang/App/Channels/this.cs` — delete the method:

```csharp
/// <summary>
/// Reads a file and deserializes its content via the serializer registry.
/// </summary>
public async Task<T?> ReadAsync<T>(string filePath, CancellationToken cancellationToken = default)
{
    var fs = _app.FileSystem;
    var content = await fs.File.ReadAllTextAsync(filePath, cancellationToken);
    var ext = fs.Path.GetExtension(filePath);
    return Serializers.Deserialize<T>(new DeserializeOptions { Value = content, Extension = ext });
}
```

- C# tests pass: `dotnet run --project PLang.Tests`.
- PLang tests pass from a clean rebuild: `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`.

**Dependencies:** None. Tier 2 stage; independent of stage 7. Either order works.

## Design

### The smell this closes

Method-on-the-wrong-class **plus** dead code. The method reads a file from disk and deserializes — it doesn't read from a channel. It was on `Channels.@this` only because the deserialization step uses `Serializers`, and Serializers used to live with Channels. Even when the placement was defensible, no consumer ever called this method.

After deletion: Channels.@this drops a method that doesn't belong to it AND a method nobody uses.

### Why "delete" instead of "relocate" (which the plan one-liner suggested)

The plan one-liner (`Goes to app.Serializers or FileSystem`) anticipated a relocation. Two findings make relocation moot:

1. **Zero callers.** Verified by `grep -rn "Channels\.ReadAsync<\|\.Channels\.ReadAsync<" PLang/ PLang.Tests/ Tests/` — empty. No code anywhere typed-reads-and-deserializes a file via this entry point.
2. **`app.Serializers` no longer exists** (deleted in stage 1; per-actor `actor.Channels.Serializers` is the new home). The stated relocation target is gone.

If a future caller wants "read a file and deserialize," they can write that two-step at the call site (one line for `File.ReadAllTextAsync`, one for the serializer call). No need for a runtime helper that nobody used.

### The new shape

After:

```csharp
// Channels.this.cs lines 59-66 (and the leading doc-comment): deleted.
```

Channels.@this loses ~8 lines.

### Files touched + caller propagation

**Files modified (1):**
- `PLang/App/Channels/this.cs` — one method deleted.

**Caller verification:**
- `grep -rn "Channels\.ReadAsync<" PLang/ PLang.Tests/ Tests/ --include='*.cs'` returns zero hits. Confirmed dead.
- `grep -rn "\.ReadAsync<[A-Z]" PLang/ --include='*.cs'` (broader pattern catching any typed ReadAsync) returns zero hits.

### Risk + dependencies

**Risk: very low.** Dead-code deletion. Build catches anything I missed.

Possible failure modes:
- A grep miss — unlikely; two distinct grep patterns scanned.

**Dependencies: none.** Independent.

### Tests

**No new tests required.** Behavior unchanged (no caller exercised the path).

**Existing test coverage to verify:**
- `PLang.Tests/App/Channels/` — channel I/O.
- `Tests/` — full PLang suite.

**Definition of done:**
- `dotnet build PlangConsole` clean.
- `dotnet run --project PLang.Tests` green (baseline 2755/2755).
- `cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test` green from a fresh rebuild (baseline 199/199).
- `grep -n "ReadAsync<T>" PLang/App/Channels/this.cs` — zero hits.

### Watch for (coder eyes-on)

- **A late-discovered caller** in code I didn't grep (e.g., reflection-based callers, generated code). If the build breaks, the call site will name itself. If the caller is real and serves a purpose, escalate before re-adding the method — likely the caller should inline the two-step (`File.ReadAllTextAsync` + Serializers call) at its own site.
- **Channels.this.cs's other methods** that look similarly mis-placed — flag if you see one (e.g., another method on Channels that doesn't actually touch a channel). Don't fix in stage 8.

### Stages that follow this one

- **Stage 7** (`callstack-promote-app-property`) — same Tier 2 batch; independent.
- **Stage 9** (`catalog-dissolve-to-modules-schema`) — substantially bigger; carved later in its own focused session.

### Out of scope

- Any restructuring of `Channels/Serializers/` — stage 15.
- The `is Channel.Stream.@this sc` casts elsewhere in Channels.this.cs — out of scope (flagged for future).

## Commit plan

```
runtime2-cleanup stage 8: drop dead Channels.ReadAsync<T>(filePath)

Channels.@this had a typed file-read-and-deserialize helper:

  public async Task<T?> ReadAsync<T>(string filePath, ...)
  {
      var fs = _app.FileSystem;
      var content = await fs.File.ReadAllTextAsync(filePath, ct);
      var ext = fs.Path.GetExtension(filePath);
      return Serializers.Deserialize<T>(new DeserializeOptions {
          Value = content, Extension = ext });
  }

The plan one-liner anticipated relocating this to app.Serializers or
FileSystem. Two findings make relocation moot:

1. Zero callers anywhere (verified across PLang/, PLang.Tests/, Tests/).
2. app.Serializers no longer exists (deleted in stage 1; per-actor
   actor.Channels.Serializers is the new home).

Just delete the method. If a future caller wants "read a file and
deserialize," they write the two-step at the call site.
```
