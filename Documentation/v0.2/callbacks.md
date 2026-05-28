# Callbacks

Callbacks are PLang's way of making *suspend-and-resume* a first-class value. A callback is a typed record that captures "where this run was paused, what state survives the pause, and how to land back here when an answer arrives". It rides through the wire as a signed `Data` — the same wire shape every other PLang value uses — and the runtime treats unsigned or tampered Data as hard errors before any dispatch happens.

There are two implementations in v1 and one verb that runs them.

## The contract

```csharp
public interface ICallback
{
    RestoredFrame? Position { get; }
    byte[] Serialize(actor.context.@this ctx);
    Task<Data.@this> Run(actor.context.@this ctx);
}
```

- `Position` answers "where does the resumed run land?". On `AskCallback` it's set at issue time. On `ErrorCallback` it materialises on Restore by walking the captured `CallStack` subsection's bottom frame.
- `Serialize` produces ready-to-wire bytes. The bytes pass through `crypto.encrypt` (v1 = identity pass-through; the real symmetric crypto lands at the same surface).
- `Run` reconstructs whatever state the impl needs, binds the answer or recovery state, jumps to `Position`, and dispatches through the normal `app.Run` path.

`Run` does **not** verify the wire signature. Verification is `callback.run`'s job — see "The seal-then-verify gate" below.

## The two implementations

### `AskCallback` — slim, ad-hoc

For the ask-user issuer. Carries:
- `Position` — the captured `RestoredFrame` (resolved live `Action` + Goal + step/action indices + Id).
- `ActorName` — `"User"` / `"Service"` / `"System"`.
- `Variables` — the developer-annotated surviving variables (the `vars:` list on the ask step).

Resume binds the answer under the sentinel name **`!ask.answer`** and re-dispatches the original `output.ask` action. The handler in `output/ask.cs` detects the sentinel, consumes it, and returns the answer as `Data.Ok(answer)`. The calling step writes it to its `write to %x%` target. No fresh ask is issued on the resumed pass.

The shape is intentionally narrow — no full Snapshot. The resumed run boots a fresh App and only the developer-named state crosses the suspend.

### `ErrorCallback` — full Snapshot

For the error-retry issuer. Single field: a complete `Snapshot.@this` (App tree). On Run, constructs a fresh App, calls `app.Restore(snapshot, ctx)`, and dispatches from `BottomFrame`.

The **wire shape is narrower than the in-process Snapshot fidelity**. `ErrorCallback.Serialize` only writes the `CallStack` and `Variables` subsections; `Errors.Trail` entries, `Providers` registrations, and `Statics` bags don't round-trip. That's deliberate — it's what current tests need, and full fidelity is a separate engineering haul. The narrow shape is enforced both by what `Serialize` emits and what `app.Restore` gates on (each subsystem's Restore is keyed on `HasSection` so missing subtrees stay missing).

## The seal-then-verify gate (`callback.run`)

`PLang/app/modules/callback/run.cs` is the public entry. It implements a strict gate so in-process and wire callbacks are **indistinguishable** to the security boundary — absence of signature is rejection, never trust.

```
1. callback.Value is ICallback?           → else TypeError
2. Callback.EnsureSigned()                → in-process signs locally;
                                            wire-deserialised short-circuits
                                            (signature already populated);
                                            no Context + no signature → throw
3. Callback.RawSignature == null?         → MissingCallbackSignature (defensive)
4. signing.verify(Callback)               → CallbackSignatureMismatch on tamper
5. await callback.Run(ctx)                → CLR exceptions wrapped as Data.FromError:
                                            CallbackGoalNotFound (404)
                                            CallbackGoalHashMismatch (409)
                                            CallbackDispatchError (500)
```

The gate is the security boundary. Neither `AskCallback.Run` nor `ErrorCallback.Run` re-checks the signature. Adding new callback types means implementing `ICallback` and trusting this gate to vet the Data before dispatch.

### Wire-size caps

Defense-in-depth caps applied in `Deserialize` (pre-decrypt and post-decrypt):
- `AskCallback.MaxWireBytes` = **1 MB**.
- `ErrorCallback.MaxWireBytes` = **4 MB** — the snapshot makes it bigger.

Channel layer remains the primary control; the caps catch anything that slips past it.

### Sensitive-property strip

The merged `application/plang` serializer composes `Sensitive.Strip` onto its STJ options (alongside `Transport.ForOutbound`); `AskCallback._options` and `ErrorCallback._options` install the same modifier via `DefaultJsonTypeInfoResolver` for their internal-shape serialisation. Anything marked `[Sensitive]` on the inner value is removed before bytes hit the wire.

## Sign-if-missing — the converter does it

