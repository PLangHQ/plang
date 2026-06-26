# Demolition worklist

Member-by-member, what must not survive this branch, grouped by mechanism. Read before cutting. The stays-list at the bottom is as load-bearing as the kill-list — flipping those would be wrong.

## Removed outright — these cease to exist

Pure deletions (distinct from the *flips* below, which lose their `?` but stay):

**Types / fields / members gone:**
- `ContextLessFallback` static field (`channel/serializer/plang/this.cs:141`) and all five use sites.
- `Step.Context` field (`goal/steps/step/this.cs:16`).
- `AnchorScopeDisposable._previousStepContext` field (`actor/context/this.cs:290`) and its capture (`:299`) and restore (`:307`).
- The `Step.Context = this` set in `AnchorScope` (`:278`).
- The `step.Context = …` / `_items[i].Context = …` stamps (`goal/steps/this.cs:53,128`).
- `Data.Null()`'s context-free static sentinel birth via `@null.Instance` — replaced by `context.Null()`.

**Code blocks / branches deleted:**
- The `if (_context == null) { … }` trust/fail-closed block in `Wire.ReadSignatureLayer` (`data/Wire.cs:211-227`) — entirely.
- The `_context != null` guards on the three typed-read branches (`Wire.cs:386, 406, 419`), and the context-less `Parse → RawSlot` fallback for typed values that they fed (now dead).
- The `?? (_type as IContext)?.Context!` fallback in `Data.Context`'s getter and the `value != null` null-guard in its setter (`data/this.cs:116,120`).
- The `… ?? ContextLessFallback` fallbacks in `data/this.Transport.cs:58,142` and `snapshot/this.Wire.cs:89`.
- `GetActor` returning `(actor.@this?)null` (`app/this.cs:259`) — replaced by a throw.

**Seams retired:**
- The 2-arg `FromWire(raw, kind)` registry seam (`snapshot/this.Wire.cs:83`) — replaced by a context-ful read registration.
- The `_context != null` MIME-guess shortcut at reads — the store binds the serializer, so there is nothing to guess.

The full member-by-member tables (including the *flips* to non-null) follow.

## A. Construction-order windows

| Member | File | Disposition |
|---|---|---|
| `App._system` (field, `actor.@this?`) | `app/this.cs:28` | non-null; both actors constructed eagerly at App start |
| `App._user` (field, `actor.@this?`) | `app/this.cs:29` | non-null; same |
| `Context.Actor` (`public ActorType? { get; internal set; }`) | `actor/context/this.cs:87` | non-null; owner passed into the Context ctor |
| `Variables._context` (field) | `variable/list/this.cs:26` | non-null; stamped at construction, not a line later |
| the line `Context.Actor = this` | `actor/this.cs:109` | gone — the ctor receives the owner instead |

`RegisterContextVariables` uses `Actor?` / `Actor!` (`context/this.cs:171-172`) inside lazy lambdas — those `?`/`!` go once `Actor` is non-null.

## B. Value carries context

| Member | File | Disposition |
|---|---|---|
| `Data.Context` getter `_context ?? (_type as IContext)?.Context!` | `data/this.cs:116` | the `!` removed; getter returns `_context` (non-null) |
| `Data.Context` setter `if (value != null …)` null-guard | `data/this.cs:120` | the null-guard removed |
| `type.Context` (`internal actor.context.@this?`) | `type/this.cs:86` | non-null |
| `dict._context` | `type/dict/this.cs:109` | non-null |
| `list._context` | `type/list/this.cs:141` | non-null |
| `path.Context` | `type/path/this.cs:120` | non-null |
| `clr.Context` | `type/clr/this.cs:16` | non-null |
| `computed.Context` | `type/item/computed.cs:20` | non-null |
| `source.Context` | `type/item/source.cs:22` | non-null |
| `Error.Context` | `error/Error.cs:107` | non-null |
| `Data.Null()` → `@null.Instance` static sentinel | `data/this.cs:534` | replaced by `context.Null()`; no context-free static value |
| `Activator.CreateInstance(choiceType, raw)` unstamped | `type/this.cs:391` | stamp `.Context = context` (context already in scope) |
| `WrapAsTyped` not receiving `_context` | `data/Wire.cs:258,265` | receive/stamp `_context` (non-null) |

