# Builder Lifecycle Documentation for Plang

## Overview

The builder lifecycle in Plang is a structured process that compiles `.goal` files in a specific order. Understanding this lifecycle is crucial for developers who want to effectively manage and extend their build processes, especially when integrating custom build events.

## Build Order

When you initiate a build in Plang, the process follows this sequence:

1. **Events Folder**: The build starts by compiling all `.goal` files located in the `Events` folder.
2. **Setup.goal**: Next, it compiles the `Setup.goal` file.
3. **Start.goal**: Following that, the `Start.goal` file is compiled.
4. **Remaining Goal Files**: Finally, the rest of the `.goal` files are compiled.

## Build Events

Plang allows developers to hook into the build process by creating an `EventsBuild.goal` file within the `Events` folder. This file enables the execution of specific goals at various points in the build lifecycle.

### Binding Build Events

You can bind a build event to execute a goal at the following stages:

- **Before Goal Build**: Execute a goal before a specific goal is built.
- **After Goal Build**: Execute a goal after a specific goal is built.
- **Before Step Build**: Execute a goal before a specific step is built.
- **After Step Build**: Execute a goal after a specific step is built.

These hooks are particularly useful for developing tools that enhance the Plang development experience.

### Example: Creating Unit Tests

The following example demonstrates how to create a unit test for a step after it has been built:

```plang
EventsBuild
- after step is built, call !CreateTest

CreateTest
- [llm] system: create unit test for the user intent...
    user: %__Step.Text__%
    scheme: {data:object}
    write to %testData%
- write %testData% to /tests/%__Step.Name__%
```

**Explanation**: 
- The `EventsBuild` goal specifies that after a step is built, the `CreateTest` goal is called.
- The `CreateTest` goal uses a language model to generate a unit test based on the step's text and writes the test data to a specified location.

### Example: Variable Checking

Another example is checking the validity of variables in a step before it is built:

```plang
EventsBuild
- before step is built, call !CheckVariables

CheckVariables
- write out 'Checking variables'
```

**Explanation**: 
- The `EventsBuild` goal specifies that before a step is built, the `CheckVariables` goal is called.
- The `CheckVariables` goal outputs a message indicating that variable checking is in progress.

## Conclusion

By leveraging the builder lifecycle and build events in Plang, developers can create robust development tools and processes. Whether it's generating unit tests or validating code, these features provide flexibility and control over the build process.