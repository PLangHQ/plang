# Stage 4: Callback Records and Verbs

**Goal:** Land `ICallback` + `AskCallback` + `ErrorCallback` with their `Serialize`/`Deserialize`/`Run` bodies; pin `%!error.callback%` materialization on `Error.@this.Callback`; ship the `callback.run` action and `crypto.encrypt`/`crypto.decrypt` v1 pass-through actions. The keystone stage — once this lands, the test-strategy's two test cuts (in-process resume + durability round-trip) pass.
**Scope:** *Included* — `ICallback` interface, both record types, both `Run` bodies, both `Serialize`/`Deserialize` bodies, `Error.@this.Callback` lazy property, `callback.run` PLang action, `crypto.encrypt`/`crypto.decrypt` v1 pass-through actions, `app.Callback` config holder. *Excluded* — real symmetric crypto (TODO entry exists in `Documentation/Runtime2/todos.md`), HTTP wire transport for ask-user (separate work), file-sidecar shape, builder support for the `vars:` annotation on `output.ask` (coder confirms with builder).
**Deliverables:**
- `ICallback` interface: `Call Position { get; }`, `byte[] Serialize(Context ctx)`, `Task<Data> Run(Context ctx)`.
- `AskCallback` record: `Position, Actor, Variables` + `Serialize`/`Deserialize`/`Run`.
- `ErrorCallback` record: `Snapshot App` field; `Position` accessor reads `App.CallStack` bottom frame; `Serialize`/`Deserialize`/`Run`.
- `Error.@this.Callback` lazy property — calls `app.Snapshot()` (which uses `app.Variables.SnapshotAt(this)`), wraps in `ErrorCallback`, returns `Data<ErrorCallback>`. PLang path `%!error.callback%` resolves through `app.Errors.Current.Callback` to this property.
- `callback.run` action handler — takes `%callback%`, dispatches into `callback.Run(ctx)`, returns the resulting `Data`.
- `crypto.encrypt(byte[]) → byte[]` and `crypto.decrypt(byte[]) → byte[]` actions. v1 bodies return input unchanged. Both wired into the existing `crypto` module.
- `app.Callback` config `@this` with a `Signature` sub-config that has `ExpiresInMs` (default `null`). PLang `- set callback timeout to 5 minutes` writes `app.Callback.Signature.ExpiresInMs = 300000`.
- C# tests for in-process `ErrorCallback.Run` and `AskCallback.Run`; PLang tests matching the test-strategy's two cuts (test-designer authors these — coder runs and lands them).
**Dependencies:** Stages 1, 2, and 3 all landed.

## Design

This stage assembles the pieces from earlier stages — Snapshot foundation, CallStack/Variables time-travel, Data lazy signing — into the developer-visible callback mechanism. The novel work here is the two record types' `Run` orchestration and the lazy materialization site on `Error`.

### `ICallback` interface

```csharp
public interface ICallback
{
    Call.@this   Position { get; }
    byte[]       Serialize(Context.@this ctx);
    Task<Data>   Run(Context.@this ctx);
}
```

`Position` is a `Call.@this` frame — same structural primitive both records use. AskCallback satisfies it directly (it has a `Position` field). ErrorCallback satisfies it by reading the bottom-most frame of its captured CallStack.

`Run` returns `Task<Data>` — every PLang action surfaces a `Data` result; the resumed action's value flows back through `Run`'s return so the caller can chain on it.

`Serialize` returns `byte[]` — the encrypted, ready-to-wire payload. The Channels Data serializer (Stage 3's `PlangDataSerializer`) calls this when writing.

Each impl exposes a static `Deserialize(byte[] bytes, Context.@this ctx)` factory. Static factories aren't on the interface (static-on-interface dispatch is more friction than it's worth); typed envelope dispatch — `Data<AskCallback>` vs `Data<ErrorCallback>` — picks the right factory.

### `AskCallback`

