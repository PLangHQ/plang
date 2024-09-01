# Plang Runtime Lifecycle Documentation

Welcome to the Plang Runtime Lifecycle documentation. This guide will help you understand the order of execution and the lifecycle of a Plang application. It will also cover how to handle errors and enable logging within your Plang goals.

## Execution Order

When you start a Plang application, the following sequence of events occurs:

1. **Load Events**: The runtime begins by executing the `Events.goal` file. This file contains event definitions that can be triggered throughout the application's lifecycle.

2. **Run Before Start App Events**: Any events defined to run before the application starts are executed.

3. **Run Setup.goal File**: The `Setup.goal` file is executed. This file typically contains initialization logic for your application.

4. **Start Scheduler**: The scheduler is started, which manages the execution of goals and events.

5. **Run Goal File**: The main goal file is executed. By default, this is `Start.goal`.

6. **Run After Start of App Events**: Any events defined to run after the application starts are executed.

## Goal File Execution

For each goal file executed, the following applies:

- **Dependency Injection**: Any dependency injection defined within the goal is registered.

- **Before Goal Events**: Events defined to run before the goal are executed.

- **Step Execution**: Each step within the goal is processed in the following manner:
  - **Before Step Events**: Events defined to run before the step are executed.
  - **Execute the Step**: The step is executed.
  - **After Step Events**: Events defined to run after the step are executed.

- **After Goal Events**: Events defined to run after the goal are executed.

## Error Handling

The execution cycle is affected by errors as follows:

- **Goal Errors**: If a goal throws an error, the "After goal" events are not executed. Instead, an "On error" event is executed, if defined.

- **Step Errors**: If a step throws an error, the "After step" events are not executed. Instead, an "On error" event is executed, if defined.

## Running Plang Without a Goal File

If you run Plang without specifying a goal file, like so:

```bash
plang run
```

The runtime will search for a `Start.goal` file in the current directory. If it cannot find this file, it will print an error message: "Could not find Start.goal to run......". Despite this, it will still execute the `Setup.goal` and any events that occur before the goal file execution.

## Logger

Plang allows you to enable log levels by adding a comment to a goal. The format for log levels is `[trace]`, `[debug]`, `[info]`, `[warning]`, `[error]`. Here is an example:

```plang
/ This is the start [trace]
Start
- write out 'hello'
```

In this example, the log level is set to trace, which will log detailed information about the execution of the goal.

This documentation should provide you with a comprehensive understanding of the Plang runtime lifecycle, error handling, and logging. For further details, please refer to the [official Plang documentation](#).