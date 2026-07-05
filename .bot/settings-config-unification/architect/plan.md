# Setting — one concept (dissolve config / options / settings into one word)

**Branch:** `settings-config-unification` (cut from `cli-app-property-override`).
**From:** architect, reshaped with Ingi across the design conversation. For coder review.

> **You own the final code shape.** Every snippet, name, and signature here is a suggestion to make the design concrete — the mechanics are yours to improve. What's *binding* is the shape: one word `setting`; action-param settings resolve through a scope chain via one generated line; the whole `config` machinery dissolves; `%!%` is the uniform in-memory setting sigil; `%setting.%` stays the separate persistent store. If a name reads as verb+noun or a shape smells, change it and tell me why.

---

## Why

Config / options / settings is **one domain smeared across three surfaces with three vocabularies**:

- `app.Config` (`app.config.@this`) — an in-memory scope registry, *misnamed* "config". It's the settings registry.
- `app.Settings` (`app.module.settings.@this`) — the persistent sqlite store.
- `IConfig` + `Config` records + `For<T>` / `Apply<TConfig>` / `Resolve<T>` / `ModuleView<T>` — the read/write API bolted around the registry.

To a plang developer, setting an http timeout, an llm model, and a build file list are *the same act* — "set a setting." Today they are three unrelated mechanisms:

```
- set %!http.request.timeout% = 30 sec     // wants app.Config scope — but nothing wires %!% to it
- set %!llm.query.model% = "claude"          // same
- set %!build.files% = "a.goal", "b.goal"    // a !-prefixed variable that mirrors app.Build.Files
```

The developer should think **one thing: a setting.** This branch collapses the three surfaces into one concept, one sigil, one read path. It does **not** fix the `--build={files}` startup crash — that's the parent branch's Direct walk (lower→convert), explicitly out of scope here.

---

## The model (settled with Ingi)

**One word: `setting`, singular.** No `config`, `options`, `configuration`. A setting is a **public-settable, plang-typed property on an app-tree node**; its address *is* the node's location in the tree. No marker attribute — a public setter is the "settable" signal. The plang name always equals the C# folder; any divergence is a bug to fix, not a mapping to support.

**The path is read off the real tree — never designed.** You don't pick the semantically-purest home; you put the setting where the code that owns the behavior actually lives, and the path falls out. No **invented nodes** (`%!number.overflow%` when there is no `number` module), no relocating to a "cleaner" namespace, no custom paths. A setting consumed by exactly one module lives on that module (`math` owns overflow/precision — see the number case below); the "consumer vs owner" question only arises when a *separate, independently-addressable* node genuinely exists.

Two sigils, **same vocabulary**, split by lifetime (this split is the only thing the developer sees):

| Sigil | Means | Written by | Persisted? |
|---|---|---|---|
| `%!path%` | in-memory setting | `--module={json}` at startup **or** `set %!path%` in code | no |
| `%setting.key%` | the store | code only (`set %setting.key%` / settings action) — **never** startup | yes (sqlite) |

`%!%` and `%setting.%` hold **unrelated values** — the llm module reading its own setting never sees `%setting.apikey%`. A developer who wants the stored key in a request types `Bearer: %setting.apikey%` themselves. Same word, two mechanisms.

**Two backends under `%!%`, invisible below the sigil** (Ingi: "`set %!build.files%` and `set %!llm.query.model%` — for him it's just a setting"):

| Node kind | Backend | Read | Owner |
|---|---|---|---|
| action-param settings (`llm.query.model`, `http.request.timeout`) | scope chain | generated getter → `context.Setting(path)` | **this branch** |
| module-level policy (`number.precision`) | scope chain | `context.Setting(path)` called directly | **this branch** |
| subsystem / root (`build.files`, `test.path`, `app.create`) | Direct live-object property | plain property access | **parent branch's walk** |

**Precedence for reading a setting-backed value** (high → low): the step's explicit value → `%!%` (goal scope → parent → root) → the `[Default]` literal. `%!%` set in code wins over `--` because code runs after startup and writes the same chain (goal shadows root).

---

## The read side — the seam is one line the generator already emits

Every action param already resolves through a fallback chain in the generated `Resolve()`. Today, for a `[Default]` param (`Emission/Property/Data/this.cs:101`):

