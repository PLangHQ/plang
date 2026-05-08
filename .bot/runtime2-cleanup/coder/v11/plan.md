# Stage 11 — coder plan (`errors-app-backref-drop`)

## What

Eliminate the post-construction injection `Errors.App = this;` at App.this.cs:297.
`Errors.@this` takes App via constructor; the back-ref becomes `private readonly`.

## Files

- `PLang/App/Errors/this.cs`:
  - Drop `internal App.@this? App { get; set; }`.
  - Add `private readonly App.@this _app;` + `public @this(App.@this app) { _app = app; }`.
  - Push: `App?.CallStack` → `_app.CallStack`; `App!.Variables` → `_app.Variables`; `e.App = App` → `e.App = _app`. The `if (stack != null)` guard becomes unconditional (stack is non-null now).
- `PLang/App/this.cs`:
  - `Errors { get; } = new();` → `Errors { get; }` + ctor allocation `Errors = new global::App.Errors.@this(this);` replacing line 297.
- `PLang.Tests/App/Errors/ErrorsScopeTests.cs` — 7 sites: `new global::App.Errors.@this()` → `await using var app = new global::App.@this("/test"); var errors = app.Errors;`. Real App instance each test; auto-disposed.

## Verification

- `grep -n "Errors\.App\s*=" PLang/App/this.cs` → 0
- `grep -n "internal App\.@this\? App" PLang/App/Errors/this.cs` → 0
- C# 2755/2755; PLang 199/199.
