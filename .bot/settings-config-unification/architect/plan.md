# Setting — one concept (dissolve config / options / settings into one word)

**Branch:** `settings-config-unification` (cut from `cli-app-property-override`).
**From:** architect — settled with Ingi and folded the coder's review (`coder/coder-review.md`). This is the build spec.

> **You own the final code shape.** Every snippet, name, and signature here is a suggestion to make the design concrete — the mechanics are yours to improve. What's *binding* is the shape: one word `setting`; settings resolve through a scope chain that the generator wires into the action params; the whole `config` machinery dissolves; `%!%` is the setting sigil (read + write); `%setting.%` stays the separate persistent store. If a name reads as verb+noun or a shape smells, change it and tell me why.

## Settled this round (deltas from the version you reviewed)

- **`configure` → dissolve**, not keep. Backward-compat was the only argument for keeping it, and Ingi dropped it. Multi-set becomes `set %!node% = {dict}` (identical shape to `--module={dict}`); the one real behavior it carried (http's redirect-lock guard) moves onto that setting's setter.
- **`%!%` routing → schema membership, split by direction** (your Blocker A): a **write** (`set %!path%`, `--path`) is a setting — schema-validated, typo errors, no fallback. A **read** (`%!path%`) tries the schema, then falls back to the small/shrinking `!`-transient set. Settings get their **own door** (`context.Setting`); the variable resolver is not overloaded (your Q2).
- **Setting layer goes in the three *typed* generator branches only**, skipping `IsPlainData` (your Concern C).
- **The settable-schema is a first-class shared artifact** (your Concern B) — one source feeds the `%!%` router, build-time validation, and the parent's Direct walk.
- **Scope is chosen in the step** — `set %!x% = 20` is goal-scoped (default), `... on app` is app-wide; `--` is app. **Action-vs-module level is chosen by address depth**, with a **scope-primary** resolution cascade.
- **Q5/Q6 closed:** `math` is the sole cross-action reader; number-policy home is `math`.

---

## Why

Config / options / settings is **one domain smeared across three surfaces with three vocabularies**:

- `app.Config` (`app.config.@this`) — an in-memory scope registry, *misnamed* "config". It's the settings registry.
- `app.Settings` (`app.module.settings.@this`) — the persistent sqlite store.
- `IConfig` + `Config` records + `For<T>` / `Apply<TConfig>` / `Resolve<T>` / `ModuleView<T>` — the read/write API bolted around the registry.

To a plang developer, setting an http timeout, an llm model, and a build file list are *the same act* — "set a setting." Today they are three unrelated mechanisms. This branch collapses them to one concept, one sigil, one read path. It does **not** fix the `--build={files}` startup crash — that's the parent branch's Direct walk (lower→convert), explicitly out of scope here.

---

## The model

**One word: `setting`, singular.** A setting is a **public-settable, plang-typed property on an app-tree node**; its address *is* the node's location in the tree. No marker attribute — a public setter is the "settable" signal. The plang name always equals the C# folder; any divergence is a bug to fix.

**The path is read off the real tree — never designed.** You put the setting where the code that owns the behavior actually lives, and the path falls out. No **invented nodes** (`%!number.overflow%` when there is no `number` module), no relocating to a "cleaner" namespace, no custom paths. A setting consumed by exactly one module lives on that module (`math` owns overflow/precision); the "consumer vs owner" question only arises when a *separate, independently-addressable* node genuinely exists.

**Address depth is the level.** The action segment's presence tells you whether a setting is action-level or module-level — read straight off the tree, no per-property classification needed:

- `%!http.request.timeout%` — action segment present → **action-level**
- `%!http.timeout%` — action segment absent → **module-level** (applies to every http action)

Two sigils, **same vocabulary**, split by lifetime (the only split the developer sees):

| Sigil | Means | Written by | Persisted? |
|---|---|---|---|
| `%!path%` | in-memory setting | `--module={json}` at startup **or** `set %!path%` in code | no |
| `%setting.key%` | the store | code only (`set %setting.key%`) — **never** startup | yes (sqlite) |

`%!%` and `%setting.%` hold **unrelated values** — the llm module reading its own setting never sees `%setting.apikey%`. A developer who wants the stored key in a request types `Bearer: %setting.apikey%` themselves. Same word, two mechanisms.

**Two backends under `%!%`, invisible below the sigil:**

| Node kind | Backend | Read | Owner |
|---|---|---|---|
| action-param settings (`llm.query.model`, `http.request.timeout`) | scope chain | generated getter → `context.Setting` | **this branch** |
| module-level settings (`math.overflow`, `http.followRedirects`) | scope chain | `context.Setting` called directly, inline default | **this branch** |
| subsystem / root (`build.files`, `test.path`, `app.create`) | Direct live-object property | plain property access | **parent branch's walk** |

---

## Resolution & scope

**Reading a setting-backed value — precedence, high → low:**

1. the step's explicit value (`- request url, timeout=5`)
2. the `%!%` setting, walked **scope-primary**: for each context level `goal → parent → … → root`, take the **most-specific address that's set** at that level — action-key (`http.request.timeout`) before module-key (`http.timeout`)
3. the `[Default]` on the action param (the floor — and it can legitimately differ per action, e.g. `request` 30s vs `download` 300s)

**Scope-primary, specificity as the within-level tiebreak** (confirmed with Ingi). Locality wins: a goal-local setting shadows an app-level one, and *within* a level the more-specific address wins. This keeps the "goal shadows app / code wins over `--`" rule as the dominant one. (The CSS-style alternative — specificity always beats scope — was considered and rejected.)

**Where writes land:**

| Write | Scope | Reaches |
|---|---|---|
| `--http={request:{timeout:20}}` | app (root overlay) | everywhere |
| `- set %!http.request.timeout% = 20` | **goal (default)** | this goal + all subgoals |
| `- set %!http.request.timeout% = 20 on app` | app (root overlay) | everywhere |

`--` and `set … on app` land on the same root overlay; a bare `set %!x%` lands on the goal scope beneath it, so it shadows `--` — "code wins over `--`" falls out with no special-casing, and it answers the coder's entry-goal precedence note (`Start`'s `set` is goal-scoped, under root).

