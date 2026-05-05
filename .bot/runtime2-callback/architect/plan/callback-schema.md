# `ICallback` — `AskCallback` and `ErrorCallback`

The wire/storage shape is a signed `Data<ICallback>` envelope — single blob, one signature covering the whole (encrypted) content. Tampering breaks the signature; the envelope is rejected wholesale. `Data` is the universal envelope type; `ICallback` is the interface for the payload.

## Why two records, not one

Ask-user and error-retry are more different than alike. Ask is a clean pause: the developer declared which variables survive (`vars:` annotation), there's no failure to reconstruct, no error trail, no provider/static state to reconcile. Error-retry is mid-flight failure recovery: it must reconstruct enough of the failed App to faithfully re-run the action.

Forcing a common payload shape would mean either (a) optional fields everywhere with no type-level meaning, or (b) one slim shape that loses what error-retry actually needs. Two records, one tiny interface, is the cleaner cut.

## The interface

```csharp
public interface ICallback
{
    Call        Position { get; }    // resume frame — same shape both records use
    byte[]      Serialize(Context ctx);
    Task<Data>  Run(Context ctx);
}
```

`Position` is a `Call` frame — the same `@this` the runtime already uses for "where execution is right now." Every Call carries its `(Goal, StepIndex, ActionIndex, …)`. By exposing `Position` on the interface, both records use the same structural primitive for "where this callback resumes" — no flat `(Goal, StepIndex, ActionIndex)` triple anywhere in the design.

`Serialize` is the Channels-side contract: "give me your bytes." `Run` is the resume verb — `- run %callback%` in PLang dispatches into it and returns whatever the resumed action would have returned, so the developer can chain on the result like any other step. `Run` returning `Task<Data>` is how every action in PLang surfaces its result — Callback is no exception.

The bodies of `Serialize` and `Run` look nothing alike between the two records — but the *intent* is shared, so they live on the interface.

Each impl exposes a static `Deserialize(byte[] bytes, Context ctx)` factory. Static factories aren't on the interface (static-on-interface dispatch is more friction than it's worth); Channels dispatches by typed envelope — `Data<AskCallback>` and `Data<ErrorCallback>` are distinct types, the deserializer picks the right factory.

## `AskCallback`

```csharp
public sealed record AskCallback(
    Call Position,                          // active frame at the moment of ask
    Actor Actor,                            // User / Service / System — the ask target
    Dictionary<string, object?> Variables   // names listed in `vars:`
) : ICallback;
```

Slim and explicit. The fields *are* what the callback is — where the ask happened (the Call frame), who was asked, which variables travel. No App snapshot: there's no failed App to reconstruct, the developer pinned the surviving names with `vars:`, post-resume they reload anything else (`%order%` from `%orderId%`).

`Position` is a single `Call` frame, not a chain — the ask doesn't need to unwind through outer Calls; it's a clean resumption at one point. That's the structural difference from ErrorCallback, which captures the whole call chain so it can unwind correctly.

Example of the contract from a developer's view:

```plang
/ some code before where we loaded stuff such as %order% and used it; we
/ then see we're missing the user's name
- ask user "what is your name?", vars: %order.id%, write to %name%
/ now because this is stateless, the developer knows they must re-fetch
/ the order from the db — they sent %order.id% as a var with the ask, so
/ it gets automatically loaded when the user responds
- select * from orders where id=%order.id%, return 1, write to %order%
```

The `vars:` annotation is the contract: only `%order.id%` survives the wire round-trip. `%order%` does not — the developer reloads it on resume from the surviving id. This is the asymmetry the slim schema enforces: explicit-ask, explicit-reload.

`AskCallback.Serialize(ctx)` walks these fields, calls `crypto.encrypt` on the payload bytes (see [encryption-layering.md](encryption-layering.md)), returns the encrypted bytes.

`AskCallback.Run(ctx)` verifies the envelope signature, decrypts, resolves `Position.Goal` against the live App's goal registry, binds `Variables` into the resumed App, dispatches into the ask action at `Position.StepIndex/ActionIndex` with `Actor` as actor context — the action returns the bound value instead of issuing a fresh ask.

## `ErrorCallback`

```csharp
public sealed record ErrorCallback(
    Snapshot App                            // full app.Snapshot() — see below
) : ICallback;

// ICallback.Position is implemented as: App.CallStack.BottomFrame
```

A single field. Everything error-retry needs is *already* somewhere in the App tree; ErrorCallback just captures that tree. No flat field names, no glossary, no schema for the test-designer to memorize — the wire shape mirrors the App.

The `Position` accessor on `ICallback` is satisfied by reading the bottom-most frame from the captured CallStack — the position is in the same place a live App's position would be.

### What's inside `App`

`Snapshot` is the type returned by `app.Snapshot()` — a recursive walk of the App's `@this` properties, asking each one that implements `ISnapshotted` for its own snapshot. `Snapshot` is the same payload type that `ISnapshotted.Capture` writes to and `ISnapshotted.Restore` reads from — there's no separate `AppSnapshot` type. One concept, one type. The shape mirrors the App tree:

