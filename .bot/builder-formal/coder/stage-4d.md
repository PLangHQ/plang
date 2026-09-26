# 4d — the plang builder builds (branch `builder-formal`)

## What works

**Green demo.** A new app, one goal, built by the installed plang builder (real decider + nano calls),
then run with `plang`:

```
Start
- set %total% = 5
- write out "hello, the total is %total%"
- if %total% > 4, write out "that is big"
```
```
hello, the total is 5
that is big
```

**The builder is installed.** `os/system/builder/**/.build/*.pr` are written by C# itself: python
(`tools/decider/bootstrap.py`) only had the two remote conversations (decider + nano) for each of the
builder's 12 goals; C# parsed the .goal files, took those answers through the builder's own path (Pick,
the types walk, Read with every check, fold) and wrote the .pr with plang.Text. A committed test
(`BootstrapTests`) replays the recorded answers: C# refuses exactly what python refused.

| file | | goals, steps |
|---|---|---|
| .build/build.pr | replaced (old shape) | Build 11 |
| .build/buildgoal.pr | replaced | BuildGoal 1 |
| .build/builderchannel.pr | rewritten | BuilderChannel 0 |
| .build/emitbuildevent.pr | replaced | EmitBuildEvent 2 |
| BuildGoal/.build/start.pr | replaced | Start 14, Compile 3, FixSteps 3, BuildSubGoal 8, SourceError 4, HandleBuildFailure 4 |
| BuildGoal/.build/decide.pr | new | Decide 13 |
| BuildGoal/.build/properties.pr | new | Properties 5 |

**The builder rebuilds itself.**
- In place (`plang build` of os/system/builder): every step is kept, nothing asked, all 7 files saved
  unchanged.
- From scratch (a copy of the builder's .goal files, no .pr, cache off): Build, EmitBuildEvent,
  BuilderChannel, BuildGoal and Decide were rebuilt. Their code is identical to the installed one in 26
  of 27 steps; the 27th exposed a goal-name bug (fixed, below).

## What fails, loudly

- **The scratch self-rebuild stops at Start.goal**: Compile step 2
  (`build.match …, on error key "ElseWithoutIf" call SourceError, on error call FixSteps first, then
  retry 1 times`) and HandleBuildFailure step 0 are still refused after nano's retry. Hard multi-clause
  steps; not chased.
- **AddItem** (`set %total% = %total% + %item%`): refused. Nano writes the arithmetic, but either the
  decider doesn't pick math.add or nano drops the write to %total%; both are now caught. Arithmetic in a
  set waits for the action-as-value design.

## Speed and cost

`plang build`, per goal (from the builder's own output):

| | Build | EmitBuildEvent | BuilderChannel | BuildGoal | Decide | Properties | Start (6 goals) |
|---|---|---|---|---|---|---|---|
| from scratch | 3.4 s | 2.4 s | 1.7 s | 1.1 s | 3.5 s | — | — |
| unchanged (kept) | 2.3 s | 0.8 s | 1.4 s | 1.6 s | 3.3 s | 1.2 s | 7.7 s |

Cost per goal: about $0.001 for nano (the 5-goal eval: $0.0053), plus two decider calls.

## Bugs found by running it (all fixed, each with a test)

- The builder's LLM messages were sent unrendered: nano got `%propertiesSystemMsg%` literally. A
  list/dict holding `%x%` is now born a template, and its .pr row says so.
- A goal-backed channel written in a goal still running tripped the goal-cycle check. Ingi's ruling:
  recursion is allowed; the depth limit is the only one.
- A build looked up a run-time channel (ChannelNotFound at build); a channel is now found at run.
- `llm.query Message=%var%` was refused at build; a dict didn't read a JSON text; the decider was called
  with no questions when every step was kept.
- `BuildGoal/Start` matched any goal named Start (the scratch rebuild wrote a call to itself).
- The files filter: a relative entry matched nothing and the build then failed on an unset variable;
  now it is from the app root, and no match is `NoGoalMatched`.
- Defaults are frozen into the .pr, typed by the slot, when a step takes its code.
- Coverage moved to where the answer is read: goal.call's Build drops a redundant `x=%x%`, and the
  save refused what Read had accepted.

## Silent misses closed tonight (checker gaps, also in the todos)

- An action held as a value in a value slot (`variable.set(Value=math.add(…))`) is refused.
- A step whose words write `%x%` (`write to %x%`, `%x% = …`) must write it.
- A goal the step's words call (`call FixSteps`, `call /system/builder/X`) must be called by its code —
  top level, a body or a recovery; dropping a whole `on error call X` clause is refused.
- Still open (needs design): a variable counts as covered when it only appears inside a quoted text.

## Since the first report

**An unchanged build is instant.** Before: the builder's 7 files, all unchanged, took 20.9 s (nano asked
about every kept step, 9.6 s; 5× FixSteps on all-kept answers, 3.5 s; the first render creating the
folder's identity, 4.7 s). Now **~0.75 s, ~0.5 s of it process start**, no .pr rewritten:
- `goal.IsCached` (its source is its `.pr`'s, every step and sub-goal cached) → Start returns
  `%goal.Cache%` as its first step.
- `goal.Step.IsAnswered` (every step cached or written in formal) → Compile asks no one and only reads.
- The 4.7 s is paid once per project folder (see the eager-render todo).

**A step written in formal is its code** (`module.action(…)` at its start): asked of no decider and no
LLM, read with every check an answer gets. A goal of formal steps alone builds with no key, in 0.1 s.
The builder uses it for its hardest steps: Compile's match step (`Order="GoalFirst"` — the installed
one had RetryFirst, so FixSteps' corrected answer was never matched) and BuilderChannel.
`BuilderPinTests` pin both.

**The builder prints its own output again**: a goal-backed channel hands the written value to its goal
as `%message%`.
```
Building path: …/hello
Found 1 goals
Building goal: Start
  Saved Start (2.3s)
```

**Also closed**: a bare `if %x%` is Left's own truth (a path: does it exist) and a comparison with no
Right is refused at build; `action.Validate` asks the handler, so a cached step that no longer holds
reopens; an invented text value (`channel="X"`) is refused; a name in an action slot is refused instead
of crashing; a cached LLM answer holding `%name%` comes back text, not a template (it crashed the build).

**Open**: the reader stamps any `%var%` string a template whatever its mode (wire-serialization.md,
"Data from outside is never a template"); a template render opens every variable in scope; a bare
word a step relies on (`to echo`) isn't covered — Tests/Channels/GoalChannelRecursion stays unbuilt.
**BootstrapTests fails on Compile** until its answers are re-recorded: the decider service answers
403 "RBAC: access denied" since ~06:30.

## Commits tonight

308bb583f pre-fill · 2c150a5b3 bootstrap + take path · be36e72a7 D fixes · 008f7d61c recursion allowed ·
2d4ea7909 install · 3b4d22f74 templates · 01e26c342 builder builds · 634bc4fed kept-step decider ·
ced91ee25 held action · 37204b577 FixSteps · 723724275 files filter · 825d1630c coverage in Read ·
db0d1f1c6 qualified names + write target · goal-name coverage (the last commit)
