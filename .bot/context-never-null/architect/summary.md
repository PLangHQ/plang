# context-never-null — architect summary

## 2026-06-26 — Design settled: remove Context?/Actor? branch-wide

**What this is.** Make `actor.context.@this` and `actor.@this` never null across the codebase — 16 nullable state fields plus 56 downstream parameters. The nullability is the root cause of the LLM-cache double-wrap that blocks `plang build`: "no context" lets the wire reader fall to a second, typed-entry-blind narrow. Context always exists (born at app start, one per actor, forwarded from the running context); null only ever means "not threaded."

**Scope correction.** The coder's seed (`.bot/context-never-null/coder/plan.md`) scoped this to the serializer/Wire read path. Ingi widened it to the whole codebase. The serializer is one of four mechanisms.

**What was decided.**
- **MIME decides verify-vs-trust, the store binds the serializer.** `application/plang` = sign-on-save + verify-on-read always + typed wire read. The settings/identity store is `application/plang` by construction. The `_context == null` trust branch at `Wire.cs:211-227` was a context-less serializer leaking in, not a policy — it dies. `View` (Store/Out) survives only to modulate freshness.
- **Values are born through the context** — `context.Null()/Error()/Ok()` — not constructed-then-stamped. No System-context floor (Ingi rejected it: if a birth needs context, the birth knows its context). The `!` in `Data.Context`'s getter is the lie this removes.
- **Reflection births stamp `.Context`** from the context in scope. Only two sites build a value (`type.cs:391` choice, `Wire.cs:265` Data<T>); both already hold a context. The rest of `Activator.CreateInstance` builds CLR intermediates/converters, not values.
- **Construction-order windows** (`Context.Actor`, `Variables._context`, `App._system/_user`) flip via ctor owner-passing / eager construction — cheap, no behavior change.
- **`Step.Context` is deleted, not flipped.** Its only consumer is `Step.Disabled`, whose every caller already holds the running context one line above. `Disabled` becomes `Disabled(context)` (+ `Disable`/`Enable(context)`); the field and the AnchorScope save/restore go.

**Authenticity decided (in this branch).** Verify today only checks the signature against its *embedded* public key (`Ed25519.cs:133`) — integrity, not authenticity. This branch adds an authenticity check inside verify: verify is `IContext`, so it navigates its own `Context.Actor.Identity` and asserts `layer.Identity ==` it (no decomposed param — OBP Rule #2). Reachable only because the read now holds `context.Actor`. The root key (system keypair, stored in the same `application/plang` settings store) loads in **root mode**: verify signature + keypair self-consistency (`PublicKey` re-derives from `PrivateKey`), no external match — possession authenticates the root. Bootstrap order must load the root identity before any other settings read. Storage stays in the settings store (option A, decided); separate keystore (B) deferred as later hardening. Out of scope: private key is plaintext in sqlite (`identity.cs:57`) — key-at-rest encryption is later hardening.

**Other caveats.** The read path needs no key, sign-on-save does; hash canonicalization must use a body-only write to avoid sign recursion.

**GetActor throws.** Actor set is closed/hardcoded (system/user/service); unknown name throws, returns non-null. Off the stays-list.

**Stays nullable.** Only the `[Choices]` static-vocab param (designed nullable).

**Settled (was open).** Settings is system-owned: you can't store a context, only the actor; the store reads through `App.System.Context` (live) and records `Actor=system`.

**OBP self-audit.** Checked new surfaces against Rules #2/#3/#4. Fixed: dropped the decomposed `verify.ExpectedIdentity` (verify navigates its own `Context.Actor.Identity`); renamed `IsDisabled`/`SetDisabled` → `Disabled(context)` + `Disable`/`Enable(context)`; pass `typeRef` whole to `Readers.Typed`. Flagged pre-existing `GetActor` verb+noun. Audit table at the foot of `demolition.md`.

**Files.** Plan spine `plan.md`; deep dives `plan/mime-and-verify.md`, `plan/value-births.md`, `plan/step-context.md`, `plan/demolition.md`, `plan/after-flow.md` (end-to-end code flow in the non-null world, before/after at each junction). The "removed outright" consolidated list and the OBP self-audit are in `demolition.md`. No stage files yet (Ingi: plan only, steps later).

**Status.** Awaiting Ingi's read-through. Stages to be carved after.
