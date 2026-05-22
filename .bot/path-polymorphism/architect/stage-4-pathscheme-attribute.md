# Stage 4: `[PathScheme]` attribute

**Goal:** Define the marker attribute that future `code.load` will use to discover scheme handlers in third-party DLLs. Define it here; don't consume it.

**Scope:** One attribute class. Apply to `FilePath` (and `HttpPath` in stage 5) for documentation, even though built-ins are registered explicitly by name at App startup.

**Out of scope:**
- Consuming the attribute (that's future `code.load` work).
- Source-generator changes (no generator pass uses this attribute).
- Reflection-based registration at App startup — built-ins are still registered by explicit name.

## Why this is a tiny stage on its own

It's one class with no consumer. Lives here as scaffolding for the future external-scheme story. Keeping it separate makes the intent obvious: "this attribute is a contract for external scheme handlers, not a runtime mechanism we depend on today."

## Design

```csharp
namespace app.types.path;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class PathSchemeAttribute : Attribute
{
    public string Scheme { get; }
    public PathSchemeAttribute(string scheme) => Scheme = scheme;
}
```

`AllowMultiple = true` so `HttpPath` can carry `[PathScheme("http")] [PathScheme("https")]`.

## Application

After this stage, scheme handler classes are decorated:

```csharp
[PathScheme("file")]
public sealed class @this : Path { ... }       // FilePath at app/types/path/file/this.cs
```

App startup still registers explicitly:

```csharp
app.Types.Scheme.Register("file", raw => new FilePath(raw));
```

The attribute is documentation + future-reflection contract — nothing runs on it on this branch.

## Constructor contract

Document on the attribute (xmldoc):

> Classes decorated with `[PathScheme]` must expose a public single-string constructor: `public @this(string raw)`. Future `code.load`-driven registration relies on this signature.

Don't enforce via analyzer in this stage — that's source-generator work this branch is intentionally avoiding. A comment + xmldoc is enough; built-ins are reviewed by humans.

## Deliverables

- `PLang/app/types/path/PathSchemeAttribute.cs` — the attribute class.
- `[PathScheme("file")]` applied to `FilePath` (`app/types/path/file/this.cs`).
- `[PathScheme("http")] [PathScheme("https")]` applied to `HttpPath` (stage 5 lands HttpPath; this attribute usage rides along).

## Tests

See `plan-test-designer.md` Stage 4:

- Attribute has `AttributeUsage(AttributeTargets.Class, AllowMultiple = true)`.
- A class with two `[PathScheme]` attributes returns both via reflection.
- Constructor-contract test: `FilePath` (and later `HttpPath`) have the `public @this(string)` constructor that future reflection-based registration will need.

## Risk

Effectively zero. Smallest stage in the branch.
