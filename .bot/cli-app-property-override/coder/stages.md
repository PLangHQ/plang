# CLI as app-property override — implementation stages (coder working list)

Derived from `architect/plan.md` (settled). This owns the *sequence + gates*; plan owns the *shape*.
Each stage ends green (build + relevant tests) before the next. `file:line` are HEAD anchors
(re-baselined 2026-07-06 against `a5ea4f140`, after the settings-config-unification merge).

Legend: ☐ todo · ▣ in progress · ☑ done

---

## RE-BASELINE 2026-07-06 — what is ALREADY landed (verified on HEAD)

Prior cli work (before the settings detour) landed the spine's hard half. Verified:

- ☑ **Stage 1a** — `app/tester/ → app/test/`; `app.Tester → app.Test`. `app/tester` gone, no `.Tester` refs.
- ☑ **Stage 1b (property + collapse)** — `app.Build : app.module.builder.@this?`, `app.Test : app.test.list.@this?`,
  `app.Debug : Debugging?` (all nullable, `this.cs:188,200,206`). Test-subsystem collapse to
  `app.test.@this` / `app.test.list.@this` shape is live.
- ☑ **Stage 1c (mostly)** — `BuildJson` gone; test element rides the wire. (Spot-verify `[Out]` if a wire test regresses.)
- ☑ **Stage 2** — subsystems born `new T(context)` (Executor `engine.Build = new ...builder.@this(ctx)`);
  **`IsEnabled` deleted** (0 real reads — the 8 grep hits are stale comments, cleanup below); `_applied` gone.
- ☑ **Stage 7** — entry dispatch already owned single-site `!= null`: `this.cs:549 if (Build != null) return await Build.RunAsync();`,
  `this.cs:614 if (Test != null)` store selection.
- ☑ **Stage 8 (swap half)** — D-sniffs read `Context.App.Build` (`type/path/file/this.Operations.cs:65,109`), not `Builder.IsEnabled`.
  (Verify the `!= null` guard + TODO(build-mode-inversion) markers are present.)

**So the remaining work is the MIDDLE of the plan — the convert walk + surface cleanup.**

---

## EXECUTION ORDER (remaining)

**Stage 3 (the bug fix) → Stage 4 → Stage 5 → Stage 6 → Stage 1b-tail → Stage 8-verify → Cleanup → Stage 9.**
Stage 3 is the spine and the whole point of the branch.

---

## Stage 3 — `app.Setting` unification + the CLI walk (THE bug fix)  ☑ (3a+3b+3c done)
**Full design: `coder/setting-design.md` (agreed with Ingi 2026-07-06).** The plan's "convert walk in
Configure" is now one method on a single unified `app.Setting` (type `app.setting.@this`) holding both
lifetimes (in-memory + persistent) behind a `Storage` switch. Two sub-stages:

### 3a — the unified `app.Setting` type
- ☑ `app.setting.@this` gains `enum Storage { InMemory, Persistent }`, `_context`, `_store` (root only),
  `_values` retyped `Dictionary<string, data.@this>` (values are **Data**, not `object?`).
- ☑ `Resolve` → **`Get(Storage, params keys)`** returning `ValueTask<data.@this>` (InMemory sync-wrapped via
  `new(...)`, Persistent async sqlite at `Root`). Mirror `Set(Storage, key, Data)`. `Resolve` deleted (Ingi disliked it).
- ☑ Not-found asymmetry: InMemory → `NotFound` (seam → `[Default]`); Persistent unset → `AskError` (prompt).
- ☑ `app.Setting` becomes the app-level root instance (holds `_store` + `System.Context`); `context.Setting`
  chains to it (`parent: app.Setting`). Persistent wrapper `app.module.setting.@this` **folds in / deleted**;
  public `SettingsStore` name dies → `_store` (internal).
- ☑ **Seam regen:** generator emits `await context.App.Setting.Get(Storage.InMemory, "module.action.param", "module.param")`
  (`Emission/Property/Data/this.cs:92`). `set %!x%` → `Set(Storage.InMemory, key, Value)` (whole Data, no `.Value()`);
  `setting.*` actions + `%setting.%` navigable → `Storage.Persistent`.
- ☑ `ScopeTests` retire; `SettingsTests` simplify; `config/this.cs` facade routes here or is deleted.

### 3b — the CLI convert-walk (the crash fix)  ☑ DONE (3dd452a89)
- ☑ `app.setting.@this.Set(object node, IDictionary settings)` — recursive: public-setter gate
  (`prop.SetMethod?.IsPublic`) → `catalog.TryConvert` leaf / **descend** composite (born-on-descend).
