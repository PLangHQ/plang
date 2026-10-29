# Auditor — event-action v1

**Verdict: FAIL (narrow).** Two pre-merge items (A1, A2); everything else is minor and deferred.
**Next bot: coder** — A1 (a fix of a few lines plus a test) and A2 (two pins). Then re-check.

Scope: runtime2 6703697fb to 434763384 (tip; 156afc9c5 is inside it). 353 files; all 327 source files read whole
(I read `event/this.cs`, `event/list/this.cs`, `event/on/this.cs`, `event/call/this.cs`, the `variable/set.cs` diff myself;
three read-only agents read the other event files, the rest of `PLang/`, and teaching/goals/tests, marked "agent-read" and
re-verified where a finding is ranked). `Tools/Bench` and `.pr` files were not read for smells (bench: codeanalyzer's lane).

**Patterns first.** The design follows 11 (lazy: `Loaded()` is the one door, claimed by the first asker, `Add` awaits it,
`Join` pushes only into lists already made) and 12 (`Item.Has(item)` is the one question; `Skips`, `Takes` and `App` sit on the
event). I found no eager placement and no second answer of weight; the findings below are about the seams.

## Tests (run by me, keys unset, PlangConsole rebuilt)
- C# sweep: Modules 1500, Types 1042, Wire 641, Data 881, Generator 191, Runtime 1481 — all green, 0 red.
- `plang --test`: 323 total, 322 pass, 0 fail, 0 timeout, 0 stale, 1 skipped; tree clean.
- Not run: live llm gate (needs a key); no mutation tests (the log forbids production edits for this audit).

## Previous reviews assessed
- **codeanalyzer (v5, 66f038a84): partial.** The shape work holds (one door, Remove has no type test). It did not read the
  file-name replace against decision 17 (A1), and `Add(name)` still carries an `empty` type test (B1).
- **tester (v5, cae38b107): partial.** Suites green and the plang plan tests are real (BindOrder, LateEventJoins,
  SystemGoalsLeftAlone, UnbindTakesTheEvent each fail if the behaviour breaks). Two pins are weak (A2); six goals the plan
  index calls do not exist (B6).
- **security (v2, 156afc9c5): agree** on F1/F2 as fixed. The probes pin the maker's check running, but not the F3 rule
  directly (A2), and the file-name hole in A1 is the same class as F1/F2 and was not probed.

## Findings

### A1 — major, pre-merge — contract / decision 17 vs code (security class of F1/F2)
`PLang/app/event/list/this.cs:173` with `PLang/app/event/this.cs:66`. A sub app does not take in a maker's event when
`made.File` is one of the file names in the sub app's own `system/event/`. `File` is the goal's file **name** only
(`Goal.Path.Name`), with no folder. Decision 17 says the sub app's own `system/event/` file replaces what the maker bound
**from the file of the same name** (the maker's system event). The code replaces an event bound in *any* goal of that name.
Scenario (verified by reading, not run): the maker binds `before each goal, call Check` in `/Start.goal`; an upload ships
`system/event/Start.goal`; the maker's check never joins the sub app. That is the escape 91 closed for `system/`.
Pattern: 12 (the event answers "which file am I", and the answer drops the folder). Missed by: codeanalyzer, security
(no probe collides a name outside `system/event/`; the tests use `Hello`/`Hello` and `Own`/`Start`).
Suggestion: replace only an event whose goal sits under the maker's `system/event/` (the event states whether its file is a
system event), and add a test with a maker event bound in `/Start.goal` and a sub app that ships `system/event/Start.goal`.
If Ingi wants replace-by-any-name, say so in the log and pin that instead.

### A2 — major, pre-merge — review-gap: two claims pinned by tests that pass vacuously or not at all
- **F3 `Takes(raised)`** (`event/this.cs:58`, used at `event/on/before.cs:63`): no test calls it. `SubOwnEventsProbeTests`
  B, B2, B3, C, C2, D assert only `worked IsNull`, true also when `Work` never ran. Only B4/E assert the `Denied` key (E is F1).
  Need: a maker's event fails; the sub app's own `on error` does **not** take it, while a failure of the sub app's own
  event **does** reach it.
- **`Sub=none` on a resumed snapshot** (`SubNoneSnapshotProbeTests.cs:54-59`): asserts `after == before`; `Count` returns
  -1 when the read fails and `resumed.Error` is logged, never asserted, so a refused resume (identity/consent) or two
  failed counts pass. The control only asserts `before > 0`. Need: assert the resume succeeded and that `before > 0` for
  the control side while `none` stays 0 in the sub app.
Missed by: tester, security.

### Minor (deferred; `plan/later.md`)
- **B1** (12, `on/this.cs:96-102`): `Add(name)` switches on `empty` inside the base class — an `is X` inside the answer.
  Let the moment answer (`this[]` returns what to make, or a virtual on the moment).
- **B2** (`list/this.cs:102-118`): `Join` appends to `_events` before adding to the made lists; `on.Add` throws `NoEvent` for a
  list whose item lacks the moment, leaving the event half-joined. `module/on/event.cs:67` checks the name against the
  reached item only. Plausible; validate before joining.
- **B3** (error rule, `list/this.cs:221`): a system event that fails to load is cracked to `started.Error?.Message` on the
  error channel (silent when there is no channel); the app then runs without it — fail-open. Answer the Error whole; whether
  the load should fail is Ingi's.
- **B4** (`test/this.cs:136`, `test/list/this.cs:187`): `Coverage.Watch`/`Time` now await the events' load (a failed load throws)
  outside the `try/finally`, so one app whose events fail to load aborts the whole run and may leave the coverage event bound.
- **B5** (11, cost, `variable/list/this.cs`): a variable object is made at every set, including the runtime's `!data` after every
  action (`step/action/this.cs:207`), to ask "is anything bound on this name". Ask the actor's events by name first; make the
  variable on yes. The `Names` walk (`variable/this.cs:106`) follows an answer path through live memory and can load the
  builder actor's events at validation.
- **B6** (`test/plan/event-action/start.goal`): calls six goals that do not exist (`OsEventLoads`, `AppReplacesOsEvent`,
  `SubNoneStaysHome`, `ReturnTwoLevels`, `CallerAppFindsTheGoal`, `AppOverridesOsGoal`), so the plan index fails if run; the
  shipped `os/system/event/Hello.goal` is never run end to end ("This is system event" is in no test).
- **B7** (teaching, `os/system/modules/on/event.notes.md:9`): says the event name is "only `start` (or `error`, or a channel's
  `write`/`read`/`ask`)" while the examples teach `.on.set`, `.on.create`, `.on.click`; `%!event.name%`/`.when` and
  `event.setting.goal`/`.system` are not taught; 11 `Properties:` examples remain in 8 files beside the typed form. A teaching
  edit re-runs the decider fixtures.
