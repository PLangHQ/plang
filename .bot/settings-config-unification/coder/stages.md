# Coder stages — settings/config unification

Build spec: `.bot/settings-config-unification/architect/plan.md` (settled).
Review + verified checks: `coder/coder-review.md`. Ordered for **green build + green tests at
every stage** (each is its own commit/push).

Legend: ☐ todo · ◐ in progress · ☑ done

**Baseline at branch start (HEAD `535fadb95`, `dev.sh full`): 141 failing** — Modules 11,
Types 30, Wire 18, Data 36, Generator 0, Runtime 46. Pre-existing (serialization/path/datetime/
compare), unrelated to settings. Each stage must add **zero** to the touched slice's count.
**Machine is slow — do NOT run `dev.sh full` per stage; run `dev.sh test <Class>` + the one
affected slice, diff failure NAMES vs baseline.** Full only once before final handoff.

---

## Stage 1 — Rename the persistent store (`settings → setting`)  ☐
Isolated mechanical rename, no logic change. Do first: cheapest, frees vocabulary, no deps.
- `app/module/settings/ → app/module/setting/`; type `app.module.setting.@this`.
- property `app.Settings → app.Setting`; navigable `%setting.%` (was `%Setting.%`).
- `SettingsTable`/`IStore`/`Sqlite`/`get`/`set`/`remove` mechanism unchanged.
- **Green gate:** build + full test suite.

## Stage 2 — `context.Setting` + scope-primary `Resolve`  ☐
Internal rename + new read entry; `app.config` still alive, delegating.
- `context.ConfigScope → context.Setting` (`actor/context/this.cs:145,362`; alias `GlobalUsings.cs:45`).
- `config/Scope.cs` → the `setting` collection type behind `context.Setting`; rekey to full path.
- add `context.Setting.Resolve(params keys)` — walk `this → Parent → root`, scope-outer/keys-inner,
  scope-primary with specificity (action-key before module-key) as within-level tiebreak.
- `app.config.Resolve<T>` temporarily delegates to the new walk (kept until Stage 4).
- **Green gate:** build + tests (behavior unchanged).

## Stage 3 — Generator cascade seam (three typed branches)  ☐
Insert the setting layer into `IsNullable` / `DefaultValue` / `else` in
`Emission/Property/Data/this.cs`; **skip `IsPlainData`** (Concern C). Generator bakes action-key
+ module-key from namespace+action+param. No settings set yet ⇒ layer is passthrough.
- **Green gate:** build (regen) + tests unchanged; inspect a generated `.cs` to confirm the layer.

## Stage 4 — Dissolve config machinery + move defaults to `[Default]`  ☐
The big cut (depends on 2+3).
- move `http.Config` / `signing.Config` / `environment.number.Config` defaults → `[Default]` on
  action params (plang-typed: `int→duration`, `long→number`, exact literals `[Default(30)]`);
  client-construction/module-level props → direct `context.Setting.Resolve` + inline default.
- rewrite readers: `http/code/Default.cs` (:78,163,204,243 + ModuleView params), `llm/OpenAi.cs:65`,
  `signing/Ed25519.cs:80-83`, `math/MathPolicy.cs:21` (→ `%!math.overflow/precision%`, fixes
  Double/Error drift).
- delete `app/config/{this,IConfig,ModuleView}.cs`, `module/IConfigure.cs`, the 3 `Config` records,
  `app.Config` property, `GetDefaults` IConfigure branch (`module/this.cs:542-551`).
- **Green gate:** build + tests; confirm math/http/llm/signing defaults still resolve.

## Stage 5 — `%!%` front door: read/write + startup overlay  ☐
- `set %!path%` write (scope side, `on app`/`on goal`, default goal); `%!path%` read (schema →
  else flat `!`-handle).
- `--module={json}` → root overlay at startup (`Executor`).
- write-router: setting-only + **reserved `!ask` sentinel** carve-out; non-reserved no-match ⇒ error.
- drop `NegationPrefixStillParses.test.goal` (C# `VariableResolveTest.cs:16` covers it).
- **Green gate:** build + tests + a `%!http.request.timeout%` round-trip test.

## Stage 6 — settable-schema + build-time validation  ☐  (gate-able as follow-up)
Shared settable-schema (reflect app tree: nodes, public setters, plang type, scope-vs-Direct tag).
Build-time reject of unknown `set %!path%` / `--` keys. Shared with parent's Direct walk.
- **Green gate:** build + tests + a typo-path build-error test.

## Stage 7 — Dissolve `configure`  ☐
Delete `http/configure.cs` + `Configure()` (`http/code/Default.cs:243`); redirect-lock guard →
onto `followRedirects`/`maxRedirects` setters. Multi-set is `set %!http.request% = {dict}`.
- **Green gate:** build + tests.

---

## Deferred (NOT this branch)
- `--build={files}` crash fix (parent's Direct walk) · subsystem Direct leaf write · shared schema
  is the coupling point with parent.
