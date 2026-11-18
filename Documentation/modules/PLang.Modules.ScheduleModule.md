
# Schedule Module in PLang

## Introduction
The Schedule module in PLang is a powerful feature that allows developers to manage time-based tasks efficiently. It provides the ability to pause execution and to call goals repeatedly, either every N seconds, minutes, hours or days, or at clock times such as "every day at 8am".

## For Beginners
If you're new to programming or unfamiliar with technical jargon, think of the Schedule module as an alarm clock or a calendar reminder for your code. It lets you tell your program to "wake up" and do something at a specific time, after a certain period has passed, or repeatedly at regular intervals.

For example, you might want to send out a weekly newsletter, check for updates every hour, or even just delay an action for a few seconds. The Schedule module helps you do all of this without having to constantly check the time yourself.

## Best Practices for Schedule
When using the Schedule module in PLang, it's important to follow some best practices to ensure your code runs smoothly and efficiently:

1. **Plan Ahead**: Decide whether a task should run every N seconds/minutes/hours/days (an interval) or at a clock time, weekday or day of month (a cron schedule).
2. **Keep Runs Shorter Than The Interval**: A job never runs twice at the same time. If a run is still going when the job is due again, that occurrence is skipped (and logged).
3. **Handle Exceptions**: Always anticipate and handle exceptions that may occur during the execution of scheduled tasks.
4. **Test Thoroughly**: Test your scheduling logic extensively to ensure it behaves as expected under various conditions.
5. **Use Comments**: Use comments to explain the scheduling logic, which can be quite complex, to help others (and your future self) understand the code.

Here's a simple example to illustrate scheduling a task:

```plang
Start
- every 1 hour, call ReminderToDrinkWater
- start scheduler

ReminderToDrinkWater
- write out "Time to drink water!"
```

In this example, the goal `ReminderToDrinkWater` is called every hour, counted from when the app started.


# Schedule Module Examples

## 1. Sleep for a Short Duration

This example demonstrates how to pause the execution for a short duration, such as 1 second.

```plang
SleepShortDuration
- sleep for 1 second
```

## 2. Call a Goal Every N Seconds, Minutes, Hours or Days (interval)

"every N seconds/minutes/hours/days" is an interval. The goal runs every N units, counted from when the job was scheduled, not aligned to the clock. "every 45 seconds" means 45 seconds between runs.

```plang
Start
- every 45 seconds, call CheckQueue
- every 5 minutes, call RefreshCache
- every 2 hours, call SyncProducts
- every day, call CleanUp
- start scheduler
```

The next run is the previous scheduled run plus the interval, so the period does not drift when a run takes time. If the app was busy or down while several periods passed, the job runs once and the next run is the next period in the future; missed runs are not caught up one after another.

## 3. Call a Goal at Clock Times (cron)

A statement that names a clock time, a weekday, a day of the month or a minute of the hour is built as a cron expression.

```plang
Start
- every day at 8am, call SendDailyReport
- on mondays at 11, call WeeklyCleanup
- every weekday at 18:00, call CloseRegister
- at minute 0 of every hour, call HourlyStats
- on the 5th of every month at 7am, call MonthlyInvoice
- start scheduler
```

Cron expressions have 5 fields, `minute hour day-of-month month day-of-week`, e.g. `0 8 * * *` for every day at 8:00. Their precision is one minute.

When a second is needed, write 6 fields with the seconds first, `second minute hour day-of-month month day-of-week`, e.g. `30 0 8 * * *` for every day at 08:00:30.

Cron expressions are evaluated in **UTC**: "every day at 8am" runs at 08:00 UTC.

Quartz-style expressions such as `0 0/5 * * * ?` (with `?` or a year field) are not valid.

## 4. Start the Scheduler

Scheduled jobs run once the scheduler has been started. Schedule the jobs, then start the scheduler, usually in `Start`:

```plang
Start
- every 10 minutes, call Backup
- start scheduler
```

The scheduler checks every second whether a job is due.

## 5. How Jobs Run

- Each job runs like a goal called with `dont wait`: on its own engine, with its own variables, in parallel with other jobs. A slow job does not delay another job.
- A job does not overlap itself. If its previous run is still going when it is due again, that occurrence is skipped and a warning is logged; the job runs again at its next scheduled time.
- A job has no execution time limit; a run that takes long only causes its own skipped occurrences.

## 6. Wait on a Variable (debounce)

`WaitOnVariable` is a debounce: it waits until an existing variable has not changed for the given time, then calls a goal once. It is not for recurring calls; use an "every ..." statement for those.

```plang
Search
- wait until %searchText% has not changed for 500ms, call DoSearch
```


## Examples
For a full list of examples on how to use the Schedule module in PLang, please visit the [PLang Schedule Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Schedule).

## Step Options
Each step in your PLang code can be enhanced with additional options to handle various scenarios. Click on the links below for more detailed information on how to use each option:

- [CacheHandler](/CachingHandler.md)
- [ErrorHandler](/ErrorHandler.md)




## Advanced
For those who are interested in diving deeper into the Schedule module and understanding how it maps to underlying C# functionality, please refer to the [advanced documentation](./PLang.Modules.ScheduleModule_advanced.md).

## Created
This documentation was created on 2024-01-02T22:20:55.
