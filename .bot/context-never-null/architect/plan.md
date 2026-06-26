# Context (and Actor) is never null

## Why

`actor.context.@this` and `actor.@this` are nullable in 16 places that hold real state, plus 56 method parameters that inherit the nullability downstream. Every one of those `?` is a place where a value or a serializer can reach a read/sign path with no context, and the code has to branch on it. That branching is the root cause of a live bug: the LLM-cache double-wrap that blocks `plang build`. It is a symptom, not the disease. The disease is that "no context" is a reachable state at all.

Context always exists. It is created at app start; each actor owns one; it is forwarded from the running context into every call. Null is never the right answer — it only ever means "we didn't bother to thread it." This branch removes `Context?` and `Actor?` as reachable states across the codebase, so the value/serializer code stops guessing and the cache fix downstream becomes trivial.

This is not a serializer rename. The serializer is one of four mechanisms; the value-type context layer and `Step.Context` are the parts with genuine design decisions.

## The invariant

`actor.context.@this Context` and `actor.@this Actor` — never `?` — on every field and property that holds one as state. A read or serialize with no context is a bug to fix at the caller, not a state to branch on. The deliberate escape hatch (`channel.serializer.plang.@this.ContextLessFallback`) is retired.

Two exceptions stay nullable on purpose, because there the null is real, not lazy — see "What stays nullable" below.

## The root cause this removes

There are two ways a Data value is read off the wire, and they disagree on a nested typed entry `{type:{name},value}`:

| read | context | result on `{type,value}` |
|---|---|---|
| `.pr` load | yes — routes through the Typed reader | born to its type |
| settings / snapshot | none — `_context != null` gate fails, falls to `item.serializer.json.Parse` | left a raw `{type,value}` dict |

`Wire.cs` gates typed reading on `_context != null` at three points (`:386`, `:406`, `:419`). The context-less branch can only guess a value's shape from its content, and `{type,value}` is genuinely ambiguous against look-alike user JSON. The Typed reader does not guess — it dispatches through `_context.App.Type.Readers.Typed(name, kind)`, reading the *declared* wire shape. So the context is the handle to the type-reader registry, and that is why the context-less path cannot be patched to tell the two apart. The only correct fix is to make context never null and delete the second narrow. Full detail: [plan/mime-and-verify.md](plan/mime-and-verify.md).

## The model: MIME decides, the store binds it

Verify-vs-trust is not a property of whether a context happens to be present. It is a property of the **MIME type**, and the **store binds the serializer**. The settings / identity / permission store is `application/plang` by construction (Sqlite schema: `data TEXT (Data via channel.serializer.plang.@this)`). `application/plang` means: sign-on-save, **verify-signature-on-read always**, and read the typed wire shape (needs context). There is no "trust at rest" — the `_context == null` branch at `Wire.cs:211-227` was never a policy, it was a context-less serializer leaking in where an `application/plang` one belonged.

`View` (Store vs Out) survives, but only to modulate **freshness** — Store skips the nonce-replay/freshness window because at-rest artifacts re-present the same nonce by design. What dies is the skip-verification-entirely branch. Store goes from "trust" to "verify signature, skip freshness" — which is already what the with-context Store path does today.

## What changes — four mechanisms

The 16 nullable fields are not one problem. The 56 parameters are downstream — flip the fields and the params follow.

**A. Construction-order windows — cheap, no behavior change.** `Context.Actor`, `Variables._context`, `App._system`/`_user`. Nullable only because of two-phase init inside a constructor (object built, stamped a line later), and production constructs each in exactly one place. Pass the owner into the ctor / construct both actors eagerly.

**B. Value carries context — born through the context.** The seven value types (`type`, `dict`, `list`, `path`, `clr`, `computed`, `source`) plus `Error` carry a nullable `Context`. This is where the actual lie lives: `Data.Context`'s getter is `_context ?? (_type as IContext)?.Context!` — that trailing `!` returns null at runtime. The fix is your factory model: values are born from the context — `context.Null()`, `context.Error(...)`, `context.Ok(...)` — so there is no construct-then-stamp window. No System-context floor; if we need context at a birth, the birth knows its context. Sentinels (`Data.Null` → `@null.Instance`) go through the factory. The two reflection births stamp `.Context` from the context already in scope. Full detail: [plan/value-births.md](plan/value-births.md).

