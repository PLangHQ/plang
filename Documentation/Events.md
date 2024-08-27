# Events in Plang

This guide provides detailed instructions on how to implement and manage events within your Plang applications. Events in Plang allow you to execute specific actions at various points in your application lifecycle, during the build process, or based on certain conditions.

## Setting Up Events

To set up events in your application, create a folder named `events` and within it, create a file called `Events.goal`.

### Application Lifecycle Events

These events are triggered at the start and end of your application's lifecycle.

```plang
Events
- before app starts, call !AppStarting
- before app ends, call !AppEnding
```

### Events with Goals and Steps

You can specify events to trigger before or after goals within a specific directory or after each goal globally.

```plang
Events
- before each goal in 'api/*', call !AuthenticateUser
- after each goal, call !Analyze.LogInfoAboutGoal
```

### Error Handling Events

Define events that trigger when an error occurs at the step or goal level.

```plang
Events
- on error in step, call !LoggerStepError
- on error on goal, call !LoggerGoalError
```

For more details on handling errors, refer to the [ErrorHandler documentation](./ErrorHandler.md).

## Builder Events

To bind events related to the Plang builder, create a file named `BuilderEvents.goal`.

```plang
BuilderEvents
- before each step, call !AnalyzeCode
- after each step, call !AnalyzeCode
```

## Conditional Events

Events can also be configured to run only if a specific startup parameter is provided.

```plang
Events
- before each step, call !SendDebug, only when start parameter is '--debug'
```

To trigger this event, start the application with the `--debug` parameter:

```bash
plang --debug
```

You can define your own parameters to control event triggering.

## Available Variables

Within your events, you can access information about the current goal, step, or event using the following variables:

- %!goal%
- %!step%
- %!event%

## External Events

Install external events from outside sources into the directory `events/external/{appName}/`. For example, `events/external/plang/` is created when you build or run a debugger.

## Binding Events to Variables

In any goal file, you can bind events to variables to trigger actions when variables are created, changed, or deleted.

```plang
- when %name% is created, call VariableNameIsCreated
- when %email% is changed, call VariableEmailIsChanged
- when %zip% is changed or created, call VariableZipIsChanged
- when %name% is deleted, call VariableNameIsDeleted
```

## Types of Events in Plang

- **Before and After Events:** Trigger actions before or after a specific goal or step.
- **Error Events:** Trigger actions when an error occurs in a step or goal.
- **Conditional Events:** Execute actions under specific conditions, such as in debug mode.
- **Build Events:** Manage events related to the build process.
- **Variable Events:** Bind events to variable lifecycle changes (creation, update, deletion).

## Source Code

The source code for event handling in Plang can be found at:

[PLang Events Source Code](https://github.com/PLangHQ/plang/tree/main/PLang/Events)

- `EventBuilder.cs` contains the code for building event files.
- `EventRuntime.cs` is the code for the runtime handling of events.
- `EventBinding.cs` includes the objects used in the builder and runtime for event management.

This documentation should help you effectively manage and utilize events in your Plang applications, enhancing the interactivity and responsiveness of your software.