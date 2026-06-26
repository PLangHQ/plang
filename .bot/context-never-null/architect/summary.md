# context-never-null — architect summary

## 2026-06-26 — Design settled: remove Context?/Actor? branch-wide

**What this is.** Make `actor.context.@this` and `actor.@this` never null across the codebase — 16 nullable state fields plus 56 downstream parameters. The nullability is the root cause of the LLM-cache double-wrap that blocks `plang build`: "no context" lets the wire reader fall to a second, typed-entry-blind narrow. Context always exists (born at app start, one per actor, forwarded from the running context); null only ever means "not threaded."

**Scope correction.** The coder's seed (`.bot/context-never-null/coder/plan.md`) scoped this to the serializer/Wire read path. Ingi widened it to the whole codebase. The serializer is one of four mechanisms.

**What was decided.**
- **MIME decides verify-vs-trust, the store binds the serializer.** `application/plang` = sign-on-save + verify-on-read always + typed wire read. The settings/identity store is `application/plang` by construction. The `_context == null` trust branch at `Wire.cs:211-227` was a context-less serializer leaking in, not a policy — it dies. `View` (Store/Out) survives only to modulate freshness.
- **Values are born through the context** — `context.Null()/Error()/Ok()` — not constructed-then-stamped. No System-context floor (Ingi rejected it: if a birth needs context, the birth knows its context). The `!` in `Data.Context`'s getter is the lie this removes.
- **Reflection births stamp `.Context`** from the context in scope. Only two sites build a value (`type.cs:391` choice, `Wire.cs:265` Data<T>); both already hold a context. The rest of `Activator.CreateInstance` builds CLR intermediates/converters, not values.
- **Construction-order windows** (`Context.Actor`, `Variables._context`, `App._system/_user`) flip via ctor owner-passing / eager construction — cheap, no behavior change.
- **`Step.Context` is deleted, not flipped.** Its only consumer is `Step.Disabled`, whose every caller already holds the running context one line above. `Disabled` becomes `IsDisabled(context)`; the field and the AnchorScope save/restore go.

**Caveats recorded.** Verify-on-read is integrity not authenticity (signature embeds its own key); the read path needs no key, sign-on-save does; hash canonicalization must use a body-only write to avoid sign recursion.

**Stays nullable.** `GetActor → Actor?` (not-found channel), the `[Choices]` static-vocab param (designed nullable).

**Open detail for implementation.** Whether the settings store binds the System actor's context (settings is system-owned) vs the running actor's.

**Files.** Plan spine `plan.md`; deep dives `plan/mime-and-verify.md`, `plan/value-births.md`, `plan/step-context.md`, `plan/demolition.md`. No stage files yet (Ingi: plan only, steps later).

**Status.** Awaiting Ingi's read-through. Stages to be carved after.
