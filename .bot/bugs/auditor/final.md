# bugs — final audit (auditor)

**Verdict: FAIL, two small pre-merge items (B1, B2).** Next: coder, then a short re-check. Tip bbc127ba9, parent runtime2
3a29633f8. Everything else holds. The design is sound, and the two blockers are a few lines each.

At the tip: build clean. Sweep: Modules 1,385, Types 973, Data 879, Generator 191, Runtime 1,388 green; Wire 608 with
6 red, the same six names as the parent (snapshot/resume). `plang --test` without llm: 305 total, 294 pass, 1 fail
(BareLiteralJudgedByForm, the parent's), 3 stale, 7 skipped; the 2 new goals (stage 4) are among the passes. The tree is
clean afterwards (no `*.test.llm.json` changed). Bench: I did not re-run it; I read codeanalyzer's by-commit rows (root birth
+48 KB, all in stage 1's two types and the IAnswer entry; time in band). Method: two read-only slice agents (stages 1–3,
4–7) with file:line evidence, each claim of theirs that I rely on re-read by me (B1, B2, `PLang.csproj:38-39`).

## Blockers

| id | sev | file:line | issue | missed by |
|---|---|---|---|---|
| B1 | major (cross-file, Release only) | `PLang/app/module/screen/type/screen/code/Shown.cs:56-57,162-165`; `window/code/Window.cs:167`; `view/this.cs:172` | A frame field of 2³¹ or more crashes the host in Release. Release builds with `CheckForOverflowUnderflow` (`PLang.csproj:38-39`), so `(int)ReadUInt32…` for w, h, n (`:56-57`) and `-dx`, `x + w`, `Width - dx` in `Move` (`:162-165`) throw `OverflowException`. The decode loops catch only Json, Format, Argument, IndexOutOfRange, InvalidOperation, KeyNotFound and AppException (`Window.cs:167`, `view/this.cs:172`), so it ends the background thread and with it plang. A browser page sends these frames. Debug builds are unchecked (`(int)0xFFFFFFFF` is -1 and `Fits` refuses it), so no test sees it. The casts are older code (same lines at 3a29633f8), but S-F2 (bugs 32) says the bound is before any rent for every form, and this form escapes it. Fix: read w, h, n as `uint`/`long` and refuse before narrowing; do `Move`'s arithmetic in `long`; refuse as BadFrame; one test run under `checked {}`. | codeanalyzer, tester, security |
| B2 | major (contract, N3's second door) | `PLang/app/type/item/path/file/this.Operations.cs:498` | `Keep` now answers its save's result (`this.Authorize.cs:57-61`), and the single-verb door returns it, so "an always whose save failed is not a yes" (N3). The move/copy door does `await each.First().at.Keep(...)` and discards the answer, then runs `PerformTransfer`. A failed "always" save proceeds as a yes, silently. No test at this door. Fix: return `Keep`'s failure; one test beside `AnAlwaysAnswer_WhoseSaveFailed_IsNotAYes` for a copy. | all |

Cheap, fold into the same commit (not blockers): **C1** `PLang/app/channel/this.cs:139,148-149,159,185,194,197,252`: `Context` is non-nullable
now (N3), but `Context ?? throw "belongs to no list"`, `Context ?? data.Context`, `context != null` and `context!` remain, and
comments at `:103,180` still describe a channel with no list; dead after this branch's own change. **C2** the identity
export teaching: `os/system/modules/identity/export.notes.md:3` and `export.examples.md:1` still say export "hands out the
private key"; only `export.description.md` changed (N1 says the teaching says so). A notes edit sets off the decider re-pins
(OpenAI calls), which is probably why it was skipped: either do it at the gate, or Ingi accepts the description alone.

## Decisions 1–33 and the N-items

All hold in code except as above. Evidence (file:line):

| item | found |
|---|---|
| N1 export refuses | `identity/code/Default.cs:198-201` `ExportWaitsForCriticalActions` 403 before any ask; IdentityPrivateKeyLeakTests (`Export_Refuses_UntilCriticalActions`, `TheExportQuestion_IsNeverAsked`). Teaching: C2. |
| N3 | `permission/this.cs:200` Add answers Save; `this.Authorize.cs:57-61` Keep answers it; the second door: B2. |
| N4 | `_standing` and `_once` apart (`permission/this.cs:28-29,57,206-209`); Find walks standing, once, persisted (`:147-148`); Revoke reaches only `_once` and persisted (`:223-238`); GrantKeepTests. |
| N5 | `PersistedGrantCannotOverrideStoresTests.cs:46,48` really saves and asserts Find non-null; the gate is `this.Authorize.cs:71-73`. |
| N8 | `permission/this.cs:92-101`: cancelled, IO, ObjectDisposed and Unanswered are denials; the rest answers through `Error.FromException`. ConsentAskTests. |
| N10 | `kind/json/reader.cs:59-61`. |
| N11 | size `Reader.cs:24,28`, parallel `Reader.cs:25`, choice `this.cs:87-90` and courier `:111-113`, `kind/this.cs:319`; the two plain guards stay (`choice:83`, `kind:345`). Decision 14 leaves `kind:218,231` plain; `start.md:51` still lists them (drift). |
| N14 | `Executor.cs:128,138` seeds from `app.filesystem.root`; `APath_IsSeededFromTheRoot` (StartRefusalTests.cs:71). |
| N15 | `path/this.cs:173-183` `Holds`, one rule; `Validate.cs:49,58,89,91` with RootComparison. |
| N16 | `start/this.cs:46,48,129`; `Executor.cs:82,97-100`; `start/property/list/this.cs`; StartRefusalTests 13. |
| N17 | `OsTrustTests` asks `IsUnder` (`:54`); `'/'` compares remain at `:141,151,169` (Windows run). |
| fast-apps N1 | `module/this.cs:175-192`, `origin/code.cs`, `actor/this.cs:216` answer `data<bool>`, ModuleShared answered not thrown; ModuleRemoveTests. |
| fast-apps N2 | `Wiring.cs`, ctor `app/this.cs:301`, Executor and Bench pass `Wiring.none`; `autoWireConsoleChannels` appears nowhere outside `.bot`. Internal bools remain behind it (`origin/this.cs:21`, `code.cs:32`, `channel/list/this.cs:95`), default false, internal. |
| fast-apps N3 | `channel/this.cs:31` ctor takes the list; `:100` `Actor => Channels.Actor!`; `:106` get-only; `channel/list/this.cs:165-169` Born guard; no `Adopt`, no late `Actor =` in production; ChannelBirthTests. |
| Stage 1 | answers file `Saved.cs` (`goal.Parent.Combine(goal.Stem + ".llm.json")`); body only, key in `Live.cs` only (grep of the six `.llm.json`: no key/bearer); changed prompt = new entry; Keep on pass only (`test/this.cs:162`); S-F1 ordinal match; `OpenAi.cs:208` `answer.Ask`, no `if testing`. Live_WritesNothing and Saved_AnswersFromTheFile run through `test.Start`. |
| Stage 4 | `channel/type/goal/this.cs:117-120` writer's context, `writer.Run(Actor, …)`; the catch answers AppException whole, else GoalChannelError holding `Error.FromException`; `actor/context/this.cs:74-82` `Run` is the one door (`actor/list/this.cs:38`); both plang goals and their `.pr` are committed. |
| Stage 6 | `Window.cs` holds a `Shown` (`:30`) and calls `Screen().Apply` (`:265,335`); no QOI decoder, no delta logic, no kept decoder, no `_max` mirror; `Shown.Put` bounds each part then the frame's sum, before any rent (`:97-104`), except B1. Windows-only paths read, not run. |
| Stage 7 | as the N rows above. |

## Locked surface

Four golden moves, each in a commit naming its decision: 9f31620f3 `type exchange` (module-owned, bugs 7); 66f0456c0
`module.Remove` answers `data<bool>` (fast-apps N1, Ingi 2026-10-25); 363bfd5da `enum app.Wiring` + the app's ctor (N2);
ad0caedd7 channel constructors take their list, `Memory`'s first parameter (N3). `test.setting.llm` and the `answer`
choice add no golden line (a module-owned choice and a test setting, the edge). `SurfaceTests` was not touched. Nothing else
on a locked surface moved: `Run` is internal, `Holds`, `Fits`, `Encodes`, `Stands`, `start.Refused` are internal with
production callers.

## Rule 35 and the boy scout rule

No production member, branch, flag or hook exists only for a test (checked by both slice reads and a grep for
`InternalsVisibleTo` additions, `if testing`, reflection hooks). `IAnswer`/`Saved`/`Keep` is the `plang --test` feature
(decisions 4, 5). Touched files read whole (permission, Authorize, Saved/Live/IAnswer/exchange, Shown, Bitmap, Window,
window/this, view, channel/list, goal channel, Executor, start, Validate, Run): no System.IO, no Console, no late stamp, no
cracked Error that the branch added. OpenAi.cs's older smells excused per bugs 23; what the branch added to it
(`OpenAi.cs:201,209` the streamed test written twice beside `query.IsStreamed()`) is a nit. Smalls from the reads:
`Saved.cs` keeps `_exchanges`, `_asked`, `_written` (three parallel collections) with locals `asking`/`asked`; `Default.cs:205-207` comment
still says Consent hands out a private key; `test/this.cs:143,162` writes `app.Code.Get<IAnswer>().Provider!` twice.

## Tests: the plan's names

`start.goal` names tests that do not exist as written: `IdentityExportTests` (the tests live in
IdentityPrivateKeyLeakTests), `ExecutorTests.APathIsSeededFromTheRoot` (it is `StartRefusalTests.APath_IsSeededFromTheRoot`),
`AppConsoleTests` (no such class; `Stage6_EntryPointWiringTests` pins Wiring.none, nothing pins that the default `Wiring.console`
wires the console), `APatchBeforeTheFirstFrame` (on the Windows run's list, by design). Fix the names in `start.goal` when the
plan moves into the concept tree. Not tested: a streamed ask through `Saved` goes live (`Saved.cs:418`); the Windows-only
paths; `ActorRunMemoryProbeTests` only assert `DoesNotContain("CALLERS-SECRET")`, so an error or a disposed-context failure
also passes (add one test that returns a literal across actors and reads it after the child is disposed).

## Next-branch list

| id | file:line | what |
|---|---|---|
| X1 | `type/item/duration/serializer/Reader.cs:24,27`, `clipboard/serializer/Reader.cs:24`, `input/serializer/Reader.cs:54,57,65` | N11's family: duration is the exact twin of size and still throws raw FormatException (lands as MaterializeFailed 400, not the type's own Error). |
| X2 | `path/file/this.cs:132-140`, `module/build/code/Default.cs:39` | other copies of the root-containment rule: `StartsWith` with a separator, and one without any boundary; both can ask `root.Holds`. |
| X3 | `window/code/Window.cs:203` | comment still describes `z` base64 deflate; text frames carry `qoi`. `Window.cs:112` canvas sized by the opener, not bounded by the max. `process/this.cs:221` reads the setting per pipe message. |
| X4 | `module/module/remove.cs:16`, `os/system/modules/module/remove.description.md` | bare `Task<Data>` reads "returns item" though it answers `data<bool>`; teaching doesn't say true/false/ModuleShared (a teaching edit: decider re-pins). |
| X5 | `OsTrustTests.cs:141,151,169` | `'/'` compares; Windows run. |
| X6 | `Executor.cs:39-41`, `app/this.cs:375` | "auto-wire" wording; `origin.code(Wiring = none)` defaults opposite to the public ctor's `console`. |
| X7 | `Documentation/v0.2/bans.md:35`, `good_to_know.md:110`, `filesystem-permission.md:71` | still say export hands the key after a yes; `_inMemory` no longer exists. Docs. |
| X8 | `Saved.cs` | the plan says the file holds body and endpoint path; it holds the body only. Fine for one provider; a note for llm-provider. |
| X9 | `Saved.cs:418` | a streamed-goes-live test. |
| X10 | open, not this branch: the json writer's setting (Ingi), Fluid's unencoded `{{ x }}` (`ui/code/Fluid.cs:187`, Ingi), the Windows run (backlog 8), `Publish/*.goal` (2.0b), the Linux view's unbounded message queue (security's note). |

## Reviews assessed

Codeanalyzer (PASS, two reviews): agrees on the design, the bench attribution and F1–F8; it missed B1, B2, C1.
Tester (PASS, 13 mutations): agrees on the sampled mutations; the plan's test names, ActorRun's weak asserts and the
streamed test were not looked at. Security (PASS, two Lows fixed): the screen bound is sound for ordinary sizes; the
Release-only overflow (B1) escaped it. Debug and Release differ here, so security's probes (run in Debug) could not see it.
