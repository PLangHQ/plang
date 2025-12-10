# LocalOrGlobalVariable

## Introduction
The `LocalOrGlobalVariable` module in the plang programming language serves as a bridge between the high-level, natural language steps defined by the user and the underlying C# methods that execute these steps. This module allows users to manage both local and global (static) variables within their plang scripts, providing a way to store and manipulate data throughout the execution of the program. Advanced users will appreciate the seamless integration of plang with C# methods, enabling complex operations to be performed with simple, natural language commands.

## Plang code examples
For simple documentation and examples, refer to [PLang.Modules.LocalOrGlobalVariableModule.md](./PLang.Modules.LocalOrGlobalVariableModule.md). The repository for examples can be found at [PLangHQ/plang LocalOrGlobalVariable Tests](https://github.com/PLangHQ/plang/tree/main/Tests/LocalOrGlobalVariable).

### Set a Local Variable
Commonly used to store data for later use within the program.
```plang
- set var 'username' to 'JohnDoe'
```
C# Method Signature: `void SetVariable(string key, object? value = null)`

### Get a Local Variable
Retrieves the value of a previously set local variable.
```plang
- get var 'username', write to %userName%
```
C# Method Signature: `object GetVariable(string key)`

### Set a Static Variable
Sets a value to a static variable that is shared across all instances of the module.
```plang
- set static var 'appVersion' to '1.0.0'
```
C# Method Signature: `void SetStaticVariable(string key, object value)`

For more detailed documentation and all examples, visit [PLang.Modules.LocalOrGlobalVariableModule.md](./PLang.Modules.LocalOrGlobalVariableModule.md) and the [PLangHQ/plang LocalOrGlobalVariable repository](https://github.com/PLangHQ/plang/tree/main/Tests/LocalOrGlobalVariable). Additionally, inspect the Program.cs source code at [PLangHQ/plang Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/LocalOrGlobalVariableModule/Program.cs).

## Source code
Program.cs is the runtime code for the `LocalOrGlobalVariable` module and can be found at [PLangHQ/plang Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/LocalOrGlobalVariableModule/Program.cs).

## How plang is mapped to C#
Modules in plang are utilized through a build and runtime process that translates natural language steps into executable C# code.

### Builder
During the build process, the .goal file is parsed:
1. Each step (line starting with `-`) is parsed.
2. A query is sent to the LLM, along with a list of all available modules, to suggest the appropriate module to use.
3. If `Builder.cs` is available in the source code, it is used; otherwise, `BaseBuilder.cs` is utilized.
4. LLM returns a JSON mapping the step text to a C# method with the necessary parameters.
5. The Builder creates a hash of the response for storage and saves a JSON instruction file with the `.pr` extension in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime
The .pr file is executed by the plang runtime:
1. The .pr file is loaded.
2. Reflection is used to load the `PLang.Modules.LocalOrGlobalVariableModule`.
3. The "Function" property in the .pr file specifies the C# method to call.
4. Parameters are provided if required by the method.

### plang example to csharp
Here's how a plang code example maps to a .pr file and the corresponding C# method:

#### Plang Code Example
```plang
- set var 'userAge' to 30
```

#### C# Method Mapping
```csharp
void SetVariable(string key, object? value = null)
```

#### Example Instruction .pr file
```json
{
  "Action": {
    "FunctionName": "SetVariable",
    "Parameters": [
      {
        "Type": "string",
        "Name": "key",
        "Value": "userAge"
      },
      {
        "Type": "object",
        "Name": "value",
        "Value": 30
      }
    ]
  }
}
```

Default values do not need to be defined in the .pr file. The "ReturnValue" property is included only if the C# method returns a value.

## Created
This documentation was created on 2024-01-02T21:59:54.