**Subgoal propagation is the up-walk, not the clone.** For "applies to all subgoals" to be timing-independent, a subgoal must resolve `this → Parent → root` at read time — not inherit a clone snapshot taken at spawn (`context/this.cs:362`), which would only carry settings set *before* the subgoal was spawned. Confirm the read path is the up-walk.

(The `on app` / `on goal` surface syntax is the builder's to parse; mechanically the `set` handler gets a scope arg, default `goal`.)

---

## The read side — the seam the generator already emits

Every action param already resolves through a fallback chain in the generated `Resolve()`. Today, for a `[Default]` param (`Emission/Property/Data/this.cs:101`): step value, else `[Default]`. The setting is the **cascade layer inserted in the middle** — the generator bakes both keys from what it knows (namespace `app.module.http` + action `request` + param → action-key `"http.request.timeout"`, module-key `"http.timeout"`):

```csharp
__local = !await __d.IsEmpty() ? __d.As<duration>()                                // 1. step value
    : context.Setting.Resolve("http.request.timeout", "http.timeout") is { Present: true } __s
        ? __s.As<duration>()                                                       // 2. %!% cascade (scope-primary)
        : new data.@this("Timeout", 30, context).As<duration>();                   // 3. [Default(30)] — exact literal
```

`Resolve(params keys)` (name is yours — must not be `GetSetting`) walks the context chain scope-outer, keys-inner, per the resolution rule above. **Inserted into the three *typed* branches only** — `IsNullable`, `DefaultValue`, `else` — **not** `IsPlainData` (`:86-93`). The plain-`Data` branch hands the ref through un-resolved on purpose (the Data-flows rule for polymorphic forwarders — `goal.call`, `llm.query`'s relay slots); a setting read there would force-resolve a relay value, and a plain `Data` slot has no `[Default]` and no single type anyway.

`context.Setting.Resolve` is the single scope-backed read entry; the generated getter is one caller, `MathPolicy` another (module-level, direct).

### What the read side deletes

The `request` handler today hand-layers step-over-config-over-hardcoded and reaches a separate `IConfig` record (`http/code/Default.cs:78`):

```csharp
var config = app.Config.For<Config>(action.Context);
var timeout = action.TimeoutInSec == null ? config.Resolve("TimeoutInSec", 30)
    : (await action.TimeoutInSec.Value())?.ToDouble() ?? 0;
```

After — read the param, already resolved through the cascade:

```csharp
var timeout = await action.Timeout.Value();
```

`http.Config`'s defaults move onto `request`'s params as `[Default(...)]`, where `query`'s already live (`[Default(0.0)] Temperature`). Same for llm: `Model` is nullable-no-default, and `OpenAi.cs:65` does the "fall back to provider settings" read — that read *becomes* the cascade (`%!llm.query.model%` **is** the provider default now), so `OpenAi.cs` stops reading config too.

---

## The `%!%` front door — schema routing

`%!%` is already a populated namespace, not a free sigil. It splits into two families, and the router uses that split:

- **Tree-nav / settings** — `!app`, `!callStack`, `!context`, and every new setting path. These *are* the app-tree; they route to the setting/nav side.
- **Flat runtime handles** — `!data` (special, stays), `!ServiceIdentity` (assume stays); `!buildData` and `!ask.answer` are retiring; `!build.cache` becomes real tree-nav to `app.Build.Cache` (the overload fix deletes the mirror). A small, shrinking set.

Routing, by **direction**:

- **Write** (`set %!path%`, `--path`) → **setting only.** Resolved against the settable-schema. Match ⇒ setting write (scope-backed or Direct); no match ⇒ **error** (typo caught at build time for `set`, at startup for `--`). No variable fallback — nothing writes a transient from plang/CLI (they're all C#-set via `Variable.Set("!…")`), so a `%!%` write is unambiguously a setting.
- **Read** (`%!path%`) → schema first (⇒ `context.Setting.Resolve`), else the flat `!`-handle store (⇒ `%!data%` etc. keep working).

This closes the typo hole (writes validate), needs zero migration of the transients, and walks toward the "everything is tree-nav" north star as the flat handles retire. Settings stay on their own resolver (`context.Setting`); the variable resolver is untouched.

---

## The settable-schema — one shared artifact (Concern B)

Build-time path validation is a **new capability** (there's no build-time variable-path validation today). It needs a schema built by reflecting the app tree: which nodes exist, which props have public setters, each prop's plang type, and **whether each leaf is scope-backed (action param) or Direct (subsystem)**. That schema is load-bearing for **three** consumers:

1. the `%!%` write router (settings vs transient),
2. build-time validation of `set %!path%` / `--` keys,
3. the parent branch's Direct walk (same "which props are settable" reflection).

Build it **once, shared** — three reflections would drift (OBP smell #3). Because the Direct leaves belong to the parent's tree, build-validating a subsystem path (`%!build.files%`) genuinely depends on the parent; the **shared schema is the real coupling point** between the branches, so design it as a first-class deliverable with its own tests. If it risks ballooning the branch, land the runtime setting-chain first and gate build-validation behind a follow-up — the read-path win doesn't depend on it.

---

## Leaf trace — every incumbent, its call sites, its disposition

### Construction seam (runtime — the generated action instance resolving its params)

| Incumbent | Where | Call sites | Disposition |
|---|---|---|---|
| `app.config.@this` (`Resolve<T>`, `For<T>`, `Apply<TConfig>`, `Set`, `Defaults`, `Cast`) | `app/config/this.cs` | http `Default.cs:78,163,204,243`; llm `OpenAi.cs:65`; signing `Ed25519.cs:80-83`; math `MathPolicy.cs:21` | **Dissolve.** Scope-walk moves onto `context.Setting.Resolve`; `Defaults` becomes the root context's overlay; `Apply`/`For`/`ModuleView` disappear. |
| `ModuleView<T>` | `app/config/ModuleView.cs` | http `Default.cs:333,345,357,400` | **Delete.** Handlers read their own params; module-level policy calls `context.Setting` directly. |
| `IConfig` (marker) | `app/config/IConfig.cs` | `http.Config`, `signing.Config`, `environment.number.Config` | **Delete.** Defaults move to `[Default]` on action params. |
| `Config` records | `http/Config.cs`, `signing/Config.cs`, `environment/number/Config.cs` | via `For<T>` above | **Delete each.** Per-property default → `[Default]` on the owning action's param (client-construction props → module-level, see audit). |
| `context.ConfigScope` | `actor/context/this.cs:145,362`; alias `GlobalUsings.cs:45` | scope reads/clone | **Rename → `context.Setting`** — drop "Scope" (names the container mechanism, not the concept; OBP smell #1). The in-memory settings that belong to this context, paralleling `app.Setting`. Resolving walks `context.Setting → Parent → … → root`. |
| `app/config/Scope.cs` | one scope level, keyed `"module.property"` | — | **Keep as the `setting` collection type, rehome + rekey.** Becomes the type behind `context.Setting`; key is the full tree path (`"http.request.timeout"`). |
| `app.Config` property | `app/this.cs` (`:162`) | the above | **Delete.** No in-memory registry survives. App-level in-memory default *is* the root context's `Setting` (where `--` writes); `app.Setting` stays purely the persistent store. |

### Validation seam (build time)

| Incumbent | Where | Disposition |
|---|---|---|
| `IConfigure<TConfig>` | `app/module/IConfigure.cs`; `Modules.GetDefaults` (`module/this.cs:542-551`) | **Delete.** `GetDefaults` keeps only its `[Default]`-attribute branch (`:565+`). |
| build-time `%!path%` validation | (new) | Resolve `%!path%` against the shared settable-schema; reject unknown paths. No LLM schema-teaching — assume a real path, validate post-parse. |

### The `%!%` front door

| Incumbent | Where | Disposition |
|---|---|---|
| `%!x%` as a `!`-prefixed variable | `Executor.cs:52,102`; `variable/list/this.cs`; `variable/this.cs:185-214` | **Reframe** into the schema router above: writes → setting (validated); reads → setting-then-transient. Retire `!buildData`/`!ask.answer` as they go; `!build.cache` becomes Direct tree-nav to `app.Build.Cache`. |
| `configure` action + `Configure()` | `http/configure.cs` (`: IConfigure<Config>`); `http/code/Default.cs:243` | **Dissolve.** Multi-set is `set %!http.request% = {timeout:60, …}` (same shape as `--http={request:{…}}`). The `Apply<Config>` sink is gone; the **redirect-lock guard** ("can't change FollowRedirects after first request") moves onto the `followRedirects`/`maxRedirects` setters, where the constraint belongs. |

### Persistent store (light touch — rename)

| Incumbent | Where | Disposition |
|---|---|---|
| `app.module.settings.@this` + `Sqlite`/`IStore`/`get`/`set`/`remove` | `app/module/settings/` | **Rename** folder `settings → setting`, type → `app.module.setting.@this`, property `app.Settings → app.Setting`, navigable `%setting.%`. Mechanism unchanged. |

---

## App-tree plang-type audit (every leaf a plang type, not raw CLR)

The `Config` records hold **raw CLR** today. Moving them to `[Default]` is the moment to type them as plang. The address-depth trick means the coder does **not** pre-classify each property action-vs-module — both addresses work via the cascade; the `[Default]` just lives on the action param it defaults. (The exception: client-construction props with no per-request param — `FollowRedirects`, `MaxRedirects`, `MaxResponseSize` — are module-level-only, read directly via `context.Setting.Resolve("http.followRedirects", default)` with an inline default, like `math`.)

| Setting | Today (CLR) | Should be (plang) |
|---|---|---|
| `http…timeout` | `int TimeoutInSec = 30` | `duration` — `[Default(30)]` (exact literal; seconds = the duration's canonical unit; drop `InSec`, name it `Timeout`) |
| `http…baseUrl` | `string? BaseUrl` | `path` or `text` — coder call |
| `http…maxResponseSize` / `maxSSEBufferSize` | `long` | `number` (bytes) — module-level |
| `signing.timeoutMs` | `long TimeoutMs = 300000` | `duration` — `[Default(300)]` (the old `300000` ms as the exact canonical-unit number) |
| `llm.query.model` | already `data<text>?` | already `text` ✓ |
| `math.overflow` / `math.precision` | CLR enums | stay enums (`OverflowMode`/`PrecisionMode` on `app.type.number`) — module-level |

**Number case — the cleanest collapse, and it fixes a drift.** `environment.number.Config` (`Overflow`, `Precision`) is a **cross-action policy** read by `MathPolicy.Resolve` (`math/code/Default.cs:36`) across every math op — not one action's param. It becomes a **module-level setting on `math`** (its sole consumer and real home), read directly with the default inline:

```csharp
Overflow = stepOverflow ?? context.Setting.Resolve("math.overflow").As<...>() ?? POverflow.Promote
```

`MathPolicy` already passes those fallbacks, so the record deletes with nothing behind it. **Deleting it fixes a live drift:** the record says `Precision = Double`; `MathPolicy` passes `Error` (with a comment arguing Error is right) — two defaults, already disagreeing; collapse to the inline `Error`. Path is `%!math.overflow%` — **not** `%!number.overflow%` (custom path, no `number` module) and **not** `%!environment.number.overflow%` (mismatch). The `number.*` key from the `ResolvePrefix` last-segment remap dies.

---

## Demolition worklist

**Dies — config machinery:**
- `app/config/` — `this.cs`, `IConfig.cs`, `ModuleView.cs`. `Scope.cs` **moves** (see stays).
- `app/module/IConfigure.cs`.
- `http/Config.cs`, `signing/Config.cs`, `environment/number/Config.cs`.
- `app.Config` property + its doc on `app/this.cs`.
- `For<T>`, `Apply<TConfig>`, `Resolve<T>`, `ModuleView<T>`, `Cast<T>`, `ResolvePrefix<T>`. (The collapse itself removes the `Apply`/`Resolve`/`For` verb+noun surface — a design win.)

**Dies — handler read sites (rewrite to read own params / call `context.Setting`):**
- `http/code/Default.cs` — the `For<Config>` + `Resolve(...)` layering at `:78,163,204,243`; `ModuleView` params at `:333,345,357,400`.
- `llm/code/OpenAi.cs:65` — the `For<http.Config>` model read.
- `signing/code/Ed25519.cs:80-83` — the `For<Config>` + `Resolve("TimeoutMs")` layering.
- `math/MathPolicy.cs:21` — `For<...number.Config>` → `context.Setting.Resolve("math.overflow"/"math.precision")`, defaults inline.
- `Modules.GetDefaults` `IConfigure` branch (`module/this.cs:542-551`).

**Dies — `configure`:** `http/configure.cs` (whole action) + `Configure()` in `http/code/Default.cs:243`. Multi-set → `set %!node% = {dict}`; redirect-lock guard → onto the leaf setters.

**Renames:**
- `context.ConfigScope → context.Setting` (drop "Scope"); `GlobalUsings.cs:45` alias. `Scope.cs` becomes the `setting` collection type behind it.
- `app/module/settings/ → app/module/setting/`; `app.Settings → app.Setting`; `%Setting.% → %setting.%`.

**Stays (do NOT demolish):**
- The **scope mechanism** — `Scope.cs`'s key-value + `Clone` + the context-`Parent` walk. Moves namespace, key deepens to the full path. The walk relocates from `config.Resolve` onto `context.Setting`.
- The **persistent store** — `Sqlite`, `IStore`, `get`/`set`/`remove`, the `%setting.%` navigable. Renamed folder only.
- **`[Default]`** attribute + its emission (`Fallback`/`DefaultRaw`). Gains a *sibling* (the cascade layer); does not change. Defaults stay **exact C# literals** — `[Default(30)]`, not `[Default("30 sec")]` — the same numeric path as the existing `[Default(16000)]`, no string-parse. The plang type's canonical unit gives the bare number its meaning; convert unit-specific old values (signing's `300000` ms → `300`).
- **`CommandLineParser`** (`!`-flag parsing). The `!`-flag → setting write is the new consumer.

**Deferred (NOT this branch):**
- The `--build={files}` crash fix (parent's Direct walk).
- Subsystem **Direct leaf write** for `set %!build.files%` — reuses the parent's walk; this branch builds the dispatch + hands subsystem leaves to it.
- Runtime subsystem toggle; the D-sniff inversions (parent `§6.C`/`§9`).

---

## Scope boundary

**This branch builds (self-contained, testable):**
1. Dissolve the config machinery; move `Config`-record defaults to `[Default]` / module-level, plang-typed.
2. `context.Setting.Resolve(keys)` — the scope-backed read entry (context-walk → root overlay), scope-primary + specificity tiebreak.
3. The **generator seam** — insert the cascade layer into the three typed param branches.
4. `%!path%` read + `set %!path%` write (scope side, with `on app`/`on goal`); `--module={json}` writes the root overlay at startup.
5. The **shared settable-schema** + the `%!%` write router.
6. Dissolve `configure`; guard onto the setter.
7. Rename the persistent store.

**Depends on the parent (`cli-app-property-override`):** the subsystem Direct backend + crash fix. This branch's `%!%` dispatch hands subsystem leaves to that walk, and shares the settable-schema with it. **Q4 settled: keep separate** — document the dependency, don't pull the parent's walk in (couples two review cycles + re-opens the deferred crash). The shared schema is the coupling to design now.

---

## OBP validation pass (new surfaces)

| Surface | Verb+noun scan | Object-decomposition scan | Verdict |
|---|---|---|---|
| `context.Setting` (collection) + `.Resolve(keys)` | `Setting` is a noun (paralleling `app.Setting`); `Resolve` is a real-work verb (scope walk), fine. Not `GetSetting`. | Returns `Data` **whole**; caller lowers at the leaf. No decomposition. | OK — **separate door** from the variable resolver (Q2 settled: no overload). |
| `context.Setting` vs `app.Setting` — same word, two owners | Both nouns; owner names the tier (context = in-memory/scoped, app = persistent). | two collections, different backing | OK — the parallel is the point. **Make the doc-comments load-bearing** so nobody reads `context.Setting[...]` and `app.Setting` across each other in C#. |
| `app.Setting` (rename of `app.Settings`) | Noun, collection node. | — | OK — singular per folder convention. |
| `[Default]` on action params | existing attribute | born from a literal with context | OK — unchanged (verify the duration round-trip). |
| The collapse removes `Apply`/`Resolve<T>`/`For`/`ModuleView` | the verb-heavy surface | — | **Win** — fewer verb+noun names after than before. |

No new abstraction for the `%!%` door beyond what's needed — the parent's "no `IAppTreeNavigator`" YAGNI was scoped "for a door that doesn't exist yet"; the door now exists, so a **minimal** dispatch is justified, not an interface layer.

---

## Resolved decisions (were the six open questions)

1. **`configure`** → **dissolve** (merits, once backward-compat dropped). Multi-set = `set %!node% = {dict}`; guard → setter.
2. **`context.Setting` vs the variable resolver** → **separate door.** One sigil to the developer, two resolvers under it, routed by schema.
3. **The `!` overload** → **schema routing, split by direction** (writes validate, reads fall back). Transient set is small and shrinking.
4. **Staging vs parent** → **keep separate**; document the dependency; the **shared schema** is the coupling to build now.
5. **Other cross-action settings?** → **No.** Only `MathPolicy` reads a non-param `Config`; http/llm/signing read their own params. (Coder confirmed against source.)
6. **Number-policy home** → **`math`.** Sole consumer, real node. `%!math.overflow%` / `%!math.precision%`.

**Remaining verification for the coder (not decisions — checks):**
- Subgoal propagation is the up-walk, not the clone snapshot (`context/this.cs:362`).
- No `.goal` in the tree does `- set %!transient%` (confirms "a `%!%` write is always a setting") — grep `set %!` across `.goal` files.
