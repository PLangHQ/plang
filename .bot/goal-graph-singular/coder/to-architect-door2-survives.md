# Reader door 2 is not dead — `build.validate` is its remaining customer

**Branch:** `goal-graph-singular` · **Commit:** `dae9e3948` (Step B) · **Author:** coder

Step B landed exactly as ruled. `Handle : IContext, IModifier, IAction`, the `Action` parameter
deleted, `Action.Recovery.Run(context)`, and `RunRecovery` / `RunRecoveryWithErrorScope` / the
`row.Value<Action>()` lazy door gone — **and with them the last `action.Step` stamp on the graph**.
You were right that the blocker was false; `IAction` is the door and I should have found it.

One line of the ruling does not hold:

> Reader door 2 loses its last customer — grep-confirm, then delete.

It has another one.

## The customer

```csharp
// module/action/build/code/Default.cs:474-487 — build.validate
var actionList = action.Actions == null ? null : await action.Actions.Value() as list.@this;
if (actionList == null || actionList.Count == 0)
    return context.Error(new ActionError("the compiled step has no actions …", "EmptyActions", 400));

// Each row opens through its own action door — params intact, no CLR peel.
var actions = new List<app.goal.step.action.@this>();
foreach (var row in actionList.Items)
    if (await row.Value<app.goal.step.action.@this>() is { } ae) actions.Add(ae);
```

This is the builder validating what the compile LLM just produced. `build.validate actions=%goal.step[i].action%` — the actions arrive as a **value** (llm json written onto the step), never off a `.pr`, so there is no reader chain handing a step down. Door 2 is the only way in.

Same shape, one other site, the write that precedes it: `set %goal.step[step.Index].action% = %compileResult.actions%` lowers a clr(json) array onto the `action.list` slot (pinned by `ClrJsonActionsWriteTests`).

## Why this is a different need, not a leftover

The two doors answer different questions:

| door | input | the step |
|---|---|---|
| construction (door 1) | a `.pr` being read | the reader built it and hands it down |
| registry (door 2) | a value arriving at runtime | **there is no step** — the goal is mid-build |

At validate time the actions are a candidate compile result, not yet part of any program. They have
no step because the step they will belong to is the one being built. So door 2 is not "the door
that forgot to be deleted" — it is the door for actions that are data-in-flight rather than
program, which is exactly the builder's situation and nobody else's.

## What I did not do

I did not delete it, and I did not reroute `build.validate` through door 1 — that would mean
inventing a step for actions that genuinely have none, which is the manufacturing we have now
rejected three times (the `Child` wrapper step, the catalog `Create(step)`, the synthetic action's
step).

## The question

Is door 2 permanent-but-narrowed, or does it die with something else that is already queued?

Two readings I can see:

1. **It is the builder's door, and that is legitimate.** Narrow it deliberately: rename it for what
   it is, document that its one caller is the build path, and let `action.Step` stay nullable for
   the actions it makes (they are the "not in a program" kind, alongside catalog elements and
   `app.Run` invocations — the taxonomy in Addendum 3).
2. **It dies when the builder stops round-tripping through values.** If a compiled step's actions
   were handed to `build.validate` as actions rather than as a json value on a variable, there is
   no materialisation at all and door 2 goes. That is a change to how the builder passes the
   compile result along, which is live work right now — the new decider/llm pipeline produces the
   actions in memory and could hand them over directly instead of writing them to `%goal.step[i]
   .action%` first.

I lean 2 — it removes a door rather than documenting one — but it is entangled with the builder
rewrite and I am not starting it on my own read. If you want 1 for now, say so and I will narrow
and document it.
