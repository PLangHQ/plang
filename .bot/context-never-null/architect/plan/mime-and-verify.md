# MIME decides verify, the store binds it

## The fork being removed

`Wire.ReadBody` gates typed reading on `_context != null` in three places:

- `:386` — the `%x%` full-match variable reference branch (`_template != null && _context != null`)
- `:406` — the main Typed-reader dispatch (`typeRef is { IsNull: false, Polymorphic: false } && _context != null && _context.App.Type.Readers.Typed(...)`)
- `:419` — the temporary `goal.call` inline-born branch

When `_context` is null, all three skip and the value falls to `item.serializer.json.Parse` (`:440`), which leaves a nested typed entry as a raw `{type,value}` dict. The `.pr` load path has a context, so it reads through the Typed reader and the value is born to its type. Settings/snapshot reads have no context, so they land on the raw dict. Those are the two divergent reads.

## Why the context-less path cannot be patched

The Typed reader dispatches through `_context.App.Type.Readers.Typed(typeRef.Name, typeRef.Kind)` — it reads the value by its *declared wire shape*. The context-less path has no handle to the type-reader registry (the registry lives on App, reached via context), so it can only guess the shape from content. A legitimate LLM result contains `{type:{name},value}`-shaped objects (action parameters), so a content-guess mis-borns them. The collision between a wire typed-entry and look-alike user JSON is real and only resolvable by reading the declared shape — i.e. only on the context-ful path. So the fix is to eliminate the context-less narrow, which requires context to never be null at a read.

## The model

The decision "verify the signature or trust it" is a property of the **MIME type**, not of whether a context is present. A store binds a serializer, and that serializer *is* the MIME. The Sqlite settings store's schema comment says it: `key TEXT PRIMARY KEY, data TEXT (Data via channel.serializer.plang.@this)`. The store is `application/plang` by construction.

`application/plang` means:

- **sign-on-save** — `Wire.Write` fires sign-if-missing.
- **verify-signature-on-read, always** — no trust-at-rest shortcut.
- **read the typed wire shape** — needs context for `App.Type.Readers`.

The settings / identity / permission / llmcache / snapshot artifacts are all `application/plang`, so they all verify on read. The null-context-means-trust coupling at `Wire.cs:211-227` was the smell; the MIME type was always the right place for the decision.

## What happens to `View` (Store vs Out)

`View` survives, but only to modulate **freshness**. At-rest (Store) artifacts re-present the same nonce on every read and outlive the wire-freshness window by design, so Store skips the nonce-replay/freshness check (`verify.SkipFreshnessCheck`, already wired at `Wire.cs:243-244` for the with-context Store path). What dies is the branch that skips verification entirely. After this branch:

- Store: verify signature, skip freshness.
- Out (transport): verify signature, enforce freshness + nonce-replay.

The `if (_context == null)` block at `:211-227` is deleted. The "Transport with no actor → fail closed" sub-case it contained becomes unreachable too (Out always has a context now), so it goes with the block.

## Production sites to thread context through

- `settings/Sqlite.cs:20` — `_serializer = new()` → construct with the owning context. Ctor signature change touches a few test callers.
- `channel/serializer/plang/this.cs` (snapshot path) — the `_snapshot` Wire is built with no context, ignoring the ctor's. Pass the context.
- `snapshot/this.Wire.cs:83,89` — the 2-arg `FromWire(raw, kind)` seam drops context, and `WireOptions ?? ContextLessFallback`. Register a context-ful read seam so `source.Value`'s reader branch carries the context; retire the 2-arg seam.
- `data/this.Transport.cs:58,142` — `_context.Actor? ?? ContextLessFallback` on Compress/Decompress. Require the actor (non-null after this branch).
- `module/crypto/code/Default.cs:22` — hashing routes through the crypto module's context (see caveat below); no `ContextLessFallback`.
- `channel/serializer/plang/this.cs:141` — delete `ContextLessFallback` once the above are converted.

## Settings is system-owned (resolved)

You can't persist a context — it's ephemeral, per-execution. What's durable is the actor identity. Settings are system-owned, so the store reads/writes *through* `App.System.Context` (the system actor's long-lived live context), and the persisted ownership fact is `Actor=system` (the signature's `identity`). The Actor ctor already registers a per-actor settings navigable (`app.Settings.Get(path, Context)` passing the actor's own context) for *resolution* — so two contexts are in play: the store's own serializer rides System, while a read flowing through another actor passes that actor's context for resolution.

## Caveats carried up to the spine

- **Authenticity lands in this branch (decided).** `Ed25519.VerifyAsync` step 6 (`:133`) checks the signature against `layer.Identity` — the public key embedded in the signature itself — and the Store/boundary read never sets `Contracts` (step 4, the only identity gate). So today verify proves the signer held the private key for the embedded key; it does not pin that key to system. This branch makes verify navigate its own `Context.Actor.Identity` (verify is `IContext`) and assert `layer.Identity ==` it — no decomposed identity passed in (OBP Rule #2). Reachable only because the read now holds `context.Actor`. See "Bootstrap: loading the root key" below for the one read that authenticates differently.
- **Read needs no key; sign-on-save does.** The plan's read-path-key worry is inverted — confirm the private key is present on every save, including first-run identity creation.
- **Hash recursion.** Crypto's context-ful canonicalizer must route through a body-only write, not sign-if-missing, or signing recurses.

## Bootstrap: loading the root key

The system keypair lives in the same `application/plang` settings store (`identity/code/Default.cs` — `store.Set/Get<Identity>(Table, name)`), and the stored `Identity` carries both `PublicKey` and the real `PrivateKey` (`identity/type/identity.cs:21,26,57`). So "verify every `application/plang` read against `App.System.Identity`" is circular for the identity read itself — that read is *how* the system pubkey gets into memory.

It breaks because the root authenticates by **private-key possession**, not by matching an external key:

1. **First run, no identity.** `GetOrCreateDefaultAsync` mints a keypair (private key in hand), `SaveAsync` signs the artifact with it and stores it. Nothing to verify — just minted.
2. **Later runs — the identity-table read is the one root read.** Read it in **root mode**: verify the signature is internally valid (integrity) and that the loaded keypair is self-consistent — `PublicKey` re-derives from `PrivateKey` (Ed25519 deterministic). A self-consistent keypair from your own store is the authentication for the root. This establishes `App.System.Identity`.
3. **Every other read** — verify navigates `Context.Actor.Identity` and asserts `layer.Identity ==` it.

So the root-mode flag is set only for the identity-table read; everything else matches against the actor's loaded identity, navigated inside verify. **Bootstrap order:** load the system identity (root mode) before any other `application/plang` read, or a settings read re-enters the identity load (itself a settings read). `App.System` is eager (mechanism A), but `MyIdentity` resolves lazily (`actor/this.cs:125-129`) — make the root load happen first.

**Decided — A.** The keypair stays in the `application/plang` settings store; the identity-table read is marked root-mode. (Considered and deferred: **B** — root keypair in a separate protected keystore loaded before any settings read. More standard root-of-trust separation, but a storage change; not this branch.)

Separate concern (not this branch): the private key is stored in plaintext in sqlite (`identity.cs:57`), so authenticity catches tampering by others, not by someone who read your key file. Key-at-rest encryption is later hardening.

## You own the final shape

The seam names here (the context-ful read registration, the body-only write entry, the root-mode read flag, `context.Null/Error/Ok`) describe intent. Pick the shapes that fit the serializer code as it stands.
