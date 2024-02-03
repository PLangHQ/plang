
# CallGoal

## Introduction
The `CallGoal` module is a powerful feature in the PLang programming language that allows you to invoke and manage the execution of other goals within your script. It acts similarly to calling a function in traditional programming languages, enabling you to modularize your code and reuse common functionality.

## For Beginners
If you're new to programming, think of a `CallGoal` as a way to ask for help from a friend who knows how to do a specific task. When you call that friend (or in this case, a goal), you're asking it to perform its task and, optionally, give you some results back. This makes your main task easier because you don't have to do everything yourself; you can rely on other goals to handle parts of the work.

## Best Practices for CallGoal
When using `CallGoal`, it's important to keep your code organized and clear. Here are some best practices:

- **Modularize your goals**: Break down your tasks into smaller, manageable goals that do one thing well.
- **Use descriptive names**: Name your goals in a way that clearly indicates what they do.
- **Pass parameters wisely**: Only send the necessary information to the goal you're calling.
- **Handle return values**: If the called goal provides a result, make sure to capture and use it appropriately.

### Example
Let's say you have a goal that greets users. Instead of writing the greeting logic every time you need it, you create a separate goal named `GreetUser` and call it whenever you need to greet someone:

```plang
- set variable %userName% to 'Alice'
- call !GreetUser name=%userName% / Call the GreetUser goal with the user's name
```

In this example, `GreetUser` is a reusable goal that can be called with different user names.

## Examples

# CallGoal Module Examples

The `CallGoal` module in PLang allows you to call another goal within your script. Below are examples of how to use the `CallGoal` module, sorted by their popularity and typical use cases.

## 1. Calling a Goal Without Parameters

This is the most basic form of calling a goal. It simply triggers another goal to run.

```plang
- call !HelloWorld / calls HelloWorld.goal
```

## 2. Calling a Goal With Parameters

Often, you'll need to pass parameters to the goal you're calling. Here's how you do it in a human-readable format.

```plang
- set variable %videoPath% to 'myvideo.mov'
- set variable %outputPath% to 'myvideo.mp4'
- call !Ffmpeg.ConvertToMp4 inputFile=%videoPath%, outputPath=%outputPath% / Convert a .mov file to .mp4
```

## 3. Calling a Goal With Parameters and Specific Execution Time

Sometimes, you might want to call a goal at a specific time. Here's an example of how to do that.

```plang
- set variable %greetings% to 'Hello'
- call !Show %greetings%, %Now% / greetings & Now are parameters
```

## 4. Calling a Goal and Waiting for Execution

By default, the script will wait for the called goal to finish execution. Here's an example of a call that explicitly waits for completion.

```plang
- call !DataBackup / Wait for the backup process to complete
```

## 5. Calling a Goal Without Waiting for Execution

In some cases, you might want to continue executing the current goal without waiting for the called goal to finish.

```plang
- call !StartBackgroundTask waitForExecution=false / Start a background task and continue without waiting
```

## 6. Calling a Goal and Handling the Return Value

If the called goal returns a value, you might want to capture it for use in subsequent steps.

```plang
- call !CalculateSum 5, 10, write to %sumResult% / Calculate the sum of 5 and 10, store the result in %sumResult%
- write out 'The sum is %sumResult%'
```

## 7. Calling a Goal With Delay When Not Waiting

You can specify a delay for when the current goal should not wait for the called goal to finish.

```plang
- call !SendEmailNotification waitForExecution=false, delayWhenNotWaitingInMilliseconds=5000 / Send an email notification, continue after a 5-second delay
```

Remember to replace the placeholders with actual goal names and parameters relevant to your specific use case. The examples provided here are for illustrative purposes and should be adapted to fit the goals and parameters of your PLang scripts.


For a full list of examples, visit [PLang CallGoal Examples](https://github.com/PLangHQ/plang/tree/main/Tests/CallGoal).

## Step Options
Each step in your PLang script can be enhanced with additional options for better control and error handling. Click the links below for more details on how to use each option:

- [CacheHandler](/modules/cacheHandler.md)
- [ErrorHandler](/modules/ErrorHandler.md)
- [RetryHandler](/modules/RetryHandler.md)
- [CancellationHandler](/modules/CancelationHandler.md)
- [Run and Forget](/modules/RunAndForget.md)

## Advanced
For those who are ready to dive deeper into the `CallGoal` module and understand how it interacts with C# under the hood, check out the [advanced documentation](./PLang.Modules.CallGoalModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:31:58.
