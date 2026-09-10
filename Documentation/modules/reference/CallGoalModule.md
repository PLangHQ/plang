# CallGoalModule

Hint: `[callgoal]`  
Type: `PLang.Modules.CallGoalModule.Program`

Call a Goal, e.g. `call goal Process`, `call Visits`

## Methods

### RunGoal

```
RunGoal(PLang.Models.GoalToCallInfo goalInfo, Boolean waitForExecution = True, Int32 delayWhenNotWaitingInMilliseconds = 50, UInt32 waitForXMillisecondsBeforeRunningGoal = 0, Boolean keepMemoryStackOnAsync = False, Boolean isolated = False, Boolean disableSystemGoals = False) : Object
```

Call/Runs a goal. If backward slash(\) is used by user, change to forward slash(/)

- `goalInfo` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `waitForExecution` *Boolean*, default `True`
- `delayWhenNotWaitingInMilliseconds` *Int32*, default `50`
- `waitForXMillisecondsBeforeRunningGoal` *UInt32*, default `0`
- `keepMemoryStackOnAsync` *Boolean*, default `False`
- `isolated` *Boolean*, default `False`
- `disableSystemGoals` *Boolean*, default `False`

Examples:

- call goal Process %name% => GoalToCallInfo.Name=Process, GoalToCallInfo.Parameters={"name":"%name%"}