```csharp
__local = await __d.IsEmpty()
    ? new data.@this("TimeoutInSec", 30, context).As<number>()   // [Default] literal
    : __d.As<number>();                                          // step's explicit value
```

Two layers. The setting is **one layer inserted in the middle** — the generator bakes the path key from what it already knows (namespace `app.module.http` + action `request` + param → `"http.request.TimeoutInSec"`):

```csharp
__local = !await __d.IsEmpty() ? __d.As<number>()                          // 1. step value
    : context.Setting("http.request.TimeoutInSec") is { Present: true } __s
        ? __s.As<number>()                                                 // 2. %!% / -- (scope chain)
        : new data.@this("TimeoutInSec", 30, context).As<number>();        // 3. [Default]
```

Inserted into all four param branches (nullable, default, plain, code) in `Emission/Property/Data/this.cs` so **every** settable param gets the chain. `context.Setting(path)` is the single scope-backed read entry; the generated getter is one caller, `MathPolicy` (below) is another.

### What the read side deletes

The `request` handler today hand-layers step-over-config-over-hardcoded and reaches a separate `IConfig` record (`http/code/Default.cs:78`):

```csharp
var config = app.Config.For<Config>(action.Context);
var timeout = action.TimeoutInSec == null ? config.Resolve("TimeoutInSec", 30)
    : (await action.TimeoutInSec.Value())?.ToDouble() ?? 0;
```

After — read the param, already resolved through the chain:

```csharp
var timeout = await action.Timeout.Value();
```

`http.Config`'s ten defaults move onto `request`'s params as `[Default(...)]`, where `query`'s already live (`[Default(0.0)] Temperature`, `[Default(16000)] MaxTokens`). Same for llm: `Model` is nullable-no-default, and `OpenAi.cs:65` does the "fall back to provider settings" read — that read *becomes* the setting chain (`%!llm.query.model%` **is** the provider default now), so `OpenAi.cs` stops reading config too.

---

## Leaf trace — every incumbent, its call sites, its disposition

### Construction seam (runtime — the generated action instance resolving its params)

