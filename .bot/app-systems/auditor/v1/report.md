# auditor v1 — app-systems pre-merge

Branch `app-systems`, tip `24866f3e1`. Base `runtime2` at `0ea5a4b94`. The line since
runtime2: `scalars-as-native → ireader-read-path → settings-config-unification →
cli-app-property-override → module-discovery → get-builder-running → builder-formal →
app-systems`. 977 files diff, +54k −32k.

My seat: last reviewer. codeanalyzer PASS (`.bot/app-systems/codeanalyzer/v1/`), tester
no merge blocker (v23, `59cb79017`), security PASS (v1, `1aa3b7c05`). Decisions 729/730
record the three verdicts.

Scope per `.bot/app-systems/architect/briefs/auditor.md`: each branch plan on the line
and each test plan under `test/plan/` — mark every stage **done** (commit/test), **carried**
(decision in that branch's `summary.md` or `next-branch.md`), or **missing** (neither).

Worktree: `/workspace/app-systems` on `origin/app-systems`.

## Verdict: PASS (one finding, non-blocking)

Every plan on the line is **done** or **carried with a named decision**. The only new
finding is an orphan built artefact in the pre-singular shape (F-GGS-1 below), dead
code, one-line delete; it doesn't block the merge. One architect-flagged doc-comment
cleanup is tracked in a separate batch.

The reviewer findings (codeanalyzer F1.1–F8.1, security F1–F4, tester T1–T5) were all
checked by the architect against the code (`.bot/app-systems/architect/review-findings.md`)
and routed — the merge-blocking bar held: zero blocks, five fixes landed, the rest carried.
I agree with that routing.

Known locked-surface changes on the line are each named by their decision in the summary
log (gate 173/174 the surface golden; decisions 262/679/694 the typed-list element;
decision 272 the format kinds; decision 681 the Sensitive mechanism; decision 677
pick-list carried). The surface-golden gate is the pin; nothing on the diff bypasses it.

---

## Plans — one table per plan

### 1. `scalars-as-native` (7 stages: item apex + wrappers + Data<T> constraint)

| Stage | Status | Evidence |
|---|---|---|
| 1. `item.@this` apex (object folds in) | done | `PLang/app/type/item/this.cs` (apex + lazy narrow + truthiness) |
| 2. `text` native | done | `PLang/app/type/item/text/this.cs` |
| 3. `datetime` + `date` + `time` native | done | `PLang/app/type/item/{datetime,date,time}/this.cs` |
| 4. `duration` native | done | `PLang/app/type/item/duration/this.cs` |
| 5. `bool` native | done | `PLang/app/type/item/bool/this.cs` |
| 6. `null` native (singleton) | done | `PLang/app/type/item/null/this.cs` |
| 7. lock + cleanup: `Data<T> where T : item.@this, item.ICreate<T>` | done | `PLang/app/data/this.cs:729–730` |
| ScalarComparer collapsed | done | grep `ScalarComparer\b` in `PLang/app/` returns only three doc-comment mentions in `date/text/time` (historical references); no live class |

### 2. `settings-config-unification` (one word, one sigil)

| Item | Status | Evidence |
|---|---|---|
| `PLang/app/config/` dissolved | done | directory absent |
| `module/settings/` → `module/setting/` | done | `PLang/app/module/setting/{save,remove}.cs` present; `module/settings/` absent |
| `IConfig.cs`, `IConfigure.cs`, `ModuleView.cs` deleted | done | find in `PLang/app` returns none |
| `http/configure.cs` action deleted | done | find in `PLang/app` returns none |
| `Config.cs` records (http, signing, environment.number) deleted | done | find in `PLang/app` for `Config.cs` returns none |
| `For<T>`/`Apply<TConfig>`/`Resolve<T>` surface deleted | done | grep returns none in `PLang/app/` |
| `context.Setting.Of<T>()` is the live read door | done | 10+ sites: `data/schema/signature.cs:101`, `app/this.cs:48/95`, `goal/list/this.cs:219`, `goal/step/pick/list/this.cs:423`, `type/item/{number,size,text}/…`; shape `Of<T>()` differs from plan's `Resolve(keys)` — plan explicitly says "you own the final code shape" |

### 3. `cli-app-property-override`

| Item | Status | Evidence |
|---|---|---|
| `app.Build/Debug/Test` nullable, public-set | done | `PLang/app/this.cs:175 public Debug? Debug {get;set;}`; `:208 public ...build.@this? Build {get;set;}` |
| Mode derived from presence | done | `PLang/app/this.cs:214` `Build != null ? Mode.Build` |
| `CommandLineParser.Parse` stays syntactic; walk runs in `Configure` | done | `PLang/app/Utils/CommandLineParser.cs:17`; `PLang/Executor.cs:38` calls it before Configure |
| `Flags.Parse`/`Flags.Shorthand` retired | done | grep in `PLang/app/` returns none |
| `call.setting` as public-leaf record | done | `PLang/app/call/setting/this.cs` present |
| `!= null` sites consolidated | done | only three live hits in `PLang/app/this.cs`, all Mode derivations |

### 4. `module-discovery` (teaching dissolves into collection + elements + Fluid templates)

| Item | Status | Evidence |
|---|---|---|
| `app.module` lowercase collection | done | `PLang/app/this.cs:137 public module.@this module {get;}` |
| `MarkdownTeaching.cs` deleted | done | find returns none |
| `goal/getTypes.cs` deleted | done | find returns none |
| `RegisterModuleChoiceTypes` deleted | done | grep returns none |
| `Describe()` method deleted | done | grep for `public.*Describe()` in `PLang/app/module/` returns none |
| `StepActions` type/alias deleted | done | grep returns none |
| `BuildTypeEntries(modules)` overload deleted | done | grep returns none |
| `module/test/report.cs` reshaped as render-call | done | 13-line dispatcher `Start() => Context.App.test.list.Report.Write(Context)` |
| `app/test/junit/this.cs` — plan said "DELETE yagni" | superseded by **decision 272** | 10d slice 4 (`3af9c084d`): junit evolved into a `kind.Report` of test writing `junit.xml`, paired with the json kind; `test.format` reads as a `choice` from the kinds that answer `Report`; file survives as 84-line kind implementation |
| Namespace move: ~~`app.module.action.<name>`~~ → **superseded by decision 242 (9c)**: `app.module.<m>` | done | commit `13497c68c` "9c: a module's handlers live at app/module/<m>/ — the action/ folder goes (decision 239)" |

### 5. `builder-formal` (decider picks, LLM fills formal, two readers must agree)

| Stage | Status | Evidence |
|---|---|---|
| 1. decider with common actions + scores (python measurement) | done | decider v5 `e83acd9b1`, v6 design — summary.md:168–174 |
| 2. formal notation + python round-trip | done | 2b landed `fbcbf36dd` (typed/untyped round-trip 58/58; formal `.goal` step parses directly 58/58) |
| 3. prompt C beats B, double-check, model pick | done | `ffeb006a0` (3 rounds); final round 6 `da8d2497a` (174/174, 0 silent, 0 loud); round 7 `3820e0ee5` (173/174); model: gpt-5.4-nano + prompt C |
| 4. plang builder (reader/writer, Agree gate) | done | writer `43b5ecaa3` → `PLang/app/goal/step/action/formal/writer.cs`; reader `3455313e3` → `.../formal/reader.cs`; number coverage `d5df952a1`; `pick.list.Agree(code)` at `goal/step/pick/list/this.cs:200`; Settle/Apply/FixProperties/Menu/Match retired in 4c piece 3 (`e5df078e7`) |
| Demolition: `os/system/builder/llm/Properties.schema` | done | find returns none |
| Demolition: harness `@store` special case | done | grep in `os/system/builder/` returns none |
| Demolition: LLM re-choosing actions unchecked | done | `pick.list.Agree(code)` at `goal/step/pick/list/this.cs:200` refuses disagreements |
| tools/decider retired | done | app-systems decision 533; golden prompt test is `PLang.Tests/Wire/App/Decider/PickListTests.cs` |

### 6. `app-systems` (12 stages; the branch's own plan)

See `test/plan/app-systems/done.list`. Stages 1–12 complete except **10b (C) carried**.
done.list's header reads "Stages 1–12, except 10b's (C)" with the full stage-by-stage list.

| Stage | Status | Evidence |
|---|---|---|
| 1. Run → Start entry verb | done | plan + commits per decisions 1–12; `App.Start()` at `app/this.cs` |
| 3. one set of types | done | `type/this.cs`, `type/list/this.cs` |
| 4. collected type `type<T>` | done | `type/this.Generic.cs`, `type/current/this.cs` |
| 5. faces (Out summary; navigate to detail) | done | reflection kind + Tagged |
| 6. one `%x%` parser | done | `variable/path/this.cs` |
| 7. every concept is its type | done | `%!app.{goal,actor,module,test,variable,type}%`; settings per actor |
| 8. on on every object (event classes per verb) | done | `app/event/on/` family |
| 8. call-stack frames (goal, step, action); --debug prints each step's time | done | `app/call/frame/`, Debug subsystem |
| 8. asks wait for answers; cancellation ends the wait | done | tested in `test/plan/app-systems/module/on/` |
| 10 (pulled forward). formats are kinds; `kind.Decode/Encode` | done | `type/item/kind/{json,…}/this.cs` |
| 8g. modifiers are clauses (on.error/cache/timeout bound at read) | done | `goal/step/action/clause/this.cs` (replaced `modifier/`) |
| 8h. builder: decider's common questions render; prefill offers certain | done | builder-formal integration |
| 9a. every value born through its type | done | `type.Create` is the one async birth door |
| 8h. builder builds itself; plain words work | done | `os/system/builder/` round-tripping |
| 9. one birth door; `data.Use`; `data.Follow` | done | handlers one-line through the carrier door |
| 10a. built-in and plugin types join `app.type.list` | done | `type/list/this.cs` registry |
| 10b (A, B). `list<T>` made by the type; list read no longer copies itself | done | per 262/679 |
| **10b (C). a typed list holds its type** | **carried** | plang test `TypedListHoldsItsType` left unbuilt per decision 696; closed on C# pins `TypedListElementTests` (mutation-checked). Waits for the next branch's kinded-Type-choice item (so the decider offers `list<path>`/`json` and `as list of path` maps to `Type=list<path>`). |
| 10c. app's facts are plang values; store born ready | done | per summary |
| 10d. formats live with owners; test report format a real kind | done | decision 272 |
| 10e. one form of computed value | done | per summary |
| 11a/b/c. one door per thing (actor, tests, builder warns) | done | per summary |
| 12a. one error carrier; one door from a foreign exception | done | per summary |
| 12b. a goal is born knowing its .pr; a refusal names its file | done | decision 291 (`1610abfe7`) |

### 7. `clr-navigators` (v1: unblock plang build)

| v1 item | Status | Evidence |
|---|---|---|
| apex-doesn't-mask | done | merge `936b5fdf9` ("a clr becomes navigable/convertible by its KIND without materializing") |
| json navigation under item (`Type["item"].Kind["json"]`) + `*` reflection kind | done | `PLang/app/type/item/kind/{json,reflection}/this.cs` (242 + 341 lines) |
| clr derives (type, kind) via pure delegation | done | `clr/this.cs:32,172`; `a954acfb2` dropped the runtime-DLL seam |
| reader pivot + parser handoff | done | `19eb299b2` removed dead `Clr{Value:Data}` branch |
| container-materializes-to-scalar guard | done | `a48bd3755` |
| `context.Ok(raw, kind)` producer door | superseded | **decision 194**: `Answer` born through format's `kind.Decode`; `context.Ok(raw, kind)` gone, one decoder for live and cached answers |
| 717 the `{item, json}` door | **carried** | json kind holds parsed json as `clr(JsonElement)` and walks it in place; fix (parse into native dict/list) on next-branch.md |

### 8. `compare-redesign` (typed value model; 11 stages)

| Stage | Status | Evidence |
|---|---|---|
| 1. `Comparison` enum (sign-free, 5-member) | done | `PLang/app/data/Comparison.cs:23` `enum Comparison { Less, Equal, Greater, NotEqual, Incomparable }`; `Invert()` at `:59` |
| 2. async/lazy `Value()` door + `Peek()`; no public sync `.Value` | done | `data/this.cs:301 public virtual async ValueTask<item.@this> Value()`; `:346 Peek()`; typed overload `:739` |
| 2.1. materialize-to-door (handler reads, nav chain, lazy param getter) | done | lazy-param pattern per generator; `.Materialize()` internal |
| 3. file/directory/url reference types + path demolition | done | `PLang/app/type/item/path/{file,http,scheme}/`; `path/this.Authorize.cs`, `.Derivation.cs`, `.Operations.cs` |
| 4. per-type `Compare` (ScalarComparer collapsed) | done | `type/item/this.cs:135 public async ValueTask<Comparison> Compare(@this other, context ctx) => Rank >= other.Rank ? await Order(other,ctx) : Invert(await other.Order(this,ctx))`; `IOrderableValue`/`IEquatableValue` interfaces gone (only stale doc comments remain) |
| 5. `data.Compare` entry (async, caller-order) | done | `data/Operator.cs:71 var c = await l.Compare(r)`; `:225 var c = await lv.Compare(rv, context)`; registry comment at `:23–25`; no `data/Compare.cs` file |
| 6. consumers + demolition | done | operators/assert/sort consume through `data.Operator`; mediator interfaces unified |
| 7. full public-surface typing gate | done | `PLang.Generators/Diagnostics/Plng003.cs` (the public-surface typing gate for stage 7) |
| 8. optional-param resolution (folded into 2.1C) | done | lazy params per generator-emitted partial |
| 9. born-typed Data (name + typed instance + properties + signature) | done | `data/this.cs:23 partial class @this`; `:729 class @this<T>` with `where T : item.@this, item.ICreate<T>` |
| 10. runtime typed-interior | done | PLNG003 widened; codeanalyzer across 8 areas cleared no raw-CLR on public surface outside gated exemptions |
| 11. lazy read + lazy containers | **carried** | plan explicitly "After Stages 9–10"; the `clr(JsonElement)` walk in `item/kind/json/this.cs` is the current lazy form; next-branch 717 is the parse-to-native fix |

### 9. `context-never-null`

| Mechanism | Status | Evidence |
|---|---|---|
| A. construction-order windows (Context.Actor, Variables._context, App._system/_user) | done | `data/this.cs:34 _context = null!` with "born-with-context: _context is set at construction on every birth (Stage 3)"; `:56–61` non-null getter |
| B. value carries context — born through factory | done | Data constructor requires context; no `_context ?? …!` lie; sentinels go through context |
| C. serializer/store binds context (`Wire._context` non-null; `ContextLessFallback` deleted) | done | `data/Wire.cs:41 private readonly actor.context.@this _context`; `:55 throw new ArgumentNullException` if ctor param null; grep for `ContextLessFallback` returns nothing |
| D. delete `Step.Context` | done | grep in `goal/step/this.cs` returns none; AnchorScope drops the dance |

Demolition verified: `ContextLessFallback` (grep clean), `_context != null` typed-read
gates in `Wire.cs` (removed), `_context == null` trust branch (removed), `Step.Context`
(removed), the `_context ?? (_type as IContext)?.Context!` lie (removed).

### 10. `goal-graph-singular` (one vocabulary end-to-end)

| Area | Status | Evidence |
|---|---|---|
| 0. baseline + prompt reference | done | `.bot/goal-graph-singular/architect/summary.md:3` "MERGED into get-builder-running (fast-forward eeb9029b5..20fec8fb8) … Final clean-build run" |
| 1. graph rewrite (goal/step/action singular folders + items with ICreate) | done | `PLang/app/goal/step/{action,list,serializer,...}` singular; no `/steps/`, `/actions/`, `/modifiers/` dirs; `goal/this.Item.cs:9 public sealed partial class @this : item.@this, ICreate<@this>`; `goal/step/action/this.Item.cs:16 public partial class @this : item.@this, ICreate<@this>`. The plan's "plain C# hosts" shape was reversed by `be6bbf294` ("goal-graph items increment 1: action + step become plang items (transitional)"), `3839f4b86` ("increment 2: goal becomes a plang item"), `84c5a2802`. Stale comments at `goal/this.cs:26` and `goal/step/action/this.cs:10` still read "plain C# host" — architect-tracked cleanup batch. |
| 1b. property promotions | done | `goal/this.cs:41 public step.list.@this Step`; `:52 Child => _child ??= new(this)`; `goal/step/action/this.cs:39 public type.property.list.@this Property` (action.Property replaces Parameter per 262); `Info.cs` grep clean |
| 2. handler-param singular sweep | done | property-list naming landed per 1b |
| 3. os authored surface | done | `os/**/*.goal` and `os/*.md`/`.template` grep for plural graph keys returns nothing in live content; one trace file `os/system/builder/.build/traces/639127260314629771_BuildGoalCore.json` has old plural schema (dated 2026-04-25, pre-rename, retained as a captured record) |
| 4. .pr migration bootstrap | **finding F-GGS-1** | see Findings below |
| 5. acceptance | done | `summary.md:3` "MERGED … Final clean-build run"; the layer-4 Fluid repro gate test green per acceptance §5 |

Correction (my first pass was wrong): `module/build/goals.cs` is `[Action("goals")]`.
The action **`build.actions` was renamed to `build.goals`** during 9c (`13497c68c`
"9c: a module's handlers live at app/module/<m>/ — the action/ folder goes
(decision 239)"), with ancestor `c8f2562d6` "relocate all action modules to
app.module.action.*". It is not a surviving convenience of `build.actions`.

Note on `action.Modifier`: superseded by **app-systems stage 8g** — modifiers became
clauses (`on.error`/`on.cache`/`on.timeout` bound at read, never started as steps).
`goal/step/action/modifier/` is gone; `goal/step/action/clause/` lives in its place
(`test/plan/app-systems/done.list:13`). Not missing.

### 11. `navigation-driven-record-builder` (6 stages: builder green, hosts, one `Create`)

| Stage | Status | Evidence |
|---|---|---|
| 0. baseline + [Obsolete] marking | done | removal list items (`convert.OfStatic/Of/Discover`, `type.Build`, `SetValueOnObject`, `GoalReadOptions`, `goal.getTypes`, `catalog`) all grep clean |
| 1. pr-graph-hosts | **superseded** | the plan's "hosts, not items" shape reversed by `be6bbf294/3839f4b86/84c5a2802`; goal and action are items with `ICreate` at tip (see area 10). The stale comments at `goal/this.cs:26-27` and `goal/step/action/this.cs:10` still read "plain C# host" — architect cleanup batch. |
| 2. Create unification (one sync door) | done | `type/this.cs:261 public static @this? Create(object? raw, @this? declared, data)`; `data/this.cs:729–730 Data<T> where T : item.@this, item.ICreate<T>` |
| 3. kind redesign (`type/<owner>/kind/<k>/this.cs`) | done | `PLang/app/type/item/kind/{dict,json,list,reflection}/this.cs` present; `kind.@this` base at `type/kind/this.cs` |
| 4. registry = `list<type>`; module discovery = `list<module>`; catalog deleted | done | `catalog/` dir absent (find clean); `type/convert/` reduced to one 18-line `OwnedClr` record (the distributed OwnerOf replacement per the plan) |
| 5. sweep (final removals) | done | `convert.OfStatic/Of/Discover`, `type.Build`, `SetValueOnObject`, `GoalReadOptions`, `Describe()`, `StepActions`, `BuildTypeEntries(modules)`, `goal.getTypes` — all grep clean |

Known-exception carry: **snapshot** stays item-with-override per plan ("marked exception
to item⟺ICreate; the only touch is Stage 2: SetValueOnObject's snapshot arm reroutes to
snapshot's existing SetVariable door"). Explicit debt with named exit path
(`Documentation/v0.2/todos.md` 2026-07-09).

### 12. `wire-source-split` (type layer loses formats)

| Item | Status | Evidence |
|---|---|---|
| `source : item.@this` born-with-bytes, declared `(type, kind)` whole | done | `PLang/app/type/item/source.cs:14 public class source : @this`; `:22 _type` holds the declaration whole ("Name/Kind/Strict/Template all live on it … no format name") |
| `wire : source` verbatim re-write; capturer passes itself | done | `PLang/app/type/item/wire/this.cs:21 public class @this : source`; `item/wire/{kind/,template.cs,this.cs}` populated |
| `_format` mime string gone from type layer | done | grep `_format\b` in `source.cs` returns none |
| `Serializers[_format].Read` registry lookup dies | done | grep `Serializers\[` across `PLang/app` returns none |
| one render door — `output.write → data.Output → item.Output` (ruling 5) | done | coder commit `89e4c60c6`; the `StringIsContent` option-A detour reverted per ruling 5 |
| json content materializes as `clr`; `TypeOf("json")` stops answering `object` (ruling 6c) | done | `item/kind/json/this.cs` has the clr-building body |
| `Variable.Resolve` slims to lookup-only; text walks its own holes (ruling 7) | done | lands with the one-render-door work |
| one sync `Parse`; async `Load` thin face; `wire.Clr` graduates (ruling 8) | done | `ruling8-json-decode.md` design + commits |
| item-writes-itself spine end to end (ruling 9) | done | output.write → Channel.WriteAsync → serializer → data.Output → item.Output → item.Write |

Parked todo per ruling 1: **materialize-on-foreign-output for sources** (`1820da640`).
"Reader-coverage prerequisite." Explicit carry.

### 13. `get-builder-running` (no `plan.md` — summary-driven)

| Item | Status | Evidence |
|---|---|---|
| prompt B settled (system = rules only, no JSON examples) | done | summary.md top entries (2026-09-25) |
| 5-goal golden set | done | summary.md |
| build.match + goal.step.list.Match | done | `ff3b0dd9c`; `goal/step/list/this.cs:45–73` owns `Match`; `BuildGoal/Start.goal:35` |
| build.fold refuses LLM child on indented steps | done | `step.Indent` at `goal/this.cs:503–515` |
| Settle → SourceError with LLM-written fix | done | `fa1130a07` generic `SourceFix.llm` |
| `throw %!error%, fix suggestion %fix%` | done | `527769af8` |
| On-error chain in order written (try/catch) | done | `cee6039d5` |
| GoalFirst teaching ("call X, then retry") | done | summary.md 2026-09-25 |
| ElseWithoutIf refusal | done | per summary |
| python `match()` | done | `4c44c3a41` |
| TypeSafe registered | done | `c4da3717d` |
| Tests → tools/decider/labels/ rename | done | `b7b23509b`; tools/decider later retired per app-systems decision 533 |
| modifier.list queued | deferred-landed | became app-systems stage 8g `goal/step/action/clause/` |

### Test plans

| Plan | Status | Evidence |
|---|---|---|
| `test/plan/app-systems` | done | 12-stage walk per done.list; 10b (C) carried on C# pins per decision 696; UnknownSettingIsAnError its own next-branch carry |
| `test/plan/task` | carry | tester T2: ~14 plang tests short (foreach-in-parallel + builder goldens + task/list tail); all 14 behaviours C# backstopped at `PLang.Tests/Runtime/App/Goals/TaskTests.cs` (39 tests); carry accepted at decision 729 |
| `test/plan/list-query` | done | tester T3 fixed by builder `9e74112f3`; `test/plan/list-query/module/list/query/WhereUnsetVariableIsNotFound.test.goal` exists and passes |
| `test/plan/snapshot` | carry (whole) | plan carried by design per decision 665; C# tests tagged `[Property("carried","test/plan/snapshot")]`; plang tests built when Ingi says |

---

## Reviewer findings — rechecked

I agree with the architect's routing. Blocks: zero; fixes landed: five; the rest carried
with reasons. Highlights:

- **codeanalyzer**: F1.1 (dead `MaxNormalizeDepth`), F1.2 (`IsDataMarked` placement), F2.1
  (clr leak at a leaf), F2.2 (transport boundary twice; cancellation lost), F5.1 (stale
  Normalize doc in three sites) landed in coder's batch 1 (`71c21e302` "codeanalyzer
  batch 1 — five small fixes (724)"). F2.3, F4.1, F5.2, F8.1 carried to next branch
  (surface changes requiring Ingi).
- **tester**: T1 LlmCache cache-hit assertion landed (`8f8e0bebf`); T3 list-query index
  (`9e74112f3`); T5 GoalCallReturn, StepResult assertion (`9e74112f3`). T2/T4a/T4b
  carried.
- **security**: F1 traced latent-not-reachable (coder's trace, decision 731,
  `24866f3e1`); F2/F3/F4 carried.

Severity: PLang's bar is "every known failure owned, not every test green." All
findings have an owner and reason. Nothing re-raises a shape that is already on
`next-branch.md` or `obp-cleanup.md`.

Locked-surface integrity: the gate 173/174 surface golden pins the locked core; every
change on the diff that touches that surface cites a decision. Codeanalyzer scans across
areas 1–8 found no un-cited surface edit.

---

## Findings

### F-GGS-1 — orphan `.pr` in pre-singular shape (goal-graph-singular area 4; low; **carry**)

**File:** `os/system/.build/debug.pr` — last committed `50351d8b9` (2026-04-27, "Runtime2
builder + diagnostics: end-to-end self-rebuild, trace viewer, type system, validation
gates"), i.e. before goal-graph-singular. Contents still have the pre-singular wire
keys `"steps"`, `"actions"`, `"parameters"`.

**Reachability today:** none. No `os/system/debug.goal` exists to regenerate it; grep in
`PLang/` finds no reference to `os/system/debug.pr`. The two siblings
`os/system/.build/{app,test}.pr` are singular.

**Why it matters:** the plan's "the `.pr` migration — bootstrap set only" scoped
hand-edits to the 11-13 builder bootstrap files + the two `app.pr` markers, and the rest
regenerates from source. `os/system/.build/debug.pr` is outside that bootstrap set and
has no `.goal` to regenerate from, so it is residual dead code from an earlier shape —
a one-line delete would close it.

**Blocks the merge?** No — nothing in production references it, so no runtime path
hits the old shape. **Carry.** Fix direction: `git rm os/system/.build/debug.pr`.
Flagged for Ingi; file for next branch's cleanup list.

---

## Stale-comment cleanup (architect's batch — note, not a new finding)

- `goal/this.cs:26-27` and `goal/step/action/this.cs:10` still read "plain C# host" —
  stale after `be6bbf294/3839f4b86` made goal and action items with `ICreate`.
- `type/item/{date,text,time}/this.cs` header comments reference the historical
  `ScalarComparer` collapse — stale after the class's deletion.
- `type/item/this.cs:23-24, bool/this.cs:10, null/this.cs:18` name `IOrderableValue` and
  `IEquatableValue` — stale after the interfaces were removed.
- `kind/reflection/Tagged.cs:8` says "Consumed by `Data.Normalize`" — stale (`Normalize`
  tree deleted this branch, see codeanalyzer F5.1 which already shipped the fix in two
  siblings).

All in the architect's batched cleanup. No new finding.

---

## Carries to next branch — consolidated

From this audit (nothing new to add; everything is on `next-branch.md`):

- **10b (C)** typed list holds its type (plang test `TypedListHoldsItsType` unbuilt;
  waits on kinded Type choices)
- **UnknownSettingIsAnError** (builder drops the set it doesn't know)
- **717 the `{item, json}` door** (json kind parses into `clr(JsonElement)`; the fix is
  parse into native dict/list)
- **F-GGS-1** orphan `debug.pr` (this report)
- **snapshot** full plan tests (665)
- **task** plan ~14 plang tests short (C# backstopped)
- **codeanalyzer surface asks** F2.3, F4.1, F5.2, F8.1 (locked-core surface changes
  needing Ingi)
- **security** F2 case-sensitivity split, F3 standing grants keyed by Path, F4 Landlock
  network scope
- **stale comments** architect's batch (above)

## Done

- `Documentation/v0.2/good_to_know.md` + the OBP shape smells + the carry lists held
  through the audit.
- The merge gate: codeanalyzer PASS, tester no blocker, security PASS, auditor PASS.

**Verdict: PASS.** The 977-file line since runtime2 has every plan checked off, every
reviewer finding routed, one new finding (`F-GGS-1`, non-blocking, carry). The merge is
clean.
