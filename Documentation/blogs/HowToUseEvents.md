# Guide to Using Events in Plang

Welcome to this step-by-step tutorial on implementing and managing events in your Plang applications. 

Events in Plang enable you to trigger specific actions at various stages in your application’s lifecycle, during the build process, or under certain conditions. 

This guide will walk you through setting up events, handling errors, managing builder events, and more.

## 1. Setting Up Events

To begin, you'll need to set up the structure for events in your Plang application. Follow these steps:

1. **Create an `events` Directory:**  
   Start by creating a folder named `events` in your project’s root directory.

2. **Create the `Events.goal` File:**  
   Inside the `events` folder, create a file called `Events.goal`. This file will house all your event definitions.

## 2. Application Lifecycle Events

Lifecycle events allow you to execute specific actions when your application starts or ends. Here’s how you can define these events:

```plang
Events
- before app starts, call !AppStarting
- before app ends, call !AppEnding
```

Now, create `AppStarting.goal` and `AppEnding.goal` files in the `events` folder.

For `AppStarting.goal`, write:

```plang
AppStarting
- write out 'App is starting'
```

And for `AppEnding.goal`, write:

```plang
AppEnding
- write out 'App is ending'
```

- **`before app starts`**: This event triggers the `AppStarting` action before the application starts.
- **`before app ends`**: This event triggers the `AppEnding` action before the application shuts down.

As always with Plang, there is no strict syntax. This means you don’t have to write exactly `before app starts`; you can write it as `when the app starts...`, `on app start...`, or in another way. Just be clear about what you want to happen.

## 3. Events with Goals and Steps

Plang allows you to trigger events before or after specific goals within a directory or globally after each goal:

```plang
Events
- before each goal in 'api/*', call !AuthenticateUser
- after each goal, call !Analyze.LogInfoAboutGoal
```

- **Before each goal in 'api/*'**: Executes `AuthenticateUser` before any goal in the `api` directory.
- **After each goal**: Executes `LogInfoAboutGoal` after every goal in your project.

## 4. Error Handling Events

Handling errors effectively is crucial. Plang lets you define events that trigger when errors occur:

```plang
Events
- on error in step, call !LoggerStepError
- on error on goal, call !LoggerGoalError
```

- **`on error in step`**: Calls `LoggerStepError` when an error occurs in any step.
- **`on error on goal`**: Calls `LoggerGoalError` when an error occurs in any goal.

For a deeper dive into error handling, refer to the [ErrorHandler documentation](./ErrorHandler.md).

## 5. Builder Events

Events related to the Plang builder can be defined in a `BuilderEvents.goal` file:

```plang
BuilderEvents
- before each step, call !AnalyzeCode
- after each step, call !AnalyzeCode
```

Plang already uses this build event in its own build. It validates all the goals that exist. You can find it after your first build in `/events/external/plang/builder/CheckGoals.goal`.

- **Before and after each step**: `AnalyzeCode` is called before and after each step during the build process, ensuring consistent code analysis.

## 6. Conditional Events

Sometimes, you may want to trigger events only under certain conditions, such as in debug mode:

```plang
Events
- before each step, call !SendDebug, only when start parameter is '--debug'
```

- **Conditional execution**: The `SendDebug` event is triggered only if the `--debug` parameter is passed at startup.

To trigger this event, run your application with:

```bash
plang --debug
```

You can define custom parameters for similar use cases.

The Plang language does this, as the debugger for the language is written in Plang. You can find it after you debug your first application at `/events/external/plang/runtime/SendDebug.goal`.

## 7. Using Available Variables

Within your events, you can access details about the current goal, step, or event using predefined variables:

- **`%!goal%`**: Represents the current goal.
- **`%!step%`**: Represents the current step.
- **`%!event%`**: Represents the current event.

These variables are useful for passing context-sensitive information to your event handlers.

## 8. External Events

You can also integrate external events from other sources. To do this, place external event files in the directory structure:

```plaintext
events/external/{appName}/
```

For example, events related to `plang` would go into `events/external/plang/`, created automatically when you build or run a debugger.

## 9. Binding Events to Variables

In Plang, you can bind events to variables to monitor their lifecycle (creation, update, or deletion):

```plang
- when %name% is created, call VariableNameIsCreated
- when %email% is changed, call VariableEmailIsChanged
- when %zip% is changed or created, call VariableZipIsChanged
- when %name% is deleted, call VariableNameIsDeleted
```

This allows for dynamic event handling based on variable state changes.

## 10. Types of Events in Plang

Plang offers various event types for different scenarios:

- **Before and After Events**: Trigger actions before or after specific goals or steps.
- **Error Events**: Handle errors by triggering specific actions when an error occurs.
- **Conditional Events**: Execute actions based on specific conditions or parameters.
- **Build Events**: Manage events during the build process.
- **Variable Events**: Bind actions to changes in variable states.

## 11. Source Code Reference

For those interested in the underlying code for event management in Plang, you can explore the following files in the [Plang repository](https://github.com/PLangHQ/plang/tree/main/PLang/Events):

- **EventBuilder.cs**: Handles the creation of event files.
- **EventRuntime.cs**: Manages the runtime execution of events.
- **EventBinding.cs**: Contains objects used for event management in both the builder and runtime.

These resources will provide you with a deeper understanding of how events are handled in Plang.

## More Information

If Plang is interesting to you, you should dig a bit deeper:

* [Basic concepts and lessons](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/Lesson%202.md)
* [Simple Todo example](https://github.com/PLangHQ/plang/blob/main/Documentation/Todo_webservice.md) is a good start
* Check out the [GitHub repo](https://github.com/PLangHQ/)
* [Meet up on Discord](https://discord.gg/A8kYUymsDD) to discuss or get help