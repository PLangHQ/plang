# File Module in plang

## Introduction
The File module in plang provides a comprehensive suite of functionalities for handling file operations such as reading, writing, and managing files and directories. This module is crucial for developers looking to perform file I/O operations within their plang scripts.

### Detailed Overview
The File module serves as an interface between plang scripts and the file system. It allows scripts to perform common file operations without needing to handle complexities like file stream management or error handling, which are abstracted away by the module. This simplifies the development process and enhances script reliability.

### How plang Integrates with C# Methods
plang scripts are translated into C# method calls during the build process. This translation is handled by the Builder, which uses natural language processing to map plang steps to corresponding C# methods in the File module. During runtime, these mappings are utilized to execute the file operations as defined in the script.

## Plang Code Examples
For more comprehensive documentation and examples, refer to:
- [File Module Documentation](./PLang.Modules.FileModule.md)
- [Example Repository on GitHub](https://github.com/PLangHQ/plang/tree/main/Tests/File)

### Example: Reading a Text File
This is a common operation to read content from a text file into a plang variable.
```plang
- read 'example.txt' into %content%
- write out %content%
```
**C# Method Signature:**
```csharp
string ReadTextFile(string path, string returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = false)
```

### Example: Writing to a Text File
This example demonstrates writing a simple string to a text file.
```plang
- write to 'output.txt', 'Hello, plang!'
```
**C# Method Signature:**
```csharp
void WriteToFile(string path, string content)
```

For additional methods and detailed examples:
- [File Module Documentation](./PLang.Modules.FileModule.md)
- [Example Repository on GitHub](https://github.com/PLangHQ/plang/tree/main/Tests/File)
- For a deeper dive, check the [Program.cs source code](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.FileModule/Program.cs)

## Source Code
The runtime code for the File module can be found at [Program.cs on GitHub](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.FileModule/Program.cs).

## How plang is Mapped to C#
### Builder
1. During the build process, the .goal file is parsed where each step (line starting with '-') is identified.
2. Each step is sent to the LLM along with a list of available modules for module suggestion.
3. Based on the step's content, the LLM suggests using the PLang.Modules.FileModule and provides a method mapping.
4. The Builder (see [Builder.cs](https://github.com/PLangHQ/plang/blob/main/PLang/Building/Builder.cs) or [BaseBuilder.cs](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/BaseBuilder.cs)) then generates a .pr instruction file containing the method mapping and parameters.

### Runtime
1. The .pr file is loaded by the plang runtime.
2. The runtime uses reflection to invoke the specified method in the PLang.Modules.FileModule.
3. Parameters and method details are extracted from the .pr file to execute the operation.

### Example Instruction .pr File
For a step that reads a text file into a variable:
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
      "VariableName": "content"
    }
  }
}
```

## Created
This documentation was created on 2024-04-17T13:40:24.