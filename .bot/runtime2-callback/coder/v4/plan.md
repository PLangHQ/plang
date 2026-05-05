# Coder v4 — Stage 4: Callback Records and Verbs

Implements architect's `stage-4-callback-records-and-verbs.md`. Builds on Stages 1-3.

## Stage 4 deliverables

| Architect deliverable | File | Notes |
|---|---|---|
| Expand `ICallback` interface | `PLang/App/Callback/ICallback.cs` | Adds `Call.@this Position { get; }` (or `RestoredFrame`), `byte[] Serialize(Context.@this ctx)`, `Task<Data> Run(Context.@this ctx)`. |
| `AskCallback` record | `PLang/App/Callback/AskCallback.cs` | Position + Actor + Variables (List<Data>) + Serialize/Deserialize/Run. |
| `ErrorCallback` record | `PLang/App/Callback/ErrorCallback.cs` | Snapshot App field; Position reads `App.CallStack.BottomFrame` (translated); Serialize/Deserialize/Run. |
| `Error.@this.Callback` lazy property | `PLang/App/Errors/Error.cs` | Calls `app.Snapshot()`, wraps in ErrorCallback, returns `Data<ErrorCallback>`. Cached per Error instance. |
| `callback.run` action | `PLang/App/modules/callback/run.cs` | Takes Data, verifies via signing.verify (skips when no signature), dispatches. |
| `crypto.encrypt` / `crypto.decrypt` actions | `PLang/App/modules/crypto/encrypt.cs` + `decrypt.cs` | v1 identity pass-through. Async signature even though body is sync return. |
| C# tests | All [S4] stubs | ~37 tests across CallbackTests/, Errors/ErrorCallbackPropertyTests, Modules/crypto/CryptoV1PassThroughTests, FailureMatrixTests. |
| `output.ask` `Variables` field | OUT OF SCOPE for this branch (per Ingi). Skip the PLang `.test.goal` AskWith*Vars stubs (they were already stale). |

## Type design

```csharp
public interface ICallback
{
    RestoredFrame? Position { get; }
    byte[] Serialize(Context.@this ctx);
    Task<Data> Run(Context.@this ctx);
}

public sealed record AskCallback(
    RestoredFrame Position,
    string ActorName,                   // "User" / "Service" / "System" — name not ref
    List<Data> Variables                // surviving vars per `vars:` annotation
) : ICallback
{
    public byte[] Serialize(Context.@this ctx) { ... }
    public static AskCallback Deserialize(byte[] bytes, Context.@this ctx) { ... }
    public async Task<Data> Run(Context.@this ctx) { ... }
}

public sealed record ErrorCallback(Snapshot App) : ICallback
{
    public RestoredFrame? Position
    {
        get
        {
            // Walk the captured CallStack frames to materialize BottomFrame.
            // Stage 2's RestoredFrame is the surrogate.
            ...
        }
    }
    public byte[] Serialize(Context.@this ctx) { ... }
    public static ErrorCallback Deserialize(byte[] bytes, Context.@this ctx) { ... }
    public async Task<Data> Run(Context.@this ctx) { ... }
}
```

## crypto v1 pass-through

```csharp
[Action("encrypt", Cacheable = false)]
public partial class encrypt : IContext
{
    [IsNotNull] public partial Data.@this<byte[]> Input { get; init; }
    public Task<Data.@this> Run() => Task.FromResult(Data.@this.Ok(Input.Value));
}
// Same for decrypt.
```

Both signatures are async (Task<Data>) per architect's note — real impl will be async.

## Error.Callback property

`Error` is a class (App.Errors.Error). Adding a lazy property requires (a) a backing field that survives test fixture creation and (b) ambient access to `app`. Errors don't carry an App ref today; pragmatic path is via `App.Errors.@this` (which has `App` from Stage 2). The property lives on Error itself but resolves App through a setter or via Errors.Push.

Cleanest route: add `internal App? App { get; set; }` on Error, populated by `Errors.Push(error)` (the natural materialization site). Then `error.Callback` triggers `App.Snapshot()` lazily.

Alternative: extension method `error.MaterializeCallback(app)` — explicit but tests get verbose. I'll go with the App back-ref approach.

## callback.run dispatch

```csharp
[Action("run", Cacheable = false)]
public partial class run : IContext
{
    [IsNotNull] public partial Data.@this<ICallback> Callback { get; init; }
    public async Task<Data.@this> Run()
    {
        // Verify signature if present; skip when missing (test for type error path).
        if (Callback.RawSignature != null)
        {
            var verifyResult = await Context.App.RunAction<verify>(
                new verify { Data = Callback }, Context);
            if (!verifyResult.Success)
                return Data.@this.FromError(new CallbackSignatureMismatch(verifyResult.Error!.Message));
        }

        if (Callback.Value is not ICallback cb)
            return Data.@this.FromError(new ServiceError("- run %x% requires an ICallback value", "TypeError", 400));

        return await cb.Run(Context);
    }
}
```

## Workflow

1. Expand ICallback interface.
2. Write AskCallback + ErrorCallback records.
3. Wire Error.Callback lazy property + Errors.Push to set App ref.
4. crypto.encrypt + crypto.decrypt v1 actions.
5. callback.run action.
6. Fill all [S4] test bodies.
7. Verify all prior stages still green; PLang tests no regressions.
8. Commit + update reports.
