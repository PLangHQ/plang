# Stage 10 — coder plan (`app-run-redesign`)

## What

Cut `App.Run` from 85 lines to ~10 by extracting two abstractions:

1. **`Context.AnchorScope(action)`** — IDisposable that captures
   `Step`/`Goal`/`Event`/`action.Step.Context` on construction, sets them
   to the action's, restores on Dispose.
2. **`Call.ExecuteAsync(handler, context)`** — wraps
   `handler.ExecuteAsync(action, context)` with error stamping
   (`SnapshotParams`, `CallFrames`), `Errors.Add`, `Audit.Add`, and OCE
   swallowing into a `ServiceError`. Reads `this.Action`, `this._stack`,
   `this.Errors`, `this.SnapshotChain()` — no extra parameters.

App.Run becomes: get handler → push call (with overflow catch) →
AnchorScope → `call.ExecuteAsync(handler, context)`.

## Files

- `PLang/App/Actor/Context/this.cs` — add `AnchorScope(Action action)` method + a private nested `AnchorScopeDisposable` struct.
- `PLang/App/CallStack/Call/this.cs` — add `ExecuteAsync(ICodeGenerated handler, Actor.Context.@this context)` method.
- `PLang/App/this.cs` — rewrite `App.Run`. Add private `HandleOverflow(...)` helper for the CallStackOverflowException path (overflow is at Push-time, before the Call frame exists).

## Behaviour preserved precisely

1. CallStackOverflowException catch tight to `Push` only (overflow happens before the Call frame exists, so HandleOverflow stays in App.Run).
2. OperationCanceledException swallowed into `ServiceError` *inside* `Call.ExecuteAsync` only — App.Run's outer flow doesn't catch OCE.
3. `Params` stamped before `CallFrames` (existing order preserved).
4. Dispose order: `using var anchor = ...; await using var disposable = call;` — but actually the original puts `await using var _ = call;` first, then sets anchors in a try/finally; the finally restores anchors *before* the `await using` runs. Replicating: declare `await using var _ = call;` *before* `using var _anchor = context.AnchorScope(action);` so reverse-order disposal restores anchors first, then disposes Call.
5. `action.Step.Context = context` swap (the "shared Step instance under parallel dispatch" guard) replicated in AnchorScope.

## Verification

- `dotnet build PlangConsole` clean.
- C# 2755/2755; PLang 199/199.
- App.Run is ≤ ~15 lines (target ~10) including the small HandleOverflow helper.
