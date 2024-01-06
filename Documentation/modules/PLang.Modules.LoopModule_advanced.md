
# Loop

## Introduction
The Loop module in plang is a powerful construct that allows for the iteration over collections such as lists and dictionaries. It is designed to integrate seamlessly with C# methods, enabling the execution of repetitive tasks with ease. Advanced users will appreciate the flexibility and the natural language approach that plang offers, which is translated into robust C# code through the use of a Language Learning Model (LLM).

## Plang code examples
For simple documentation and examples, refer to [PLang.Modules.LoopModule.md](./PLang.Modules.LoopModule.md). The repository containing a variety of loop examples can be found at [PLangHQ/plang - Loop Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Loop).

### Foreach Loop Through a List
A common usage of loops in plang is to iterate over a list of items and perform actions on each item. Below is an example of a foreach loop in plang that iterates through a list of numbers and prints each number.

```plang
- add 1 to list, write to %numbers%
- add 2 to list, write to %numbers%
- add 3 to list, write to %numbers%
- go through %numbers% call !PrintNumber
```

**C# Method Signature:**
```csharp
Task RunLoop(string variableToLoopThrough, string goalNameToCall, Dictionary<string, object>? parameters = null)
```

For more detailed documentation and all examples, see [PLang.Modules.LoopModule.md](./PLang.Modules.LoopModule.md) and the [Loop examples repository](https://github.com/PLangHQ/plang/tree/main/Tests/Loop). Additionally, inspect the Program.cs source code for a deeper understanding of the loop module's implementation: [PLang.Modules.LoopModule - Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.LoopModule/Program.cs).

## Source code
The runtime code for the Loop module, Program.cs, is available at [PLangHQ/plang - LoopModule Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.LoopModule/Program.cs).

## How plang is mapped to C#
Modules in plang are utilized to map natural language steps to C# methods.

### Builder
During the build process initiated by `plang build`, the .goal file is parsed:

1. Each step (line starting with `-`) is parsed.
2. A query is sent to LLM along with a list of all available modules to suggest the appropriate module, in this case, `PLang.Modules.LoopModule`.
3. The builder sends all methods in the Loop module to LLM along with the step.
4. This is done using `Builder.cs` ([source code](https://github.com/PLangHQ/plang/blob/main/PLang/Building/StepBuilder.cs)) or `BaseBuilder.cs` ([source code](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/BaseBuilder.cs)) depending on the availability of `Builder.cs`.
5. LLM returns a JSON mapping the step text to a C# method with the required parameters.
6. The Builder creates a hash of the response for storage with the instruction file.
7. An instruction file with the .pr extension is saved at `.build/{GoalName}/01. {StepName}.pr`.

### Runtime
The .pr file is then used by the plang runtime to execute the step:

1. The plang runtime loads the .pr file.
2. Reflection is used to load the `PLang.Modules.LoopModule`.
3. The "Function" property in the .pr file specifies the C# method to call.
4. Parameters are provided if required by the method.

### plang example to csharp
Here's how a plang code example maps to a .pr file and subsequently to a C# method in the Loop module:

**plang code example:**
```plang
- go through %numbers% call !PrintNumber
```

**Example Instruction .pr file:**
```json
{
  "Action": {
    "FunctionName": "RunLoop",
    "Parameters": [
      {
        "Type": "string",
        "Name": "variableToLoopThrough",
        "Value": "%numbers%"
      },
      {
        "Type": "string",
        "Name": "goalNameToCall",
        "Value": "!PrintNumber"
      }
    ]
  }
}
```

## Created
This documentation was created on 2024-01-02T22:03:50.
