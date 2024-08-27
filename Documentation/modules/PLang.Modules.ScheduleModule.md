
# Schedule Module in PLang

## Introduction
The Schedule module in PLang is a powerful feature that allows developers to manage time-based tasks efficiently. It provides the ability to pause execution, schedule tasks for future execution, and execute recurring tasks at specified intervals.

## For Beginners
If you're new to programming or unfamiliar with technical jargon, think of the Schedule module as an alarm clock or a calendar reminder for your code. It lets you tell your program to "wake up" and do something at a specific time, after a certain period has passed, or repeatedly at regular intervals.

For example, you might want to send out a weekly newsletter, check for updates every hour, or even just delay an action for a few seconds. The Schedule module helps you do all of this without having to constantly check the time yourself.

## Best Practices for Schedule
When using the Schedule module in PLang, it's important to follow some best practices to ensure your code runs smoothly and efficiently:

1. **Plan Ahead**: Determine the exact timing requirements for your tasks. Do they need to run at a specific time, after a delay, or periodically?
2. **Avoid Overlapping Tasks**: Make sure scheduled tasks do not overlap in a way that could cause performance issues or conflicts.
3. **Handle Exceptions**: Always anticipate and handle exceptions that may occur during the execution of scheduled tasks.
4. **Test Thoroughly**: Test your scheduling logic extensively to ensure it behaves as expected under various conditions.
5. **Use Comments**: Use comments to explain the scheduling logic, which can be quite complex, to help others (and your future self) understand the code.

Here's a simple example to illustrate scheduling a task:

```plang
ReminderToDrinkWater
- every 1 hour, write out "Time to drink water!"
```

In this example, the goal `ReminderToDrinkWater` is set to remind you to drink water every hour.


# Schedule Module Examples

The Schedule module in PLang provides functionality for waiting, sleeping, and scheduling tasks based on time delays or cron expressions. Below are examples of how to use the Schedule module, sorted by their expected popularity in real-world usage.

## 1. Sleep for a Short Duration

This example demonstrates how to pause the execution for a short duration, such as 1 second.

```plang
SleepShortDuration
- sleep for 1 second
```

## 2. Schedule a Recurring Task

Scheduling a task to run at regular intervals using a cron expression is a common use case. Here's how to schedule a goal to be called every minute.

```plang
ScheduleRecurringTask
- every 1 minute, call !ItIsCalled
```

## 3. Output Current Time

Outputting the current time can be useful for logging or time-stamping events.

```plang
OutputCurrentTime
- write out \%Now\%
```

## 4. Schedule a Task for a Specific Time

Sometimes you may want to schedule a task to run at a specific time in the future. Here's an example of scheduling a goal to run at a specific date and time.

```plang
ScheduleSpecificTime
- at 2.1.2024 22:19:49, call !TaskAtSpecificTime
```

## 5. Schedule a Task with a Delay

If you need to schedule a task to run after a certain delay, you can use the sleep function with a longer duration.

```plang
ScheduleAfterDelay
- sleep for 5 minutes
- call !DelayedTask
```

## 6. Start the Scheduler

Starting the scheduler is necessary to begin processing scheduled tasks.

```plang
StartScheduler
- start with 'default settings', 'engine', 'parser', 'logger', 'runtime', and 'file system'
```

## 7. Run Scheduled Tasks

To execute the tasks that have been scheduled, you would call the `RunScheduledTasks` method.

```plang
RunScheduledTasksExample
- run scheduled tasks with 'settings', 'engine', 'parser', 'logger', 'runtime', and 'file system'
```

## 8. Schedule a Task with a Cron Expression and Next Run

For more complex scheduling, you can specify a cron expression and an optional next run time.

```plang
ScheduleWithCronAndNextRun
- every '0 0/5 * * * ?' starting at 2.1.2024 22:19:49, call !ComplexScheduledTask
```

## 9. Schedule a Task with a Cron Expression

Using just a cron expression, you can schedule a task without specifying the next run time.

```plang
ScheduleWithCron
- every '0 0/30 * * * ?', call !HalfHourlyTask
```

## 10. Schedule a One-Time Task

To schedule a task that only runs once at a specific time, you can use a cron expression that corresponds to that particular time.

```plang
ScheduleOneTimeTask
- at '0 15 10 15 * ?', call !AnnualMeetingReminder
```

Remember to replace placeholders like 'default settings', 'engine', 'parser', 'logger', 'runtime', and 'file system' with actual references to your application's components when implementing these examples.


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
