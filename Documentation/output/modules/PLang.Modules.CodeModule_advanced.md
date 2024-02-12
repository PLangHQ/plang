# Code

## Introduction
The Code module in plang is a powerful tool designed to bridge the gap between natural language instructions and C# method execution. It leverages a Language Learning Model (LLM) to interpret user-defined steps and map them to corresponding C# methods. This documentation is tailored for advanced users who are familiar with programming concepts and are looking to understand the intricacies of how plang integrates with C#.

## Plang code examples
For a quick start and common usage patterns, refer to the simple documentation and examples at ('./PLang.Modules.CodeModule.md') and the repository at https://github.com/PLangHQ/plang/tree/main/Tests/Code.

### Example: Generating a New GUID
A common task in programming is generating a new GUID (Globally Unique Identifier). In plang, this can be done with a simple step that maps to a C# method for GUID generation.

```plang
- [code] create a new GUID, write to %newGuid%
```

Default C# signature:
```csharp
Guid NewGuid()
```

For more detailed examples and documentation, visit:
- ('./PLang.Modules.CodeModule.md')
- https://github.com/PLangHQ/plang/tree/main/Tests/Code
- Program.cs source code at https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CodeModule/Program.cs

## Source code
The source code for the runtime execution is available at https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CodeModule/Program.cs. For the building of steps, refer to https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CodeModule/Builder.cs.

## How plang is mapped to C#
Modules in plang are utilized through a two-phase process: Building and Runtime.

### Builder
During the build phase:
1. The .goal file is read, and each step (line starting with '-') is parsed.
2. The StepBuilder sends a question to LLM along with a list of all available modules.
3. LLM suggests a module, typically PLang.Modules.CodeModule, based on the step.
4. Builder.cs (or BaseBuilder.cs if Builder.cs is not available) sends the methods in the Code module to LLM with the step.
5. LLM returns a JSON mapping the step text to a C# method with the required parameters.
6. Builder.cs or BaseBuilder.cs creates a hash of the response and saves a JSON instruction file with the .pr extension in the .build/{GoalName}/ directory.

### Runtime
During the runtime phase:
1. The .pr file is loaded by the plang runtime.
2. Reflection is used to load the PLang.Modules.CodeModule.
3. The "Function" property in the .pr file dictates the C# method to call.
4. If required, parameters are passed to the method.

### plang example to csharp
Here's how a plang code example maps to a .pr file and subsequently to a C# method:

#### plang code example
```plang
- [code] format %fileSizeBytes% to human readable form, write to %readableSize%
```

#### Example Instruction .pr file
```json
{
  "Action": {
    "FunctionName": "FormatBytesToHumanReadable",
    "Parameters": [
      {
        "Type": "long",
        "Name": "fileSizeBytes",
        "Value": "10485760"
      }
    ],
    "ReturnValue": {
      "Type": "string",
      "VariableName": "readableSize"
    }
  }
}
```

## Created
This documentation is created 2024-02-10T13:51:23