- **B8** (stale words): `setting/this.cs:3-5` and `unbind.cs:4` ("when it starts", "at its birth"), `InheritedBindingsTests.cs`
  names/comments (`..._AtBirth_...`), `module/on/event.cs:6` (`%!channels.audit…`), `plan/start.md` "stage 2 waits".
- **B9** (question): `code.@this` does not override `IsInheritable`, so a maker's C# events (debug watch, test timing,
  coverage) go to a sub app as `inherited` and run against the sub app's steps. Confirm that is wanted.
- **B10** (boy scout, older): `item._on` is a field on every item for one subclass (the action); `goal/list/this.cs:358`
  `seen.Add(source.Raw)` beside `source.Equals`; `pattern.Matches(string)` takes a CLR primitive; creative names
  (`stepped`, `answers`, `given`, `binding`, `bare`, `saved`, `made`, `joined`); `goal.list.Find` of a name with `*`/`?` now
  answers a pattern, so a runtime name fails at `Start` as `NotAGoal` (400), not `NotFound` (404).
- **B11** (note): `Skips(goal)` on the event and `Skips(context)` on the list are two different questions under one name;
  `Takes` reads `context.call.event` at catch time, correct only because the frame restores it in `finally`.

Known and ruled, not re-raised: the `_events` scan on a set with no list (73, 81); the request app's events inside its
maker's `system/` copies (92a); a hidden denial (92b); F4; the writer scores on event paths.

## Locked core
`Has`, `IsMany`, `Is(pattern)` (item base), `goal.App`, `[Kinds]` on channel/action, `event.App`, `Takes`, `Skips`: each is
covered by a named decision (39, 42, 43, 45, 46, 92) and the surface golden re-pins cite them. No unapproved surface change.
