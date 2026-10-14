# Reviewer findings on app-systems, as the architect checked them

Each finding the reviewer bots report, checked against the code before it goes anywhere. Status: **fix** (sent to the coder, batched), **carried** (already on `next-branch.md`), **blocks** (to Ingi), **rejected** (with why).

## codeanalyzer (plang-ae), area 1: data and the wire (26fa8f4e7)

| finding | checked | status |
|---|---|---|
| the set-child bridge, `data/this.Navigation.cs:51–63` | cited, on next-branch.md (59's remainder) | carried |
| readers throw their refusal, `data/schema/signature.cs:83` | cited, on next-branch.md (714) | carried |
| parsed json is a clr carrier, `Operator.IsType`'s promise | cited, on next-branch.md (the `{item, json}` door, 717) | carried |
| dead `MaxNormalizeDepth`, `data/this.Output.cs:21` | grep: referenced only at its declaration | fix (batch 1) |
| `IsDataMarked(JsonElement)` on data, `data/this.cs:56` | grep: only caller is `type/item/serializer/json.cs:94,187`; a static taking a `JsonElement` on data is a stray helper and a clr reach; it belongs on the json serializer beside its caller; `internal`, so not a surface change | fix (batch 1) |

## codeanalyzer (plang-ae), area 2: channel and stream (29864e666)

| finding | checked | status |
|---|---|---|
| the channels' `catch (Exception) when (not fatal)` | read: the base doors (`channel/this.cs:143,166,190`) turn a transport throw into a typed error and let `AppException` keep its key; a real I/O boundary, not a smell | cleared |
| F2.1 `question?.Clr<string>()` for an emptiness check, `stream/this.cs:160` | read: lowers the question to a CLR string to ask "is it empty"; the value answers its own emptiness (opened box / clr leak at a leaf) | fix (batch 1) |
| F2.2 the transport boundary twice | read: `AskAsync` (`channel/this.cs:189–194`) catches what `stream.Ask` (`stream/this.cs:197–202`) already catches, and `ReadAsync` (`:165–170`) what `stream.Read` (`:135–139`) does; the two disagree: stream lets `OperationCanceledException` pass, then the base turns it into AskError (stored twice, and the cancel is lost). One boundary, in the base door, cancel excluded; the coder checks for direct callers of `stream.Read`/`Ask` first | fix (batch 1) |
| F2.3 `ReadAllBytesAsync` throws on `!CanRead`, "kept for v1 callers", `stream/this.cs:245–264` | grep: `ReadAllTextAsync` has no production caller (two tests); `ReadAllBytesAsync` has one, the stream's own `Read` (`:130`), whose catch turns the throw into ReadError. Both are public on stream, a locked surface: removing them changes the surface golden | carry (a surface removal is Ingi's; batch it with other surface asks) |

## codeanalyzer (plang-ae), area 3: the core value types (3d45490d8)

| finding | checked | status |
|---|---|---|
| the seams (number's Ladder, path's scheme registry, list's one door to C#) | its summary: one owner or a registry at each, no per-type fork; list's reflection is interop (building a `List<T>` for a runtime T), not the carried member-read reflection | cleared |
| F3.1 the parse-or-convert failure written nine times | grep: `((value as item)?.Clr<object>() ?? value) is string` in bool, datetime, date, time, duration, size, guid, image, list (`bool/this.cs:79–81` read). Stored twice, and each copy lowers the source to C# to ask "were you text?". Its suggested fix, a protected helper on `item`, is an addition to item's locked surface and a helper; the shape is the source answering its own failure (a text says "cannot parse 'x' as T", anything else "cannot convert Y to T"), T giving only its hint | carry (next-branch) |
| F3.2 `dict/this.cs:84` `catch (JsonException) { return null; }` | read: "is this text a dict" answered by a swallowed parse; the dict narrowing a `clr(JsonElement)` at `:89` accommodates the carried `{item, json}` door | carry (with the `{item, json}` door) |

## codeanalyzer (plang-ae), area 4: the type system (480ea48c1)

| finding | checked | status |
|---|---|---|
| no inherited fork in `type.@this` or item's base; their reflection is 673's carried member-read machinery; `type/convert` moved onto each type's `this.Owns.cs` | its census and summary | cleared |
| F4.1 `property.IsDefault` decides equality by type, `type/property/this.cs:118–138` | read: four arms (a bool by its word, `Type.Name == "variable"` by name without `%`, a double parse, else the raw text) plus a `System.Text.Json` decode of a quoted slice. A fork by type name; a new type whose written form needs normalizing falls to the raw-text arm and is silently "not default". **No new member is needed:** the value already answers equality, `item.Compare(other, context)` (`type/item/this.cs:135`), so IsDefault is `Compare(Type.Make(Default), context) == Equal`. But that is async and needs a context, so `IsDefault`'s signature changes (two callers: `pick/list/this.cs:216`, `property/list/this.cs:120`): a change to property's locked surface | carry; surface ask for Ingi (batch) |
| F4.2 `type/this.cs:547` writes `name<kind>` only for list, dict and choice | cited: a site of 711 (a kind is a type) | carried (711) |

## Surface asks for Ingi, collected (asked together at the end of the codeanalyzer's run)

- F2.3: remove stream's `ReadAllBytesAsync`/`ReadAllTextAsync` ("kept for v1 callers"; no production caller but the stream's own Read).
- F4.1: `property.IsDefault(value)` becomes async with a context, answered by the value's own `Compare`.

