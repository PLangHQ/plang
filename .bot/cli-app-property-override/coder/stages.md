# CLI as app-property override — implementation stages (coder working list)

Derived from `architect/plan.md` (settled). This is my ordered, verifiable checklist —
plan owns the *shape*, this owns the *sequence + gates*. Each stage ends green
(build + relevant tests) before the next starts. `file:line` are HEAD anchors.

Legend: ☐ todo · ▣ in progress · ☑ done

---

## Stage 0 — baseline
- ☐ Clean build + note current test state (which fail pre-branch).
- ☐ Reproduce the bug: `plang '--build={"files":["Scratch/Hello.goal"]}'` → `InvalidCastException: String cannot lower to this` at startup. Keep as the Stage-9 regression check.

## Stage 1 — Renames + test-subsystem collapse (element / collection / current)
Two renames plus a structural collapse Ingi signed off (2026-07-05): the whole
`test`/`Run`/`Results` triad becomes ONE element + ONE collection, following the
`app.X.@this` / `app.X.list.@this` / `app.X.current` convention. This is the
general shape Ingi wants across the codebase.

**1a. Renames:**
- ☑ `app.Tester → app.Test`: folder `app/tester/ → app/test/`; type `app.tester.@this → app.test.@this`; property; global usings; tests.
- ☐ `app.Builder → app.Build`: property; `engine.Builder` sites; the `builder` type/namespace (`app.module.builder.@this`) → full rename per §8. Drop `--builder` + `--tester` aliases and the `build`/`builder` normalization (`Executor.cs:33-34`). `--test`/`--build` canonical (Q3).

**1b. Collapse (target shape):**
```
app/test/this.cs        app.test.@this        ONE test — lifecycle element  [PlangType("test")]
                          Goal, Status(Ready│Stale│Skipped│Pass│Fail│Timeout), StatusReason,
                          Tags, Output, Timings, Error   (execution fields empty until run)
app/test/list/this.cs   app.test.list.@this   the SESSION (collection of tests)
                          StartedAt, Pass/Fail/Skip/Timeout counts, Coverage,
                          Timeout/Parallel/Include/Exclude/Verbose/Format, owns list<test>
app.Test property       app.test.list.@this?  null = not testing; non-null = live session
app.test.current        the running test      (today's CurrentTest — test.tag's only reader)
```
- ☐ Merge old `test` (discovery item) + `Run` (execution record) → `app.test.@this` element (Status spans lifecycle).
- ☐ Merge old runner `app.test.@this` + `Results` → `app.test.list.@this` session collection.
- ☐ `app.Test` property retyped to `app.test.list.@this` (nullable lands in Stage 2).
- ☐ `CurrentTest` → `app.test.current`.
- ☐ **Dissolve:** `Run`, `Results`, `app.module.test.test`.
- ☐ Update discover/run/report/tag (`app.module.test`) to the new shape; update tests.

**1c. Full plang-typing (Ingi 2026-07-05 "do fully" — proving ground for plang-types-everywhere; test subsystem is not perf-critical):**
- ☐ Element/session scalars → plang types: `duration` (Duration, +add `TotalMilliseconds`), `text` (StatusReason/Output), `datetime` (StartedAt), `number` (TimeoutSeconds/Parallel), `@bool` (Verbose), `choice<Format>` (new `enum Format{Json,JUnit}`). `Status` stays enum (rides `[Out]` like Goal.Visibility).
- ☐ Collections → plang lists: `list<test>` (Tests, session-owned lock), `list<text>` (Include/Exclude/Tags/UserTags), `list<timing>` (Timings; promote `record Timing`→`timing` item {number StepIndex; duration Elapsed}).
- ☐ Mark `test` props `[Out]` → element rides the wire.
- ☐ **`BuildJson` dies** — report writes tests through the wire serializer; `junit` becomes a format renderer (the one real external XML). Resolves the Verb+Noun smell + the "write the wire" ask.
- ☐ Read sites: bare plang-type fields read directly (`Duration.TotalMilliseconds`) — Peek/Value only when Data-wrapped.

## Stage 2 — Subsystems nullable, born-with-context (delete IsEnabled)
- ☐ `Build`/`Debug`/`Test` → `public T? { get; set; }`, default null (`this.cs:187,198,203`).
- ☐ ctor takes `context`; drop `_app` fields → `Context.App` (all three subsystems).
- ☐ Delete `IsEnabled` (`debug/this.cs:22`, `test/this.cs:16`) and the `IsEnabled` check inside `Debug.Write` (`debug/this.cs:104`). `Debug.Write` **survives**.
- ☐ Delete `_applied` (`debug/this.cs:72`).
- ☐ Bare `--build`/`--test`/`--debug` → `new T(context)`; startup-only, born once (§2 — no runtime toggle).
- ☐ Compile will break at every `IsEnabled` read (Stage 6/7 fix them). OK to stay red *within* this stage until §6/§7 land — or stub reads to `!= null` as I go.

