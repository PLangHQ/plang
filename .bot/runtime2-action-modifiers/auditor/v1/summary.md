# Auditor v1 Summary — runtime2-action-modifiers

## What this is

Cross-cutting audit of the action modifiers feature, which promotes `onError`, `cache`, and `timeout` from step-level JSON properties to per-action modifier actions using `IModifier.Wrap()`, right-to-left fold, and builder grouping.

## What was done

Reviewed all prior bot reports (codeanalyzer v1, tester v1-v4, security v1), then independently verified:

- **Serialization round-trip**: Action.Modifiers survives build → .pr → deserialize → runtime. The `[Store]` attribute on Modifiers, STJ's ability to populate `IList<T>` collections with parameterless constructors, and the builder's GroupModifiers call all work correctly.
- **Clone family**: Step.Clone correctly deep-copies modifier chains, with one minor asymmetry (modifier clones miss Defaults/Errors/Warnings fields). Step.Clone is not currently called anywhere.
- **GoalCall mutation**: Confirmed still unfixed. `handle.cs:113` reassigns `goalCall.Parameters` and `:126` stamps `goalCall.Action` on the shared deserialized singleton.
- **OBP compliance**: Modifier fold, smart collections, navigate-don't-pass all followed correctly.
- **Test suite**: 2150/2151 pass (1 unrelated LLM snapshot failure).

## Verdict: FAIL

**1 major, 3 minor, 2 nit findings.**

The major finding is the GoalCall shared-state mutation in `error/handle.cs` — flagged by both codeanalyzer (low) and security (medium) but never addressed across 4 coder iterations. The fix is trivial: clone GoalCall instead of mutating it.

### Key finding

**error/handle.cs:106-128** — `CallErrorGoal` mutates the deserialized GoalCall singleton:

```csharp
// CURRENT (mutates shared state):
goalCall.Parameters = parameters;           // line 113
goalCall.Action ??= context.Step?.Actions.FirstOrDefault();  // line 126

// FIX (clone instead):
var call = new GoalCall {
    Name = goalCall.Name,
    Parameters = parameters,
    Action = context.Step?.Actions.FirstOrDefault() ?? goalCall.Action,
    PrPath = goalCall.PrPath,
    Parallel = goalCall.Parallel,
    Description = goalCall.Description
};
return await context.App!.RunGoalAsync(call, context);
```

## Recommendation

Send back to **coder** for the GoalCall clone fix (Finding 1). The 3 minor findings can be addressed in the same pass or deferred.
