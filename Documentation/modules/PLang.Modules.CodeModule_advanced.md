
# Code Module for plang

## Introduction
The Code module in plang is a powerful tool designed for advanced users who are familiar with programming concepts and C# language specifics. It allows users to write plang code in a natural language format, which is then interpreted by a Language Learning Model (LLM) and mapped to corresponding C# methods. This documentation provides an overview of how plang integrates with C# methods and includes examples of common usage patterns.

## Plang code examples
For simple documentation and examples, refer to [PLang.Modules.CodeModule.md](./PLang.Modules.CodeModule.md). The repository containing a comprehensive list of examples can be found at [PLang Code Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Code).

### Example: Writing to a File
This is a common operation where the user wants to write data to a file. The plang code example below demonstrates how to perform this action, which maps to the `WriteToFile` method in C#.

#### plang code
```plang
Code
- set %filePath% as 'C:\\path\\to\\file.txt'
- set %content% as 'Hello, World!'
- [code] write %content% to file at %filePath%
```

#### C# method signature
```csharp
void WriteToFile(string content, string filePath)
```

### Example: Reading from a File
Reading data from a file is another frequent operation. The following plang code example shows how to read the contents of a file, which corresponds to the `ReadFromFile` method in C#.

#### plang code
```plang
Code
- set %filePath% as 'C:\\path\\to\\file.txt'
- [code] read from file at %filePath%, write to %fileContent%
```

#### C# method signature
```csharp
string ReadFromFile(string filePath)
```

For more detailed documentation and all examples, please refer to [PLang.Modules.CodeModule.md](./PLang.Modules.CodeModule.md) and the [PLang Code Examples Repository](https://github.com/PLangHQ/plang/tree/main/Tests/Code). Additionally, inspect the Program.cs source code for a deeper understanding of the runtime behavior at [Program.cs Source Code](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CodeModule/Program.cs).

## Source code
The runtime code for the Code module, Program.cs, can be found at [Program.cs Source](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CodeModule/Program.cs).
The Builder.cs, responsible for the construction of steps, is available at [Builder.cs Source](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CodeModule/Builder.cs).

## How plang is mapped to C#
The mapping of plang to C# methods is a two-step process involving the Builder and Runtime.

### Builder
1. During the build process, the .goal file is read, and each step (line starting with '-') is parsed.
2. The StepBuilder sends a question to LLM along with a list of all available modules, suggesting the use of PLang.Modules.CodeModule for the given step.
3. The LLM returns a JSON mapping the step text to a C# method with the required parameters.
4. Depending on availability, either Builder.cs or BaseBuilder.cs creates a hash of the response and saves a JSON instruction file with the .pr extension in the .build/{GoalName}/ directory.

### Runtime
1. The plang runtime loads the .pr file.
2. Reflection is used to load the PLang.Modules.CodeModule.
3. The "Function" property in the .pr file specifies the C# method to call.
4. Parameters are provided if required by the method.

### plang example to csharp
Below is a plang code example and its mapping to a C# method in the Code module, along with the corresponding .pr file content.

#### plang code example
```plang
Code
- set %data% as 'Sample data'
- [code] process %data%, write to %result%
```

#### Example Instruction .pr file
```json
{
  "Action": {
    "FunctionName": "ProcessData",
    "Parameters": [
      {
        "Type": "string",
        "Name": "data",
        "Value": "Sample data"
      }
    ],
    "ReturnValue": {
      "Type": "string",
      "VariableName": "result"
    }
  }
}
```

## Created
This documentation was created on 2024-01-02T21:35:48.
