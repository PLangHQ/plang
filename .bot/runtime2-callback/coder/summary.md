# coder summary — runtime2-callback

## Version
v4 — Stage 4 (Callback Records and Verbs) — **branch complete**

## What this is

The keystone stage. Lands `ICallback` + `AskCallback` + `ErrorCallback` + their `Serialize`/`Deserialize`/`Run` bodies; pins `%!error.callback%` materialisation on `Error.Callback`; ships the `callback.run` action and `crypto.encrypt`/`crypto.decrypt` v1 pass-through. After this stage all 97 test-designer C# stubs are green.

## What was done across all four stages

| Stage | Lands | Tests flipped (cumulative) |
|---|---|---|
| **v1 — S1** | `ISnapshotted`, `Snapshot.@this`, `App.Snapshot()/Restore()`, six subsystem captures (Variables, Errors.Trail, Providers, Statics, Build, Testing). IProvider gains `IsBuiltIn`/`Source`. `Trail.Add()` rejects on frozen. | 17 (97 → 80) |
| **v2 — S2** | `Call.@this` Capture, `App.CallStack` Capture/Restore, `RestoredFrame` surrogate, `BottomFrame`, `EventsSince(t)`, `Variables.SnapshotAt(error)`, `Errors.Push` auto-flips Flags.Diff and wires CallStack-level diff stream. Hard errors `CallbackGoalNotFound` / `CallbackGoalHashMismatch`. | 21 (80 → 59) |
| **v3 — S3** | `SignedData → Signature` rename. Lazy `Data.Signature` (only auto-populates for ICallback values; `EnsureSigned()` is the explicit hook for non-callback wire writes). `RawSignature` peek accessor. `app.Callback` config + `app.Callback.Signature.ExpiresInMs`. `ICallback` marker. `PlangDataSerializer` for `application/plang+data`. `GetByMimeType` with `UnregisteredMimeType` hard error. | 22 (59 → 37) |
| **v4 — S4** | `ICallback` interface (Position + Serialize + Run). `AskCallback`. `ErrorCallback`. `Error.Callback` lazy property cached per Error instance. `callback.run` action. `crypto.encrypt`/`crypto.decrypt` v1 identity pass-through. | 37 (37 → 0) — **all green** |

C# tests: 2720 / 2720 passing.
PLang tests: 192 / 181 pass / 0 fail / 11 stale (callback `.test.goal` stubs awaiting builder support for `output.ask vars:`).

## Stage 4 specifics

- **`AskCallback`** — Position + ActorName + List<Data> Variables. Serialize JSON-encodes a Wire shape (positional triple = goalPrPath + goalHash + step/action indices), then pipes through `crypto.encrypt` (v1 identity). Deserialize reverses, resolving Position via `app.Goals.Get(prPath)` with hash-match. Hard-errors with `CallbackGoalNotFound` / `CallbackGoalHashMismatch` per Stage 2's contract. Run binds the surviving variables onto the resumed Variables and dispatches the original Action through `app.Run` (which Pushes a fresh live Call).
- **`ErrorCallback`** wraps a `Snapshot.@this`. Serialize JSON-encodes the captured CallStack frames + Variables sections. Deserialize reverses. Run calls `ctx.App.Restore(snapshot, ctx)` then re-executes the action at `BottomFrame` through `app.Run`.
- **`Error.Callback`** lazy property: requires `Errors.Push(error)` to have run (which sets the App back-ref). Caches per Error instance; reading twice returns the same `Data<ErrorCallback>`. Two distinct errors get two distinct callbacks.
- **`callback.run`** action: takes `Data` whose `.Value` must be `ICallback`. If `RawSignature != null` invokes `signing.verify`; failure produces a `CallbackSignatureMismatch`-keyed ServiceError. Otherwise dispatches into `cb.Run(ctx)` and returns its Data.
- **`crypto.encrypt`/`crypto.decrypt`**: identity pass-through with async signature so the wiring (Callback's Serialize/Deserialize) is real and the v2 swap is just a body change.

## Code example

```csharp
public sealed class AskCallback : ICallback
{
    public RestoredFrame? Position { get; init; }
    public string ActorName { get; init; } = "User";
    public List<Data> Variables { get; init; } = new();

    public byte[] Serialize(Context ctx)
    {
        var wire = new Wire { /* position triple, actor, vars */ };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(wire, _options);
        var encrypted = ctx.App.RunAction<encrypt>(...).GetAwaiter().GetResult();
        return (byte[])encrypted.Value;
    }

    public async Task<Data> Run(Context ctx)
    {
        foreach (var v in Variables) ctx.Variables.Set(v.Name, v.Value);
        return await ctx.App.Run(Position!.Action, ctx);
    }
}
```

## Stage-1-3 integrity preserved across the branch

A couple of regressions surfaced and were fixed during the cascade:
- Stage 3's lazy `Data.Signature` getter initially read `Value` (which forced `DynamicData`'s lazy factory and tripped sandbox FS). Fixed to read `_value` (the raw field).
- Test fixture provider DLLs (TestProvider, NoCtorProvider) were stale after Stage 1's `IsBuiltIn`/`Source` interface additions — rebuilt and re-staged.
- Variables.Set fires `OnCreate` for first-time names (not `OnSet`); both diff capture paths (per-Call and CallStack-level stream) now subscribe to both events.

## What's NOT in this branch

- **Builder support for `output.ask` `vars:` annotation** — out of branch per Ingi. The 11 stale Plang `.test.goal` stubs wait on this builder work.
- **Real symmetric crypto** — `crypto.encrypt`/`decrypt` are identity pass-through. Tracked in `Documentation/Runtime2/todos.md`.
- **HTTP wire transport for ask-user** — separate work item per architect.
- **Full Snapshot fidelity through `ErrorCallback.Serialize`** — current wire shape carries CallStack frames + Variables. Richer subsystems (Errors.Trail entries, Providers registrations, Statics bags) round-trip in-process via `App.Snapshot()/Restore()` but aren't yet emitted to the JSON wire. Adequate for the [S4] tests; production needs a subsequent pass.

## Final state

All code committed in 4 stage commits (no PR per Ingi):
- f93dcdd3 — Stage 1
- 88bdfcc6 — Stage 2
- 02b18bd7 — Stage 3
- (Stage 4) — this commit

Suggested next bot: **codeanalyzer** to review OBP compliance + simplification opportunities.
