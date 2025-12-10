# Events in Plang

Events in Plang provide a powerful mechanism to handle various application lifecycle stages, error handling, and conditional execution. This documentation will guide you through setting up and using events in your Plang applications.

## Setting Up Events

To bind events in your application, create a folder named `events` and a file called `Events.goal`. This file will contain the event definitions for your application.

### Application Lifecycle Events

You can define events that trigger at specific points in the application's lifecycle. Here's how you can set up events to trigger before the application starts and ends:

```plang
Events
- before app starts, call !AppStarting
- before app ends, call !AppEnding
```

In this example, the `!AppStarting` event is called before the application starts, and the `!AppEnding` event is called before the application ends.

### Events with Goals and Steps

Events can also be associated with specific goals and steps within your application. For example:

```plang
Events
- before each goal in 'api/*', call !AuthenticateUser
- after each goal, call !Analyze.LogInfoAboutGoal
```

This setup ensures that the `!AuthenticateUser` event is called before each goal in the 'api/*' path, and the `!Analyze.LogInfoAboutGoal` event is called after each goal.

### Error Handling Events

Error handling is crucial in any application. Plang allows you to define events that trigger on errors:

```plang
Events
- on error in step, call !LoggerStepError
- on error on goal, call !LoggerGoalError
```

These events will call `!LoggerStepError` when an error occurs in a step and `!LoggerGoalError` when an error occurs in a goal. For more details on handling errors, refer to [Error Handling Documentation](./modules/handlers/ErrorHandler.md).

## Builder Events

To bind events to the Plang builder, create a file named `BuilderEvents.goal`:

```plang
BuilderEvents
- before each step, call !AnalyzeCode
- after each step, call !AnalyzeCode
```

This configuration will call the `!AnalyzeCode` event before and after each step during the build process.

## Conditional Events

You can define events to run only when a specific parameter is provided at the startup of the app:

```plang
Events
- before each step, call !SendDebug, only when start parameter is '--debug'
```

This event will only be bound if you start the app with the `--debug` parameter:

```bash
plang --debug
```

You can define your own parameters to control event execution.

## Available Variables

Within events, you can access information about the goal, step, and event using the following variables:
- `%!goal%`
- `%!step%`
- `%!event%`

## External Events

External events installed from an outside source should be placed in the `events/external/{appName}/` folder. For example, `events/external/plang/` is created when you build or run a debugger.

## Bind Events to Variables

In any goal file, you can bind events to variable actions such as creation, update, or deletion:

```plang
- when %name% is created, call VariableNameIsCreated
- when %email% is changed, call VariableEmailIsChanged
- when %zip% is changed or created, call VariableZipIsChanged
- when %name% is deleted, call VariableNameIsDeleted
```

## Types of Events in Plang

- **Before and After Events**: Trigger actions before or after a specific goal or step.
- **Error Events**: Trigger actions when an error occurs in a step or goal.
- **Conditional Events**: Execute actions under specific conditions, like in debug mode.
- **Build Events**: Events related to the build process.
- **Variable Events**: Bind events to variable actions such as creation, update, or deletion.

## Source Code

The source code for events in Plang can be found in the [Plang GitHub repository](https://github.com/PLangHQ/plang/tree/main/PLang/Events). Key files include:
- `EventBuilder.cs`: Contains the code for building event files.
- `EventRuntime.cs`: Contains the code for the runtime.
- `EventBinding`: Objects used in the builder and runtime.

This documentation should provide a comprehensive understanding of how to use events in Plang, enabling you to effectively manage application lifecycle, error handling, and more.