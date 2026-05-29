# Stage 4: `number` — arithmetic + policy

**Goal:** Give `number` policy-aware arithmetic (overflow + precision axes via `app.config`), the divide/power promotion that keeps `7/2 = 3.5`, and retype the `math.*` actions to return `Data<number>` — deleting the scattered `MathHelper` kind-preservation.
**Scope.** *Included:* `NumberPolicy`, `number.Config : IConfig`, the `app.config` resolution, `this.Arithmetic.cs` (policy-aware `Add/Sub/Mul/Div/Mod/Pow`, `Data`-returning), `math.intdiv`, the `math.*` retype, deletion of `MathHelper.ToDouble`/`PreserveType`. *Excluded:* `list.*` numeric reducers can follow in the same spirit but aren't required to prove the stage; a settings UI for the config.
**Deliverables (per [plan/policy.md](plan/policy.md) + [plan/storage.md](plan/storage.md) Arithmetic):**
- `NumberPolicy.cs` — `readonly struct NumberPolicy { OverflowMode Overflow; PrecisionMode Precision }`, with `Lenient`/`Strict` presets. Enums `OverflowMode { Promote, Throw }`, `PrecisionMode { Double, Decimal }`.
- `Config.cs` — `sealed record Config : IConfig { OverflowMode Overflow = Promote; PrecisionMode Precision = Double }` under `app.modules.math.number` (or the agreed namespace).
- `this.Arithmetic.cs` — `static Data<number> Add(@this, @this, NumberPolicy)` and siblings. Catches `OverflowException`/`DivideByZeroException` internally → `Data.Fail("MathOverflow"/"DivideByZero")`. **Divide and Power leave the integer track**: `Int/Int → Decimal` (lenient) so `7/2 → 3.5`; `Power` promotes on negative/fractional exponents. Promotion table + integer-overflow widening in storage.md.
- `math/intdiv.cs` — truncating integer division (`7 intdiv 2 → 3`), the opt-in for the old C# semantics.
- `math/*.cs` retype — `Run()` returns `Data<number>`, reads policy via `Context.App.Config.For<number.Config>(Context)`, optional nullable `Overflow`/`Precision` step params (`?` is the optional marker — no `[Optional]`), calls the `Data`-returning `number.Add(...)`.
- Delete `MathHelper.ToDouble` / `MathHelper.PreserveType` at the end of the sweep.
**Dependencies:** Stage 3 (`number` value type). `app.config` (`PLang/app/config/this.cs`) already provides `For<T>(context)` + the `ConfigScope → parent → Defaults → record-default` walk.

## Design

> **You own the code.** [plan/policy.md](plan/policy.md) holds the resolver + handler shape; intent, not dictation.

**Policy reuses `app.config` — no new `environment` tree, nothing on `Goal`.** Scopes resolve through the existing walk: **step** (nullable action param) → **context** (`context.ConfigScope`, set by `- set math.number.overflow = throw`) → **parent contexts** → **app default** (`App.Config.Defaults`, `default: true`) → **record default**. Sub-goal inheritance falls out for free (the walk climbs `context.Parent`). `Goal` isn't guaranteed thread-safe, so nothing policy-related is stored there.

**Defaults are lenient** (`Promote`/`Double`) — "PLang sorts it." Strict (`Throw`/`Decimal`) is one `set` away for finance/crypto. The 18-digit-precision discussion (decimal carries ~28 digits; `Precision=Decimal` is the crypto escape hatch; `double` is the lossy boundary) is in policy.md.

**The divide footgun is the load-bearing behavior call:** `7 / 2` must not be `3`. `/` and `^` resolve out of the integer kinds by default; `math.intdiv` is the named opt-in for truncation. This was an architect's recommendation Ingi can still veto on read of the stage — if he wants C# integer-division semantics back, it's `Divide` sharing `Add`'s promotion and dropping `intdiv`.

**The handler relays `Data`, never throws.** `math.add.Run()` calls `number.Add(...)` which returns `Data<number>` (catching internally). An overflow becomes `Data.Error("MathOverflow")` the Lifecycle/Events `[OnError]` bindings can see — surface-uniform with file errors. Operators on `number` (Stage 3) still throw; they're the back-of-curtain path the named methods wrap.