## Stage 3 — The convert walk in Configure (the bug fix)
- ☐ Replace `catalog.@this.Populate` (`catalog/Conversion.cs:87`, the `Create(raw).Clr(propType)` lift-then-lower) with an **app-owned** walk: navigate → convert-via-`TryConvert` → public-setter gate.
- ☐ Walk filters `prop.SetMethod?.IsPublic == true` (not `CanWrite`) — §3.
- ☐ Leaf conversion via `TryConvert` (`catalog/Conversion.cs:144`), element-wise for collections. `string→path` via `path.Resolve`, born with context.
- ☐ Composite leaf (settable `record class`/class) → **descend** field-by-field; construct null composite first (`new()` for config records, `new T(context)` for subsystem nodes). Gate holds at every level (§8).
- ☐ Delete the four-way flag branch in `Executor.cs:56-100` (debug/tester/app/builder); route all `!`-flags through the one walk. `--app` is the sole remap (root alias, Q2). Non-`!` args route as today (`Executor.cs:50-54`).
- ☐ Unknown `!`-flag / no public-setter match → hard error at startup (locked-typo rule).
- ☐ Cohesive factoring (navigate/convert/gate as its own step) but **no `IAppTreeNavigator` interface** (§1 YAGNI).

## Stage 4 — Run-state sweep (closed, finite) + settable config forms
Demote run-state public setters to `internal set`:
- ☐ App-root: `Culture`, `Name`, `Id`/`Created`/`Updated`/`Version`, `OsDirectory`, `Parent`, `CurrentActor`, `Cache` → `internal set` (§3 table). Keep `Create`, `Environment` public.
- ☐ `Test.CurrentTest`, `CallStack.Variables`, `Run.Output` → `internal set`.
- ☐ `Test.Include`/`Exclude`: `HashSet<string> { get; }` → public settable `List<string>` so the walk can reach them.
- ☐ Reshape `CallStack.Flags` (`callstack/Flags.cs`, `record struct` + `Parse` + `Shorthand`) → `CallStack.Setting` (`setting.@this` **record class**, public-set leaves `Timing`/`Diff`/`DeepDiff`/`Tags`/`History`/`MaxFrames`), walked field-by-field (Q1). `Flags.Parse`/`Shorthand` die. Preserve error-recovery flip via `with` (`error/list/this.cs:80,111`).
- ☐ Verify: `Config`/`Settings`/`Format`/`KeepAlive`/`Event`/`Error`/`Code`/`Statics` have no public-set leaves (sweep closed — §3).

## Stage 5 — Validation onto the types (delete Test.Apply)
- ☐ `Test.TimeoutSeconds` → `uint` (0 = no timeout), `Parallel` → `uint` (0 = auto). No positive-checks.
- ☐ `Test.Format` → enum/`choice<T>`; unknown value rejected by conversion.
- ☐ `Debug.Level`, `Debug.Llm.Output` → `choice`/enum (§3 table).
- ☐ Delete `Test.Apply` entirely (`test/this.cs:60`) — was loose types + missing generic mechanism.
- ☐ No shorthands (§4): `[{"name":"foo"}]` only; drop `variables` normalization in old `Debug.Apply`.

## Stage 6 — Debug activation into the ctor (delete Debug.Apply)
- ☐ Everything `Debug.Apply` wired (`debug/this.cs:114`) → `new Debug(context)` + populate: subscribe watchers, hook `OnBeforeRequest`/`OnAfterResponse`, compile grep regex. Watch/LLM output still via existing `Debug.Write` path (no new channel — §6.A/§6.B).
- ☐ Delete `Debug.Apply`, `_applied`, the `variables` shorthand, and the **`callstack` cross-node write** (Debug no longer writes `CallStack.Flags`/`Setting` — §6.B). Callstack config flows via `--callstack` → `app.CallStack.Setting`.
- ☐ Release-note line: `--debug` no longer carries callstack flags — use `--callstack={"setting":...}`.

## Stage 7 — Staged entry dispatch (owned single-site checks)
- ☐ `app/this.cs:545` `if (Builder.IsEnabled)` → `if (Build != null) return await Build.RunAsync();`
- ☐ `app/this.cs:610` `if (Tester.IsEnabled)` (store selection) → `if (Test != null)`.
- ☐ `Executor.cs:104` Tester Start-routing → `Test != null`.
- ☐ Full dissolve to entry-action-at-birth = **deferred follow-up branch** (§6.C). Not here.
- ☐ `Debug.Write` sniff callers (`module/goal/call.cs`, `module/builder/code/Default.cs`, `module/llm/code/OpenAi.cs`) stay on `App.Debug?.Write(...)` — the `?.` is now the correct gate (§6.A).

## Stage 8 — D foreign-sniff sites (mechanical swap + TODO)
- ☐ `type/path/file/this.Operations.cs:63,105` and `module/llm/code/OpenAi.cs:157`: `App.Builder.IsEnabled` → `App.Build != null` (llm: `App.Build != null && !App.Build.Cache`).
- ☐ Each gets the visible-debt marker: `// TODO(build-mode-inversion): build mode sniffed from a foreign layer — invert (plan §6.D)`. Full inversion = separate branch.
- ☐ Leave `Executor.cs:99` `%!build.cache%` sync + the D-llm inversion for later (§9).

## Stage 9 — Regression + full green
- ☐ `plang '--build={"files":["Scratch/Hello.goal"]}'` builds + runs, no startup crash.
- ☐ `plang --test`, `--debug={"goal":"Start","step":3}`, `--callstack={"setting":{"timing":true}}` all work.
- ☐ Full C# suite green; PLang tests green. Report + push.

---

## Ordering / risk notes
- Stages 1→3 are the spine; 4-6 clean the surface; 7-8 are the localized `!= null` swaps.
- Stage 2 leaves the tree red until 6/7 replace `IsEnabled` reads — expected. If churning, stub reads to `!= null` inline during Stage 2 so each subsequent stage builds.
- Highest blast radius: Stage 7 (run root + persistence). Kept to owned single-site checks by design — do **not** attempt the full dissolve here.
- Demolition worklist (plan) is the completeness check — cross it off against Stages 1-8 before Stage 9.
