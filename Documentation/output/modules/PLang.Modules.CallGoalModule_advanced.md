# CallGoal

## Introduction
The `CallGoal` module in the plang programming language serves as a bridge between the high-level, natural language steps defined by the user and the underlying C# methods that execute these steps. Advanced users familiar with programming concepts will appreciate the seamless integration of plang with C# methods, enabling complex workflows to be defined in an intuitive and readable manner.

In plang, a goal functions similarly to a method or function in traditional programming languages. It encapsulates a series of steps, which are executed in sequence to achieve a specific outcome. The `CallGoal` module allows these goals to be invoked from within other goals, providing modularity and reusability.

When a plang script is built, each step is mapped to a corresponding C# method through a process involving the LLM (Language Learning Model). This process ensures that the natural language instructions are accurately translated into executable code, leveraging the capabilities of the C# programming language.

## Plang code examples
For simple documentation and examples, please refer to [PLang.Modules.CallGoalModule.md](./PLang.Modules.CallGoalModule.md). The repository for additional examples can be found at [PLangHQ/plang CallGoal Tests](https://github.com/PLangHQ/plang/tree/main/Tests/CallGoal).

### Example 1: Calling a Goal Without Parameters
This is a common usage where a goal is called without any additional parameters.
```plang
- call !BackupDatabase / Calls the BackupDatabase.goal to initiate a database backup
```
C# method signature:
```csharp
Task RunGoal(string goalName, Dictionary<string, object>? parameters = null, bool waitForExecution = true, int delayWhenNotWaitingInMilliseconds = 0)
```

### Example 2: Calling a Goal With Parameters
In this example, parameters are passed to the goal being called.
```plang
- set variable %filePath% to 'C:/data/users.csv'
- call !ProcessFile filePath=%filePath% / Calls the ProcessFile.goal with the filePath parameter
```
C# method signature:
```csharp
Task RunGoal(string goalName, Dictionary<string, object>? parameters = null, bool waitForExecution = true, int delayWhenNotWaitingInMilliseconds = 0)
```

For more detailed documentation and examples, please refer to [PLang.Modules.CallGoalModule.md](./PLang.Modules.CallGoalModule.md) and the [CallGoal Test Repository](https://github.com/PLangHQ/plang/tree/main/Tests/CallGoal). Additionally, the source code for the `CallGoal` module can be found in [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CallGoalModule/Program.cs).

## Source code
The runtime code for the `CallGoal` module is available at [PLangHQ/plang CallGoal Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CallGoalModule/Program.cs).
The builder code for the `CallGoal` module is available at [PLangHQ/plang CallGoal Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CallGoalModule/Builder.cs).

## How plang is mapped to C#
The mapping of plang to C# methods is a two-step process involving the Builder and Runtime.

### Builder
When the user runs `plang build`, the .goal file is processed as follows:
1. Each step in the goal file (line starting with `-`) is parsed.
2. A query is sent to LLM for each step, along with a list of all available modules.
3. LLM suggests the appropriate module, in this case, `PLang.Modules.CallGoalModule`.
4. Builder.cs (or BaseBuilder.cs if Builder.cs is not available) sends the method information from `CallGoal` to LLM along with the step.
5. LLM returns a JSON mapping the step text to a C# method with the required parameters.
6. Builder.cs (or BaseBuilder.cs) creates a hash of the response for storage with the instruction file.
7. An instruction file with the .pr extension is saved in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime
The .pr file is then used by the plang runtime to execute the step:
1. The plang runtime loads the .pr file.
2. Reflection is used to load the `PLang.Modules.CallGoalModule`.
3. The "Function" property in the .pr file specifies the C# method to call.
4. Parameters are provided if required by the method.

### plang example to csharp
Here's how a plang code example is mapped to a method in `CallGoal` and the resulting .pr file:

plang code example:
```plang
- call !GenerateReport write to %reportStatus%
```

This step would map to the following .pr file:
```json
{
  "Action": {
    "FunctionName": "RunGoal",
    "Parameters": [
      {
        "Type": "string",
        "Name": "goalName",
        "Value": "GenerateReport"
      }
    ],
    "ReturnValue": {
      "Type": "string",
      "VariableName": "reportStatus"
    }
  }
}
```

## Created
This documentation was created on 2024-01-02T21:32:50.