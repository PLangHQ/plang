# CLI as app-property override — settled plan

**Branch:** `cli-app-property-override` (off `variable-as-value`).
**From:** architect. Supersedes `coder/plan.md` — reshaped with Ingi across the review. One flagged decision remains (§9, D-interim); everything else is settled.

**You own the final code shape.** Every snippet here is a suggestion to make the design concrete — names, signatures, and mechanics are yours to improve. What's binding is the *shape*: nullable born-with-context subsystems, access-level exposure, no shorthands, validation on types, and the ambient/session split behind the `!= null` sites.

## Why

`plang '--build={"files":["Scratch/Hello.goal"]}'` crashes at startup — `InvalidCastException: String cannot lower to this` — before any build runs. `Populate` lifts each raw string to a `text` value then *lowers* it to `Build.Files`'s element type `path`; the lower door is terminal, and `text`→`path` is a *convert*, not a lower. That's the whole bug. Fixing it opens a cleaner model: a CLI flag is an app-root property the console user overrides, and the value builds itself through the type system. That replaces the four-way `--build`/`--debug`/`--test`/`--app` branch and the JSON→CLR→plang→CLR double-parse with one uniform path.

## 1. The model

Every `--flag` names a public-settable property on the app root. The value builds via the conversion catalog (the type owns "make me from this raw value"). Nested JSON keys set sub-properties.

```
--build={"files":[...], "cache":false}
   → app.Build                     (property named after the flag, case-insensitive)
   → Files (List<path>) ← convert ["..."] element-wise → [path(...)]  (string→path via path.Resolve)
   → Cache (bool)       ← false
```

**The flag path is the app-tree path — exactly.** A flag maps to wherever a property actually lives in the app object graph; nothing is remapped, folded, or shorthanded. `app.CallStack.Flags` is reached by `--callstack={"flags":{...}}` (or `%!callstack.flags%`), because `CallStack` is a top-level app node — *not* by folding callstack config under `--debug`. Any "this flag really configures that other node" is a special case, and special cases are what this design removes. One consequence up front: `Debug` stops writing `CallStack.Flags` (it does today) — that cross-node write is exactly the special case; callstack config flows through `--callstack`.

Two stages, no reordering of bootstrap:

- **`CommandLineParser.Parse` stays syntactic** — argv → `{flag → raw JSON}`. It validates JSON and hands back a raw CLR tree (strings/dicts/lists). No context, no plang types — there is no app yet, and a value can't be born without context.
- **The walk runs in `Configure`, after the engine is built.** For each `!`-flag it finds the app property, and at each leaf converts raw→plang via the conversion catalog where the property's declared type is a plang type (not all are; the set grows over time). User variables (non-`!` bareword args) route exactly as today — the walk only replaces the `!`-flag branch.

### Where the walk runs (the open §1 from `coder/plan.md`): option (A), decisively

No flag configures app *construction*. The startup directory is chosen from raw argv in `PlangConsole/Program.cs` (`GetCurrentDirectory` matches `/apps/...`) *before* the `Executor` exists; the app ctor takes only `startupDirectory` + `autoWireConsoleChannels`. Every flag configures the already-built app. So (B)/(C) reorder a bootstrap that doesn't need reordering. Keep the parser type-blind; the walk is app-aware and runs post-build — which is where config apply already is.

