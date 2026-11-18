# Schedule

## Introduction

The Schedule module in the plang programming language is designed to facilitate time-based operations such as delaying execution, scheduling tasks for future execution, and managing recurring tasks. This module is particularly useful for scenarios where timing and scheduling are critical, such as in automation scripts, background job processing, or any application that requires precise control over task execution timing.

In plang, scheduling operations are expressed in natural language steps that are then mapped to corresponding C# methods. This mapping is facilitated by a Language Learning Model (LLM) which interprets the user's intent and translates it into executable code. The integration of plang with C# is seamless, allowing for powerful and flexible scheduling capabilities within the plang environment.

## Plang code examples

For simple documentation and examples, refer to the [Schedule Module Documentation](./PLang.Modules.ScheduleModule.md) and the [example repository on GitHub](https://github.com/PLangHQ/plang/tree/main/Tests/Schedule).

### Sleep for a Short Duration

This is a common operation to pause the execution for a specified amount of time.

```plang
SleepShortDuration
- sleep for 1000 milliseconds
- write to %sleepResult%
```

**C# Method Signature:**
```csharp
Task Sleep(int sleepTimeInMilliseconds)
```

### Schedule a Recurring Task

A recurring task is either an interval ("every N seconds/minutes/hours/days", stored as `intervalInSeconds`) or a cron expression for clock times (5 fields, or 6 fields with seconds first, evaluated in UTC). A step sets exactly one of them.

```plang
ScheduleRecurringTask
- every 45 seconds, call CheckQueue
- every day at 8am, call SendDailyReport
- start scheduler
```

**C# Method Signature:**
```csharp
Task<IError?> Schedule(GoalToCallInfo goalName, string? cronCommand = null, long? intervalInSeconds = null, DateTime? nextRun = null)
```

`every 45 seconds` builds as `intervalInSeconds: 45`, `every day at 8am` as `cronCommand: "0 8 * * *"`. Each due job is started like a `dont wait` goal call, so jobs run in parallel; a job whose previous run is still going skips that occurrence.

For more detailed documentation and all examples, please visit the [Schedule Module Documentation](./PLang.Modules.ScheduleModule.md) and the [example repository on GitHub](https://github.com/PLangHQ/plang/tree/main/Tests/Schedule). Additionally, inspect the Program.cs source code for a deeper understanding of the module's implementation [here](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/ScheduleModule/Program.cs).

## Source code

The runtime code for the Schedule module, `Program.cs`, can be found at the [PLang GitHub repository](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/ScheduleModule/Program.cs). The settings for the module, `ModuleSettings.cs`, are available at the same repository [here](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/ScheduleModule/ModuleSettings.cs).

## How plang is mapped to C#

### Builder

When a user runs the plang build command, the .goal file is processed as follows:

1. Each step in the goal file (lines starting with `-`) is parsed.
2. For each step, a query is sent to the LLM, which includes a list of all available modules.
3. The LLM suggests a module to use, in this case, `PLang.Modules.ScheduleModule`.
4. The builder sends all the methods in the Schedule module to the LLM along with the step.
5. This is done using `Builder.cs` if available, otherwise `BaseBuilder.cs` is used.
6. The LLM returns a JSON object that maps the step text to a C# method with the necessary parameters.
7. The Builder creates a hash of the response and stores it with the instruction file.
8. An instruction file with the .pr extension is saved in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime

The .pr file is then used by the plang runtime to execute the step:

1. The plang runtime loads the .pr file.
2. Reflection is used to load the `PLang.Modules.ScheduleModule`.
3. The "Function" property in the .pr file specifies which C# method to call.
4. Parameters are provided if the method requires them.

### plang example to csharp

Here's how a plang code example maps to a C# method in the Schedule module:

**plang code example:**
```plang
- sleep for 1000 milliseconds
- write to %sleepResult%
```

**Mapped to C# method in Schedule:**
```csharp
Task Sleep(int sleepTimeInMilliseconds)
```

**Example Instruction .pr file:**
```json
{
  "Action": {
    "FunctionName": "Sleep",
    "Parameters": [
      {
        "Type": "int",
        "Name": "sleepTimeInMilliseconds",
        "Value": "1000"
      }
    ],
    "ReturnValue": {
      "Type": "Task",
      "VariableName": "sleepResult"
    }
  }
}
```

## Created

This documentation was created on 2024-01-02T22:21:49.