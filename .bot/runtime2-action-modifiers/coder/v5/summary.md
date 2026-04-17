# Coder v5 Summary — Auditor Fixes

## What this is

Fixes for 1 major + 3 minor findings from the auditor v1 review of the action modifiers feature.

## What was done

### Fix 1: GoalCall shared-state mutation (major, auditor F1)

**File:** `PLang/App/modules/error/handle.cs:106-130`

`CallErrorGoal` was mutating `goalCall.Parameters` and `goalCall.Action` on the shared deserialized GoalCall singleton. Under concurrent execution, two invocations would race on the same object. Fixed by creating a local GoalCall copy with the modified parameters and action, leaving the original untouched.

### Fix 2: Step.Clone modifier asymmetry (minor, auditor F2)

**File:** `PLang/App/Goals/Goal/Steps/Step/this.cs:168-176`

Modifier clones in Step.Clone were missing Defaults, Errors, and Warnings fields that the parent action clone includes. Added all three fields to mirror the parent pattern.

### Fix 3: cache.wrap cached Data mutation (minor, auditor F3)

**File:** `PLang/App/modules/cache/wrap.cs:33-37`

`cached.Name = "__data__"` was mutating the cached Data object by reference. Fixed by calling `ShallowClone()` before mutating, so the cache entry stays pristine.

### Fix 4: GroupModifiers leading modifier warning (minor, auditor F4)

**File:** `PLang/App/Goals/Goal/Steps/Step/Actions/this.cs:60-69`

When a modifier action has no preceding executable action, it was silently dropped. Now adds an Info warning to the step's Warnings list with key "DroppedLeadingModifier" so the developer gets a signal.

## Code example

The major fix (GoalCall clone):

```csharp
// Before (mutates shared singleton):
goalCall.Parameters = parameters;
goalCall.Action ??= context.Step?.Actions.FirstOrDefault();
return await context.App!.RunGoalAsync(goalCall, context);

// After (local copy):
var call = new GoalCall
{
    Name = goalCall.Name, Description = goalCall.Description,
    Parallel = goalCall.Parallel, Parameters = parameters,
    PrPath = goalCall.PrPath,
    Action = context.Step?.Actions.FirstOrDefault() ?? goalCall.Action
};
return await context.App!.RunGoalAsync(call, context);
```

## Test results

2150/2151 pass (1 pre-existing unrelated failure). No regressions.
