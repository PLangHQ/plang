# Callback Module

Run a callback envelope — verify it, then dispatch into whatever the callback was paused at.

A *callback* is PLang's way of representing "where this run paused, what survives the pause, and how to resume". They're produced by actions that suspend ([`output.ask`](output.md#ask)) or by error-recovery flows. They ride between processes (or to/from a file, or across an HTTP boundary) as signed `Data` envelopes — no callback ever dispatches without first being verified.

In most goals you don't run callbacks yourself; the channel layer does it for you when an answer comes back from a paused ask. You'll write `- run %callback%` when you're consuming a callback envelope from somewhere external (a file, a queue, an HTTP body).

## Actions

### run

Verify the callback's signature, then dispatch it.

```plang
/ Read a serialised callback envelope, run it
- read 'pending-callback.pdata' into %callback%
- run %callback%, write to %result%
- write out 'Resumed: %result%'
```

**Parameters:**

| Name | Type | Required | Description |
|------|------|----------|-------------|
| Callback | Data | yes | The callback envelope to run. The wrapped value must be a callback record |

**Returns:** the `Data` produced by the resumed action — same as if the original suspending action had returned it directly.

#### What `run` actually does

1. Confirms the wrapped value is a callback record (else: `TypeError`).
2. Seals the envelope (`EnsureSigned`). In-process callbacks sign locally; envelopes that arrived from the wire already carry their signature; if neither path applies, the call is rejected.
3. Verifies the signature via `signing.verify`. Tampered or unsigned envelopes never reach dispatch.
4. Dispatches into the callback's own `Run`.

If anything goes wrong, you get a typed error back as `Data` — never a raw exception. The error keys you'll see:

| Error key | Meaning |
|---|---|
| `MissingCallbackSignature` | Envelope had no signature and no way to sign locally |
| `CallbackSignatureMismatch` | Signature didn't verify (tamper or wrong identity) |
| `CallbackGoalNotFound` | The goal the callback wants to resume into doesn't exist in the live App |
| `CallbackGoalHashMismatch` | The goal exists but its source has changed since the callback was issued |
| `CallbackDispatchError` | Anything else that went wrong during dispatch |

The signature check is unconditional — there is no "unsigned in-process" path. This is by design: in-process and wire callbacks are indistinguishable to the security boundary.

## What kinds of callbacks exist?

Two in v1:

- **Ask callback** — produced by `- ask user '…', vars: …`. Slim shape: position + actor + named surviving variables. Resumes by binding the answer and re-dispatching the original ask. See [output.ask](output.md#ask).
- **Error callback** — produced by error-recovery flows. Carries a snapshot of the App tree (CallStack + Variables) so the resumed run can land at the bottom frame and continue.

Both ride the same `application/plang+data` envelope and the same `callback.run` gate. New callback types may be added; the gate is the one place to extend security checks.

## Limits

- `Ask` callback envelopes are capped at 1 MB on the wire.
- `Error` callback envelopes are capped at 4 MB on the wire (they carry a snapshot, so they're bigger).
- Properties marked `[Sensitive]` on the wrapped value are stripped before serialisation — the envelope never carries them off the host.

## See also

- [output](output.md) — `ask` is the main producer of callbacks.
- [signing](signing.md) — the signing/verify pipeline `callback.run` uses.
