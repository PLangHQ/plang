# Callbacks

A callback is a run handed to someone else to go on with later: an answer to an ask over a connection that isn't kept, a
retry after an error. **Callbacks are later.** They're not built on this branch, and nothing below describes their
shape.

What exists today:

- **`start %callback%`** (`PLang/app/module/callback/start.cs`): a Data that carries a snapshot (its `Snapshot` slot)
  is resumed through that snapshot (`snapshot.Resume`). A Data with none answers `NoSnapshot`.
- **An error's throw-time snapshot** (`%!error.callback%`, `PLang/app/error/this.cs`): its first read takes
  `app.Snapshot(error, context)`, the asker's variables and call stack as they stood at the throw (the live stack has
  unwound past the failing action by the time a handler runs). An error that holds no run (it wasn't raised inside one,
  or it came back from a stored state) has no callback: `NoCallback` (400).
- **A message channel's ask** carries a snapshot of the run (`channel/type/message/this.cs`), so the channel can go on
  once the user replies.

A snapshot, how it's stored and how a run resumes from it: `snapshots.md`.

## Signing on the wire

This part isn't about callbacks: it's how any Data is signed on the wire.

### Sign-if-missing — the converter does it

Signing happens inside `app.data.Wire.Write`, for every Data the converter walks. The rule is sign-if-missing,
idempotent: if `data.Signature` is null, `data.EnsureSigned()` fires before the bytes are emitted; if it's already
populated (the Data was read from a signed wire payload, or some prior path signed it), the converter skips. Forwarding
preserves provenance: Alice's signed `D1` wrapped in Bob's `D3` round-trips with both signatures intact.

- Any Data through any channel seals itself on the way out. A verify that gates on `data.Signature == null` sees
  `null` only on Data that has never been written.
- Canonicalization for `crypto.Hash` uses the same options the wire converter uses. Hash bytes ≡ wire bytes minus the
  outer `Signature` field. Tampering with `name`, `type`, `value`, `properties`, or any nested Data's signature
  invalidates the outer signature.

### A signed value that arrives

A replay is the same wire arriving twice. The read that brings a signed value in off a transport births its signature
layer, and that read is one **arrival**. The layer belongs to the arrival, so the nonce is presented once for it, by
whatever verifies it first (`signature.Present`):

1. The hash and the signature are checked first. A tampered wire fails there and presents nothing, so it does not burn
   the nonce: the genuine wire still arrives after it.
2. The nonce is presented last, with one atomic add-if-absent (`nonce:<nonce>` in the cache, kept until the signature
   expires). A concurrent verify of the same arrival awaits that one answer; it is not a second presentation.

What follows from that:

- **The value the read produced verifies, and names its signer (`%x!signature.identity%`), as many times as it is read,
  its copies too.** Every copy of the layer shares the arrival's one answer.
- **The same bytes arriving again is a new arrival.** Its nonce is already there, so it answers `NonceReplay` (400).
- **`identity` is named only once verify has passed** on the bearer. A bearer changed after signing answers
  `DataHashMismatch`.
- **A signature read from plang's own store is not an arrival.** It has origin `stored`, no window, and re-presents its
  nonce on every read, which is not a replay (see `filesystem-permission.md`, "Grant verification").
- A layer is built by the wire reader (an arrival) or by `sign`; nothing else builds one directly.

### The `application/plang` mimetype

`application/plang` is the single wire serializer for the full Data shape: `{name, type, value, properties,
signature}` (`properties` omitted when empty, `signature` when null), signing each Data sign-if-missing as it walks.
The value slot is built via `data.Normalize(View) → IWriter`, so domain types ride as `[Out]`-tagged property bags
(`Documentation/Runtime2/data-spec.md`). A read of a signed payload verifies the signature layer on a transport read
and fails closed without an actor context (`wire-serialization.md`, "`Wire.Read` auto-verifies").

## See also

- `Documentation/v0.2/snapshots.md` — snapshots, the walk, resume.
- `PLang/app/module/callback/start.cs` — `start %callback%`.
- `PLang/app/data/Wire.cs` — the converter that walks the wire shape and fires sign-if-missing on every Data.