| Incumbent | Where | Call sites | Disposition |
|---|---|---|---|
| `app.config.@this` (`Resolve<T>`, `For<T>`, `Apply<TConfig>`, `Set`, `Defaults`, `Cast`) | `app/config/this.cs` | http `Default.cs:78,163,204,243`; llm `OpenAi.cs:65`; signing `Ed25519.cs:80-83`; math `MathPolicy.cs:21` | **Dissolve.** The scope-walk logic moves onto `context.Setting(path)`; `Defaults` becomes the root context's overlay; `Apply`/`For`/`ModuleView` disappear. |
| `ModuleView<T>` | `app/config/ModuleView.cs` | http `Default.cs:333,345,357,400` (param type) | **Delete.** Handlers read their own params; cross-cutting policy calls `context.Setting` directly. |
| `IConfig` (marker) | `app/config/IConfig.cs` | `http.Config`, `signing.Config`, `environment.number.Config` | **Delete.** Defaults move to `[Default]` on action params. |
| `Config` records | `http/Config.cs`, `signing/Config.cs`, `environment/number/Config.cs` | via `For<T>` above | **Delete each.** Per-property default → `[Default]` on the owning action's param. (See number caveat below.) |
| `context.ConfigScope` | `actor/context/this.cs:145,362`; alias `GlobalUsings.cs:45` | scope reads/clone | **Rename → `context.Setting`** — drop "Scope" (it names the container mechanism, not the concept; OBP smell #1). The in-memory settings that belong to this context, paralleling `app.Setting`. Mechanism stays (per-goal overlay, clone-on-child). Resolving a path walks `context.Setting → Parent.Setting → … → root context.Setting`. |
| `app/config/Scope.cs` | one scope level, keyed `"module.property"` | — | **Keep as the `setting` collection type, rehome + rekey.** Becomes the type behind `context.Setting`; key is the full tree path (`"http.request.timeoutinsec"`). |
| `app.Config` property | `app/this.cs` (`Navigation:` doc `:162`) | the above | **Delete.** No in-memory registry object survives. App-level in-memory default *is* the root context's `Setting` (where `--` writes); `app.Setting` stays purely the persistent store. |

### Validation seam (build time — the builder computing param defaults + validating paths)

| Incumbent | Where | Disposition |
|---|---|---|
| `IConfigure<TConfig>` | `app/module/IConfigure.cs`; consumed by `Modules.GetDefaults` (`module/this.cs:542-551`) | **Delete.** `GetDefaults` keeps only its `[Default]`-attribute branch (the `else` at `:565+`). `http.configure : IConfigure<Config>` loses the interface. |
| build-time validation of `set %!path%` | (new) | The builder must resolve `%!path%` against the **real app-tree settable schema** (which nodes exist, which props have public setters, each type) and reject unknown paths. No LLM schema-teaching — assume the dev wrote a real path, validate it post-parse. This is the load-bearing requirement behind "`%!%` is build-validated, `--` is runtime-validated." |

### The `%!%` write/read front door

| Incumbent | Where | Disposition |
|---|---|---|
| `%!x%` as a `!`-prefixed **variable** | `Executor.cs:52,102`; `variable/list/this.cs` (skips `!` keys); `variable/this.cs:185,198,214` | **Reframe.** `%!path%` becomes the setting sigil (read+write), dispatching Direct (subsystem node) vs scope (action param). `set %!path%` writes the goal `SettingScope` for scope-backed paths; hands subsystem-node leaves to the parent's Direct walk. (Open Q3: the `!`-transient overload, `%!buildData%`.) |
| `configure` action (multi-set) | `http/configure.cs` | **Open Q1** — dissolve into individual `set %!http.request.*%`, or keep as multi-set sugar that writes `context.Setting`. |

### Persistent store (light touch — rename for vocabulary)

| Incumbent | Where | Disposition |
|---|---|---|
| `app.module.settings.@this` + `Sqlite`/`IStore`/`get`/`set`/`remove` | `app/module/settings/` | **Rename folder `settings → setting`**, type → `app.module.setting.@this`, property `app.Settings → app.Setting`, navigable prefix `%setting.%`. Mechanism unchanged. |

---

## App-tree plang-type audit (every leaf must be a plang type, not raw CLR)

The `Config` records hold **raw CLR** today (OBP/leaf-type smell). Moving them to `[Default]` on action params is the moment to type them as plang:

| Setting | Today (CLR) | Should be (plang) |
|---|---|---|
| `http.request.timeout` | `int TimeoutInSec = 30` | `duration` — `[Default("30 sec")]`, named `Timeout` (drop `InSec`; the type carries the unit) |
| `http.request.baseUrl` | `string? BaseUrl` | `path` (an http path) or `text` — coder call |
| `http.request.maxResponseSize` / `maxSSEBufferSize` | `long` | `number` (bytes) |
| `signing.timeoutMs` | `long TimeoutMs` | `duration` |
| `llm.query.model` | `string?` (already `data<text>?`) | already `text` ✓ |
| `environment.number.*` (precision policy) | CLR | `number` / plang types |

**Number caveat for the coder — the cleanest collapse of the three, and it fixes a drift.** `environment.number.Config` (`Overflow = Promote`, `Precision = Double`) is a **cross-action policy** read by `MathPolicy.Resolve` (`math/code/Default.cs:36`) across every math op — not one action's param. It does **not** map to a per-action `[Default]` (no single owning action; `math.add`/`math.subtract`/… share the one knob). It becomes a **module-level setting on `math`** (its sole consumer and real home), read directly, default inline at the read site:

```csharp
Overflow = stepOverflow ?? context.Setting["math.overflow"].As<...>() ?? POverflow.Promote
```

`MathPolicy` already passes those fallbacks (`view.Resolve("overflow", Promote)`), so the `Config` record deletes with nothing left behind. **Deleting it fixes a live drift:** the record declares `Precision = Double`; `MathPolicy`'s fallback passes `Error` (with a comment arguing Error is correct). Two defaults for one setting, already disagreeing — collapse to `MathPolicy`'s inline `Error`. The step-override params (`stepOverflow`/`stepPrecision`) stay as the action handlers' nullable params (layer 1); `MathPolicy` consults them, then the setting, then the inline default. This is the model's proof it must cover node-level settings, not only action-param ones ("a setting is a settable property on any node").

**Path home = `math`, read off the real tree.** The path is not designed for semantic purity — it's read off where the owning code actually lives. `%!number.overflow%` is a **custom path** (there is no `number` module; `app.type.number` is a type, not a node) — invented, forbidden. `%!environment.number.overflow%` is a mismatch (`environment` is env/run config; the `Config` record floats in that namespace). The setting's real home is `math` — the module that holds the arithmetic behavior and its **sole consumer** (`MathPolicy` is the only reader). Here the **consumer is the owner**, and that does *not* violate the parent's tree-mirror rule: that rule only bites when a *separate, independently-addressable* node exists (callstack had `--callstack` to flow to); overflow/precision has no such node. So `%!math.overflow%` / `%!math.precision%`; the `number.*` key from the `ResolvePrefix` last-segment **remap** dies.

---

## Demolition worklist

**Dies — config machinery (delete outright):**
- `app/config/` — `this.cs` (the `@this` registry), `IConfig.cs`, `ModuleView.cs`. `Scope.cs` **moves** (see stays).
- `app/module/IConfigure.cs`.
- `http/Config.cs`, `signing/Config.cs`, `environment/number/Config.cs` (the three `IConfig` records).
- `app.Config` property + its `Navigation:` doc on `app/this.cs`.
- `For<T>`, `Apply<TConfig>`, `Resolve<T>`, `ModuleView<T>`, `Cast<T>`, `ResolvePrefix<T>` — the whole verb-heavy read/write API. (Note: the *collapse itself* removes the `Apply`/`Resolve`/`For` verb+noun surface — a design win, not just a rename.)

**Dies — handler read sites (rewrite to read own params / call `context.Setting`):**
- `http/code/Default.cs` — the `For<Config>` + `Resolve(...)` layering at `:78,163,204,243` and `ModuleView` params at `:333,345,357,400`.
- `llm/code/OpenAi.cs:65` — the `For<http.Config>` model read.
- `signing/code/Ed25519.cs:80-83` — the `For<Config>` + `Resolve("TimeoutMs")` layering.
- `math/MathPolicy.cs:21` — `For<...number.Config>` → `context.Setting["math.overflow"/"math.precision"]` directly, defaults inline (deletes `environment.number.Config`; rehomes the policy onto `math`, its sole consumer; fixes the Double/Error drift).
- `Modules.GetDefaults` `IConfigure` branch (`module/this.cs:542-551`).

**Dies — the `IConfig` interface on `http.configure`** (Q1 decides configure's fate).

**Renames:**
- `context.ConfigScope → context.Setting` (drop "Scope"); `GlobalUsings.cs:45` alias. `Scope.cs` becomes the `setting` collection type behind it.
- `app/module/settings/ → app/module/setting/`; `app.Settings → app.Setting`; `%Setting.% → %setting.%`.

**Stays (explicit — do NOT demolish):**
- The **scope mechanism** — `Scope.cs`'s key-value + `Clone` + the context-`Parent` walk. It's correct; it moves namespace and its key format deepens to the full path. The *walk across levels* relocates from `config.Resolve` onto `context.Setting`.
- The **persistent store** — `Sqlite`, `IStore`, `get`/`set`/`remove`, the `%setting.%` navigable. Renamed folder only.
- **`[Default]`** attribute + its generator emission (`Emission/Property/Data/this.cs` `Fallback`/`DefaultRaw`). It gains a *sibling* (the setting layer), it does not change.
- The context **`Parent` chain + clone-on-child** (`actor/context/this.cs:362`) — how goal scope inherits.
- **`CommandLineParser`** (`!`-flag parsing). The `!`-flag → setting write is the new consumer; parsing is unchanged.

**Deferred (NOT this branch):**
- The `--build={files}` crash fix (parent's Direct walk, lower→convert).
- Subsystem **Direct leaf write** for `set %!build.files%` — reuses the parent's walk when it lands; this branch builds the dispatch, not the parent's leaf mechanism.
- Runtime subsystem toggle; the D-sniff inversions (both parent `§6.C`/`§9`).

---

## Scope boundary (so the coder can push back on the split)

**This branch builds, buildable + testable on its own:**
1. Dissolve the config machinery (the deletes above); move `Config`-record defaults to `[Default]` on action params (plang-typed).
2. `context.Setting(path)` — the scope-backed read entry (context-walk → root overlay). Rename `ConfigScope → SettingScope`.
3. The **generator seam** — insert the setting layer into all four param branches.
4. `%!module.action.param%` read + `set %!module.action.param%` write (scope side); `--module={json}` writes the root overlay at startup.
5. Rename the persistent store (`settings → setting`).

**Depends on / defers to the parent (`cli-app-property-override`):** the subsystem Direct backend + the crash fix. This branch's `%!%` dispatch is *built to hand* subsystem-node leaves to that walk, so the Direct side of the uniform surface lights up when the parent's Stage 3 lands. **Open Q4:** does the coder pull the parent's walk into this branch to make `%!build.files%` demonstrable here, or keep them separate?

---

## OBP validation pass (new surfaces)

| Surface | Verb+noun scan | Object-decomposition scan | Verdict |
|---|---|---|---|
| `context.Setting` (the collection) + its path read | `Setting` is a noun — the settings that belong to this context, paralleling `app.Setting`. Not verb+noun. The read door (chain-walk by path) is the coder's to name — indexer `context.Setting["…"]` or a `Resolve`-style method; whatever it is must not be `GetSetting`. | Returns `Data` **whole**; caller lowers at the leaf (`__s.As<T>()`). No decomposition. | OK — but **consider routing the read through the existing `%!%` variable resolver** (`variable.Get` already dispatches `!` keys) instead of a new door, since "one concept to the developer" argues for one resolution path. Coder call (open Q2). |
| `context.Setting` vs `app.Setting` — same word, two owners | Both nouns; owner names the tier (context = in-memory/scoped, app = persistent). | two collections, different backing | OK — the parallel is the point; `%!%` → `context.Setting`, `%setting.%` → `app.Setting`. |
| `app.Setting` (rename of `app.Settings`) | Noun. Store facade / collection node. | — | OK — singular per the folder convention. |
| `[Default]` on action params | existing attribute, no new name | value born from a literal with context (existing `DefaultRaw` path) | OK — unchanged |
| The collapse removes `Apply`/`Resolve`/`For`/`ModuleView` | these were the verb-heavy surface | — | **Win** — fewer verb+noun names after than before |

No new interface for the `%!%` door beyond what's needed (honoring the parent's YAGNI — but note that YAGNI was scoped "for a door that doesn't exist yet"; the door now exists, so a **minimal** navigator/dispatch is justified, not an `IAppTreeNavigator` abstraction layer).

---

## Open questions for the coder

1. **`configure` action** — dissolve into individual `set %!http.request.*%`, or keep as multi-set sugar writing `context.Setting`? (`- configure http, timeout 60, baseurl "..."` sets several at once; `set %!%` is one-at-a-time.)
2. **`context.Setting` vs the `%!%` variable resolver** — new method, or extend the existing `!`-key variable resolution so there's one read door? (Leaning: whichever keeps "one concept" honest without conflating settings with variables.)
3. **The `!` prefix overload** — `%!buildData%` (a transient build-pass result handle) and `%!build.cache%` (a subsystem mirror) are `!`-prefixed *variables* today. If `%!path%` now navigates the setting tree, does it navigate-then-fallback-to-variable, or do the system-transients move off `!`?
4. **Staging vs the parent** — pull the parent's Direct walk into this branch (so `%!build.files%` is demonstrable end-to-end here), or keep the subsystem side as a documented dependency?
5. **Sanity-check the leaf trace** — is `environment.number.Config` really the only cross-action (module-level, non-param) setting, or are there others hiding behind `For<T>` that don't map to a single action's `[Default]`?
6. **Number-policy home = `math`** (settled with Ingi). `MathPolicy` is the sole consumer and `math` is the real module node; `%!number.overflow%` is a custom path (no `number` module) and `%!environment.number.overflow%` is a mismatch. Rehome `Overflow`/`Precision` onto `math` as `%!math.overflow%` / `%!math.precision%`. Coder: confirm `math` is genuinely the only reader before rehoming (ties to Q5's sweep).
