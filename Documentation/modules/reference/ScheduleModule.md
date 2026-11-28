# ScheduleModule

Hint: `[schedule]`  
Type: `PLang.Modules.ScheduleModule.Program`

Wait, Sleep and time delay. Schedule a goal to be called repeatedly ('every 45 seconds', 'every 5 minutes', 'every day at 8am'), interval and cron scheduler

## Methods

### EnableScheduler

```
EnableScheduler() : object
```

Clears --no-scheduler/--standby and starts the scheduler. e.g. `enable scheduler`


Examples:

- enable scheduler =>

### Schedule

```
Schedule(PLang.Models.GoalToCallInfo goalName, String cronCommand = null, Nullable<Int64> intervalInSeconds = null, Nullable<DateTime> nextRun = null) : object
```

Calls goalName repeatedly, on every 'every ...' / 'each ...' / cron statement. The step registers the job and returns at once; it does not block the steps after it.
Set exactly one of intervalInSeconds or cronCommand.
intervalInSeconds: when the statement is 'every N seconds/minutes/hours/days' and names no clock time, weekday or day of month. Convert N to seconds. The goal runs every N seconds counted from when it was scheduled, not aligned to the clock. Examples:
'every 5 seconds, call X' => 5
'every 45 seconds call goal X' => 45
'every minute' => 60
'every 5 minutes, call X' => 300
'every 90 minutes' => 5400
'every hour' / 'hourly' => 3600
'every 2 hours' => 7200
'every day' / 'daily' => 86400
cronCommand: when the statement names a clock time, a weekday, a day of the month or a minute of the hour. A cron expression of 5 fields (minute hour day-of-month month day-of-week), or 6 fields with seconds first (second minute hour day-of-month month day-of-week) only when a second is named. Evaluated in UTC. Examples:
'every day at 8am' => '0 8 * * *'
'every day at 7:30am' => '30 7 * * *'
'at 11:00 on Monday' / 'on mondays at 11 am' => '0 11 * * 1'
'every weekday at 6pm' => '0 18 * * 1-5'
'at minute 0 of every hour' / 'on the hour' => '0 * * * *'
'at 15 and 45 minutes past every hour' => '15,45 * * * *'
'on 5th of every month at 7am' => '0 7 5 * *'.
goalName is the goal that should be called, a whole word with possible slash(/), written without a prefix.

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
StartScheduler(Boolean force = False) : object
```

Starts the scheduler. Does nothing when the app was started with --no-scheduler or --standby, unless force is true. e.g. `start scheduler`, `start scheduler, force`


Examples:

- start scheduler => force=false
- start scheduler, force => force=true

### WaitIncreasingly

```
WaitIncreasingly(String key, List<Int32> millisecondsDelay = null, Int32 timeoutInSeconds = 300) : object
```

Waits increasingly in a key


### WaitOnVariable

```
WaitOnVariable(String variableName, PLang.Models.GoalToCallInfo goalToCall, Int64 timeInMilliseconds = 1000) : object
```

Blocks the step until the existing variable %variableName% has not been changed for timeInMilliseconds, then calls goalToCall once (debounce). Only use when the user names a variable to wait on, e.g. 'wait until %searchText% has not changed for 500ms, call Search'.
NEVER use for a recurring call such as 'every 15 seconds call X' or 'every 5 minutes, call X'; use Schedule for those.

- `variableName` *String*
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `timeInMilliseconds` *Int64*, default `1000`