## codeanalyzer (plang-ae), area 5: the serializer (cd9381622)

| finding | checked | status |
|---|---|---|
| one wire serializer; Encode calls `data.Output`; sign-if-missing in Encode | its trace, followed from Encode into `data.Output` | cleared |
| F5.1 the writer contract still names `Normalize`, deleted this branch: `format/IWriter.cs:7,11,126`, `kind/json/writer.cs:12,129,155,194,200`, `NormalizeException` | grep: no live Normalize dispatcher; the docs and the exception's name describe a pipeline that's gone | fix (batch 1: comments and the error text; the exception's rename rides with F5.2) |
| **F5.2 (found by the architect checking F5.1; sent back to the codeanalyzer to write up)** a second write door decides by type: `IWriter.Value(object)` (`format/IWriter.cs:129`; json at `kind/json/writer.cs:131–203`) switches on the runtime type, with its own Data, dict and list cases beside the CLR primitives, while dict and list also write themselves (`dict/this.cs:175`, `list/this.cs:299`); and item has two self-write doors, the sync `Write(IWriter)` (`type/item/this.cs:838`, 61 overrides) and the async `Output` (`:916–918` calls the sync one "the OLD Peek().Write path"). Still reached from `type/kind/this.cs:340`, `type/item/this.cs:937`, `signature/this.cs:221`, `kind/dict/this.cs:45` | read in full; not on next-branch.md or obp-cleanup.md | pending the codeanalyzer's write-up; likely carry with a surface ask (IWriter and item are locked) |

**F5.2 confirmed** by the codeanalyzer (2c28ddcf7): fork and stored twice; surface change yes; doesn't block (both doors agree and are green), but the heaviest shape debt the merge carries, so named for Ingi. Its fix direction: one door, every value through its own `Output`; the writer's dict, list and item arms collapse to "the value writes itself"; the sync `Write`/`Value(object)` retire once their four callers move to `Output`. The miss was the brief's: area 5's paths left out `type/item/kind/` (+1,376, all new), now added; the codeanalyzer scans the rest of it.

- Added to the surface asks: **F5.2**, retire item's sync `Write(IWriter)` and `IWriter.Value(object)`'s plang arms (carry to the next branch; named as the largest shape debt).

## codeanalyzer (plang-ae), area 6: call and variable memory, events (e2417e57c)

| finding | checked | status |
|---|---|---|
| the `on/` events: a registry and one type per event kind, no dispatcher on the name; call/'s collections each lock their own private field; variable memory reading its own entries isn't a courier | its summary | cleared |
| F6.1 "is this a system variable" (`StartsWith("!")`) and "is it worth saving" (`is not DynamicData`) open-coded across variable memory | grep: 10 sites in `variable/list/this.cs` (`:362, 377, 394, 406, 408, 418, 438–441, 474, 496–497`), `this.Snapshot.cs:25–29`, `this.SnapshotAt.cs:39`. Two questions, not drift: a listing keeps `Now`/`GUID` (they're readable variables), a snapshot drops them (always fresh, not worth saving). The shape is each answering for itself: the name says it's a system name, the Data says it's always fresh (a member on data, locked). A private predicate would only gather the `is DynamicData` type check in one place | carry (next-branch) |

## codeanalyzer (plang-ae), area 5b: the item codecs, `type/item/kind/{dict,list,reflection}` (a397379d9)

| finding | checked | status |
|---|---|---|
| kind/dict and kind/list (the C# host codecs) | every behaviour on a carried list: member access via the reflection kind (673), Convert narrowing `clr(JsonElement)` (717), Output through `WriteReflected` (two more sites of F5.2), Set throwing on a bad index (59's remainder) | carried |
| kind/reflection | the carried 673 door; `Property`/`Method`/`Twice` (`reflection/this.cs:36/56/69`) are the stopgap statics 673 names; `Tagged.cs` the one owner of which C# properties ship ([Out]/[Sensitive]/[Masked], 681) | carried |
| F5.1 one more stale `Normalize`: `reflection/Tagged.cs:8` | grep confirmed | fix (batch 1, with F5.1) |

## codeanalyzer (plang-ae), area 7: the module registry and app (96e7f7955)

| finding | checked | status |
|---|---|---|
| actions resolve by a dictionary lookup, no switch on the action name; the clause/loop/keep/plain mint runs once at registration; `app` owns each collection once, no naked public collection, no service locator; path schemes register with factories (`app/this.cs:310–312`) | its reading | cleared |
| reflection at `module/this.cs:194, 203–215`, `app/this.cs:432`; the reader's thrown refusal at `app/this.cs:437` | cited | carried (673, 714) |

## codeanalyzer (plang-ae), area 8: the builder pipeline (5903dc85c), and the verdict

| finding | checked | status |
|---|---|---|
| the pick list's name forks (`Code()` on "loop.foreach"/"variable.set", IsCapture, the Type/Default/Name checks) plus one more of the family, the elseif/else chain read by name (`pick/list/this.cs:308`) | cited | carried (677) |
| Cover and Freeze: one answer (`action/this.Build.cs:92`), Cover doesn't re-walk it | read | cleared (b982cbc94) |
| the formal reader's parse by type name | the reader owns plang's surface grammar (the writer its inverse); parser design, not behaviour scattered on the types | cleared |
| F8.1 "is this an action slot": `Type.Name == "action" \|\| (Type.Name == "list" && kind.Name == "action")` | grep: four sites in three files, `action/this.Validate.cs:54–55`, `formal/reader.cs:221` and `:349`, `action/serializer/Reader.cs:153–154`. Stored twice, and "is X?" by name. The shape isn't a better predicate (`IsActionSlot`, a member on locked type); the action type and a list of actions each read their own value, so no site asks. A kind being a type (711) is where `list<action>` answers for itself | carry (next-branch, with 711) |

**Verdict (the codeanalyzer): PASS, merge-clean on behaviour.** Checked: agreed. Batch 1 (five fixes) is with the coder. The surface asks are F2.3, F4.1, F5.2 and F8.1, all carried to the next branch; none is needed for the merge. F5.2 was already on next-branch.md (added with 724).

## tester (plang-4f), part 1: the baseline

| finding | checked | status |
|---|---|---|
| the baseline reproduces: C# only the 6 carried; plang 245/16/12/3 in 2 of 3 clean runs, the same 16 names; `git status` clean after each run | its runs | cleared |
| run 2 was 244/17: the llm tests call api.openai.com live, and a transient "network unreachable" failed LlmCache | the llm tests need the network and a key | carry (next-branch: llm tests run offline, recorded answers or a skip without a key) |
| T1 `test/module/llm/LlmCache.test.goal` passes for the wrong reason: titled "verify second returns cached result", it only asserts both answers are not null | read the goal: the two asserts are `is not null` | fixed (builder, 8f8e0bebf: `assert %second!Cached% is true`, the query result's own cache-hit signal; red with the second query's cache off, then reverted) |
| `./dev.sh build` doesn't restore a fresh checkout | dev.sh's msbuild calls (`:205`, `:215`) run `-t:Build` without `-restore`. The tester's "exits 0" was its own pipe (`./dev.sh build 2>&1 \| tail -30` reports tail's exit); dev.sh exited 2 with NETSDK1004 | fixed (coder, 181468c11: a never-restored or changed project is restored first, serially; a failed restore exits 2) |

## tester (plang-4f), parts 2–5 (59cb79017; notes in `.bot/app-systems/tester/v23/`, report `test-report.json`)

| finding | checked | status |
|---|---|---|
| part 2: no mechanical wrong save in any of the 265 test `.pr` (dropped operand, type word as a truth check, dropped channel, an on.error doing nothing), except the carried goalchannelrecursion | its scan scripts (`smell.py`, `weakonly.py`) | cleared |
| T5 `module/goal/return/GoalCallReturn`: titled "with return value", asserts only not null | read: one assert, `%answer% is not null` | fix (builder: `equals 42`) |
| weak-only `app/stepresult/StepResult`: asserts only `%!data% is not null` after `set %x% = "hello"` | its notes | fix (builder: `equals "hello"`); the identity and binding smoke tests stay |
| T3 `plan/list-query/start.goal:47` calls `WhereUnsetVariable`; the file is `WhereUnsetVariableIsNotFound` | grep: confirmed | fix (builder) |
| T2 the task plan's plang rewrite is about 14 tests short (Stage 3 foreach-in-parallel, Stage 4 the builder goldens, three of Stage 2's list tests); `TaskTests.cs` (39) covers them all in C# | its notes | carry (next-branch: finish the task plan's plang tests) |
| T4a the run's coverage report marks on.cache and on.timeout uncovered though passing tests build them (94/142 undercounts) | its notes | carry (next-branch: the report counts clauses) |
| T4b no plang test for http.download/upload, timer.start/end, variable.get/exists | its notes | carry (next-branch's missing-tests list) |

**The tester's verdict: no merge blocker.** Agreed.

**The builder's fixes (9e74112f3):** GoalCallReturn asserts `equals 42` (red with 41, put back); the list-query index points at `WhereUnsetVariableIsNotFound`; StepResult asserts `%!data% is true`, not "hello": after `set %x% = "hello"` the step's answer is the set's success (`name.Set`), which the run showed (`Actual: true`). That answer differs from `set default`'s (the existing value, `variable/set.cs:130–134`), so what a set answers is on next-branch.md for Ingi. Suite 245/16/12/3.

## security (plang-a4), areas 1–6 (1aa3b7c05; report `.bot/app-systems/security/v1/report.md`, `security-report.json`)

| finding | checked | status |
|---|---|---|
| 382 holds: every actor gets read+execute on `/os/**` (`actor/permission/this.cs:29`), the system actor adds write+delete (`actor/list/this.cs:15–17`); a user's write under `/os/` is asked; the gate's path canonicalization still holds; the terminal sandbox fails closed (PermissionNotEnforced without Landlock 3); `[Sensitive]` kept out of Out, masked in Debug; settings rows per actor; PLNG002 holds | its report | cleared |
| F1 `signing.verify` throws on a live signature with no expiry: SignAsync allows `Expires` null (`signing/code/Ed25519.cs:36–37`); `signature.Expired` answers false for null (`type/item/signature/this.cs:80`); step 6 reads `signature.Expires!.Value` (`Ed25519.cs:129`) | read: confirmed; fails safe (the throw becomes a failed verify), but a legitimate sign-then-verify without an expiry fails | coder: trace first (does auto-signing reach it?), the shape is a replay-window choice |
| F2 the `/os/**` grant's glob is case-sensitive (`permission/this.cs:109–139`) while `Places` compares ignoring case (`path/file/this.Validate.cs:80–83`) | latent: both sides come from `app.OsAbsolutePath` today | carry (next-branch) |
| F3 standing grants keyed by path only, so the system actor's rights depend on the order of two `Stand` calls (`actor/permission/this.cs:36–47`) | intended overwrite today; an order dependence | carry (next-branch) |
| F4 Landlock 3 doesn't scope the network | the sandbox's own doc says so (`terminal/code/Sandbox.cs:27–29`) | carry (when the kernel has Landlock's network scope) |

**Security's verdict: PASS, no merge blocker.** Agreed; F1 is a bug to fix if the shape is small.

**F1 traced (the coder): latent, not reachable today.** A signature read live gets `Expires` = the earlier of the signer's expiry and Created + its window (`signature/this.cs:66–74`), and the wire reader, the only maker of a Live signature (`data/schema/signature.cs:96–101`), always passes the signing setting's Expiry (default 5 min). Sign-then-verify without the wire is Origin.Signed, so step 6 doesn't run. Both auto-signers sign with no Expires, and live reads still get Created + 5 min. Only a hand-built Live signature with no window throws (a 500, fails safe). Security's "a legitimate caller reaches it" doesn't hold. **Ruled C (731):** the signature's constructor refuses Live with no window; the window is unchanged.

## auditor (plang-90): the plans on the line

| plan | auditor's check-off | checked | status |
|---|---|---|---|
| scalars-as-native (7 stages) | all done: item apex, the 8 wrappers, `Data<T> where T : item, ICreate<T>`, ScalarComparer gone (only doc mentions), no `Data<rawCLR>` | grep: the constraint at `data/this.cs:509, 539, 566`; no live ScalarComparer | done |
| settings-config-unification | all done: `app/config/` gone, `module/settings` → `setting`, IConfig/IConfigure/ModuleView/http.configure/Config records gone; `context.Setting.Of<T>()` the live read door (the plan's `Resolve(keys)` shape differs; the plan let the coder own the shape) | `ls`/grep: config folder and the interfaces gone, `module/setting/` present | done |
| cli-app-property-override | all done: app.Build/Debug/Test nullable with public set, Mode from presence (`app/this.cs:175, 208, 214`); the command-line parser stays syntactic; Flags.Parse/Shorthand gone; `call/setting` a record | grep: the nullables and Mode at those lines; no Flags or Shorthand left | done |
| module-discovery (4a–4g) | done, junit evolved: `app.module` lowercase; MarkdownTeaching, getTypes, RegisterModuleChoiceTypes, Describe, StepActions, BuildTypeEntries gone; test.report a render call; junit kept as a `kind.Report` of test (272 superseded the plan's "delete") | 272 read (3af9c084d: junit a kind of test answering junit.xml). **One correction:** the auditor's "the `app.module.action.<name>` namespace move landed" is out of date: 9c (242) moved the handlers to `app/module/<m>/`, namespace `app.module.<m>` (`assert/isTrue.cs`, `variable/set.cs`), and no `app.module.action` namespace is left | done (with 242 and 272) |
| builder-formal | sent back for depth: the auditor checked stage 4 by its files only, and called `tools/decider/` the measurement harness; it was retired (533). Asked for stages 1–3 by their decisions in builder-formal's log, Settle's check (Cover/Agree), and the demolition table (`Properties.schema` gone and no `@store` special case under `os/system/builder`, both checked by me) | pending |
| app-systems | done: stages 1–12; 10b closed on its C# pins (696), its plang test waiting on the next branch's kinded Type choices | corrected two of the auditor's lines: 10b isn't a carried stage (696 closed it), and UnknownSettingIsAnError is its own next-branch item; `done.list`'s header and this log's "Now" were stale, both updated (27867d0bc) | done |
| builder-formal (corrected) | done: stage 1 decider v5 (e83acd9b1), stage 2 formal round-trip 58/58 (fbcbf36dd), stage 3 C beats B, gpt-5.4-nano + C (ffeb006a0), stage 4 formal reader/writer and `pick.list.Agree` (e5df078e7, `pick/list/this.cs:200`); demolition: `Properties.schema` gone, no `@store` special case, unchecked re-choosing refused by Agree; the Python tools retired (533) | the four commits exist with those subjects; Agree at `:200` | done |
| clr-navigators | done: a clr carrier navigable and convertible by its kind (merged 936b5fdf9); json and `*` reflection kinds under item; the runtime-DLL seam dropped (a954acfb2); the dead `Clr{Value:Data}` branch gone (19eb299b2); the container-to-scalar guard (a48bd3755); stage 6's `context.Ok(raw, kind)` superseded by 194 (the answer born through its kind's Decode); the `{item, json}` clr walk carried (717) | the four commits exist with those subjects; 194 read | done (6 superseded by 194) |
| compare-redesign (11 stages) | done but 11 (carried with 717): the Comparison enum, the async `Value()` door, path's file/http, per-value Compare, Operator's one registry, the PLNG003 public-surface gate, lazy params, born-typed Data | verdict holds; three evidence lines corrected: there's no `data/Compare.cs` (the entry is `item.Compare`, `type/item/this.cs:135`, through `Operator.cs:32–36`); `IOrderableValue`/`IEquatableValue` no longer exist as types; stage 7's gate is `PLang.Generators/Diagnostics/Plng003.cs`. Stale doc comments still name the two interfaces (`type/item/this.cs:23–24`, `bool/this.cs:10`, `null/this.cs:18`) | done; the comments to cleanup batch 2 |
| context-never-null | done: Data's context set at construction (`data/this.cs:34, 58–61`); Wire's context non-null, refused when missing (`data/Wire.cs:41, 55`); `ContextLessFallback` gone; `Step.Context` gone | grep: those lines; no `ContextLessFallback` | done |
| goal-graph-singular | sent back: areas 1, 1b, 2 hold (singular folders, `goal/this.cs:41` Step, `action/this.cs:39` Property, Info gone, modifiers → clauses via 8g); areas 0, 3, 4, 5 had no evidence. **Found by me:** `os/system/.build/debug.pr` keeps the old plural graph keys (steps, actions, parameters), committed 2026-04-27 (50351d8b9), with no `os/system/debug.goal` and nothing in `PLang/app` loading it: an orphan built file in the pre-singular shape. And `build.actions` isn't goals.cs's survivor: goals.cs is `[Action("goals")]` | pending; the orphan to Ingi (deleting a built file is his call, 720) |
| navigation-driven-record-builder | sent back on two rows: stage 1 cited stale comments (`goal/this.cs:26–27` "No item.@this base", `action/this.cs:10`) while `goal/this.Item.cs:9` and `action/this.Item.cs:16` make both items with ICreate, so stage 1 was superseded, not done as written; stage 0 "implicit". Stages 2–5 and the demolition read right (one Create door, kinds under their owner, catalog gone, the removals grep clean); snapshot's item-with-override carried as the plan named | pending; the two stale comments to cleanup batch 2 |
| goal-graph-singular (corrected) | done: areas 0 and 5 by `.bot/goal-graph-singular/architect/summary.md:3` (merged into get-builder-running, eeb9029b5..20fec8fb8, after a clean-build run); 1 the singular folders and goal/action as items (`goal/this.Item.cs:9`, `action/this.Item.cs:16`), not the stale "plain host" comments; 1b `Step`/`Child`/`Property`, Info gone; 3 no plural graph keys in os goals, templates or prompts (one dated trace kept as a record); `build.actions` was renamed `build.goals` in 9c (13497c68c). **F-GGS-1:** the orphan `os/system/.build/debug.pr` (pre-singular, no goal, nothing loads it) | done; F-GGS-1 to Ingi |
| navigation-driven-record-builder (corrected) | done; stage 1 superseded: goal, step and action became plang items (be6bbf294, 3839f4b86, 84c5a2802; `goal/this.Item.cs:9`, `action/this.Item.cs:16`); stage 0 closed by the removals that followed (all grep clean); stages 2–5 as before | the three commits exist with those subjects | done (1 superseded); the stale "plain C# host" comments (`goal/this.cs:26–27`, `action/this.cs:10`) to cleanup batch 2 |
| wire-source-split | done: `source : item` holding its declared type whole (`item/source.cs:14`); `wire : source` (`item/wire/this.cs:21`); `_format` and the `Serializers[...]` lookup gone (grep: 0); one render door, output.write → data.Output → item.Output (ruling 5; 89e4c60c6); the json kind owns its decode (rulings 6c, 8); `Variable.Resolve` a lookup (`variable/this.cs:213`); materialize-on-foreign-output parked (1820da640) | the class lines, the grep, the three commits; rows 7 and 8 cite rulings rather than code, accepted since the rulings are the plan's own and their code is what areas 1 and 5 of the codeanalyzer read. Note: "json materializes as clr" is the shape 717 carries, and "item.Output → item.Write" is the spine F5.2 carries | done |
| get-builder-running (no plan.md; its summary.md) | done: prompt B, the 5-goal golden set, build.match (ff3b0dd9c), ElseWithoutIf to SourceError (fa1130a07), `throw …, fix suggestion` (527769af8), on-error clauses as one try/catch in order (cee6039d5), TypeSafe registered (c4da3717d); the Python pieces (4c44c3a41, b7b23509b) done then retired (533); modifier.list landed as 8g's clauses; the bar met at builder-formal round 6 (da8d2497a, 174/174, 0 silent) | the eight commits exist with those subjects | done |
| test/plan/app-systems | done (as the branch plan above) | — | done |
| test/plan/task | carried: about 14 plang tests not written (Stage 3 foreach-in-parallel, Stage 4 builder goldens, three of Stage 2's list tests), all covered in C# (`TaskTests.cs`) | the tester's T2; on next-branch.md (729) | carried |
| test/plan/list-query | done: stage 3's four actions gone (`module/list/` has `query.cs`, no where/sort/group/unique); its tests already live in the concept tree (`test/module/list/query/`, not under `test/plan/list-query/` as the auditor wrote); the index fixed (9e74112f3). One doc line still names `list.where`: `PLang/app/data/this.code.md` | `ls module/list/`; grep | done; the doc line to cleanup batch 2 |
| test/plan/snapshot | carried whole (665) | the 6 C# tests tagged carried; the plan not built | carried |
