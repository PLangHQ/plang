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

## Commits tonight

308bb583f pre-fill · 2c150a5b3 bootstrap + take path · be36e72a7 D fixes · 008f7d61c recursion allowed ·
2d4ea7909 install · 3b4d22f74 templates · 01e26c342 builder builds · 634bc4fed kept-step decider ·
ced91ee25 held action · 37204b577 FixSteps · 723724275 files filter · 825d1630c coverage in Read ·
db0d1f1c6 qualified names + write target · goal-name coverage (the last commit)
