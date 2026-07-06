# Coder stages — settings/config unification

Build spec: `.bot/settings-config-unification/architect/plan.md` (settled).
Review + verified checks: `coder/coder-review.md`. Ordered for **green build + green tests at
every stage** (each is its own commit/push).

> **PIVOT (settled with Ingi) — read `coder/action-param-is-a-setting.md` first.**
> An action param IS the innermost scope of a setting. There is no config/policy/option — one
> concept, resolved `step → action-key → module-key → [Default]`. So the **generator seam is THE
> mechanism**; every module (math/http/llm/signing) falls out of it — no per-module resolvers,
> no `NumberPolicy`/`MathPolicy`/`Config` records. Revised order below: **seam FIRST**, then
> convert modules to it. (Diverges from the architect's number-caveat — flagged in that doc.)
>
> DONE so far: Stage 1 (`context.Setting` door) + app-default-tier collapse (settings live on
> the one context chain; `isDefault`/`Defaults` gone). Both pushed, zero regressions.

Legend: ☐ todo · ◐ in progress · ☑ done

**Baseline at branch start (HEAD `535fadb95`, `dev.sh full`): 141 failing** — Modules 11,
Types 30, Wire 18, Data 36, Generator 0, Runtime 46. Pre-existing (serialization/path/datetime/
compare), unrelated to settings. Each stage must add **zero** to the touched slice's count.
**Machine is slow — build/test are the productivity killers, not the edits.** Batch a whole
vertical slice's edits, then **ONE** build + **ONE** targeted test run at the slice boundary.
NEVER rebuild/test per small edit. No `dev.sh full` per stage — run the one affected slice's
binary directly, diff failure NAMES vs the FULL baseline (below). Build with
`-p:UseSharedCompilation=false` (prevents the VBCSCompiler runaway). Full only before final handoff.

**Corrected full baseline** (partial-capture "Modules 11" was wrong — real is 37):
Modules **37**/987, Types 30/727, Wire 18/494, Data 36/901, Generator 0/198, Runtime 45/786.

---

## Stage 1 — Rename the persistent store (`settings → setting`)  ☑ DONE (8b1be4ceb)
Isolated mechanical rename, no logic change. Do first: cheapest, frees vocabulary, no deps.
- `app/module/settings/ → app/module/setting/`; type `app.module.setting.@this`. ✓ (git-tracked rename)
- property `app.Settings → app.Setting`; navigable key `"Settings" → "setting"` (%setting.%). ✓
- `SettingsTable="settings"` (sqlite table, data compat) + `app.SettingsStore` + IStore/Sqlite mechanism unchanged. ✓
- Tests: namespace/type/navigable refs updated; `ResolveTableName` → `"setting"`. Zero new failures (5 SettingsData + 1 DataSource pre-existing, byte-identical to HEAD).
- `.goal`/`.pr` navigable migration OUT OF SCOPE per Ingi (422 legacy .pr use `%Settings.%`; separate plang-side pass).
- **Merged** settings-config-unification → cli-app-property-override (clean ff, 26 commits).

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

## http conversion — DONE (commit 60e439b23): all config values are per-request [Default] props;
## keyed client cache; configure dissolved. NEXT below: llm, signing.

Same shape as math, but bigger — do it as its own slice with fresh budget.

**UNIFORM model (Ingi): every config value is a property on the action with `[Default]`. No
"module-level direct read" case — it's all properties.** `request` already has TimeoutInSec,
ContentType, Encoding, Unsigned, Method as `[Default]` props. Add the rest as props the same way:
BaseUrl (`text?`, no default — absolute-only when unset), DefaultHeaders (`dict?`), FollowRedirects
(`@bool` `[Default(true)]`), MaxRedirects (`number` `[Default(10)]`), MaxResponseSize (`number`
`[Default(100*1024*1024)]`), MaxSSEBufferSize (`number` `[Default(10*1024*1024)]`) — and whatever
download/sse use (MaxDownloadSize). The seam resolves each `http.request.x → http.x → [Default]`.

Then in `http/code/Default.cs` (SendAsync ~78-85 + 2 siblings ~163,~204 + `ResolveUrl`/`MergeHeaders`):
delete every `config.Resolve("X", …)` and the dead `action.X == null ? …` ternaries — just read
`action.X`. Delete `config = For<Config>(…)`.

**Redirects — RESOLVED (Ingi): keyed client cache, redirects ARE per-request props (no exception).**
The single cached `_client` (`:335`) is why redirects looked client-level. Instead: cache one
HttpClient per distinct `(FollowRedirects, MaxRedirects)` combo, so redirects become ordinary
per-request properties AND we keep socket-reuse/pooling. Bounded by distinct combos actually used
(~1-2; `follow=false` normalizes to one key ignoring max).
- ONE method named **`Client`** (NOT `ClientFor` — `-For` is the verb/preposition smell), logic
  inlined (no `CreateClient` submethod):
  ```csharp
  private readonly Dictionary<(bool follow, int max), HttpClient> _clients = new();
  private HttpClient Client(bool follow, int max) {
      var key = follow ? (true, max) : (false, 0);
      if (!_clients.TryGetValue(key, out var c)) { c = _handler != null ? new(_handler) : new(new HttpClientHandler {
          AllowAutoRedirect = follow, MaxAutomaticRedirections = max }); _clients[key] = c; }
      return c;
  }
  ```
- `SendHttpAsync(msg, opt, config, ct)` → `(msg, opt, follow, max, ct)` → `Client(follow, max).SendAsync(...)`.
- Handlers read `follow=(await action.FollowRedirects.Value())!.Value`, `max=(await action.MaxRedirects.Value())!.ToInt32()`.
- **`configure` dissolves with NOTHING special** — no shared mutable client → the redirect-guard is
  gone (each combo has its own client). Delete `configure.cs` + `Configure()`; multi-set → `set %!http.request%={…}`.
- `ResolveUrl(url, baseUrl:string?, ctx)` (from `action.BaseUrl`); `MergeHeaders(headers, defaultHeaders:dict?)`
  (from `action.DefaultHeaders`). `_client`/`Dispose` → the `_clients` dict.
- Add the same config props to `download`/`upload` actions (they share the helpers).
- Then delete `http/Config.cs`, `For<T>`/`Apply`/`ModuleView`/`IConfig`. Update `RequestActionTests`
  (`Config.Set("http.X",…)` still writes `context.Setting` so reads keep working; drop `IConfig`/`Config` refs).
- Reads follow the `T?` convention (`(await …)!` / IsNull) — flag `// T? convention — plang-null pass converts this`.

**Then delete:** `http/Config.cs`, `For<Config>`/`ModuleView<Config>` params (~333,345,357,400),
`app.Config.For`/`Apply`/`ModuleView`/`IConfig`. **Dissolve `configure`** (`http/configure.cs` +
`Configure()` ~235): multi-set → `set %!http.request% = {dict}`; the redirect-lock guard moves onto
the followRedirects/maxRedirects **setter**. Update `RequestActionTests` (its `Config.Set("http.X",…,
isDefault:true)` still writes `context.Setting`, so direct reads keep working; remove `IConfig`/`Config`
type refs).

Then **llm** (`OpenAi.cs:65` `For<http.Config>` model read → the `%!llm.query.model%` cascade) and
**signing** (`Ed25519.cs:80` `For<Config>`) — smaller, same pattern. Then the `%!%` front door, the
settable-schema + build validation, the persistent-store rename.

## Deferred (NOT this branch)
- `--build={files}` crash fix (parent's Direct walk) · subsystem Direct leaf write · shared schema
  is the coupling point with parent.