- ☑ **Deleted `catalog.@this.Populate`** (+ its dead test facade). Rewired `Executor.cs:80,99` (`!app`, `!build`)
  and `debug.Apply` to the walk. `TryConvert` stays.
- ☑ Unknown flag / no public setter → hard error (`UnknownSetting`).
- ☑ **Green gate met:** `plang '--build={"files":[...]}'` no longer throws `InvalidCastException: String cannot
  lower to this` at Configure — the walk converts `{files:[...]}` → `Build.Files` (List<path>). Zero C# regressions (Modules 37).
- ✗ **Declined (four-way collapse):** considered — merging the 5 `!`-flag branches into one `Set(app, merged)` walk —
  needs Debug/Test activation off `Apply` (Stages 5/6). Today `!debug`/`!test` still have their own branch;
  `catalog.Populate` is gone from all three, so the crash + lift-lower are dead now.
- ☑ Rename local `engine` → `app` in `Executor.cs` (needs `global::app.@this` for the namespace clash) — cosmetic, deferred.
- ☑ Rename local `engine` → a name that doesn't shadow the `app` namespace in `Executor.cs` (Ingi: name what it is; but `app` local clashes with `app.@this` refs — use `global::app.@this` or agree a name).
- **Staging:** full four-way collapse needs Debug/Test activation off `Apply` (Stages 6/5); until then `!debug`/`!test`
  stay born+`Apply`, with `Apply` calling the walk internally so the lift-lower dies everywhere now.

## Stage 3c — C# actions dispatch through the seam (`app.Run`)  ☑ DONE (mechanism) — **`coder/action-dispatch-design.md`**
Built + verified + pushed: **seed pass-through** (C# calls run the seam, set params pass through
untouched, unset fill from setting → `[Default]`; `PreboundHandler` deleted), the **raw-args ctor**
per action (`new request(context, url:…, method: POST)`; unset optionals → Uninitialized so settings
win), **`RunAction → Run`** rename, and the http tests migrated (all 35 green). **+18 tests fixed on
the mechanism, +4 http, zero regressions.** Remaining (optional, ergonomic only): migrate the C#
callers (OpenAi/builder/signing/path/schema) to the raw-args ctor — the seed already routes them
through the seam correctly, so this is call-site cleanup + the rare "setting overrides an unset
`[Default]`" edge. 2 non-http pre-existing (SetTests ValidateBuild, DataSource null-value) unrelated.

### (superseded) original `app.RunAction` framing — **Design settled: `coder/action-dispatch-design.md`**
Shape: `await app.RunAction(new request(context, url: endpoint, method: HttpMethod.POST, unsigned: true))`.
Two mechanisms: (1) generated **raw-args ctor** per action (typed named optional args, wrapped with
`__ctx`; required = non-nullable+no-`[Default]`, optional otherwise; defaults stay in the seam, never
baked); (2) fix **`app.RunAction`** to run the seam (extract set params → Resolve) not the
`PreboundHandler` skip, and read `handler.Context` (drop the context arg). Removes `PreboundHandler`,
`RunAction`'s context arg, and all `new data.@this<T>("",…)` wrapping. `Run()` stays the author's
logic. Migrate OpenAi/builder/http/signing/path/schema; the 4 `RequestActionTests` go green.
Superseded original proposal below (kept for history):

### (superseded) original `app.Action<T>()` sketch  ← DESIGN FIRST (Ingi)
The setting seam (`ICodeGenerated.Resolve`) only runs on the `.pr` path. C# callers use
`app.RunAction(new X(ctx){...})`, which sets `PreboundHandler` and **skips `Resolve`**
(`action/this.cs:332` — "params already set by inline C# composition, so we skip Resolve"). So
**settings + `[Default]` never apply to C#-invoked actions.** Pervasive: OpenAi (the http request,
`OpenAi.cs:227`), builder (list/read/save), http (signing.verify ×3), signing (identity/hash),
path (ask), schema (verify) — and the 4 `RequestActionTests` (they `new request(Ctx){...}`, which is
why BaseUrl/DefaultHeaders/MaxResponseSize never resolve — pre-existing, fail identically on parent).
- **Two sanctioned construction doors only** — kill `new X + RunAction` as a way to *run*:
  **(1)** `.pr` / test `Make` (the real path runs `Resolve`); **(2)** a C# dispatch that routes through `Resolve`.
- **Proposed API** (Ingi likes `app.Action<T>`, still thinking): `app.Action<request>(context).With(r => r.Url, v).Run()`
  — builds the action entity → `GetCodeGenerated` → **`Resolve` (seam)** → Execute; unset params filled from settings/defaults.
  Generic on the ACTION type (`http` is a namespace, not a type; `request` is one, and `ResolveModuleName(typeof(request))`
  already derives its module). Known params ride a bag so the seam fills the rest. String form
  `app.Module("http").Action("request", …)` also viable (loses type/param safety).
