# Stage 6 — coder plan (`app-data-inheritance-drop`)

App stops inheriting from `Data.@this<@this>`. Becomes a plain class with
just `IAsyncDisposable`.

## Files

`PLang/App/this.cs` only:

- Line 19 base list: `: Data.@this<@this>, IAsyncDisposable` → `: IAsyncDisposable`.
- Delete the `public new string Path => "/"` shadow at line 63 (zero readers verified — see grep below).
- Drop the `: base("!app")` ctor initialiser on the primary `(string absolutePath, ...)` ctor — it was inherited-base-only and breaks compilation once Data is gone.

`PLang/App/this.Snapshot.cs` is the only secondary partial; it has no base
list, so no change needed.

## Verification

- `grep -n ": Data.@this<@this>" PLang/App/this.cs` → 0
- `grep -n "public new string Path" PLang/App/this.cs` → 0
- `grep app.Path` (excluding `FileSystem.Path` / `Path.Combine` / `Path.Get*`) across PLang/, PLang.Tests/, Tests/ → 0
- C# 2755/2755; PLang 199/199; build clean (warnings down to 68 from
  the inherited-Data-surface noise — bonus).

## Caller note

Brief enumerated 6 inherited Data members to verify zero use of: all
returned empty as documented. The build catches anything missed.
