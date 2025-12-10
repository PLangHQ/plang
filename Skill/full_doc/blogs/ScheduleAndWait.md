# Scheduling and Waiting in Plang

Welcome to the tutorial on scheduling and waiting in Plang! This tutorial will guide you through how to schedule tasks and implement delays (waiting) in your code using Plang's natural language programming approach. Whether you're a beginner or experienced, you'll find these features simple and powerful.

## Scheduling Tasks

In many projects, tasks need to run at specific times. In other programming languages, scheduling can be complex and might require external libraries or services. Plang simplifies this by allowing you to express your scheduling needs in natural language.

### Basic Scheduling

Let's start with a simple example. Suppose you want to run different processes at specific times:

```plang
ScheduleTask
- on monday at 9am, call !ProcessWeekend
- every weekday at 14:32, call !WeekDayProcess
- on 1st of every month, call !MontlyProcess

ProcessWeekend
- write out 'Running ProcessWeekend'

WeekDayProcess
- write out 'Running WeekDayProcess'

MontlyProcess
- write out 'Running MontlyProcess'
```

In this example:
- `ProcessWeekend` runs every Monday at 9 AM.
- `WeekDayProcess` runs every weekday at 2:32 PM.
- `MontlyProcess` runs on the first day of every month.

### Scheduling with Parameters

You can also pass parameters to your scheduled tasks. For instance, if you need to generate reports on specific days with different parameters, you can do the following:

```plang
SemiDailyReport
- on mondays at 9, call !SendReport days=3
- on wednesdays at 9, call !SendReport days=2
- on fridays at 9, call !SendReport days=2

SendReport
- select * from report where created BETWEEN DATE('now', '-%days% days') AND DATE('now'), write to %results%
/ now format and send the results
```

Here, the `SendReport` task is called with different `days` parameters:
- On Mondays, it includes data from the last 3 days.
- On Wednesdays and Fridays, it includes data from the last 2 days.

### How Scheduling Works in the Background

When you build your Plang code, the scheduling statements are converted into cron patterns. For example, "Monday at 9" becomes `0 9 * * 1`. These patterns are stored in the `.db/system.sqlite` database, and the Plang runtime executes the tasks when the time matches the pattern.

## Implementing Wait (or Sleep)

Sometimes, you need your code to pause for a certain duration before proceeding to the next step. Plang makes this easy to express:

```plang
Waiting
- write out 'running'
- wait 1 second
- write out ' after 1 sec'
- sleep for 1 ms
- write out ' after 1ms'
- go to sleep for 1 hours and 20 sec
- write out ' after 1 hour and 20 seconds'
```

Notice how you can use `wait`, `sleep for`, or `go to sleep for` interchangeably. The key thing with Plang is expressing your intent. You don't need to learn specific syntax—just write what you want to happen.

## More Information

If Plang is interesting to you, you should dig a bit deeper:

* [Basic concepts and lessons](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md)
* [Simple Todo example](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_webservice.md) is a good start
* Check out the [GitHub repo](https://github.com/PLangHQ/)
* [Meet up on Discord](https://discord.gg/A8kYUymsDD) to discuss or get help
* Detailed documentation about Scheduler and waiting can be found [here](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.ScheduleModule.md)

## Conclusion

By now, you should have a good understanding of how to schedule tasks and implement waiting periods in Plang. These features allow you to manage time-based tasks and delays effortlessly, using simple and readable natural language statements.

Feel free to explore more and experiment with different schedules and waiting periods in your projects. Happy coding with Plang!