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

## Stage 3 — The convert walk in Configure (THE bug fix)  ☐  ← START HERE
Replace the lift-then-lower `catalog.Populate` with an **app-owned** walk; collapse the four-way branch.
- ☐ `catalog.@this.Populate` still called at **3 sites**: `Executor.cs:80` (`!app`), `Executor.cs:99` (`!builder`),
  `debug/this.cs:136`. Replace with a walk: navigate → convert-via-`TryConvert` → **public-setter gate** (`prop.SetMethod?.IsPublic == true`, not `CanWrite`).
- ☐ Leaf conversion via `TryConvert` (`type/catalog/Conversion.cs`), element-wise for collections.
  `string→path` via `path.Resolve` (born with context).
- ☐ Composite leaf (settable `record class`/class) → **descend** field-by-field; construct null composite first
  (`new()` for config records, `new T(context)` for subsystem nodes). Gate holds at every level (§8).
- ☐ Delete the four-way flag branch in `Executor.cs` (`!debug`:57, `!tester`:64, `!app`:79, `!builder`:87);
  route all `!`-flags through the one walk. `--app` is the sole remap (root alias, Q2). Non-`!` args route as today.
- ☐ Unknown `!`-flag / no public-setter match → hard error at startup (locked-typo rule).
- ☐ Cohesive factoring (navigate/convert/gate as its own step) but **no `IAppTreeNavigator`** (§1 YAGNI).
- ☐ **Green gate:** the Stage-9 regression (`--build={"files":[...]}`) stops crashing once this lands.

## Stage 4 — Run-state sweep (closed, finite) + CallStack.Setting  ☐
- ☐ Demote to `internal set` (§3 table): app-root `Culture`/`Name`/`Id`/`Created`/`Updated`/`Version`/`OsDirectory`/`Parent`/`CurrentActor`/`Cache`
  (`this.cs:248` CurrentActor is still `public ... { get; set; }`); `Test.CurrentTest`, `CallStack.Variables`, `Run.Output`.
  Keep `Create`, `Environment` public.
- ☐ Reshape `CallStack.Flags` (`callstack/Flags.cs`, `record struct` + `Parse` + `Shorthand` + `Default`) →
  `CallStack.Setting` (`setting.@this` **record class**, public-set leaves Timing/Diff/DeepDiff/Tags/History/MaxFrames),
  walked field-by-field (Q1). `Flags.Parse`/`Shorthand` die. Preserve error-recovery flip via `with` (`error/list/this.cs`).
- ☐ `Test.Include`/`Exclude` already `list<text>` get-only — the convert walk descends element-wise (add), so no settable-List change needed. Confirm the walk can populate them.
- ☐ Verify `Config`/`Setting`/`Format`/`KeepAlive`/`Event`/`Error`/`Code`/`Statics` have no public-set leaves (sweep closed — §3).

## Stage 5 — Validation onto the types (delete Test.Apply)  ☐
- ☐ Delete `Test.Apply` (`test/list/this.cs:105`) — replaced by the walk + type-level validation.
- ☐ `Test.TimeoutSeconds` → `uint` (0 = no timeout), `Parallel` → `uint` (0 = auto). No positive-checks.
- ☐ `Test.Format` → enum/`choice<T>`; unknown value rejected by conversion. `Debug.Level`, `Debug.Llm.Output` → `choice`/enum.
- ☐ No shorthands (§4): `[{"name":"foo"}]` only.

## Stage 6 — Debug activation into the ctor (delete Debug.Apply)  ☐
- ☐ `Debug.Apply` (`debug/this.cs:107`, calls `Populate` at :136) → `new Debug(context)` ctor + populate:
  subscribe watchers, hook `OnBeforeRequest`/`OnAfterResponse`, compile grep regex.
- ☐ Delete `Debug.Apply`, the `variables` shorthand, and the **`callstack` cross-node write** (§6.B).
  Callstack config flows via `--callstack` → `app.CallStack.Setting`. (`_applied` already gone.)
- ☐ Release-note line: `--debug` no longer carries callstack flags — use `--callstack={"setting":...}`.

## Stage 1b-tail — drop aliases + builder type rename  ☐
- ☐ Drop `--builder` + `--tester` aliases and the `build`/`builder` normalization (`Executor.cs:31-34,63`).
  `--test`/`--build` canonical (Q3).
- ☐ (§8, low-pri) `app.module.builder.@this` type/namespace rename to match `app.Build`. Defer if it balloons scope.

## Stage 8-verify — D foreign-sniff markers  ☐
- ☐ Confirm `type/path/file/this.Operations.cs:65,109` + the llm cache sniff read `App.Build != null`
  (swap already done) and carry `// TODO(build-mode-inversion): ... (plan §6.D)`. Full inversion = separate branch.

## Cleanup — stale IsEnabled comments  ☐
- ☐ 8 stale `IsEnabled` mentions in comments/docs (`this.cs:168,186,313`, `builder/code/Default.cs:645`,
  `test/run.cs:25,27`, `test/list/this.cs:9`, `actor/this.cs:117`) — update wording to presence-based. Cosmetic.

## Stage 9 — Regression + full green  ☐
- ☐ `plang '--build={"files":["Scratch/Hello.goal"]}'` builds + runs, no startup crash (the original bug).
- ☐ `plang --test`, `--debug={"goal":"Start","step":3}`, `--callstack={"setting":{"timing":true}}` all work.
- ☐ Full C# suite green; PLang tests green. Report + push.

---

## Ordering / risk notes
- The tree is **already green** (IsEnabled gone, dispatch on `!= null`), so Stage 2's old "stays red until 6/7" caveat no longer applies — every stage can land green.
- **Stage 3 is the spine and the highest-value change** — it's the actual bug fix (lift-lower `Populate` → typed convert walk). Do it first.
- Stage 4's `CallStack.Flags → Setting` reshape is the trickiest single edit (record struct → record class, `with`-flip preserved).
- Demolition worklist (plan §"Demolition worklist") is the completeness check — cross off before Stage 9.
