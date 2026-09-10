# ScheduleModule

Hint: `[schedule]`  
Type: `PLang.Modules.ScheduleModule.Program`

Wait, Sleep and time delay. Cron scheduler

## Methods

### Schedule

```
Schedule(String cronCommand, PLang.Models.GoalToCallInfo goalName, Nullable<DateTime> nextRun = null) : object
```

cronCommand is a standard 5-field cron expression (minute hour day-of-month month day-of-week). Map the natural-language interval to the cron fields exactly; do NOT collapse an interval to a single daily run. Examples:
'every minute' => '* * * * *'
'every 5 minutes' => '*/5 * * * *'
'every 10 minutes' => '*/10 * * * *'
'every 1 hour' / 'every hour' / 'hourly' => '0 * * * *'
'every 2 hours' => '0 */2 * * *'
'every day at 8am' => '0 8 * * *'
'every day at 7:30am' => '30 7 * * *'
'at 11:00 on Monday' => '0 11 * * 1'
'on 5th of every month at 7am' => '0 7 5 * *'.
goalName is the goal that should be called, it should be prefixed by ! and be whole word with possible slash(/).

- `cronCommand` *String*
- `goalName` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `nextRun` *Nullable<DateTime>*, default `null`

### Sleep

```
Sleep(Int32 sleepTimeInMilliseconds) : object
```

WaitForExecution is always true when calling Sleep


### StartScheduler

```
StartScheduler() : object
```


### WaitIncreasingly

```
WaitIncreasingly(String key, List<Int32> millisecondsDelay = null, Int32 timeoutInSeconds = 300) : object
```

Waits increasingly in a key


### WaitOnVariable

```
WaitOnVariable(String variableName, PLang.Models.GoalToCallInfo goalToCall, Int64 timeInMilliseconds = 1000) : object
```

- `variableName` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `timeInMilliseconds` *Int64*, default `1000`

