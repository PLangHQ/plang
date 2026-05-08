# Stage 4 — coder plan (`dispose-self-owns`)

## What

`Modules.@this` and `Providers.@this` implement `IAsyncDisposable` and own
their own dispose iteration. App.DisposeAsync stops peeking into
`_modules.All` and `Providers.All()`.

## Files

- `PLang/App/Modules/this.cs`:
  - `class @this` → `class @this : IAsyncDisposable`.
  - Add `_disposed` bool.
  - Add `DisposeAsync()` that iterates `_modules.Values.SelectMany(a => a.Values).Where(e => e.Instance != null)` (same projection as `All`) and disposes each handler.
- `PLang/App/Providers/this.cs`:
  - `partial class @this` → `partial class @this : IAsyncDisposable`.
  - Add `_disposed` bool.
  - Add `DisposeAsync()` iterating `_providers.Values.SelectMany(p => p.Values)` (same projection as `All()`).
- `PLang/App/this.cs` — DisposeAsync: replace the two foreach blocks with `await _modules.DisposeAsync(); await Providers.DisposeAsync();`. Final order matches the brief.

## Verification

- `grep -n "_modules\.All\|Providers\.All()" PLang/App/this.cs` → 0
- C#: 2755/2755 pass.
- PLang: 199/199 pass.

## Note

`Modules.All` / `Providers.All()` retained as public surface — out of scope
to remove dead readers.
