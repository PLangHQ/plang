
# CallGoal

## What is CallGoal(Module)
The CallGoal module in PLang allows for the invocation of other goals, akin to calling functions in traditional programming, enabling modular and reusable code structures.

### Another way to explain CallGoal(Module)

#### ELI5
Imagine you have a cookbook with different recipes. CallGoal is like telling someone to make a dish using one of the recipes. You just say the name of the dish, and they know what to do.

#### Business Perspective
From a business standpoint, CallGoal acts as a delegation tool, allowing one part of a workflow to activate another, ensuring efficient task management and process automation.

# Examples

### Example 1: Calling a Goal with Parameters
```plang
CallGoalWithParameters
- set variable %greetings% to 'Hello'
- call !Show %greetings%, %Now% / %greetings% and %Now% are passed as parameters to the Show goal
```

### Example 2: Calling a Goal Without Parameters
```plang
CallGoalWithoutParameters
- call !HelloWorld / This calls the HelloWorld goal without any parameters
```

### Example 3: Calling a Goal with Named Parameters
```plang
CallGoalWithNamedParameters
- call !Ffmpeg.ConvertToMp4 inputFile='myvideo.mov', outputPath='myvideo.mp4' / Named parameters are provided for the conversion process
```

### Example 4: Using Variables in Goal Calls
```plang
CallGoalUsingVariables
- set variable %inputFile% to 'myvideo.mov'
- set variable %outputPath% to 'myvideo.mp4'
- call !Ffmpeg.ConvertToMp4 inputFile=%inputFile%, outputPath=%outputPath% / Variables are used to pass file paths to the goal
```

### Example 5: Calling a Goal and Waiting for Execution
```plang
CallGoalAndWait
- call !DataProcessing / This step will wait for the DataProcessing goal to complete before moving to the next step
```

# Caching, Retries & Error Handling

In PLang, steps can be enhanced with additional properties such as caching, retries, and error handling. These properties allow developers to control how steps behave in the event of errors, how often they should be retried, and whether their results should be cached. Below are examples of how to apply these properties to steps within a PLang program.

## Caching

Caching can be used to store the result of a step for a specified duration. This can improve performance by avoiding repeated executions of the same step when the result is unlikely to change.

### Example with Caching
```plang
CallGoalWithCaching
- call !GenerateReport
      cache for 2 hours / The result of GenerateReport will be cached for 2 hours
```

## Retry

Retry properties allow a step to be retried a certain number of times with a specified delay between attempts. This is useful for steps that may occasionally fail due to transient issues.

### Example with Retry
```plang
CallGoalWithRetry
- call !GetDataFromService
      retry 3 times over 5 minutes / If GetDataFromService fails, it will be retried up to 3 times with a delay, over a period of 5 minutes
```

## Error Handling

Error handling properties enable a step to call a specific goal in case of an error or to ignore errors altogether. This allows for graceful degradation and better control over the program's flow when encountering issues.

### Example with Error Handling
```plang
CallGoalWithErrorHandling
- call !ProcessData
      on error call !HandleProcessError / If ProcessData encounters an error, HandleProcessError will be called
```

### Example with Specific Error Handling and Ignoring Errors
```plang
CallGoalWithSpecificErrorHandling
- call !UpdateDatabase
     on error 'connection timeout', call !NotifyAdmin
     ignore all other errors / If UpdateDatabase fails with a 'connection timeout' error, NotifyAdmin is called. All other errors are ignored.
```

By utilizing these properties, developers can create more robust and efficient PLang programs that handle various scenarios gracefully. Caching can reduce the load on systems by reusing results, retries can overcome temporary issues without manual intervention, and error handling can ensure that the program responds appropriately to unexpected situations.

# Best Practices for CallGoal

- **Use Descriptive Goal Names**: Choose goal names that clearly describe their purpose, making your code more readable and maintainable.
- **Pass Parameters Explicitly**: When calling goals with parameters, be explicit about the values you pass to avoid confusion and errors.
- **Handle Dependencies**: Ensure that any goal you call does not have unresolved dependencies that could cause failures.
- **Manage Execution Flow**: Decide whether to wait for the goal to complete based on the needs of your workflow. Use asynchronous calls judiciously to avoid blocking operations unnecessarily.
- **Error Handling**: Implement error handling within your goals to manage exceptions and provide feedback to the calling goal.



# CSharp

## CallGoalModule

Source code: [CallGoalModule.cs](https://github.com/PLangHQ/Plang/modules/CallGoalModule.cs)

### Methods in the CallGoalModule class

- **Goal Property (Getter and Setter)**
  - **Summary**: Manages the `Goal` object that the `Program` instance will execute.

- **RunGoal Method**
  - **Summary**: Executes a specified goal, optionally with parameters, and can either wait for completion or continue immediately with an optional delay.