- Migrate off `new X + RunAction`: OpenAi, builder, http, signing, path, schema — then the 4 http tests via `Make`/dispatch.
- **DESIGN DISCUSSION with Ingi before writing code** — he owns the API shape.

## Stage 4 — Run-state sweep (closed, finite) + CallStack knobs  ☑ (fb460e53a, dbf44a02f, 822fc03b8, bb9e9d02f)
- ☑ Demoted to `internal set`: app-root `Culture`/`Name`/`Id`/`Created`/`Updated`/`Version`/`OsDirectory`/`Parent`/`CurrentActor`/`Cache`;
  `test.list.Current`, `CallStack.Variables`. Kept `Create`, `Environment` public. (`Run.Output` no longer exists as run-state.)
- ☑ CallStack knobs — reshaped **not** to a `CallStack.Setting` record but to **plain plang-typed props** on CallStack
  (`Timing`/`Diff`/`DeepDiff`/`Tags`/`History` : `@bool`, `MaxFrames` : `number`), set by the same convert-walk
  (`--callstack={...}` → `Setting.Set(app.<actor>.CallStack, dict)`). `Flags.cs`/`Parse`/`Shorthand` deleted.
  Error-recovery flip preserved (`error/list/this.cs`). **Plus:** CallStack moved App→Actor (each actor owns its tree),
  and `App.Snapshot()` de-CurrentActor'd. See `Documentation/v0.2/conventions.md` "Actor Owns Its CallStack".
- ☑ `Test.Include`/`Exclude` confirmed `list<text>` get-only — walk descends element-wise.
- ☑ Verified `Config`/`Setting`/`Format`/`KeepAlive`/`Event`/`Error`/`Code`/`Statics` have no public-set leaves.

