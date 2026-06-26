# context-never-null — coder handoff

Branch: `context-never-null`. All commits below are production-green and the App boots.

## Done

- **S1 — construction-order non-null.** `Context.Actor` (ctor `owner`), `App._system/_user` (eager), `Variables` (ctor context), `GetActor` throws + returns non-null.
- **S2 — `Step.Context` deleted.** `Disabled(context)` / `Disable(context)` / `Enable(context)`; AnchorScope + steps-collection stamps gone.
- **S3 — values born through context.**
  - Factories on `actor.context.@this`: `Ok() / Ok(value[,type]) / Ok<T>(value) / Null(name?) / Error(err) / Error<T>(err) / NotFound(name?)`. Data ctors take an optional `context` so the type-build path sees its context at construction.
  - **Context is born first** in the App ctor (system+user actors → contexts → *then* Type/Code/Settings). Verified the actor/context ctor needs neither Type nor Code (App touched only via lazy lambdas; type seeds are pure statics).
  - **Context lives on the class**, not threaded through methods: `type.catalog`, `module.code`, `tester`, `builder`, `channel.list` each hold their own context (ctor-injected for catalog/code; owner-sourced `System.Context` for tester/builder; `Actor.Context ?? System.Context` for channel.list).
  - **~470 of ~534 births swept** to born-from-context.
- **S4a/b — settings store binds context.** `Sqlite` serializes through and births from the **system context** (`application/plang`, system-owned), so its reads route through the **typed wire reader** instead of the context-less narrow — the substrate of the `plang build` double-wrap fix. The plang serializer's `_snapshot` Wire also carries the ctor context.

## Remaining (gated, in order)

### 1. Long-tail births (~70 sites, mostly the genuinely context-less corners)
Grep: `grep -rEn 'data\.@this(<[^>]*>)?\.(Ok|FromError|Null|Fail|NotFound|Uninitialized)\(' PLang/app | grep -v actor/context/this.cs`
- **Has a context, just needs wiring:** `module/http/code/Default` (action.Context for instance, thread a param for the static read helpers), `goal/list/this` LoadFromFile (App→System.Context), `module/debug/this` Write (`_engine.System.Context`), `module/builder/validateResponse` static.
- **Static value-type helpers — thread a `context` param from the (context-having) caller:** `type/number/this.Arithmetic` (`Wrap`), `type/number/this.Convert` (`Fail`), `type/text/this` (`Split`), `condition/code/Default` (`EvaluationError`).
- **Low-level crypto — thread `action.Context`:** `signing/code/Ed25519` `Sign`/`Verify` (called from the handler which has `action.Context`).
- **Genuinely context-less — decide:** options-built serializers (`Json`/`Text` deserialize on the write/.pr path), the `noop` channel sink (process-wide static, no actor), `IClass.Build` default interface method, `type/signature/this.Wire.FromWire` (Stage-4 retires this 2-arg seam), `OpenAi` static `ParseToolArguments`/`RestoreFromCache`, `module/IClass`. For these, either thread context from the caller or accept a born-via-parent/list re-stamp; they are read for `.Success`/`.Value`, not `.Context`.

### 2. Getter flip — the S3 enforcement
`data/this.cs` `Data.Context` getter `_context ?? (_type as IContext)?.Context!` → `=> _context;` and drop the setter null-guard. Gated on (1): once no birth is context-less, the `!` lie is removable. Make `_context` field truly non-null then.

### 3. Stage 4 enforcement (after getter flip)
- `data/Wire.cs`: make `_context` non-null (`:73`); drop the `_context != null` guards on the three typed-read branches (`:386/406/419`); delete the `if (_context == null) {…}` trust/fail-closed block in `ReadSignatureLayer` (`:211-227`); pass `typeRef` whole to `Readers.Typed(typeRef)` (`:407`).
- `channel/serializer/json/converter.cs` `_context` non-null (`:24,47`).
- Retire `ContextLessFallback` (`channel/serializer/plang/this.cs:141`) + its consumers: `data/this.Transport.cs:58,142` (use `Context.Actor`), `snapshot/this.Wire.cs:89` (context-ful read registration; retire 2-arg `FromWire`), `module/crypto/code/Default.cs:22` (crypto module context; **body-only write** to avoid sign recursion).
- `View` keeps only the freshness role.

### 4. Stage 5 — authenticity (SECURITY — do carefully)
- `module/signing/code/Ed25519.VerifyAsync` step 6: assert `layer.Identity == action.Context.Actor.Identity` (navigate own context; no decomposed param).
- `module/signing/verify.cs`: add `Root` (bool, default false) — skip the actor-identity match for the root identity read.
- `module/identity/code/Default.cs`: identity-table read sets `verify.Root=true`; provider checks keypair self-consistency (`PublicKey` re-derives from `PrivateKey`).
- Bootstrap: load system identity (root mode) before any other `application/plang` read.
- Rename PLang var `%MyIdentity%` → `%Identity%` (`actor/this.cs:125`).

### 5. Stage 6 — invariant proof
- Tripwire in `Wire.ReadBody`: `if (_context == null && View == Store) throw`.
- Test-fixture sweep (~61 fixtures construct context-less serializers/apps/Variables — Stage 1-4 ctor changes broke test compilation: `new Variables()`, `new context.@this(app, …)`, `GetActor` tuple, `Sqlite(dbPath)`/`InMemory(name)`, `type.catalog.@this()`). A shared helper that supplies a context.
- Un-skip `DictTypedEntryRoundTripTests` (acceptance).
- Re-validate `plang build` fresh + cache-hit (rebuild from clean per the stale-binary rule).

## Notes
- Build production-only with `dotnet build PlangConsole -p:RunAnalyzers=false`; test projects don't compile until the S6 fixture sweep.
- `.From(source)` re-types an existing Data and forwards its context — NOT a context-less birth; leave those.
- `Data` ctor now: `_context = context ?? parent?._context!` — births with a `parent` inherit context; list/dict re-stamp children — so many "context-less" leaf births are actually covered.
