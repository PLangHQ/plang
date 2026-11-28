# CallGoalModule

Hint: `[callgoal]`  
Type: `PLang.Modules.CallGoalModule.Program`

Call a Goal, e.g. `call goal Process`, `call Visits`. A step that starts with `call goal X` or `call X` belongs here whatever the goal X is named: `call goal ShowError message="..."` runs the goal ShowError with the parameter message, it does not throw or show an error itself

## Methods

### RunGoal

```
RunGoal(PLang.Models.GoalToCallInfo goalInfo, Boolean waitForExecution = True, Int32 delayWhenNotWaitingInMilliseconds = 50, UInt32 waitForXMillisecondsBeforeRunningGoal = 0, Boolean keepMemoryStackOnAsync = False, Boolean isolated = False, Boolean disableSystemGoals = False, Boolean onlyExplicitReturn = False) : Object
```

Call/Runs a goal. If backward slash(\) is used by user, change to forward slash(/)

- `goalInfo` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `waitForExecution` *Boolean*, default `True`
- `delayWhenNotWaitingInMilliseconds` *Int32*, default `50`
- `waitForXMillisecondsBeforeRunningGoal` *UInt32*, default `0`
- `keepMemoryStackOnAsync` *Boolean*, default `False`
- `isolated` *Boolean*, default `False`
- `disableSystemGoals` *Boolean*, default `False`
- `onlyExplicitReturn` *Boolean*, default `False`

Examples:

- call goal Process %name% => GoalToCallInfo.Name=Process, GoalToCallInfo.Parameters={"name":"%name%"}
- call goal ShowError message="Code not found" => GoalToCallInfo.Name=ShowError, GoalToCallInfo.Parameters={"message":"Code not found"}

