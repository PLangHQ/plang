# Stage 3 — coder plan (`keepalive-collection`)

## What

`_keepAlive` list, `KeepAlive(x)`, `RemoveKeepAlive(x)`, and the dispose-loop
become a single `App.KeepAlive.@this` collection that owns the discipline.
App holds `KeepAlive { get; } = new();` and calls `await KeepAlive.DisposeAsync()`.

## Files

- **New**: `PLang/App/KeepAlive/this.cs` — sealed `@this : IAsyncDisposable`
  with `Add(object)`, `Remove(object)` (sync-dispose semantics preserved),
  `DisposeAsync()`, and `_disposed` guard.
- **Modify**: `PLang/App/this.cs`:
  - Delete `_keepAlive` field (line 24).
  - Delete `KeepAlive(object)` and `RemoveKeepAlive(object)` (lines 270, 275).
  - Add `KeepAlive { get; } = new();` property in their place.
  - Replace 7-line dispose-and-clear block with `await KeepAlive.DisposeAsync();`.

## Verification

- `grep -n "_keepAlive" PLang/App/` → 0
- `grep -n "public.*KeepAlive(\|RemoveKeepAlive" PLang/App/this.cs` → 0
- C#: 2755/2755 pass.
- PLang: 199/199 pass.

## Caller note

Zero external callers of `app.KeepAlive(x)` or `app.RemoveKeepAlive(x)` —
verified across PLang/, PLang.Tests/, Tests/. Safe to delete (not deprecate).
