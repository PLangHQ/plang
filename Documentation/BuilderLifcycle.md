# Builder Lifecycle in plang

## Introduction

The builder lifecycle in plang is a systematic process that compiles `.goal` files in a predefined order. This process is crucial for developers to understand as it dictates how the code is built and executed. Additionally, plang offers a feature called build events, which can be leveraged to enhance the development workflow by automating tasks during the build process.

## Build Order

The build process in plang follows a specific sequence, ensuring that `.goal` files are compiled in the correct order. Here's how the build order is structured:

1. **Events Folder `.goal` Files**: Initially, all `.goal` files within the `Events` folder are built in order.
    1. Events.goal 
    2. EventsBuild.goal
    3. Other goal files in Events folder
2. **Setup.goal**: The `Setup.goal` file is built immediately after the Events folder `.goal` files.
3. **Start.goal**: Following `Setup.goal`, the `Start.goal` file is built.
4. **Other `.goal` Files**: Lastly, the remaining `.goal` files are built.

## Build Events

Build events are a powerful feature in plang that allow developers to execute specific goals at different stages of the build process. To enable build events, you must create an `EventsBuild.goal` file within your `Events` folder. These events can be configured to trigger:

- Before a goal is built
- After a goal is built
- Before a step is built
- After a step is built

### Utilizing Build Events

Build events can significantly improve your development tools and workflow in plang. Here are two practical examples of how build events can be utilized:

#### Example 1: Unit Testing

Automatically generate a unit test for a step immediately after it has been built. This ensures that each step is tested for its functionality.

```plang
EventsBuild
- after step is built, call !CreateTest

CreateTest
- [llm] system: create unit test for the user intent...
    user: %__Step.Text__%
    scheme: {data:object}
    write to %testData%
- write %testData% to /tests/%__Step.Name__%.json
```

#### Example 2: Code Analysis

Perform an analysis to check if all variables in a step are valid or provide guidance for code improvement before the step is built.

```plang
EventsBuild
- before step is built, call !CheckVariables

CheckVariables
- write out 'Checking variables'
/ some more logic
```

## Implementing Build Events

To implement build events in your plang project, follow these steps:

1. Create an `EventsBuild.goal` file in your `Events` folder.
2. Define the build events using the following structure:

```plang
- before goal build call !YourCustomGoal
- after goal build call !YourCustomGoal
- before step build call !YourCustomGoal
- after step build call !YourCustomGoal
```

By integrating build events into your development process, you can automate repetitive tasks, ensure code quality, and streamline your workflow. 

Embrace the power of build events to make your plang development experience more robust and efficient.