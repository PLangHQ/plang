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

## The settings-belongs-to-system nuance

Ingi flagged settings as the possible exception to "forward the running actor's context." Settings are system-owned, so the settings store likely binds **`App.System.Context`**, not the calling actor's. Note that the Actor ctor already registers a per-actor settings navigable (`app.Settings.Get(path, Context)` passing the actor's own context) for *resolution*. So there are two contexts in play: the store's own (System) and the resolving actor's. Confirm which one the Sqlite store is constructed with before wiring — do not assume the running actor's.

## Caveats carried up to the spine

- **Verify is integrity, not authenticity.** `Ed25519.cs:201` imports the public key from the signature's own `identity` field, so verify confirms internal consistency, not authenticity, unless `verify.Contracts`/`Headers` supply an expected identity. There is therefore no bootstrap root-of-trust problem: reading your identity at boot needs no external key.
- **Read needs no key; sign-on-save does.** The plan's read-path-key worry is inverted — confirm the private key is present on every save, including first-run identity creation.
- **Hash recursion.** Crypto's context-ful canonicalizer must route through a body-only write, not sign-if-missing, or signing recurses.

## You own the final shape

The seam names here (the context-ful read registration, the body-only write entry) describe intent. Pick the shapes that fit the serializer code as it stands.