```
App
├── CallStack         → list of Call frames; bottom frame = resume point (Goal, StepIndex, ActionIndex, …)
├── Variables         → name → value (throw-time view, diff reverse-applied at materialization time)
├── Errors            → Trail
├── Providers         → default selections per type + runtime registrations (names, not instances)
├── Build             → IsEnabled
├── Testing           → IsEnabled
├── _statics          → name → value (provisional, until that TODO closes)
└── …
```

Each `@this` decides what it captures. `App.Modules`, `App.Channels`, `App.Cache`, `App.Goals`, `App.Events`, etc. either reconstruct-on-build (return nothing, or a marker) or drop entirely (live network/IO state). The three buckets — snapshot-and-restore / reconstruct-on-build / drop — are documented in [snapshotted-system.md](snapshotted-system.md).

### Position lives in `App.CallStack`

The captured CallStack is a list of `Call` frames. The **bottom-most frame** is the resume point — that's where the throwing action was. Outer frames are the Caller chain, used so unwinding after the resumed action completes returns control through the right outer Calls.

`Call` is itself an `@this` and owns its own snapshot/restore: emits the Goal as a stub (`{ PrPath, Hash }`), restores it by resolving the path against the live registry. Goal-hash mismatch on restore = hard error, same as before.

### `ErrorCallback.Serialize(ctx)`

1. Build flat byte representation of `App` (the snapshot tree).
2. Pipe through `crypto.encrypt`.
3. Return encrypted bytes.

### `ErrorCallback.Run(ctx)`

1. Verify envelope signature (`signing.verify`).
2. Decrypt payload (`crypto.decrypt`).
3. Construct a fresh `App` (RegisterDefaults runs, modules and channels boot normally).
4. `app.Restore(snapshot, ctx)` — walks subtrees and dispatches to each `@this`: `App.Variables.Restore`, `App.Errors.Restore`, `App.Providers.Restore` (replays runtime registrations, then applies default selections — hard error if a name doesn't resolve), `App.Build.Restore`, `App.Testing.Restore`, `App._statics.Restore`, `App.CallStack.Restore`.
5. `Position` (the bottom frame of the restored CallStack) is the resume point. The main loop's first tick lands at `(frame.Goal, frame.StepIndex, frame.ActionIndex)` and re-executes the failed action against the bound state.

## Signing config: `app.Callback.Signature.ExpiresInMs`

Signing is `Data.@this`'s own concern (see [transparent-signing.md](transparent-signing.md)). The *config* for how callback envelopes get signed lives in the App's config tree:

```
app.Callback.Signature.ExpiresInMs   (default null)
```

This reads as: the App has a `Callback` config `@this`; that config has a `Signature` sub-config; on which `ExpiresInMs` is the value. PLang's app-config tree is just runtime classes' properties addressed by dot-path. `app.Callback` is *not* an `ICallback` instance — it's a config holder.

```plang
- set callback timeout to 5 minutes
```

writes `app.Callback.Signature.ExpiresInMs = 300000`. When `Data.@this`'s lazy `Signature` property first populates, it reads this config from `Data.Context.App.Callback.Signature.ExpiresInMs` and seeds `Data.Signature.Expires = now + N` (or leaves it null).

One source of truth: the app config is the *config*; `Data.Signature.Expires` is the *carrier* on the wire. Two distinct things share the word *Signature* — `Data.Signature` is the wire envelope; `app.Callback.Signature` is the config holder.

Future Callback configs (size limits, encryption mode) all live as properties on `app.Callback`, no parallel registry.

## Lazy materialization of `%!error.callback%`

`%!error.callback%` is a property on the current error — `app.Errors.Current.Callback`. Reading it triggers materialization:

1. The runtime calls `app.Snapshot()` — recursive walk of all `ISnapshotted` `@this`.
2. `App.Variables.SnapshotAt(error)` applies the diff reverse-apply to produce the throw-time view (see [variable-capture.md](variable-capture.md)).
3. `App.CallStack.Snapshot()` walks the live frames up to the throw point and emits Call frames.
4. Result is wrapped in `ErrorCallback(snapshot)` and then `Data<ErrorCallback>`. `Data.Signature` is *not* populated yet — it's a lazy property; first access at serialize time triggers signing.

The materialization is a pure function of `(error, current state)` — see [variable-capture.md](variable-capture.md#idempotent-materialization). `Error.@this` owns the `Callback` property; the property *is* the lazy materialization. No external "synthetic property handler" floating in the runtime.

## Lazy materialization for ask-user

`AskCallback` is built when `- ask <actor> ...` runs and the actor isn't immediately answering (e.g. async/HTTP path):

1. `Position` from the active Call (the ask action's own frame).
2. `Actor` from the action's actor parameter.
3. `Variables` from the explicit `vars:` annotation — no implicit capture.
4. Wrap in `Data<AskCallback>`.

No App snapshot, no full state walk.
