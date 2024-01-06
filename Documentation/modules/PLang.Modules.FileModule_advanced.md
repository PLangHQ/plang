# File Module Documentation

## Introduction
The File module in plang is a comprehensive suite designed to facilitate file and directory operations within the plang programming environment. It provides a seamless interface for reading, writing, and manipulating files and directories, leveraging the power of C# methods behind the scenes. Advanced users with programming experience will appreciate the module's ability to handle a wide range of file types, including text, CSV, and Excel files, as well as its support for file monitoring and directory management.

The integration of plang with C# is achieved through a sophisticated mapping system that translates natural language steps defined in plang into corresponding C# method calls. This process involves the use of a Language Learning Model (LLM) to interpret the user's intent and select the appropriate method from the File module's C# implementation.

## Plang code examples
For a quick start and common usage examples, refer to the simple documentation and examples provided in the [PLang.Modules.FileModule.md](./PLang.Modules.FileModule.md) file. Additionally, a repository of examples is available at [PLangHQ/plang](https://github.com/PLangHQ/plang/tree/main/Tests/File).

### Read Text File
Reads the content of a text file and stores it in a variable.
```plang
- read 'example.txt' into %fileContent%
- write out %fileContent%
```
C# Method Signature: `Task<string> ReadTextFile(string path, string returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = false, bool loadVariables = false, bool emptyVariableIfNotFound = false)`

### Write to Text File
Writes content to a text file, with the option to overwrite if the file already exists.
```plang
- write to 'example.txt', 'This is a content', overwrite if exists
```
C# Method Signature: `Task WriteToFile(string path, string content, bool overwrite = false, bool loadVariables = false, bool emptyVariableIfNotFound = false)`

### Append to Text File
Appends content to the end of a text file.
```plang
- append ', some more content' to 'example.txt'
```
C# Method Signature: `Task AppendToFile(string path, string content, string seperator = null, bool loadVariables = false, bool emptyVariableIfNotFound = false)`

For more detailed documentation and additional examples, please refer to the [PLang.Modules.FileModule.md](./PLang.Modules.FileModule.md) file and the [PLangHQ/plang](https://github.com/PLangHQ/plang/tree/main/Tests/File) repository. To understand the implementation details, you can also examine the source code of the [Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.FileModule/Program.cs) file.

## Source code
The runtime code for the File module, `Program.cs`, is available at [PLangHQ/plang](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.FileModule/Program.cs).

## How plang is mapped to C#
Modules in plang are utilized through a build and runtime process that translates plang steps into executable C# code.

### Builder
During the build process initiated by `plang build`, the following occurs:
1. The .goal file is read, and each step (line starting with `-`) is parsed.
2. For each step, a query is sent to LLM along with a list of all available modules.
3. LLM suggests a module to use, such as `PLang.Modules.FileModule`.
4. The builder sends all methods in the File module to LLM along with the step.
5. This is done using `Builder.cs` or `BaseBuilder.cs`, depending on availability.
6. LLM returns a JSON mapping the step text to a C# method with required parameters.
7. The builder creates a hash of the response and saves a JSON instruction file with the `.pr` extension in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime
During runtime, the .pr file is used to execute the step:
1. The plang runtime loads the .pr file.
2. Reflection is used to load the `PLang.Modules.FileModule`.
3. The "Function" property in the .pr file specifies the C# method to call.
4. Parameters are provided if required by the method.

### plang example to csharp
Here's how a plang code example maps to a C# method and the resulting .pr file:

#### plang code example
```plang
- read 'example.txt' into %fileContent%
```

#### C# method mapping
```csharp
Task<string> ReadTextFile(string path)
```

#### Example Instruction .pr file
```json
{
  "Action": {
    "FunctionName": "ReadTextFile",
    "Parameters": [
      {
        "Type": "string",
        "Name": "path",
        "Value": "example.txt"
      }
    ],
    "ReturnValue": {
      "Type": "string",
      "VariableName": "fileContent"
    }
  }
}
```

## Created
This documentation was created on 2024-01-02T21:52:29.