There is no `ICallback` carve-out anymore. As of `data-serialize-cleanup` (Stage 2), signing happens inside `app.data.WireJsonConverter.Write` — for every Data the converter walks. The rule is sign-if-missing, idempotent: if `data.Signature` is null, `data.EnsureSigned()` fires before the bytes are emitted; if it's already populated (because the Data was deserialised from a signed wire payload, or because some prior path signed it), the converter skips. Forwarding preserves provenance — Alice's signed `D1` wrapped in Bob's `D3` round-trips with both signatures intact.

Three consequences fall out for callbacks:

1. `callback.run`'s `Callback.EnsureSigned()` (gate step 2 above) is mostly belt-and-braces — by the time bytes are deserialised the converter has already populated `Signature`. The explicit call remains for the *in-process* path where no serialisation happened.
2. Any Data through any channel auto-seals on egress. The verify-path code that previously gated on `data.Signature == null` still sees `null` only on Data that has never been written.
3. Canonicalization for `crypto.Hash` was switched to the same options bag the wire converter uses (`plang.@this.OutboundOptions`). Hash bytes ≡ wire bytes minus the outer `Signature` field. Tampering with `name`, `type`, `value`, `properties`, or any nested-Data signature invalidates the outer signature.

## The `application/plang` mimetype

`application/plang` is the single wire serializer for the full Data shape. The historical `application/plang+data` mimetype was merged into it on `data-serialize-cleanup` — the `+data` variant and the `PlangDataSerializer` class no longer exist.

- The serializer (`app/channels/serializers/serializer/plang/this.cs`) composes the `Json` engine with `WireJsonConverter` + `Transport.ForOutbound` + `Sensitive.Strip` modifiers; no `JsonSerializer.X` calls live in the serializer itself.
- `WireJsonConverter` emits the five-field wire shape `{name, type, value, properties, signature}` (the `properties` field is omitted when empty; `signature` when null), signing each Data sign-if-missing as it walks.
- **Read** does **not** auto-verify. The reconstructed Data has its signature populated-but-unverified; consumers (`callback.run`) call `signing.verify` explicitly.

JSON wire shape (the wire format is the coder's call — could become CBOR or length-prefixed binary later without breaking the contract):

```json
{
  "name": "answer",
  "type": "ask",
  "value": { /* ICallback wire shape */ },
  "properties": { /* optional metadata, omitted when empty */ },
  "signature": { /* signing.SignedData wire shape */ }
}
```

## Position semantics

`RestoredFrame` is a **surrogate**, not a `Call.@this`. It carries:
- The resolved live `Action` (linked to its Step → Goal in the live app.goals registry).
- The positional triple `(StepIndex, ActionIndex, Id)` captured at issue time.

It is **not pushable** into the AsyncLocal `Current`. It has no Stopwatch, no `OnSet`, no lifecycle. Restoring into a real `Call.@this` would tear up those invariants because Call's ctor is internal and lifecycle-coupled. Callbacks read `RestoredFrame` to identify the resume point; `callback.Run` dispatches the bottom frame's Action through `app.Run`, which Pushes a fresh live Call.

## Errors → App back-reference

`Errors.Push` sets `error.App = this.App` so `Error.Callback` can materialise on demand via `app.Snapshot()`. Without the back-ref the `Error` would have no path to the live App tree at the point a recovery callback is needed.

## Configuration

```csharp
app.Callback.Signature.Expires       // default expiry (TimeSpan) for callback signatures;
                                     // null = no expiry; integrity is unconditional
```

Read by `EnsureSigned`'s expiry stamping for `ICallback`-valued Data. The PLang surface for writing this is on the v0.2 todos list.

## Out of scope on this branch (open work)

- **HTTP wire transport** for `ask-user` resume across an HTTP boundary. The in-process resume in `AskCallback.Run` is the only path today.
- **Real symmetric crypto** for `crypto.encrypt`/`decrypt`. v1 = identity pass-through.
- **Stale `.test.goal`** scenarios depending on the above: `AskVarsOnNonAsk` (builder-validator), `CallbackTimeoutSetting` (PLang verb to write `Expires`), `DurabilityRoundTrip` (mime-tagged file write/read), `TamperedSignature` (byte-mutation reach into raw bytes).

## See also

- `Documentation/v0.2/snapshots.md` — the `ISnapshotted` pattern and per-subsystem wire shapes that `ErrorCallback` rides on.
- `PLang/app/modules/callback/` — `@this`, `ICallback`, `AskCallback`, `ErrorCallback`, `Signature/this.cs`.
- `PLang/app/callstack/RestoredFrame.cs` — the position surrogate.
- `PLang/app/modules/callback/run.cs` — the seal-then-verify gate.
- `PLang/app/modules/output/ask.cs` — the issuer + resume sentinel.
- `PLang/app/channels/serializers/serializer/plang/this.cs` — the merged `application/plang` wire serializer.
- `PLang/app/data/WireJsonConverter.cs` — the converter that walks the `{name, type, value, properties, signature}` wire shape and fires sign-if-missing on every Data.