New surface: `context.Null()`, `context.Error(...)`, `context.Ok(...)` factory methods on `actor.context.@this` (names are the coder's call).

## C. Serializer / store binds context

| Member | File | Disposition |
|---|---|---|
| `ContextLessFallback` (static field) | `channel/serializer/plang/this.cs:141` | deleted |
| `… ?? ContextLessFallback` (Compress/Decompress) | `data/this.Transport.cs:58,142` | deleted; require the actor |
| `… ?? ContextLessFallback.SnapshotOptions` | `snapshot/this.Wire.cs:89` | deleted; context-ful options |
| `FromWire(raw, kind)` 2-arg seam | `snapshot/this.Wire.cs:83` | retired for a context-ful read registration |
| `=> ContextLessFallback` (crypto hash serializer) | `module/crypto/code/Default.cs:22` | routes through the crypto module's context |
| `Wire._context` (`actor.context.@this?`) | `data/Wire.cs:73` | non-null |
| `_context != null` gate (var-ref branch) | `data/Wire.cs:386` | `_context != null` dropped |
| `_context != null` gate (Typed-reader dispatch) | `data/Wire.cs:406-407` | `_context != null` dropped |
| `_context != null` gate (`goal.call` inline) | `data/Wire.cs:419` | `_context != null` dropped |
| `if (_context == null) { … }` trust/fail-closed block | `data/Wire.cs:211-227` | deleted entirely (Store now verifies; Out always has context) |
| `_serializer = new()` (context-less) | `settings/Sqlite.cs:20` | `new(context)` |
| `converter._context` (`actor.context.@this?`) ×2 | `channel/serializer/json/converter.cs:24,47` | non-null |

`View` is **not** demolished — it keeps the freshness-modulation role (`SkipFreshnessCheck` on Store). Only its trust-vs-verify role goes.

## D. Step.Context

| Member | File | Disposition |
|---|---|---|
| `Step.Context` (`[JsonIgnore] actor.context.@this?`) | `goal/steps/step/this.cs:16` | field deleted |
| `Step.Disabled` reading `Context` directly | `goal/steps/step/this.cs:24-38` | parameterized — `Disabled(context)` query + `Disable(context)` / `Enable(context)` mutation |
| `Step.Context = this` (AnchorScope set) | `actor/context/this.cs:278` | removed (keep `context.Step = …`) |
| `_previousStepContext = action.Step?.Context` (save) | `actor/context/this.cs:299` | removed |
| `if (_action.Step != null) _action.Step.Context = _previousStepContext` (restore) | `actor/context/this.cs:307` | removed |
| `_previousStepContext` field on AnchorScopeDisposable | `actor/context/this.cs:290` | removed |
| `step.Context = Context` / `_items[i].Context = context` stamps | `goal/steps/this.cs:53,128` | removed; pass the local to `Disabled(context)` / `Disable`/`Enable(context)` |

## Downstream — the 56 parameters

The nullable `context.@this?` parameters (e.g. `data/this.Normalize.cs:44`, `type/this.cs:409`, `catalog/Conversion.cs:76,90,147`, `type/reader/ReadContext.cs:20`, `type/this.cs:Create context=null`) are nullable because the fields they receive were. After A–D they take a non-null `context`. Drop the `?` and the `= null` defaults as each call chain becomes non-null. This is mechanical fallout, not separate design — but it is the bulk of the line count, so budget for it.

## Stays-list — do NOT flip these

- **`[Choices]` static-vocabulary context parameter** (`type/choice/list/this.cs:71`). Nullable by design — static vocabularies ignore context. Decide keep-nullable vs pass-and-ignore; either is fine, it is not a state field.
- Read-through getters whose `?` mirrors a genuinely absent upstream — e.g. `context.CallStack => App?.CallStack` (`context/this.cs:49`). Audit case by case; not part of the state-field sweep.

## Flip — was on the stays-list, now corrected

- **`GetActor(string? name) → actor.@this`** (`app/this.cs:250`, `(actor.@this?)null` at `:259`). The actor set is closed and hardcoded (system/user/service), so an unknown name is a critical miss, not a soft one. **Throw** on an unknown name; return a non-null `actor.@this`. Removes `Actor?` from the type.

## Additions this branch introduces (not demolition)

- **Authenticity check inside verify (no new param).** `verify` is `IContext`, so it navigates its own `Context.Actor.Identity` and asserts `layer.Identity ==` it (OBP Rule #2 — don't pass `actor.Identity` in). New surface is the equality check, not an input.
- **`verify.Root`** (bool, default false). When true, the data being verified is the actor's own root identity: run the normal checks (the signature check validates the self-signature) but **skip the actor-identity match**. Request-state flag (Rule #6), set only for the identity-table load. Single word, describes what the read is — not the `Skip…` imperative style.
- **Keypair self-consistency in the identity provider, not verify.** The check that `PublicKey` re-derives from `PrivateKey` belongs to the provider's load (it owns the keypair); `verify` does not learn what a keypair is. So `verify.Root` only means "no external identity to match."
- **PLang variable rename `%MyIdentity%` → `%Identity%`.** Registered at `actor/this.cs:125`; ~3 identity test files reference the old name (`IdentityHandlerTests`, `IdentityErrorPathTests`, `VariablesSnapshotTests`). `%Identity%` resolves in the actor's own context, so the `My` is redundant.
- **`context.Null()` / `context.Error(...)` / `context.Ok(...)`** factory methods on `actor.context.@this`.
- **Tripwire** in `Wire.ReadBody`: `if (_context == null && View == Store) throw`.
- **`CallStack? CallStack => App?.CallStack`** (`context/this.cs:49`) and other read-through getters whose null reflects a genuinely absent upstream — audit case by case; do not blanket-flip a getter whose `?` mirrors a real "not yet" (e.g. no call in flight). These are not part of the Context?/Actor? state-field sweep.

## OBP self-audit (Rules #2, #3, #4)

The new surfaces this branch introduces were checked for decomposition and verb+noun names. Outcome:

| Surface | Smell | Resolution |
|---|---|---|
| `verify.ExpectedIdentity = _context.Actor.Identity` | Rule #2 — decomposed actor into `.Identity` and passed the primitive | Dropped. `verify` is `IContext`; it navigates `Context.Actor.Identity` itself. |
| `step.IsDisabled(context)` / `step.SetDisabled(context, value)` | Rule #3 — `Is`/`Set`+adjective verb+noun | `step.Disabled(context)` (query) + `step.Disable(context)` / `step.Enable(context)` (real-work verbs). |
| `Readers.Typed(typeRef.Name, typeRef.Kind)` (`Wire.cs:407`, pre-existing, on a line we edit) | Rule #2 — `typeRef` decomposed into Name + Kind | Opportunistic fix: pass `typeRef` whole — `Readers.Typed(typeRef)`. |
| `GetActor(name)` (pre-existing) | Rule #3 — `Get`+`Actor` verb+noun | Flagged. We touch it to throw; aligning with the existing `actor.Resolve(name, context)` is a candidate but tangential. |
| `context.Null()` / `context.Ok(...)` / `context.Error(...)` | none | Single-word factories, mirror `Data.Ok`. Kept. |
| `step.Disable/Enable(context)`, `context.Null/Ok/Error(...)` arguments | none | Whole `context` passed, not a field of it. Clean. |

## Tripwire (the proof, not a demolition)

Add to `Wire.ReadBody` after the no-declared-type throw: `if (_context == null && View == Store) throw` naming the slot + type. Keep off until C lands, then on. Green suite with the tripwire on = the invariant holds.