The walk is **app-owned**, not a static reaching in. Drop `catalog.@this.Populate(target, dict, context)` (a static that reflects and mutates another object's properties from outside is OBP smell #1). The app applies its own config; each subsystem is born with context and populated through the catalog's convert path.

**Write the walk cohesively — navigate → convert → public-setter-gate — not welded into argv parsing.** A future plang `set %!cache.name% = 'redis'` (`%!...%` resolving to the app-property namespace) would land on the same write path, so keeping the navigation as its own step rather than tangled into flag-parsing leaves that door open. This is just clean factoring — **do not add an interface/abstraction layer *for* the hypothetical plang door.** If the CLI-clean shape turns out to be callable from elsewhere, reuse falls out; that's the only bar. No `IAppTreeNavigator` for a door that doesn't exist (§8's YAGNI applies to the walk too).

## 2. Subsystems become nullable, born with context

`app.Build`, `app.Debug`, `app.Tester` become `public T? { get; set; }`, default **null**. **Presence is the enable signal** — there is no `IsEnabled` field anywhere.

```csharp
public Build? Build { get; set; }   // null = off; non-null = on
```

- The ctor takes context; everything flows from it (`context.App`, `context.Actor`, `context.App.User`). Matches the born-with-context discipline on the parent branch. Drop the `_app` field — reach it as `Context.App`.
- Bare `--build` → `app.Build = new Build(context)`; the ctor sets its own defaults.
- `--build={...}` → `new Build(context)`, then the walk populates its sub-properties.
- No wasted construction — nothing exists until a flag asks for it.

**Startup-only for this branch — no runtime toggle.** The setters are public (the walk assigns them once in `Configure`), but this branch does **not** claim or support toggling a subsystem on/off mid-run. Runtime debug toggling has real depth the plan can't hand-wave: debug registers persistent plang `EventBinding`s + C# watch delegates that close over the instance, there's no teardown/unregister path today, and a forever-running app that toggles wants *suspend* (keep the watches) not *teardown* (drop everything) — two operations one null/non-null switch can't express. Deferred to its own design (todo: "Debugger runtime toggle: suspend vs teardown semantics"). So activation is **born once** at startup — which makes dropping `_applied` honest (no re-activation to guard). Leaf config from plang (`set %!cache.name%`) is unaffected; only whole-subsystem toggling is out.

The walk constructs `new T(context)` uniformly; one ctor contract for every configurable subsystem, `context` the only argument.

## 3. Exposure is the access level — no marker

C# already names the three categories; use them instead of an attribute:

- `public set` → CLI-configurable (and runtime-settable).
- `internal set` → framework sets it at runtime; **CLI can't**.
- `private set` → owner-only.

The walk filters on `prop.SetMethod?.IsPublic == true` — not merely `CanWrite`. Running in-assembly it *could* reach `internal` setters; the public-only filter is what makes `internal set` mean "runtime yes, CLI no." A flag with no matching public-setter property → hard error at startup (the locked typo rule).

App-root setter audit:

| Property | Today | Set to | Why |
|---|---|---|---|
| `Build` / `Debug` / `Tester` | `{ get; }` | **public set** (nullable) | the point — CLI + runtime toggle |
| `Create` | public set | **public set** | `--app={"create":true}` |
| `Environment` | public set | **public set** | `--environment=prod`; grows into a rich object later |
| `Culture` | public set | **internal set** | folds into the future `Environment` object; not its own flag |
| `Name` | public set | **internal set** | app identity — framework writes it |
| `Id` / `Created` / `Updated` / `Version` | public set | **internal set** | identity/derived |
| `OsDirectory` | public set | **internal set** | `Configure` sets it from the resolved path |
| `Parent` | public set | **internal set** | runtime app-tree link |
| `CurrentActor` | public set | **internal set** | runtime execution state |
| `Cache` | public set | **internal set** | future `--cache={"name":"redis"}` — the cache loads its own dll by name; out of scope now |

### The audit is a tree-wide sweep, not a root table

Because the flag path *is* the app-tree path, the walk descends through getter-containers and sets any public-set leaf at any depth (`--build={"files":...}` reaches `Build.Files` *through* the `Build` getter). So "public set = config surface" is only true after **every public setter reachable through the walk is audited and the run-state ones demoted to `internal`**. Run-state keeps leaking as a public setter — `CurrentActor`, `Tester.CurrentTest`, `CallStack.Variables`, `Run.Output` are all public-set for a runtime reason, not config. The sweep is what makes the surface honest; it is a real chunk of the branch's work, not a root-only edit.

Leaf findings from the current tree (crawled — the config subsystems + the getter-containers):

| Node | Leaf | Today | Set to | Note |
|---|---|---|---|---|
| `Build` | `Files`, `Cache` | public set | **public set** | clean config |
| `Debug` | `Goal`, `Step`, `Variables`, `MaxLength`, `Grep`, `Verbose`, `Llm` | public set | **public set** | debug knobs; `--debug={"goal":"Start","step":3}` maps straight to `Goal`/`Step` (so the `Start:3` scalar shorthand just dies, §4) |
| `Debug` | `Level`, `Llm.Output` | `string` | **`choice`/enum** | fixed value sets — tighten per §5 |
| `Tester` | `Verbose` | public set | **public set** | clean |
| `Tester` | `TimeoutSeconds`, `Parallel`, `Format` | `int`/`int`/`string` | **`uint`/`uint`/enum** | §5 |
| `Tester` | `Include`, `Exclude` | `HashSet<string> { get; }` | **settable form** | get-only today — the walk can't reach them; need `{ get; set; }` (List/set) so `--tester={"include":["tag"]}` lands |
| `Tester` | `CurrentTest` | `Run? { get; set; }` | **`internal set`** | run state, not config |
| `Tester` | `Results`, `Coverage` | `{ get; }` | unchanged | run output — correctly get-only |
| `CallStack` | `Flags` | public set | **public set** | reached by `--callstack={"flags":...}` (its real tree location), not folded under `--debug` |
| `CallStack` | `Variables` | public set | **`internal set`** | run state |

`Config`, `Settings`, `Format`, `KeepAlive`, `Event`, `Error`, `Code`, `Statics` all have **no public-set leaves** — the walk reaches them but there's nothing to set. So the sweep is **finite and closed**, not open-ended: the only run-state demotions are `CurrentActor`, `Tester.CurrentTest`, `CallStack.Variables`, `Run.Output`, plus §3's app-root identity setters. No node left un-crawled; no deferred todo.

## 4. No shorthands

`[{"name":"foo"}]` only — never `["foo"] → [{"name":"foo"}]`. Shorthands are special cases; add them later if a real need shows up. Drop the `variables` normalization in `Debug.Apply`.

## 5. Validation lives on the types, not in an Apply

`Tester.Apply`'s bounds checks dissolve into the property types:

- `TimeoutSeconds` → `uint`. Conversion rejects `-5` on its own; `0` means "no timeout" (the runner decides how to treat it). No positive-check.
- `Parallel` → `uint`. `0` means "auto degree" (the runner picks). No positive-check.
- `Format` → an enum / `choice<T>`. An unknown value is rejected by the conversion, not a hand-rolled `case`.
- Include / Exclude tags → `List<string> { get; set; }`. They're `HashSet<string> { get; }` (get-only) today, so the walk can't reach them — give them a public settable form. `Apply`'s `.Clear()`-then-add becomes a plain assign of a fresh list.

`Tester.Apply` dies entirely — it was only loose types plus a missing generic mechanism.

## 6. The `!= null` sites — traced and given owners

Rule Ingi set: a scattered `App.Build != null` means the optionality is modeled wrong. Each incumbent site and its disposition. `Build`/`Debug`/`Tester` each bundle an **ambient capability** (always callable) and a **session** (exists only when enabled); every smell is an ambient capability reached through the session.

### A. `Debug.Write` — move the sink off the session

`App.Debug?.Write(msg)` is wrong — it pushes Debug's own gate out to all 3 callers (and every future one). Diagnostics are ambient: they belong to the debug **channel**.

```csharp
// before — reaches an ambient sink through the session object
await context.App.Debug.Write($"llm.query: no pricing entry for model {model}");
// after — always-present owner; the channel is the gate
await context.Diagnostic($"llm.query: no pricing entry for model {model}");
```

`context.Diagnostic` writes to the `Debug` channel. Production has no sink wired → the write drops. Under `--debug` the born session wires that channel's sink. Gate owned by the channel layer, not a bool on a nullable object, not a per-caller `?.`. Three callers change: `module/goal/call.cs`, `module/builder/code/Default.cs`, `module/llm/code/OpenAi.cs`. (`Diagnostic` is a placeholder name — one transparent word; yours to settle.)

### B. Debug session activation — on birth

Everything `Debug.Apply` wires runs in `new Debug(context)` + populate: subscribe watchers, hook `OnBeforeRequest`/`OnAfterResponse` for LLM tracing, compile the grep regex, wire the debug channel sink. Null session = none of it wired; subscriber lists are simply empty, so no event site null-checks `app.Debug`. The `_applied` idempotency guard disappears — born once.

Debug does **not** set `CallStack.Flags` — that cross-node write is the special case the tree-mirror rule (§1) removes. Callstack config flows through `--callstack` → `app.CallStack.Flags` on its own, wherever `CallStack` sits in the tree. Drop the `callstack` key handling from the old `Apply` entirely.

### C. App entry dispatch — staged: one owned check now, dissolve later

`if (Builder.IsEnabled) return Builder.RunAsync()` (`app/this.cs:545`), the Start-routing (`:610`, `Executor.cs:104`), and the settings-store selection (`if (Tester.IsEnabled) return Sqlite.InMemory(...)`) all inspect mode. The *target* is to dissolve them — a born `Build`/`Tester` sets the app's entry action + store choice at birth, `Start` just runs the entry, zero `!= null`. But that move touches the run root **and** datasource selection at once — the highest blast radius in the branch, and a regression there spans entry dispatch and persistence, hard to localize.

**This branch takes the staged path** (not the full dissolve): swap `IsEnabled` for **one owned `if (Build != null)`** at the run root and one owned `if (Tester != null)` for the store selection — green, mechanical, localizable. The full dissolve to entry-action-set-at-birth lands as its own follow-up branch, verified against Start/Build/test routing on its own. This ships the branch with ~3 *owned, single-site* presence checks (run dispatch, store, + the two deferred D-sniffs), all staged and documented — not scattered, not hidden.

### D. Foreign-layer sniffing — pre-existing smell, mostly out of scope

Two sites reach into build mode from a layer that shouldn't know it exists:

- `type/path/file/this.Operations.cs:63,105` — the filesystem read sniffs build mode to snapshot `.pr`.
- `module/llm/code/OpenAi.cs:157` — the llm handler sniffs build mode + `Build.Cache` to bypass its cache.

These are wrong *today* with `IsEnabled`; nullable only renames the smell. The right design is the inversion — build-born installs a `.pr` read decorator (the file op stays mode-blind); build-born sets cache-off as the config the llm already reads via `action.Cache`. That inversion is **its own work**, deeper than this branch — do not bundle it, or the branch grows to own the filesystem and llm layers. See §9 for what these two sites read in the interim.

### E. Snapshot — presence bit, fine

`Capture`/`Restore` of `isEnabled` becomes `App.Build = present ? new Build(context) : null`. Serializing an optional's presence isn't a runtime null-check — leave it.

## 7. The bug fix itself

The walk's leaf conversion goes through the conversion catalog (`TryConvert` / the `Convert` infra door), never `Create(raw).Clr(propType)`. A collection converts element-wise; each `string` routes through the path family's converter → `path.Resolve(string, context)`, born with context. This is "the type builds itself from the raw value" — the design was already there; `Populate` called the wrong door. It also fixes any future flag whose leaf type differs from its wire type, for free.

## 8. Prerequisite rename + YAGNI

- `app.Builder → app.Build`. Drop the `--builder` alias and the `build`/`builder` normalization in `Configure`. Mechanical: `app.Builder`, `engine.Builder`, the `Builder` type/namespace, tests.
- **No recursive any-depth walk.** The motivating example (`app.environment.culture.number.decimal`) has no property path on today's app (`Environment` is a `string`). `Populate`'s per-leaf convert already hands a nested subtree to the property's own type conversion — the type constructs itself from its subtree, which is the more-OBP answer. Build recursion when a real nested-config property exists.
- **No `[NoCliOverride]` marker** — access level is the control (§3).

## 9. Flagged decision — D's interim spelling

Removing `IsEnabled` while leaving D un-inverted means the two D sites must read *something* to still compile. Recommended: the mechanical swap `App.Build != null`, each with an explicit marker:

```csharp
// TODO(build-mode-inversion): build mode sniffed from a foreign layer — invert (plan §6.D)
if (Context!.App.Build != null && Extension == ".pr") ...
```

Honest, visible debt — not a design to copy elsewhere. The alternative (a single `App.Building => Build != null` accessor) centralizes the check but reads like `IsEnabled` resurrected and hides the debt. Ingi's call on his read-over.

Related: `Executor.cs:99` syncs `Build.Cache` to the `%!build.cache%` user variable for `Build.goal`. Leave the sync for now; it retires with the D-llm inversion.

## Demolition worklist

Dies in this branch:

- `IsEnabled` on `Build`/`Tester`/`Debug`, and every read of it — presence replaces it (all 13 sites accounted for in §6 + the two set-sites → `new T(context)` + `test/run.cs:90`).
- `Debug.Apply` — activation moves to the ctor (§6.B).
- `Tester.Apply` — validation moves to the types (§5).
- `_applied` guard.
- `Debug.Write` on the Debug object — becomes `context.Diagnostic` (§6.A).
- `catalog.@this.Populate` (the lift-then-lower static) — replaced by the app-owned convert walk.
- The four-way flag branch + `build`/`builder` normalization + `--builder` alias in `Configure`.
- The `variables` shorthand normalization in `Debug.Apply` (§4).
- `_app` fields on the three subsystems — reach context instead.
- The `callstack` key handling in the old `Debug.Apply` (the `CallStack.Flags` cross-node write) — callstack config goes via `--callstack` (§1, §6.B).
- **Public setters demoted to `internal set`** — the run-state sweep, now a *closed, finite* list (whole tree crawled): `CurrentActor`, `Tester.CurrentTest`, `CallStack.Variables`, `Run.Output`, plus the app-root identity/runtime setters in §3.
- `Tester.Include`/`Exclude` change from `HashSet<string> { get; }` to a public settable `List<string>` (§5) — get-only can't be walked.

Stays (explicit):

- `CommandLineParser` JSON parse/validate + raw tree — the perimeter, type-blind.
- The conversion catalog / `TryConvert` — what the walk calls.
- `Build.Files` (`List<path>`), `Build.Cache` — populated via convert now.
- The Debug channel — now the diagnostic sink.
- User-variable routing (non-`!` args) — unchanged.
- The two D sites — swapped to `!= null` with a TODO (§9), *not* inverted here.

Deferred (own follow-up, not this branch):

- Runtime subsystem toggle (debug on/off mid-run) — startup-only here; suspend-vs-teardown design is its own todo (§2).
- Full entry-dispatch dissolve to entry-action-at-birth (§6.C) — one owned `!= null` this branch.
- The D foreign-sniff inversion (file `.pr` decorator, llm cache config) (§6.D).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `app.Build/Debug/Tester` nullable, born `new T(context)` | born-with-context; presence = state | Correct — no `IsEnabled`, no per-class enable leaf |
| Access level = exposure | category named by the language, not a bolt-on marker | Correct — walk filters `SetMethod.IsPublic` |
| Flag path = app-tree path | no remap/fold/special-case (callstack via `--callstack`, not `--debug`) | Correct — Debug's `CallStack.Flags` write removed |
| Run-state as public setter | `CurrentActor`, `CurrentTest`, `CallStack.Variables`, `Run.Output` | Demote to `internal` — closed finite list (whole tree crawled) |
| Walk uses the conversion catalog per leaf | type builds itself from raw | Correct — the actual fix |
| `catalog.Populate` static | applies config to another type from outside (smell #1) | Removed — app-owned walk |
| `Debug.Write` via nullable session | ambient capability through a session object | Fixed — `context.Diagnostic` → channel |
| Debug activation on birth | behavior on the element, registration not null-check | Correct |
| Entry dispatch | presence inspected for run/store routing | **Staged** — one owned `if (Build != null)`/`if (Tester != null)` this branch; full dissolve deferred (§6.C) |
| Runtime toggle (subsystem on/off mid-run) | debug's persistent events + no teardown path | **Deferred** — startup-only this branch; suspend-vs-teardown design is its own todo (§2) |
| D foreign sniffs | build mode read by file/llm layers | Known smell — inversion tracked separately (§6.D, §9) |
| Names | `Build` (verb) property, `Diagnostic` helper | `Build` mirrors `app.Debug` precedent; `Diagnostic` is a placeholder — one word, settle in code |

## Path

1. `app.Builder → app.Build` rename; drop the `--builder` alias + `Configure` normalization.
2. Subsystems nullable, `new T(context)`, `_app` → `Context.App`; delete `IsEnabled`. Startup-only activation (born once) — no runtime toggle, no teardown (§2).
3. The convert walk in `Configure` (app-owned, cohesive per §1), public-setter-only, over `!`-flags; delete the four-way branch and `catalog.Populate`.
4. Run-state sweep (closed, finite): demote `CurrentActor`, `Tester.CurrentTest`, `CallStack.Variables`, `Run.Output` + §3 app-root setters to `internal`; give `Tester.Include`/`Exclude` a settable `List<string>`.
5. Validation onto the types (`uint` timeout/parallel, enum/`choice` format); delete `Tester.Apply`.
6. `Debug.Write` → `context.Diagnostic`; `Debug.Apply` activation → the ctor; delete `_applied`, the shorthand, and the `callstack` cross-node write. Release-note line: `--debug` no longer carries callstack flags — use `--callstack={"flags":...}`.
7. **Staged** entry dispatch: one owned `if (Build != null)` at the run root + `if (Tester != null)` for the store; full dissolve to entry-action-at-birth deferred to a follow-up branch (§6.C).
8. D sites: mechanical `!= null` + TODO (§9); the full inversion is a separate branch.
9. Regression: `--build={"files":[...]}` builds and runs `Hello.goal` with no startup crash.