```csharp
public sealed record AskCallback(
    Call.@this Position,
    Actor Actor,
    Dictionary<string, object?> Variables
) : ICallback
{
    public byte[] Serialize(Context.@this ctx)
    {
        var bytes = SerializeFields();   // implementation choice — CBOR, JSON, custom
        return ctx.App.Modules.Get("crypto").EncryptAsync(bytes).Result;
    }

    public static AskCallback Deserialize(byte[] bytes, Context.@this ctx)
    {
        var plain = ctx.App.Modules.Get("crypto").DecryptAsync(bytes).Result;
        return DeserializeFields(plain);
    }

    public async Task<Data> Run(Context.@this ctx)
    {
        // Signature was verified by the consumer of Data<AskCallback> already
        // (callback.run action calls signing.verify before dispatching here);
        // payload was decrypted by Deserialize.

        // Resolve Position.Goal stub against live registry (Call.Restore-style hash-match)
        var liveGoal = ResolveGoal(ctx, Position.Goal);   // hard error on mismatch

        // Bind the surviving variables into the resumed App's Variables
        BindVariables(ctx, Variables);

        // Dispatch the ask action at Position.StepIndex/ActionIndex with Actor context.
        // The action returns the bound value instead of issuing a fresh ask.
        return await DispatchAskWithBoundValue(ctx, liveGoal, Position, Actor, Variables);
    }
}
```

Slim and explicit. No App snapshot, no full state walk. The developer's `vars:` annotation pinned what survives; everything else is fresh App boot.

### `ErrorCallback`

```csharp
public sealed record ErrorCallback(
    Snapshot.@this App
) : ICallback
{
    public Call.@this Position => App.CallStack.BottomFrame;

    public byte[] Serialize(Context.@this ctx)
    {
        var bytes = SerializeSnapshot(App);
        return ctx.App.Modules.Get("crypto").EncryptAsync(bytes).Result;
    }

    public static ErrorCallback Deserialize(byte[] bytes, Context.@this ctx)
    {
        var plain = ctx.App.Modules.Get("crypto").DecryptAsync(bytes).Result;
        var snap = DeserializeSnapshot(plain);
        return new ErrorCallback(snap);
    }

    public async Task<Data> Run(Context.@this ctx)
    {
        // Construct fresh App (RegisterDefaults runs, modules + channels boot normally)
        var freshApp = new App.@this(...);

        // Walk subtrees and dispatch Restore on each @this:
        //   App.Variables.Restore, App.Errors.Restore, App.Providers.Restore (hard error
        //   on unresolvable name), App._statics.Restore, App.Build.Restore,
        //   App.Testing.Restore, App.CallStack.Restore (hard error on Goal-hash mismatch
        //   for any frame).
        freshApp.Restore(App, ctx);

        // Position is the bottom frame of the restored CallStack — the engine starts
        // its main loop's first tick there.
        return await freshApp.RunFrom(freshApp.CallStack.BottomFrame);
    }
}
```

A single field. Everything error-retry needs is somewhere in the App tree; ErrorCallback just captures and replays that tree.

### `Error.@this.Callback` lazy property

This is the home for `%!error.callback%`. The PLang path resolves through `app.Errors.Current.Callback` — i.e., the *current error has a callback property*; reading it triggers materialization.

```csharp
// On Error.@this
private Data<ErrorCallback>? _callback;
public Data<ErrorCallback> Callback
{
    get
    {
        if (_callback is null)
        {
            // Note: this body lives on Error but reads through to App; the Error type
            // needs an ambient App reference (likely through Context, same way Data does).
            var snap = _ctx.App.Snapshot();   // walks ISnapshotted properties incl.
                                              // app.Variables.SnapshotAt(this) for throw-time view
            var cb = new ErrorCallback(snap);
            _callback = new Data<ErrorCallback>(cb, _ctx);
        }
        return _callback;
    }
}
```

