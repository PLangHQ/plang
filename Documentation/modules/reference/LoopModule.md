# LoopModule

Hint: `[loop]`  
Type: `PLang.Modules.LoopModule.Program`

While, for, foreach, loops, repeat, go through a list and call a goal

## Methods

### Repeat

```
Repeat(Int32 repeatCounter, PLang.Models.GoalToCallInfo goalToCall, Int32 startIndex = 0) : object
```

Repeat a call to a function number of time. Great for pagination, where repeatCount is page count and startIndex is page number. startIndex starts at 0

- `repeatCounter` *Int32*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `startIndex` *Int32*, default `0`

### RunLoop

```
RunLoop(String variableToLoopThrough, PLang.Models.GoalToCallInfo goalToCall, PLang.Modules.LoopModule.Program+MultiThreaded multiThreaded = null, PLang.Modules.LoopModule.Program+LinqOptions linqOptions = null) : object
```

Predefined variables are %list%, %item%, %position%, %listCount%, user can overwrite those using parameters, e.g. `- go through %products%, call goal ProcessProduct item=%product%`, parameter key is "item" and value is "%product%". cpuUsage is percentage of cpu cores available, 80% => 0.8

- `variableToLoopThrough` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `multiThreaded` *PLang.Modules.LoopModule.Program+MultiThreaded*, default `null` — (see Type information in SupportingObjects)
- `linqOptions` *PLang.Modules.LoopModule.Program+LinqOptions*, default `null` — (see Type information in SupportingObjects)

