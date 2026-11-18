# ScheduleModule

Hint: `[schedule]`  
Type: `PLang.Modules.ScheduleModule.Program`

Wait, Sleep and time delay. Schedule a goal to be called repeatedly ('every 45 seconds', 'every 5 minutes', 'every day at 8am'), interval and cron scheduler

## Methods

### Schedule

```
Schedule(PLang.Models.GoalToCallInfo goalName, String cronCommand = null, Nullable<Int64> intervalInSeconds = null, Nullable<DateTime> nextRun = null) : object
```

Calls goalName repeatedly. Set exactly one of intervalInSeconds or cronCommand.
intervalInSeconds: for 'every N seconds/minutes/hours/days' with no clock time, weekday or day of month. The goal runs every N seconds counted from when it was scheduled, not aligned to the clock. Examples:
'every 45 seconds' => 45
'every 5 minutes' => 300
'every hour' / 'hourly' => 3600
'every day' / 'daily' => 86400
cronCommand: when the statement names a clock time, a weekday, a day of the month or a minute of the hour. 5 fields (minute hour day-of-month month day-of-week), or 6 fields with seconds first when a second is named. Evaluated in UTC. Examples:
'every day at 8am' => '0 8 * * *'
'every day at 7:30am' => '30 7 * * *'
'at 11:00 on Monday' => '0 11 * * 1'
'at minute 0 of every hour' => '0 * * * *'
'on 5th of every month at 7am' => '0 7 5 * *'.
goalName is the goal that should be called, a whole word with possible slash(/), written without a prefix.

Each due job runs like a goal called with `dont wait`, in parallel with other jobs. A job whose previous run is still going skips that occurrence.

- `goalName` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `cronCommand` *String*, default `null`
- `intervalInSeconds` *Nullable<Int64>*, default `null`
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

Debounce: blocks the step until the existing variable has not changed for timeInMilliseconds, then calls goalToCall once. Not for recurring calls; use Schedule for those.

- `variableName` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `timeInMilliseconds` *Int64*, default `1000`

