
# Conditional

## Introduction
The Conditional module in plang is a powerful feature that allows users to define logic flows based on conditions. This module is particularly useful for creating decision-making processes within a plang program. Advanced users familiar with programming will appreciate the seamless integration of plang's natural language steps with C# methods, enabling complex logic to be expressed in an intuitive and readable manner.

In plang, conditionals are expressed using natural language, which is then mapped to C# methods by the plang compiler. This mapping is facilitated by the Builder.cs or BaseBuilder.cs, depending on the availability of Builder.cs in the source code.

## Plang code examples
For simple documentation and examples, please refer to [PLang.Modules.ConditionalModule.md](./PLang.Modules.ConditionalModule.md). The repository for additional examples can be found at [PLang Conditional Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Conditional).

### Example: Checking a Boolean Condition
This is a common usage where a boolean condition is checked to determine the flow of execution.
```plang
- if %UserIsAdmin% then call !GrantAccess, else !DenyAccess
```
This plang step checks if the `%UserIsAdmin%` variable is true and calls the `GrantAccess` method; otherwise, it calls the `DenyAccess` method.

Default C# signature for the mapped method:
```csharp
public async Task<bool> CheckConditionAsync(bool condition);
```

For more detailed examples and documentation:
- [PLang.Modules.ConditionalModule.md](./PLang.Modules.ConditionalModule.md)
- [PLang Conditional Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Conditional)
- [Program.cs source code](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/ConditionalModule/Program.cs)

## Source code
The runtime code for the Conditional module can be found at [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/ConditionalModule/Program.cs). The Builder.cs, responsible for the building of steps, is available at [Builder.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/ConditionalModule/Builder.cs).

## How plang is mapped to C#
Modules in plang are used to encapsulate functionality and map user-defined steps to C# methods.

### Builder
When a user runs `plang build`, the .goal file is read and processed as follows:
1. Each step in the goal file (line starts with `-`) is parsed.
2. For each step, a query is sent to the LLM, along with a list of all available modules.
3. The LLM suggests a module to use, in this case, `PLang.Modules.ConditionalModule`.
4. The builder sends all the methods in the Conditional module to the LLM along with the step.
5. This is done using Builder.cs or BaseBuilder.cs, depending on the availability of Builder.cs.
6. The LLM returns a JSON that maps the step text to a C# method with the required parameters.
7. Builder.cs or BaseBuilder.cs creates a hash of the response to store with the instruction file.
8. An instruction file with the .pr extension is saved at `.build/{GoalName}/01. {StepName}.pr`.

### Runtime
The .pr file is then used by the plang runtime to execute the step:
1. The plang runtime loads the .pr file.
2. Reflection is used to load the `PLang.Modules.ConditionalModule`.
3. The "Function" property in the .pr file specifies the C# method to call.
4. Parameters are provided if the method requires them.

### plang example to csharp
Here's how a plang code example maps to a method in the Conditional module and the corresponding .pr file:

#### Plang code example:
```plang
- if %UserIsAdmin% then call !GrantAccess, else !DenyAccess
```

#### Example Instruction .pr file:
```json
{
  "Action": {
    "FunctionName": "CheckConditionAsync",
    "Parameters": [
      {
        "Type": "bool",
        "Name": "condition",
        "Value": "%UserIsAdmin%"
      }
    ],
    "ReturnValue": {
      "Type": "bool",
      "VariableName": "accessGranted"
    }
  }
}
```

## Created
This documentation was created on 2024-01-02T21:40:38.
