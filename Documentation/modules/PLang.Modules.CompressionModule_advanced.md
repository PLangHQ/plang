
# Compression

## Introduction
The Compression module in plang is designed to provide developers with an intuitive way to handle file and directory compression and decompression within their plang scripts. This module abstracts the complexities of C# methods for compression tasks, allowing users to write natural language steps that are then mapped to C# methods by the plang language processing system.

## Plang Code Examples
For simple documentation and examples, refer to [PLang.Modules.CompressionModule.md](./PLang.Modules.CompressionModule.md). The repository for additional examples can be found at [PLang Compression Tests](https://github.com/PLangHQ/plang/tree/main/Tests/Compression).

### Compress a Single File
This is a common operation to compress a single file into a zip archive.
```plang
- Compress `report.txt` to `report.zip`
```
C# Method Signature:
```csharp
Task CompressFile(string filePath, string saveToPath, int compressionLevel = 0)
```

### Decompress to a Directory
Another frequent operation is to decompress an archive to a specified directory.
```plang
- Decompress `archive.zip` to `./extracted/`, overwrite
```
C# Method Signature:
```csharp
Task DecompressFile(string sourceArchiveFileName, string destinationDirectoryName, bool overwrite = false)
```

For more detailed documentation and all examples, see [PLang.Modules.CompressionModule.md](./PLang.Modules.CompressionModule.md) and explore the [PLang Compression Examples Repository](https://github.com/PLangHQ/plang/tree/main/Tests/Compression). For a deeper understanding, review the `Program.cs` source code at [PLang Compression Module Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CompressionModule/Program.cs).

## Source Code
The runtime code for the Compression module, `Program.cs`, can be found at [PLang Compression Module Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/PLang.Modules.CompressionModule/Program.cs).

## How Plang is Mapped to C#
Modules in plang are utilized through a build and runtime process.

### Builder
During the build process (`plang build`), the .goal file is processed:
1. Each step (line starting with `-`) is parsed.
2. A query is sent to the LLM, along with a list of all available modules, to suggest the appropriate module to use.
3. If `Builder.cs` is available in the source code, it is used; otherwise, `BaseBuilder.cs` is utilized.
4. LLM returns a JSON mapping the step text to a C# method with the required parameters.
5. The Builder creates a hash of the response and saves a JSON instruction file with the `.pr` extension in the `.build/{GoalName}/01. {StepName}.pr` directory.

### Runtime
The .pr file is executed by the plang runtime:
1. The .pr file is loaded.
2. Reflection is used to load the `PLang.Modules.CompressionModule`.
3. The "Function" property in the .pr file specifies the C# method to call.
4. Parameters are provided if required by the method.

### Plang Example to CSharp
Here's how a plang code example maps to a .pr file and the corresponding C# method:

Plang Code Example:
```plang
- Compress `report.txt` to `report.zip`
```

Mapped to C# Method in Compression:
```csharp
Task CompressFile(string filePath, string saveToPath, int compressionLevel = 0)
```

Example Instruction .pr file:
```json
{
  "Action": {
    "FunctionName": "CompressFile",
    "Parameters": [
      {
        "Type": "string",
        "Name": "filePath",
        "Value": "report.txt"
      },
      {
        "Type": "string",
        "Name": "saveToPath",
        "Value": "report.zip"
      }
    ]
  }
}
```

## Created
This documentation was created on 2024-01-02T21:38:29.
