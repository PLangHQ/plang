# Test coverage matrix

Companion to [test-strategy.md](test-strategy.md). Per-topic coverage, failure paths, and an inventory of new surfaces this branch introduces. Test-designer uses this to plan the test plan; coder uses it to know what's expected to pass before stage close.

Layer abbreviations:
- **C#** = C# TUnit test in `PLang.Tests/` (pins internal behavior on `@this` types)
- **goal** = PLang `.goal` test in `Tests/` (pins developer-facing surface)
- **integration** = one of the two integration cuts in [test-strategy.md](test-strategy.md)

## Coverage matrix

Organized by topic file. "Layer" says where the test lives; "Sense" is green-path or negative-path.

### `snapshotted-system.md`

| Behavior | Layer | Sense |
|---|---|---|
| `ISnapshotted.Capture` writes typed entries; `Restore` reads them in order | C# | green |
| `App.Snapshot()` walks `@this` properties and aggregates per-type captures into a `Snapshot.@this` tree | C# | green |
| `App.Restore(snap, ctx)` dispatches each subtree to the matching `@this.Restore` | C# | green |
| `App.Variables` round-trip — values survive, partition rules honored (skip `!`-prefix, DynamicData, SettingsVariable) | C# | green |
| `App.Errors.Trail` round-trip — Trail is read-only after restore | C# | green |
| `App.Providers` round-trip — default selections + runtime registrations both restored | C# | green |
| `App.Providers.Restore` two-step: registrations first, then default selections applied | C# | green |
| `App.Providers.Restore` raises hard error on unresolvable runtime registration source (DLL missing) | C# | negative |
| `App.Providers.Restore` raises hard error on unresolvable default-selection name (registration succeeded but name doesn't match) | C# | negative |
| `App._statics` round-trip | C# | green |
| `App.Build` round-trip — `IsEnabled` survives | C# | green |
| `App.Testing` round-trip — `IsEnabled` survives | C# | green |
| `App.Cache` is *not* in the snapshot — fresh App after Restore has empty cache | C# | green (asserting absence) |
| `App.Modules`/`App.Goals`/`App.Channels`/etc. reconstruct from boot, never touched by Restore | C# | green (asserting absence in snapshot) |

### `variable-capture.md`

| Behavior | Layer | Sense |
|---|---|---|
| `app.Variables.SnapshotAt(error)` returns a `Variables.@this` projection of the throw-time view | C# | green |
| `SnapshotAt` consults `App.CallStack.EventsSince(error.throwTime)` and reverse-applies | C# | green |
| Diff stream stays on CallStack; Variables doesn't store time-ordered events | C# | green (asserting structure) |
| `Flags.Diff` auto-flips on for the duration of error processing; restored to prior state after | C# | green |
| Multiple reads of `%!error.callback%` with no intervening state change return the same Data instance (idempotent) | C# | green |
| Throw-time view excludes mutations made by the error handler — `set %x%=2` in handler doesn't appear in `SnapshotAt(error).["x"]` | C# | green |

### `callback-schema.md`

| Behavior | Layer | Sense |
|---|---|---|
| `ICallback.Position` returns a `Call.@this` frame on both records | C# | green |
| `AskCallback` round-trip — `Position`, `Actor`, `Variables` survive | C# | green |
| `ErrorCallback` round-trip — `App` snapshot survives, `Position` reads the bottom CallStack frame | C# | green |
| `ICallback.Serialize(ctx)` returns encrypted bytes (calls `crypto.encrypt`); `Deserialize(bytes, ctx)` reverses | C# | green |
| Dispatch-by-typed-envelope: `Data<AskCallback>` and `Data<ErrorCallback>` resolve to the right `Deserialize` factory | C# | green |
| Goal stub on the wire is `{ PrPath, Hash }` only — no full Goal | C# | green (asserting wire bytes) |
| `app.Callback` is a config `@this`, not an `ICallback`; `app.Callback.Signature.ExpiresInMs` defaults to `null` | C# | green |
| `- set callback timeout to 5 minutes` writes `app.Callback.Signature.ExpiresInMs = 300000` | goal | green |

### `plang-surfaces.md`

| Behavior | Layer | Sense |
|---|---|---|
| `%!error.callback%` resolves through to `app.Errors.Current.Callback` and yields a `Data<ErrorCallback>` | goal | green |
| Reading `%!error.callback%` outside an error handler scope is invalid (or returns null — confirm with coder) | goal | negative |
| `- run %callback%` dispatches into `callback.Run(ctx)` and returns the resumed action's `Data` | goal | green |
| `- ask <actor> "...", vars: %x%, write to %y%` issues an `AskCallback` with `Variables` containing only `%x%` | goal | green |
| `vars:` annotation on a non-ask action is rejected by the builder | goal | negative |
| Resume of an ask callback binds `Variables` and dispatches the ask action with the bound value (no fresh ask) | goal | green |

### `resume-mechanics.md`

| Behavior | Layer | Sense |
|---|---|---|
| `ErrorCallback.Run` flow: verify → decrypt → fresh App → `app.Restore` → run from bottom frame | C# | green |
| `AskCallback.Run` flow: verify → decrypt → resolve Goal stub → fresh App → bind variables → dispatch ask | C# | green |
| `Call.Restore` raises `CallbackGoalHashMismatch` when `live.Hash != stub.Hash` | C# | negative |
| `Call.Restore` raises a referent-integrity error when `Goals.LoadByPrPath` returns nothing (file deleted/moved) | C# | negative |
| `callback.run` action calls `signing.verify` *before* dispatching into `Run` | C# | green |
| `callback.run` raises a hard error when `signing.verify` fails | C# / goal | negative |
| `Run` propagates the resumed action's `Data` result up through `Task<Data>` | C# | green |
| Outer CallStack frames are restored — unwind after resumed action returns through the right Caller chain | C# | green |
| **Both integration cuts** (test-strategy.md) — full end-to-end | integration | green |

### `transparent-signing.md`

| Behavior | Layer | Sense |
|---|---|---|
| `Data.Signature` lazy property — first access populates via `signing.SignAsync` | C# | green |
| `Data.Signature` cached after first populate — subsequent reads return the same Signature | C# | green |
| `JsonSerializer.Write` emits `data.Value` only; never reads `data.Signature` (no signing triggered) | C# | green |
| `PlangDataSerializer.Write` emits Type + Value + Signature; reads `data.Signature` (triggers signing) | C# | green |
| `Data.Signature.Expires` seeded from `app.Callback.Signature.ExpiresInMs` *only when* `data.Value is ICallback` | C# | green |
| Non-ICallback Data wrapped in `application/plang+data` gets `Expires == null` regardless of callback config | C# | green (asserting isolation) |
| Channels look up serializer by mimetype; route accordingly | C# | green |
| Channel handed an unregistered mimetype raises an error (or falls back per coder's call) | C# | negative |
| `application/plang+data` round-trip: write through PlangDataSerializer, read through PlangDataSerializer, signature populates on read (unverified) | C# | green |
| Reading `Data` does *not* auto-verify; verification is the consumer's explicit step | C# | green (asserting absence) |
| Verifying a Data with a tampered Value byte fails | C# | negative |
| Verifying an expired Signature fails | C# | negative |

### `signature-rename.md`

| Behavior | Layer | Sense |
|---|---|---|
| `App.modules.signing.Signature` exists; old name `SignedData` is gone | C# | green |
| Existing tests/callsites that referenced `SignedData` updated and compile cleanly | C# | green |

### `encryption-layering.md`

| Behavior | Layer | Sense |
|---|---|---|
| `crypto.encrypt(byte[]) → byte[]` action exists; v1 returns input unchanged | C# / goal | green |
| `crypto.decrypt(byte[]) → byte[]` action exists; v1 returns input unchanged | C# / goal | green |
| `ICallback.Serialize` calls through `crypto.encrypt`; `Deserialize` calls through `crypto.decrypt` | C# | green |
| Data layer never sees plaintext — `Data.Signature` signs encrypted bytes | C# | green (asserting structure) |
| Round-trip with v1 crypto: encrypted bytes byte-identical to plaintext (passes through unchanged) | C# | green |

## Failure matrix

Consolidated negative paths. Each row is a way the resume *should* fail; the test asserts the failure is hard, typed, and at the right layer.

| Failure mode | Detected by | Error type | Layer |
|---|---|---|---|
| Tampered bytes (any field) | `signing.verify` in `callback.run` | signature mismatch | C# / goal |
| Expired signature | `signing.verify` (when `Expires` set) | signature expired | C# |
| Goal file deleted/moved between issue and resume | `Call.Restore` | referent-integrity error | C# |
| Goal file present but `Hash` differs (redeployed prose) | `Call.Restore` | `CallbackGoalHashMismatch` | C# |
| Provider runtime registration source (DLL) missing | `App.Providers.Restore` | referent-integrity error | C# |
| Provider default-selection name not registered | `App.Providers.Restore` | referent-integrity error | C# |
| Identity name in Selections doesn't resolve | `App.Providers.Restore` (via Identity provider) | referent-integrity error | C# |
| Reading `Data` does not auto-verify | none — by design | (no failure; assert absence of verify call) | C# |
| `vars:` annotation on a non-ask action | builder validation | builder error | goal |
| `- run %callback%` on a Data whose value isn't `ICallback` | type system / handler validation | type error | goal |

## New surfaces this branch introduces

Inventory of types, methods, and registrations the coder will create. Test-designer names tests against these without spelunking 11 topic files.

### Interfaces and types

| Surface | Path | Notes |
|---|---|---|
| `ISnapshotted` | `PLang/App/Snapshot/ISnapshotted.cs` (or wherever cross-cutting interfaces live) | `Capture(Snapshot.@this s)` + `static abstract Restore(Snapshot.@this s, Context.@this ctx)` |
| `Snapshot.@this` | `PLang/App/Snapshot/this.cs` | Typed payload: write entries on Capture, read on Restore |
| `ICallback` | `PLang/App/Callback/ICallback.cs` | `Position`, `Serialize(ctx) → byte[]`, `Run(ctx) → Task<Data>` |
| `AskCallback` | `PLang/App/Callback/AskCallback/this.cs` | Record: `Position, Actor, Variables` |
| `ErrorCallback` | `PLang/App/Callback/ErrorCallback/this.cs` | Record: `Snapshot App` |
| `App.Callback.@this` | `PLang/App/Callback/this.cs` | Config holder; has `Signature` sub-config |
| `App.Callback.Signature.@this` | `PLang/App/Callback/Signature/this.cs` | Has `ExpiresInMs : int?` (default null) |
| `Serializer.@this` | `PLang/App/Channels/Serializers/this.cs` (or interface there) | Per-mimetype family; `Write(data, stream, ctx)` + `Read(stream, ctx)` |
| `JsonSerializer : Serializer.@this` | `PLang/App/Channels/Serializers/Json/this.cs` | Handles `text/html`, `application/json` |
| `PlangDataSerializer : Serializer.@this` | `PLang/App/Channels/Serializers/PlangData/this.cs` | Handles `application/plang+data` |
| `CallbackGoalHashMismatch` | wherever `Call.Restore` raises errors | Referent-integrity error type |
| `App.modules.signing.Signature` (renamed from `SignedData`) | `PLang/App/modules/signing/Signature/this.cs` | OBP cleanup |

### New methods on existing types

| Surface | On | Notes |
|---|---|---|
| `Snapshot()` | `App.@this` | Walks `ISnapshotted` properties, returns `Snapshot.@this` tree |
| `Restore(snap, ctx)` | `App.@this` | Dispatches to each `@this.Restore` |
| `Capture(s)` | `App.Variables`, `App.Errors`, `App.Providers`, `App._statics`, `App.Build`, `App.Testing`, `App.CallStack`, `Call` | Per-`@this` `ISnapshotted` impl |
| `Restore(s, ctx)` (static) | same list | Per-`@this` `ISnapshotted` impl |
| `SnapshotAt(error)` | `App.Variables.@this` | Throw-time projection — consults CallStack for events-since-T |
| `EventsSince(t)` | `App.CallStack.@this` | Returns diff events with `timestamp > t` |
| `Callback` (lazy property) | `Error.@this` | Reads through to `app.Snapshot()` + wraps in `ErrorCallback` |
| `BottomFrame` | `App.CallStack.@this` | Reads the bottom-most frame (resume point) |
| `Signature` (lazy property) | `Data.@this` | First read populates via `signing.SignAsync` |
| Constructor change | `Data.@this` | Now carries `Context.@this` (Stage 3 architectural decision — see stage doc) |

### New PLang actions

| Action | Module | Notes |
|---|---|---|
| `callback.run` | `callback` (new module folder) | Verifies signature, dispatches into `ICallback.Run` |
| `crypto.encrypt` | `crypto` (existing) | v1 identity pass-through |
| `crypto.decrypt` | `crypto` (existing) | v1 identity pass-through |

### New MIME registrations

| MIME | Notes |
|---|---|
| `application/plang+data` | Sibling of existing `application/plang+json`; full-envelope wire shape |

### Existing surfaces this branch touches by reference

These exist; tests reference them but the branch doesn't change their signatures.

| Surface | Path |
|---|---|
| `Goal.Hash` | `PLang/App/Goals/Goal/this.cs:121` (SHA-256 of name + step text; identity primitive) |
| `signing.SignAsync` | `PLang/App/modules/signing/providers/Ed25519Provider.cs:23` |
| `signing.verify` (action) | `PLang/App/modules/signing/` |
| `App.Goals.LoadByPrPath` (or whatever the registry's load-by-path API is named) | `PLang/App/Goals/` — coder confirms exact name |
| `Goals` registry | `PLang/App/Goals/this.cs` |
| `App.Channels.Serializers` collection | `PLang/App/Channels/Serializers/` (existing; new serializers register here) |