**C. Serializer / store binds context.** `Wire._context` and the two json converters become non-null. Production sites thread their context in: Sqlite settings store `new()` → `new(context)`, the plang snapshot serializer, `snapshot/this.Wire`, `data/this.Transport`, and crypto (hashing routes through the crypto module's context). `ContextLessFallback` dies at all five sites. The `_context != null` gates and the `_context == null` trust branch in `Wire.cs` are removed. Full detail: [plan/mime-and-verify.md](plan/mime-and-verify.md).

**D. Delete `Step.Context`.** Its only consumer is `Step.Disabled`, and every caller of `Disabled` already holds the running context as a local one line above. The field is pure choreography — stash the context on a shared Step, read it back through `Disabled`, and have AnchorScope save/restore the stash per dispatch. `Disabled` becomes context-parameterized and the field is deleted, not nulled. Full detail: [plan/step-context.md](plan/step-context.md).

## The after-state — how the code flows once context is non-null

[plan/after-flow.md](plan/after-flow.md) walks the whole path in the new world — app start → actor/context births → value creation → wire write → the single read path → verify-with-authenticity → bootstrap root key → `Step.Disabled` — with before/after code shapes at each junction. The consolidated list of what is **removed outright** (distinct from the flips) is at the top of [plan/demolition.md](plan/demolition.md).

## Leaf trace — the incumbents and each call site

- **`Wire._context`** (`data/Wire.cs:73`) — read at the three typed-read gates (`:386/406/419`) and the trust branch (`:211-227`). Disposition: non-null; gates and trust branch deleted; `View` keeps only the freshness role.
- **`Data.Context` getter** (`data/this.cs:114-123`) — the `_context ?? (_type as IContext)?.Context!` lie. Disposition: non-null once values are born with context; the `!` is removed.
- **`Step.Context`** (`goal/steps/step/this.cs:16`) — written at `steps/this.cs:53,128` and AnchorScope `context/this.cs:278`; the only read is AnchorScope's own save-for-restore at `:299`; the only *behavioral* read is `Step.Disabled` (`:24-38`). Disposition: field deleted; `Disabled(context)` parameterized; AnchorScope drops the Step.Context dance, keeps `context.Step`.
- **`ContextLessFallback`** (`channel/serializer/plang/this.cs:141`) — used at `data/this.Transport.cs:58,142`, `snapshot/this.Wire.cs:89`, `module/crypto/code/Default.cs:22`. Disposition: each caller gets its real context; the field is deleted.
- **`Context.Actor`** (`actor/context/this.cs:87`) — set one line after Context construction in the Actor ctor (`actor/this.cs:108-109`); read for channels/serializers/signing actor. Disposition: non-null via owner passed into the Context ctor.
- **`App._system`/`_user`** (`app/this.cs:28-29`) — lazy backing for the two actors. Disposition: non-null via eager construction at App start.

## The demolition worklist

A member-by-member audit of what must not survive, organized by when each dies, with the explicit stays-list, lives in [plan/demolition.md](plan/demolition.md). Read it before cutting — it is the spine of the implementation.

## What stays nullable (the null is real, not lazy)

- **`[Choices]` static-vocabulary context param** (`type/choice/list/this.cs:71`) — nullable by design; static vocabularies ignore context. Decide during implementation whether to keep the nullable contract or pass-and-ignore a non-null context. Low stakes; not a state field.

`GetActor` does **not** stay nullable. The actor set is closed and hardcoded (system/user/service), so an unknown name is an error, not a soft miss — **throw** on an unknown name and return a non-null `actor.@this`. A missing actor is a critical miss, not a state to branch on.

## Caveats — true, and worth being clear-eyed about

- **Authenticity lands in this branch (decided).** `Ed25519.VerifyAsync` step 6 (`:133`) today checks the signature against the public key *embedded in the signature* (`layer.Identity`), and the Store/boundary read never sets `Contracts`, so nothing pins the signer to system — a local-write adversary can re-sign tampered data with their own keypair and pass. This branch adds an **authenticity check inside verify**: verify is `IContext`, so it navigates its own `Context.Actor.Identity` and asserts `layer.Identity ==` it — nothing decomposes the actor at the call site (OBP Rule #2). Reachable only because the read now holds `context.Actor` (hence the actor's loaded identity) — context-never-null is what enables it. The bootstrap carries an explicit root-mode flag (request state) that switches verify to keypair self-consistency. The bootstrap (loading the root key) is the one read that authenticates differently — see [plan/mime-and-verify.md](plan/mime-and-verify.md) "Bootstrap: loading the root key". **Out of scope:** the private key sits in the sqlite store in plaintext (`identity.cs:57`), so authenticity catches tampering by others, not by someone who already read your key; encrypting the key at rest is separate hardening.
- **The read path needs no keys; sign-on-save does.** Because verify is self-contained, reading a signed artifact never needs a key in hand. The private key is needed on the *write* path. That is the path to confirm is always keyed — including first-run identity creation.
- **Hash canonicalization must use a body-only write.** Crypto hashes by canonicalizing through the (now context-ful) wire, but `Wire.Write` fires sign-if-missing. The hasher must write body-only or signing recurses (hash → canonicalize → sign → hash). The body-only path exists today; confirm crypto's new context-ful canonicalizer routes through it.

## Settings is system-owned (resolved)

You can't persist a context — it's ephemeral, per-execution. What's durable is the actor identity. So the settings store reads/writes *through* a live context — `App.System.Context`, the system actor's long-lived context — and the persisted ownership fact is `Actor=system` (in the signature's `identity`). Reads that flow through another actor pass that actor's context for resolution, but the store's own serializer rides the system context.

## Acceptance

`DictTypedEntryRoundTripTests` (the dict-of-typed-entries Store round trip, currently red context-less) goes green. A tripwire in `Wire.ReadBody` — `if (_context == null && View == Store) throw` — stays off until the production sites are converted, then on, guarding the invariant. The downstream cache fix and `remove-goalcall` are *not* in this branch.

## You own the final shape

Any code path, signature, or factory name in these files (`context.Null/Error/Ok`, `step.Disabled(context)`, etc.) is a suggestion that captures the intent. The coder owns the final names and shapes. If a cleaner seam appears while implementing, take it — the invariant is the contract, not the spelling.
