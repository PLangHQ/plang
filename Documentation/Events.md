# Introduction to Events in PLang

Events in PLang are a powerful feature that allows you to define actions that should occur in response to certain triggers within your program, such as before a goal starts or after it ends.

## ELI5 Explanation of Events

Think of events like a doorbell. When someone presses the doorbell (the trigger), it causes the bell to ring (the action). In PLang, events work similarly: when something specific happens in your program (like starting a goal), it can trigger an action (like calling another goal).

## Understanding Events in PLang

In PLang, events are used to trigger goals or steps under specific circumstances, like before or after a goal runs, or when an error occurs.

### Syntax for Events
```plang
- before app starts, call !AppStarting
- on error in step, call !Logger.StepError
```

## Practical Examples of Events

### Application Lifecycle Events
```plang
Events
- before app starts, call !AppStarting
- before app ends, call !AppEnding
```

### Events with Goals and Steps
```plang
- before each goal in 'api/*', call !AuthenticateUser
- after each goal, call !Analyze.LogInfoAboutGoal
```

### Error Handling Events
```plang
- on error in step, call !Logger.StepError
- on error on goal, call !Logger.GoalError
```

### Conditional Events
```plang
- before each step, include private goals, call !SendDebug, only in debug mode
- before goal ends, include private goals, call !SendDebug, only in debug mode
```

## Types of Events in PLang

- **Before and After Events**: Trigger actions before or after a specific goal or step.
- **Error Events**: Trigger actions when an error occurs in a step or goal.
- **Conditional Events**: Execute actions under specific conditions, like in debug mode.
- **Build Events**: Read more about [build events](./BuilderLifcycle.md)

## Using Events with Goals

Events in PLang can be associated with specific goals, steps, or broader application states, allowing for flexible and dynamic program behavior.

## Best Practices

- Use events to keep your code organized and modular.
- Clearly define event triggers and actions for maintainability.

## Summary and Key Takeaways

Events in PLang are essential for creating responsive and dynamic applications that react to certain triggers during their execution.
