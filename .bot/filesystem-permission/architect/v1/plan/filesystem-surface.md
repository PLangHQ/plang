# Filesystem Surface — Deep Dive

## The shift

`IPLangFileSystem` today inherits `System.IO.Abstractions.IFileSystem` — a BCL-shaped surface where every method takes `string` paths. That works against us in three ways:

1. The verb being requested (read vs. write vs. delete) is implicit in the method name and lost by the time `ValidatePath` runs.
2. The calling goal isn't carried — `ValidatePath` has no idea who's asking.
3. Every method returns raw .NET types and throws on error, making `Data`-based composition impossible.

The rewrite drops the inheritance and makes the surface our own. Every method takes `Path`. The Path object carries its absolute and raw forms *and* the calling goal (via `Context.Goal`). Return shapes are `Data<T>` so errors are first-class.

## What we're keeping

The shape `Path` itself, mostly. `Path.cs` already exists, already carries Context, already has `Resolve(rawPath, context)` that handles relative-to-goal-folder resolution. We don't redesign Path — we let it carry the foreign key the permission system needs.

The Default code (current `PLangFileSystem`) keeps using `System.IO.Abstractions` *internally*. The BCL surface stays as the local-disk implementation language; it just stops being the public language of the filesystem layer.

## What we're dropping

Everything BCL-shaped from the public surface:

- `IPLangFileSystem : IFileSystem` — inheritance gone.
- The `Factory` classes (`PLangFileStreamFactory`, `PLangDirectoryInfoFactory`, etc.) as public exports — they become internal helpers of the Default code, or disappear entirely.
- The `PLangFile`, `PLangFileInfo`, `PLangDirectoryWrapper` types as public types — same.

If a consumer needs them today, that's a call site we need to migrate.

## The survey (Pass 1, 2, 3)

The closed list of operations the action layer and runtime actually use must be the minimum public surface. The big mapping work in Stage 3:

**Pass 1 — Action layer.** `PLang/App/modules/file/` (read, save, copy, move, delete, exists, list) and their `code/` subfolders. Any other action that touches the FS — output handlers writing files, snapshot, build, the Diagnostics writer. Grep for `FileSystem.` calls.

**Pass 2 — Runtime internals.** `PLang/Runtime2`, `PLang/App/Goals`, `PLang/App/Snapshot`, `PLang/App/Settings` — the engine loads goals, reads `.pr` files, writes snapshots, persists settings. Grep for `IPLangFileSystem` and `app.FileSystem` calls.

**Pass 3 — Directory operations.** `Directory.Exists`, `EnumerateFiles`, `EnumerateDirectories`, `CreateDirectory`. Same passes, looking for the Directory subsurface.

Output: a flat list of operations. Probably ~15 distinct ones once de-duplicated.

## Pinning the signatures (open in this plan)

For each operation in the closed list, decide three things:

**A. Return shape.** Three candidates per read-like operation:
- `Data<string>` — content as text (eager, encoding-aware)
- `Data<byte[]>` — content as bytes (eager, no encoding decision)
- `Data<Stream>` — lazy stream (consumer chooses how to read; supports large files)

A read action probably wants `Data<byte[]>` or `Data<string>` depending on whether MIME is text. The runtime might want streams for big files. The closed list will tell us which we actually need.

**B. Verb explicit-or-implicit.** Two options for every operation:
- `Read(Path, Verb.@this requested)` — caller spells out exact verb (allows passing `Read{Recursive: true}` for listing)
- `Read(Path)` — method constructs the default verb internally; caller can't customize
- Recommendation: explicit for operations whose verb meaningfully varies (listing wants Recursive; reading doesn't). Implicit for trivially-typed operations (Exists is always `Read{Metadata=true}`).

**C. Error decomposition.** `Data.Error` carries a typed payload. Two distinct error kinds:
- `PermissionRequired(Path path, Verb.@this requested)` — recoverable; the runtime escalates to the user.
- `IoFailure(Path path, Exception cause)` — not recoverable by prompting; bubbles to the caller as a hard fail.
Each lives as its own type. The caller's error-handling can distinguish.

## Indicative method shapes (not pinned)

```csharp
public interface IPLangFileSystem
{
    // File-level
    Data<byte[]>  Read(Path path);
    Data<Stream>  OpenRead(Path path);
    Data          Write(Path path, byte[] content, Verb.@this requested);
    Data          Delete(Path path);
    Data<bool>    Exists(Path path);

    // Directory-level
    Data<IReadOnlyList<Path>> List(Path directory, Verb.@this requested);
    Data                      CreateDirectory(Path path);

    // Misc
    Data          Move(Path source, Path destination);
    Data          Copy(Path source, Path destination);
}
```

These are sketches — real names and shapes come out of the surveys. The point is the *shape language*: `Path` in, `Data<T>` out, `Verb.@this` parameter only when intent varies.

## The Default code

Inside `PLang/App/FileSystem/Default/` (or whatever the Code folder is named under the new scheme), the implementation does:

1. Receive a `Path` and (optional) `Verb.@this requested`.
2. Call `Permission/@this.Check(path, requested)`. On `Data.Fail`, return that.
3. On Ok, delegate to `System.IO.Abstractions` (or `System.IO` directly) using `path.Absolute`.
4. Wrap the BCL result in `Data<T>` — success or `IoFailure` on exception.

The Permission check happens once, at the FS boundary. Internal helpers below that line trust the path.

## What this enables for stage 5

When Messages tries `Read(/apps/Email/system.sqlite)`:
1. Path is constructed with Messages' goal in Context.
2. FS.Read calls Permission.Check.
3. No grant; returns `Data.Fail(PermissionRequired)`.
4. Runtime catches the typed error, raises the prompt (the prompt knows the calling goal from `path.Context.Goal.Path` and the verb from the error payload).
5. User says "always"; PLang plumbing signs a `Data<Permission>`; Permission.Add stores it in the system variable.
6. The action retries; Check succeeds; bytes return in `Data<byte[]>`.

Every piece is owned by exactly one type.
