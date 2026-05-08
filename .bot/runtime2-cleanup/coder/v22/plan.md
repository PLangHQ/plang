# Stage 22 — coder plan (`app-shortcuts-drop`)

Delete `app.Context` and `app.Variables` shortcut properties on App.this.cs.
Sweep callers to be explicit about which actor's Context/Variables they need.

## Files

- `App/this.cs` — delete:
  ```csharp
  public Actor.Context.@this Context => CurrentActor.Context;
  public Variables.@this Variables => Context.Variables;
  ```
  `CurrentActor` itself stays — used internally during boot.

- Production sweep:
  - `App/Actor/this.cs:137` (MyIdentity lambda) → `app.System.Context`.
  - `App/Errors/Error.cs:265` (fallback context) → `app.System.Context`.
  - **Brief missed 4 internal sites** (bare unqualified `Context` / `Variables` inside App's own partials and adjacent subsystems):
    - `App/Errors/this.cs:75` `_app.Variables` → `_app.CurrentActor.Context.Variables` (preserves "current actor's variables" semantic for diff-stream subscription during error scope).
    - `App/Debug/this.cs:228` `_engine.Context.Events` → `_engine.CurrentActor.Context.Events`.
    - `App/Variables/this.Snapshot.cs:39` `ctx.App.Variables` → `ctx.Variables` (the Context's own Variables — no need to round-trip through App).
    - `App/this.Snapshot.cs:19, 41` bare `Variables` / `Context` (resolved to the now-deleted properties) → `CurrentActor.Context.Variables` / `CurrentActor.Context`.
    - `App/modules/Events.cs:30` `Context.App?.Context` → `Context.App?.CurrentActor.Context`.

- Test sweep (~70 files): `engine.Variables` / `engine.Context` / `_app.Variables` / `_app.Context` / `app.Variables` / `app.Context` / `engine2.X` / `src.X` / `dst.X` → `*.User.Context.Variables` / `*.User.Context` per the brief's heuristic (User is the dominant test actor).

  Two extra files needed beyond the regex (manual cleanup): `DataAsTResolutionTests.cs` and `DataResolutionTests.cs` had `app2.Context` / `subApp.Context` that the broad pattern didn't catch.

## Verification

- `grep -rn "\bapp\.Variables\b\|\bapp\.Context\b" PLang/ --include='*.cs'` → 0 (only `app.Variables.Navigators` matches, the structural property).
- `grep -rn "\bengine\.Variables\b\|\bengine\.Context\b" PLang.Tests/` → 0.
- C# 2752/2752; PLang 199/199; build clean.

## Notes

- `CurrentActor.Context` preserves the original semantic where the brief was prescriptive about System (Errors, Identity). For internal subsystem code that genuinely meant "current actor", explicit `CurrentActor.Context` is the explicit form — same fragility under parallel multi-Context, but no longer dressed up as App-level.
- The brief flagged "default to User" for tests; my regex did exactly that.
