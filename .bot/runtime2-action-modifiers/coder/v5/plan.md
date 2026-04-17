# Coder v5 Plan — Auditor Fixes

Addressing auditor v1 findings: 1 major, 3 minor.

## Fix 1: GoalCall shared-state mutation (major, auditor F1)

**File:** `PLang/App/modules/error/handle.cs:106-128`

**Problem:** `CallErrorGoal` mutates `goalCall.Parameters` (line 113) and `goalCall.Action` (line 126) on the shared deserialized GoalCall singleton. Under concurrent execution, two invocations race on the same GoalCall.

**Fix:** Build a local GoalCall copy instead of mutating the original:

```csharp
private async Task<global::App.Data.@this> CallErrorGoal(GoalCall goalCall, global::App.Data.@this failedResult,
    Actor.Context.@this context)
{
    var parameters = (goalCall.Parameters ?? new())
        .Where(p => p.Name != "!error")
        .Append(new global::App.Data.@this("!error", failedResult.Error))
        .ToList();

    // Clone — never mutate the shared deserialized GoalCall
    var call = new GoalCall
    {
        Name = goalCall.Name,
        Description = goalCall.Description,
        Parallel = goalCall.Parallel,
        Parameters = parameters,
        PrPath = goalCall.PrPath,
        Action = context.Step?.Actions.FirstOrDefault() ?? goalCall.Action
    };

    // Record error on the callstack for history
    var callStack = context.CallStack;
    if (callStack != null)
    {
        var action = context.Step?.Actions.FirstOrDefault();
        if (action != null && failedResult.Error != null)
            callStack.PushError(action, failedResult.Error, context.Variables);
    }

    return await context.App!.RunGoalAsync(call, context);
}
```

## Fix 2: Step.Clone modifier asymmetry (minor, auditor F2)

**File:** `PLang/App/Goals/Goal/Steps/Step/this.cs:168-173`

**Problem:** Modifier clones copy only Module, ActionName, Parameters — missing Defaults, Errors, Warnings that parent action clones include.

**Fix:** Mirror the parent action clone pattern:

```csharp
Modifiers = new ActionModifiers(a.Modifiers.Select(m => new Action
{
    Module = m.Module,
    ActionName = m.ActionName,
    Parameters = new List<Data.@this>(m.Parameters),
    Defaults = m.Defaults != null ? new List<Data.@this>(m.Defaults) : null,
    Errors = new List<Info>(m.Errors),
    Warnings = new List<Info>(m.Warnings)
}))
```

## Fix 3: cache.wrap cached Data mutation (minor, auditor F3)

**File:** `PLang/App/modules/cache/wrap.cs:34`

**Problem:** `cached.Name = "__data__"` mutates the cached Data object by reference.

**Fix:** ShallowClone before mutating:

```csharp
var result = cached.ShallowClone();
result.Name = "__data__";
context.Variables.Put(result);
return result;
```

## Fix 4: GroupModifiers drops leading modifiers silently (minor, auditor F4)

**File:** `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs:58-68`

**Problem:** If LLM starts a step with a modifier action (no preceding executable), it's silently dropped.

**Fix:** Add an Info warning to the step's Warnings list when this happens. GroupModifiers needs access to the Step (already available via `this.Step`).

```csharp
if (modules.IsModifier(action.Module, action.ActionName))
{
    if (current == null)
    {
        Step?.Warnings.Add(new Info
        {
            Key = "DroppedLeadingModifier",
            Message = $"Modifier '{action.Module}.{action.ActionName}' has no preceding action and was dropped"
        });
        continue;
    }
    current.Modifiers.Add(action);
}
```

## No new tests needed

These are all defensive fixes on existing paths. The existing test suite (2150/2151) covers these code paths already. The GoalCall clone fix is verified by the existing `DoesNotMutateOriginalParameters` test pattern.
