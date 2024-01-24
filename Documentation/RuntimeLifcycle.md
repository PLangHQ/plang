# Plang Runtime Lifecycle Documentation

Welcome to the Plang Runtime Lifecycle documentation. This guide will walk you through the sequence of operations that occur when you initiate a Plang application. Understanding this lifecycle is crucial for effectively managing the behavior of your Plang applications.

## Execution Order

When you launch a Plang application, the following sequence of operations is performed:

1. **Load Events**: The system loads any predefined events by running `Events.goal`.
2. **Run 'Before Start App' Events**: Any events that are set to run before the application starts are executed.
3. **Run `Setup.goal` File**: The `Setup.goal` file is executed to perform initial setup tasks.
4. **Start Scheduler**: The scheduler responsible for managing timed tasks is initiated.
5. **Run Goal File**: The main goal file is executed (by default, this is `Start.goal`).
6. **Run 'After Start of App' Events**: Any events that are set to run after the application has started are executed.

## Goal File Execution

The execution of all goal files, including those triggered by events, follows this pattern:

- **Dependency Injection**: Any dependency injection defined within the goal file is registered.
- **Before Goal Events**: Any events set to run before the goal are executed.
- **Step Execution**: Each step within the goal file is processed in the following manner:
  - **Before Step Events**: Events set to run before each step are executed.
  - **Step Execution**: The step itself is executed.
  - **After Step Events**: Events set to run after each step are executed.
- **After Goal Events**: Once all steps are completed, any events set to run after the goal are executed.

## Error Handling

The execution cycle is influenced by errors as follows:

- **Goal-Level Errors**: If a goal file throws an error, the 'After Goal' event will not be executed. However, an 'On Error' event will be triggered if it is defined.
- **Step-Level Errors**: If a step within a goal file throws an error, the 'After Step' event will not be executed. An 'On Error' event will be triggered if it is defined.

## Default Goal File Execution

When running Plang without specifying a goal file, as in the following command:

```bash
plang run
```

The Plang runtime will search for a `Start.goal` file in the current directory. If it cannot find this file, it will output an error message stating 'Could not find Start.goal to run...'. Despite this, the `Setup.goal` and any events that occur before the goal file execution will still be run.

## Conclusion

By understanding the Plang Runtime Lifecycle, you can better structure your applications and handle events and errors more effectively. This knowledge is essential for developing robust and reliable Plang applications.