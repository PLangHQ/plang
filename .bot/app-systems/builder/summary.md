# builder — summary

## Version
v2 (2026-10-02) — builder bug-fixing round (continuous, driven by architect's issue queue
`.bot/app-systems/architect/builder-issues-2026-10-01.md` + cross-session reports). v1 was the
docs/familiarization pass (see v1/plan.md).

## What this is
The builder's role: other bots report mapping bugs (a natural-language step compiling to the wrong
`.pr`); builder maps each to the PLang-written builder and fixes the builder's own source
(`os/system/builder/**`, `os/system/modules/**` teaching, `PLang/app/module/build/**`) so a developer
gets a correct mapping first try. Core (`goal/step/**`, `app/**`, `type/**`) → diagnose + hand to the
coder via architect. Fixes are measured in fresh folders, cache off (a cache:false rebuild of an
UNCHANGED goal now really rebuilds — see the cache fix below; before it, same-folder loops silently
skipped).

## Landed this session (all pushed, accepted unless noted)
- **Write-back** (cd4fdcc62 + 7a45c0ebb): destination-less `list.query` writes its answer back
  (`build.Match`→`WriteBack`), + insert-position/parsed-variable follow-ups. Goldens + pins.
- **cache:false fix** (414a1cfa3): `build.Goals` skips `MergePrData` when cache is off, so an unchanged
  goal rebuilds in full — every measurement depends on this. Automated test still owed.
- **Issue 21** (9b928dcd8 decider principle + 37d795cb6 golden): `if <file> exists` surfaces file.exists
  (decider.state principle) and the coder's `[Question]`/`action.Place` leads it before the if.
- **Issue 9** (5523eeb46 + pin 53e9e2022): a json/dict `body` post → http.request (not http.upload) —
  disambiguated the request/upload descriptions; build-only C# pin `HttpBuildPinTests`.
- **Issue 29** (c28f03b6e): `assert X contains Y` → Container=X, Value=Y (was swapped) — the mapping was
  taught only in the description's 2nd paragraph, which never reaches the writer; moved to notes.
- **Issue 28 partial** (910a7b8dd): hash Algorithm note. Full fix rides issue 25's Option question.
- **Issues 1 & 31** (2829786ff): `call goal X`/`call the goal X` drops the keyword (it's the word "goal",
  not the slash — slash paths resolve fine).
- **Issue 25 rename→revert**: Variables rename (5ad2929a5) then superseded — Ingi chose `Template` as a
  choice of template-kind + a decider stage-2 Option question (decisions 496/506). Shape handed to coder.
- **Issue 30 cleanup**: deleted unused MapVariables + leftover Run.goal + /system/Build.goal; rebuilt the
  reopened os/ .pr (AsDefault→Default) + tracked StartWindow.pr. os/ build now reaches SetupApp.
- Removed a premature LoadVars golden (42410d4d4); .bot bookkeeping.

## Key pattern (teach the writer, not the dev's goal)
The Properties writer reads an action's **notes** (not examples, not a description's 2nd+ paragraph) and
anchors on the decider's `=> formal:` starting line. Two recurring lessons:
1. Teaching that must reach the writer goes in `*.notes.md` (tagged `say:`/`builder:`), never only in
   examples or a description's later paragraphs.
2. A param the writer must set from a trigger word it can **drop** (Algorithm's "with sha256", load-vars'
   "load vars", Conversation's "%answer%") is unreliable by teaching alone (~1-3/5) — the fix is the
   decider surfacing it into the starting line (issue 25's Option question), so the writer copies it.

## Blocked on the coder (I measure/finish after)
- Issue 25 v1 Option question: core = template-kind type, `Kind.Option`, `pick/list` Questions/Take/
  Prefill, reading the option's note `ask:` tag. Then I write decider2.template's `when "Option"` case +
  the `ask:` lines + teaching. **Finalized shape:** always a choice over the kind's Values + "none";
  Prefill enters the chosen value; Template first, then Algorithm (now `choice<hash>`, 82346436f).
- Issue 26(1) Conversation: Option v2 for a non-choice option — offers = the step's own variables + none.
- Issue 30: SetupApp `app.event` has no serializer Reader (`app/type/app.event/serializer/Reader.cs`);
  then `set as developer` over-fill is mine.
- Issue 24, 26(2) BeginArray: fixed by the coder (closed).

## Blocked on Ingi
- `Wait`→`Parallel` collapse (goal.call): `Parallel` already means "concurrent AND wait" (tool loop),
  so folding "don't wait" in would make `call X in parallel, write to %r%` lose the result. Paused.

## Closed since (later in v2)
- **Issues 25 & 28** — the decider **Option question** (coder's core 26eef5568; mine: decider2.template's
  `when "Option"` case + the `ask:` note lines on file.read Template & crypto.hash Algorithm). load-vars →
  `Template=plang` 5/5, hash → `Algorithm=sha256` 5/5, guards clean. The lever teaching couldn't be.
- **Issues 1 & 31** — `call goal X`/`call the goal X` drops the keyword (it's the word "goal", not the slash).
- **on.event** — the event is `start`/`error`, never the fused `on.before`/`on.after`/`on.end` (the When).
- **Condition/loop notes tagged** (decision 411); plang-d2 owns the page render + goldens.
- **Leftovers deleted** (unused, unreferenced): MapVariables, Run.goal, /system/Build.goal, AskSystem.

## The os/ build tail — HANDED TO A FRESH SESSION
`plang build` from os/ no longer dies early (llm regression, formal reader, SetupApp all fixed), but each
reopened hand-authored system goal surfaces its own step the builder can't cleanly rebuild. Architect's
two-pass plan (note: 819f239c6 moved **no** goals — it changed only C#/tests, the four goal flags now
derive from the goal's path, and the `.pr` dropped IsSetup/IsSystem/IsTest/IsEvent; AskSystem was already
under `os/system/events/`):
1. **Sweep** every `os/**/*.goal` for who references it (C#/.goal/template/.llm/doc/`call`-by-name); send
   architect the unreferenced list → delete in one commit.
2. **Rebuild each remaining reopened goal** individually (`--build={"files":[…],"cache":false}`), don't stop
   at the first failure; one table: goal, built/not, refusal, class — **leftover** (delete), **writer
   mis-map** (builder's teaching fix), **missing action param/core** (coder, e.g. output.ask has no Actor),
   **write in formal**. Architect routes from the table; builder does the writer-mis-map rows.

## Blocked on Ingi
- `Wait`→`Parallel` collapse (goal.call): `Parallel` already means "concurrent AND wait" (llm tool loop),
  so folding "don't wait" in would make `call X in parallel, write to %r%` lose the result. Paused for Ingi.