## Stage 5 — Validation onto the types (delete Test.Apply)  ☑ (4035baef7)
- ☑ Deleted `Test.Apply` + helpers — replaced by the setting walk (`Set(app.Test, dict)`); Executor wires it.
- ☑ `TimeoutSeconds`/`Parallel` — **kept `number`, NOT `uint`** (Ingi's call: no CLR lowering). No positive-checks;
  sentinels live at the consumer (`run.cs`: `TimeoutSeconds ≤ 0` → no timeout, `Parallel ≤ 0` → ProcessorCount).
- ☑ `Test.Format` = `choice<Format>` (already); walk conversion rejects unknown. **Include/Exclude** made settable
  `list<text>` + **root-fixed** the catalog so a plang `list<T>` builds through its own value-sequence ctor
  (born-with-context) instead of an STJ deserialize fallback (`catalog/Conversion.cs` `ConvertIntoPlangList`).
- ☑ No shorthands / no aliases: keys are property names; walk is strict on unknown keys (like every flag).
- ⏭ **Deferred to Stage 6:** `Debug.Level`, `Debug.Llm.Output` → `choice`/enum (done alongside `Debug.Apply` removal).

## Stage 6 — Debug activation split (delete Debug.Apply)  ☑ (8340e0da1, 620c878e9)
- ☑ `Debug.Apply` → born-then-Activate: Executor does `new Debug(context)` → `Set(app.Debug, dict)` (walk,
  surfaces config errors) → `Activate()` (void side-effects: watchers, LLM `OnBeforeRequest`/`OnAfterResponse`
  hooks, grep regex, event bindings). Config-parse no longer mixed with side-effects.
- ☑ Deleted `Debug.Apply`, the `variables` bare-string shorthand ([{name:...}] only), callstack cross-node write
  (was already gone in Stage 4B). Callstack config flows via `--callstack`.
- ☑ `Debug.Level` → `choice<Level>` (walk-validated; enum named `Level`, not `DebugLevel`).
- ⏭ `Llm.Output` left `string` — the LLM-trace output is channel routing + Debug↔OpenAi coupling; carved out as
  its own follow-up (todos 2026-07-07) rather than converted to a throwaway enum.
- 🐛 Also fixed `.gitignore` `[Dd]ebug/` silently ignoring the SOURCE `app/module/debug/` folder (new files vanished).
- ☑ Release-note: `--debug` no longer carries callstack flags (use `--callstack`); `variables` shorthand removed.
  (No changelog file in repo yet — carry into Stage 9 release notes.)

## Stage 1b-tail — drop aliases + builder type rename  ☑ (4c2ff004a)
- ☑ Dropped `--builder`/`--tester` alias reads; Executor reads only `!build` / `!test`. The `plang build`
  subcommand survives (retargeted to normalize to `--build`, not dropped — CLAUDE.md workflow relies on it).
- ☑ (§8) `app.module.builder` → `app.module.build` DONE (e210ab23b) — namespace/folder + teaching folder,
  compiler-verified, baseline-clean. C# only (Ingi: builder is broken, leave the `.goal`/`.pr`). Leftovers:
  `app.builder.type` (build-time type-schema) not renamed — distinct subsystem; and the builder `.goal`/`.pr`
  still call `builder.*` (rebuild when the born-source regression is cleared). `environment.run` temp name still pending.
- ☑ Adjacent naming-pass win: alias `Debugging` → `Debug` (09a5cf931) to match `app.Debug` (5 files, clean).
  No `Debugger` class existed. (DebugSmokeTests fully-qualifies the ctor — App/Debug/ namespace shadows the alias.)

## Stage 8-verify — D foreign-sniff markers  ☑ (verify only, no change)
- ☑ Confirmed `type/path/file/this.Operations.cs:65,109` (`Context.App.Build`) + `llm/code/OpenAi.cs:158`
  (`app.Build`, `build != null && !build.Cache`) read `App.Build != null` and carry the
  `// TODO(build-mode-inversion): … (plan §6.D)` markers. Full inversion = separate branch.

## Cleanup — stale IsEnabled comments  ☑ (f14163993)
- ☑ 6 stale `IsEnabled` comment mentions → presence-based wording (`this.cs`, `test/run.cs`,
  `builder/code/Default.cs`, `test/list/this.cs`, `actor/this.cs`). No code (IsEnabled already gone);
  the 2 remaining mentions correctly document its absence.

## Stage 9 — Regression + release notes  ◐
- ☑ Release notes: new `Documentation/v0.2/cli-changes.md` + updated `debug.md`/`build.md` (c8c2b9bec).
- ☑ Full C# suite final counts (native binary + `< /dev/null`, ~7s/project — NOT `dev.sh full`, which hangs):
  Runtime 39, Modules 37, Data 36, Types 29, Wire 18 failed, Generator green — **all == pre-existing baseline**.
  Zero new failures from the cumulative Stage 4-9 work; several pre-existing reds fixed as a bonus.
- ☑ CLI smoke — **Configure-level validated** (valid flags walk clean; invalid values + unknown keys reject cleanly
  via the real CLI; original InvalidCastException gone). **End-to-end goal execution blocked** on the born-source regression on this branch breaks
  live goal execution, so end-to-end plang runs can't be greenlit here; the original `--build` startup crash
  (Stage 3b) is fixed. Note for whoever clears the born-source regression.
- NOTE: "full green" is unreachable while the branch carries the ~149 pre-existing born-source reds (unrelated
  to this work). Scope of this branch = the CLI-override plan, verified regression-free against that baseline.

## Stage 10 — Dissolve `app.builder.type` (split the LLM catalog)  ☑
Fell out of the `builder → build` rename: `app.builder.type` (= `Modules.Schema`) is the LLM's view of the
action+type catalog, misnamed/misplaced. Split it (full plan: this file):
- ☑ (10a) `Example`/`Action` → `app.type.spec.*` (NEUTRAL — authored by math/error/…; must not
  depend on `build`). Naming knob: `app.type.spec.Example`/`.Action` (avoid `app.type.action` clash).
- ☑ (10c) type-view → `app.type.catalog.view.@this` (derives plang name "view") (it already owns
  `BuildTypeEntries`/`GetBuilderTypeNames` — the catalog describes itself).
- ☑ (10b) `Render` → `app.type.spec.render.@this` (corrected: it's a Modules.Describe concern, not build; born per-use with a modules handle)
- ☑ `app/builder/` deleted; `Modules.Schema` repointed. Types 30 / Modules 37 / Runtime 39 = baseline, zero new.
- Follow-on (own commits, optional): generalize `build.actions`/`build.types` for dev introspection; expose the
  catalog as **LLM tools** (function-calling) instead of pre-rendering — the high-value bit.

---

## Ordering / risk notes
- The tree is **already green** (IsEnabled gone, dispatch on `!= null`), so Stage 2's old "stays red until 6/7" caveat no longer applies — every stage can land green.
- **Stage 3 is the spine and the highest-value change** — it's the actual bug fix (lift-lower `Populate` → typed convert walk). Do it first.
- Stage 4's `CallStack.Flags → Setting` reshape is the trickiest single edit (record struct → record class, `with`-flip preserved).
- Demolition worklist (plan §"Demolition worklist") is the completeness check — cross off before Stage 9.
