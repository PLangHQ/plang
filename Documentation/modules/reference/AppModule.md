# AppModule

Hint: `[app] (ambiguous, matches AppModule, WindowAppModule)`  
Type: `PLang.Modules.AppModule.Program`

Call an App. When the user has the word 'app' in his statement, this should be called.

## Methods

### RunApp

```
RunApp(PLang.Models.AppToCallInfo appToCall, Boolean waitForExecution = True, Int32 delayWhenNotWaitingInMilliseconds = 50, UInt32 waitForXMillisecondsBeforeRunningGoal = 0, Boolean keepMemoryStackOnAsync = False) : Object
```

Call/Runs another app. app can be located in another directory, then path points the way. goalName is default "Start" when it cannot be mapped

- `appToCall` *PLang.Models.AppToCallInfo* — (see Type information in SupportingObjects)
- `waitForExecution` *Boolean*, default `True`
- `delayWhenNotWaitingInMilliseconds` *Int32*, default `50`
- `waitForXMillisecondsBeforeRunningGoal` *UInt32*, default `0`
- `keepMemoryStackOnAsync` *Boolean*, default `False`