The materialization is a pure function of `(error, current state)` — see `plan/variable-capture.md`. Caching is fine because the error's identity is immutable once thrown, and changes to current state would be a developer mutating between reads (pathological, not the design target).

`Data.Signature` on the returned envelope is *not* yet populated. It's a lazy property on Data (Stage 3) — first read by a serializer triggers signing. So `%!error.callback%` is pure-cost-free until the developer writes it through a channel.

### `callback.run` action

Thin shim. PLang's `- run %callback%` compiles to dispatching this action.

```csharp
public class CallbackRunHandler
{
    public async Task<Data> Run(Data<ICallback> callback, Context.@this ctx)
    {
        // Verify the envelope signature (callers don't auto-verify on Data read).
        var verified = await ctx.App.Modules.Get("signing").VerifyAsync(callback);
        if (!verified) throw new CallbackSignatureMismatch(callback);

        // Hand off to the typed Run.
        return await callback.Value.Run(ctx);
    }
}
```

The signing.verify is here (in the action handler), not inside `ICallback.Run`. Run's contract assumes the signature has been verified; the action handler is the gate.

### `crypto.encrypt` / `crypto.decrypt` v1 pass-through

Add two actions to the existing `crypto` module:

```csharp
public class CryptoEncryptHandler
{
    public Task<byte[]> Encrypt(byte[] input, Context.@this ctx) => Task.FromResult(input);
}

public class CryptoDecryptHandler
{
    public Task<byte[]> Decrypt(byte[] input, Context.@this ctx) => Task.FromResult(input);
}
```

That's it for v1. The wiring is real — Callback's `Serialize`/`Deserialize` calls through them — so when the real implementation lands (tracked in `Documentation/Runtime2/todos.md`), only the action bodies change.

### `app.Callback` config holder

`app.Callback` is *not* an `ICallback` instance. It's a config `@this` on the App, exposing a `Signature` sub-config which has `ExpiresInMs` (default `null`).

```csharp
public class @this  // App.Callback
{
    public Signature.@this Signature { get; } = new();
}

// Inside Signature.@this:
public int? ExpiresInMs { get; set; }
```

PLang's `- set callback timeout to 5 minutes` writes `app.Callback.Signature.ExpiresInMs = 300000`. Stage 3's lazy `Data.Signature` getter reads this value when wrapping an `ICallback`.

### OBP smells to avoid

- *Don't expose `signing.Verify` as a step inside `ICallback.Run`.* Run assumes verified input; the gate is the action handler. Mixing them puts crypto orchestration inside Callback.
- *Don't add `Callback.From(error)` factory on the `ICallback` interface.* The materialization site is `Error.@this.Callback` — that's where the property lives. Don't duplicate.
- *Don't add a `CallbackBuilder` class.* If construction needs work, it's `Error.@this.Callback`'s body or a private helper inside that property — not a separate type.
- *Don't make `crypto.encrypt`/`crypto.decrypt` synchronous.* They're `Task<byte[]>` even though v1 returns immediately — the contract is async because the real impl will be (key access, hardware modules).

### Test shape

Two halves:

**C# unit tests:**

- `AskCallback.Run` happy path: build `AskCallback(position, actor, vars)`, call `Run(ctx)`, assert it dispatches into the ask action with the bound value.
- `AskCallback.Run` with goal-not-found: capture, delete goal, Run — expect referent-integrity error.
- `ErrorCallback.Run` happy path: capture from a thrown error, Run, assert resumed App's Variables match throw-time view, assert position lands at the bottom frame.
- `Error.@this.Callback` idempotency: read twice with no state changes, assert same Data instance returned (cache hit).

**PLang tests** (test-designer's territory):

- The two cuts in `plan/test-strategy.md` — in-process resume and durability round-trip. Test-designer translates into TUnit; coder lands them.

If both halves pass, Stage 4 closes and the branch is ready for review.
