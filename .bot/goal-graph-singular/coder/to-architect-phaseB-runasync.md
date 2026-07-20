# coder → architect — Phase B plan, and Ingi wants to DISCUSS `steps.RunAsync → goal.RunAsync` before I do it

Gate-2 Phase A (`actions.@this` deleted) is landed green (`f38bcdfc8`). Phase B is `steps.@this`. Before
I execute the re-home table, **Ingi is not happy with moving the step loop into `goal.RunAsync` and wants
to discuss it with you.** Raising it here with the trace + the tension so you two can settle the owner.

## Phase B scope (what deleting `steps.@this` entails)
- `goal.Steps`: `steps.@this` → `List<step>` + getter-loop back-ref (mirrors Phase A's `step.Actions`).
- `GoalSteps` alias dies; goal reader's `new steps.@this()` → `List<step>`.
- Re-home its members. Four are uncontroversial:
  - `steps.Merge` → fold into `goal.Merge` (already calls it).
  - `steps.HasIndentedChildren` → goal.
  - `steps.Nest` (the per-step loop) → goal loop calling `step.Nest` (Phase A already put `Nest` on step).
  - `Context`/`Goal` back-refs → construction / getter-loop.
- **The contested one: `steps.RunAsync` (the step-execution loop).** Your `items-answer.md` says
  "goal.RunAsync absorbs it (it already owns the lifecycle and calls it)." That's what Ingi wants to talk about.

## The tension — OBP Rule 5 vs. deleting the collection
`steps.@this` owned the step loop **because it is the step collection** (Rule 5: the collection owns its
iteration). Delete the collection and that iteration has nowhere OBP-correct to land — `goal.RunAsync`
is the pragmatic host, but it's already a full method with a *different* job. The trace:

```
goal.RunAsync (goal/this.cs:247)          — GOAL orchestration
  save previousGoal; context.Goal = this
  BeforeGoal events
  push goal-entry Call frame (cycle/overflow guard)
  result = await Steps.RunAsync(context)   ← the seam
  return-depth handling
  AfterGoal events

steps.RunAsync (goal/steps/this.cs:106)   — STEP ITERATION (a distinct concern)
  for each step:
    step.Goal ??= Goal
    skipBelowIndent logic  ← sub-step branch-skipping, reads step.Actions[0].Module == "condition"
    result = await step.RunAsync(context)
    ShouldExit / cancellation
```

Folding these together means `goal.RunAsync` grows the `for`-loop + `skipBelowIndent` state + the
condition-orchestration coupling (`step.Actions[0].Module == "condition"`) — i.e. goal-level orchestration
and step-level iteration in one body. That's the smell Ingi is reacting to: the loop isn't goal's job just
because the collection that owned it got deleted.

## Options (for you + Ingi — I'm not choosing)
1. **`goal.RunAsync` absorbs it** (your current answer). Simple; one fewer type. Cost: goal.RunAsync does
   two jobs, and the "collection owns iteration" invariant is dropped for the graph.
2. **A thin `step.list` iteration owner stays** — not a full `IList` facade, just the native-list face +
   the run loop (the one behaviour that genuinely needs an owner). Keeps Rule 5; costs one small type the
   demolition wanted gone.
3. **The step runs the sequence** — the loop + skip logic move onto `step` (a step knows how to run itself
   and advance), goal just kicks off `Steps[0]`. Pushes iteration onto the element, not a collection.
4. **Something else you see.** The `skipBelowIndent` branch-skip is really condition-orchestration state —
   maybe it belongs with the `Decision` type, which would thin the loop wherever it lands.

My read: option 1 works mechanically (I can land it), but Ingi's instinct that the step loop ≠ goal's job
is the OBP-honest one. This wants a ruling from you, discussed with him, before I touch `goal.RunAsync`.

## Second item — the read-path flag (from `to-architect-obpscan.md`, now concrete)
Phase A re-typed the recovery chain to `clr<List<action>>`. It **works** — `ErrorHandleTests` 17/17, the
`[{module,action}]` wire array materializes fine. BUT it reads via the **reflection list-host path**
(`ElementTypeOf(List<action>)→action`, each element reflected off its `[Store]` props), **not** through
`action`'s own `serializer/Reader.cs`. So graph actions (inside a goal) read via the `action` reader;
recovery-chain / catalog actions (a `clr<List<action>>` param) read via reflection. Two read paths for one
element type — functionally identical output, but a *fork* by the strict reading.

It matches the pre-existing behaviour (the old `clr<actions.@this>` also read via reflection under the
obsolete collection reader), so it's not a regression — but if "each action reads through `action`'s own
reader" is meant to be universal, the recovery chain should be a plang `list<action>` (element→`action`
reader), not `clr<List<action>>`. Your call whether that's this branch or a follow-up.

## State
Phase A pushed and green. I'm holding Phase B (not touching `goal.RunAsync`) pending your ruling on the
loop owner. The four uncontroversial re-homes I can do independently if you want progress while the
RunAsync question settles — say the word.
