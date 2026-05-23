# Stage 2: `path` abstract + Scheme registry

**Goal:** Turn `path` (now `@this` at `app.types.path`) into an abstract base with virtual verb methods. Move today's concrete implementation into `FilePath : Path`. Stand up the per-App `scheme` registry. Wire App startup to register `"file"` and bare-paths. Wire the PLang `path` type-mapper to dispatch through the registry.

**Scope:** Abstract base, first subclass, registry mechanism, type-mapper integration. One scheme only (`file://` + bare paths). All existing behavior preserved.

**Out of scope:**
- HttpPath, http/https registration (stage 5).
- `[PathScheme]` attribute (stage 4).
- Handler simplification (stage 3 — handlers still call the old `IFile` surface at the end of this stage).
- Killing `IFile`/`DefaultFileProvider`/the `[Code]` partial mechanism (stage 3).

## Design

### `path` becomes abstract

`PLang/app/types/path/this.cs` — abstract class with virtual verb methods:

```csharp
namespace app.types.path;

[PlangType("path", Example = "/some/file.json")]
public abstract partial class @this : modules.IContext
{
    public abstract string Raw { get; }
    public abstract string Absolute { get; }    // canonical-form per scheme (FilePath: OS-normalized; HttpPath stage 6)
    public abstract string Scheme { get; }       // "file", "http", "https", ...

    public abstract Task<data.@this> ReadText();
    public abstract Task<data.@this> ReadBytes();
    public abstract Task<data.@this> WriteText(string content);
    public abstract Task<data.@this> WriteBytes(byte[] content);
    public abstract Task<data.@this> Append(string content);
    public abstract Task<data.@this> Save(data.@this content);
    public abstract Task<data.@this> Delete();
    public abstract Task<data.@this> Exists();
    public abstract Task<data.@this> Stat();
    public abstract Task<data.@this> List();

    // Cross-scheme defaults — overridable for same-scheme fast paths.
    public virtual async Task<data.@this> CopyTo(Path dest) { /* ReadBytes -> dest.WriteBytes */ }
    public virtual async Task<data.@this> MoveTo(Path dest) { /* CopyTo + Delete */ }
}
```

The exact verb list mirrors today's `this.Operations.cs` surface (formerly `path.Operations.cs`) — see that file for the authoritative set. Don't invent new verbs in this stage; just abstract the existing ones.

`this.Authorize.cs` (the Permission gate partial) stays non-virtual — Permission gating is scheme-agnostic and lives on the base.

### `FilePath : Path`

`PLang/app/types/path/file/this.cs` — concrete subclass for the file scheme:

```csharp
namespace app.types.path.file;

public sealed class @this : Path
{
    public @this(string raw) { /* normalize, capture raw */ }

    public override string Raw => _raw;
    public override string Absolute => _absolute;     // OS-normalized
    public override string Scheme => "file";

    public override Task<data.@this> ReadText()  { /* System.IO.File.ReadAllText */ }
    public override Task<data.@this> WriteText(string c) { /* System.IO.File.WriteAllText */ }
    // ... etc.
}
```

Body of each verb comes from today's `this.Operations.cs` impl on the base `path` class. Move the logic; don't rewrite it.

Constructor signature `public @this(string raw)` is the standard — every scheme subclass needs this exact shape so the registry's factory delegate is uniform.

Add the `FilePath` global alias in `PLang/app/GlobalUsings.cs`:

```csharp
global using FilePath = app.types.path.file.@this;
```

### Scheme registry

`PLang/app/types/path/scheme/this.cs`:

```csharp
namespace app.types.path.scheme;

public sealed class @this
{
    private readonly ConcurrentDictionary<string, Func<string, Path>> _factories
        = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string scheme, Func<string, Path> factory)
        => _factories[scheme] = factory;

    public Path From(string raw)
    {
        var scheme = ParseScheme(raw);              // "" if no scheme
        if (scheme == "")
            return new FilePath(raw);               // bare paths default to file
        if (_factories.TryGetValue(scheme, out var factory))
            return factory(raw);
        throw new SchemeNotRegistered(scheme);      // caught by type-mapper, becomes data.@this.Fail
    }

    private static string ParseScheme(string raw) { /* split on first ':' if URI-shaped */ }
}
```

**Unknown-scheme policy:** `From` throws a typed exception that the PLang `path` type-mapper catches and shapes as a `data.@this.Fail` with a "scheme X not registered" message. Don't let the exception escape past the type-mapper boundary — Data flow uniformity matters.

### App-start wiring

In the App init code (per-App services construction in `PLang/app/this.cs` or whatever owns service init):

```csharp
// During App construction:
Types.Path.Scheme.Register("file", raw => new FilePath(raw));
// http/https come in stage 5
```

The `Scheme` instance is owned by `app.Types.Path` — exposed as a property: `app.Types.Path.Scheme`. Mirror however `Choices` is exposed on `app.types.@this` today (`app.Types.Choices`). The accessor pattern is already established there.

### `app.types.@this` exposes `Path`

`PLang/app/types/this.cs` grows a `Path` accessor analogous to its `Choices` accessor:

```csharp
public sealed partial class @this
{
    public choices.@this Choices { get; } = new();
    public path.scheme.@this Scheme { get; } = new();
    // ...
}
```

Accessor name `Scheme` mirrors the folder-leaf naming pattern of `Choices` (which lives at `app.types.choices.@this`). No collision with the global `Path` alias. Slight under-specification ("scheme of what?") is fine — the return type of `Scheme.From(...)` is `Path`, so context disambiguates at every call site. Alternatives considered (`Path`, `PathSchemes`, `Paths`) rejected: `Path` shadows the alias; `PathSchemes` is verbose; `Paths` plural lies about the contents.

### PLang type-mapper update

`PLang/app/types/Conversion.cs` currently handles `path` → CLR `path` conversion. Today it likely constructs a `path` directly. After this stage, it calls the scheme registry:

```csharp
// inside Conversion's path mapping
return context.App.Types.Scheme.From(raw);
```

The signature change ripples to any other type-mapper consumer.

## Deliverables

- `PLang/app/types/path/this.cs` — abstract base (replaces concrete `this.cs` from stage 1).
- `PLang/app/types/path/this.Operations.cs` — partial, thinned (most logic moved into `FilePath`); keep cross-scheme defaults for `CopyTo`/`MoveTo`.
- `PLang/app/types/path/this.Authorize.cs` — unchanged shape, stays on the base.
- `PLang/app/types/path/file/this.cs` — `FilePath : Path`. Body = relocated `this.Operations` impl.
- `PLang/app/types/path/scheme/this.cs` — registry.
- `PLang/app/this.cs` (or per-App services init) — calls `Scheme.Register("file", ...)`.
- `PLang/app/types/this.cs` — exposes the scheme registry accessor (whatever shape matches existing `Choices` accessor).
- `PLang/app/types/Conversion.cs` — `path` type-mapper routes through the registry.
- `PLang/app/GlobalUsings.cs` — adds `global using FilePath = app.types.path.file.@this;`.

## Tests

See `plan-test-designer.md` Stage 2. Key surfaces:

- Scheme registry Register/From.
- Bare paths route to FilePath.
- Unknown scheme produces `data.@this.Fail` (not exception leak).
- FilePath verb round-trip (essentially the existing path coverage, repointed at `Path.X()`).
- PLang type-mapper integration.
- Multi-App isolation of registrations.

## Risk

The abstract refactor itself is mechanical. The risk is mismatch between what `this.Operations.cs` does today and what `FilePath` does after — especially around path normalization that may happen in subtle places. Run the full existing test suite against the refactored shape before declaring done.
