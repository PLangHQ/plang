# File Module in plang

## Introduction
The File module in plang provides a comprehensive suite of operations for file management, including reading, writing, copying, and deleting files, as well as working with directories. This module serves as an interface between plang's natural language processing capabilities and the underlying C# methods that perform the actual file operations.

When a `.goal` file is built in plang, each step (prefixed with a `-`) is interpreted, and the StepBuilder consults with a Language Learning Model (LLM) to suggest the most appropriate module to handle the step. The builder then presents all the methods available in the chosen module to the LLM, which maps the natural language step to a specific C# method.

## Plang Code Examples

### Request Access to Path
Check if a path is accessible within the application's scope.
```plang
- request access to 'C:/Users/Example/Documents'
```
C# Signature: `Task<bool> RequestAccessToPath(string path)`

### Read Binary File and Convert to Base64
Read a binary file and encode its content to Base64.
```plang
- read 'image.png' as binary, convert to base64
```
C# Signature: `Task<string> ReadBinaryFileAndConvertToBase64(string path, string returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = false)`

### Read Text File
Read the content of a text file into a variable.
```plang
- read 'log.txt' into %logContent%
```
C# Signature: `Task<string> ReadTextFile(string path, string returnValueIfFileNotExisting = "", bool throwErrorOnNotFound = false, bool loadVariables = false, bool emptyVariableIfNotFound = false)`

### Write to Text File
Write content to a text file, with options to overwrite or append.
```plang
- write 'Hello World' to 'greeting.txt'
- append ' Have a great day!' to 'greeting.txt'
```
C# Signature: `Task WriteToFile(string path, string content, bool overwrite = false, bool loadVariables = false, bool emptyVariableIfNotFound = false)`

### Copy File
Copy a file from one location to another.
```plang
- copy 'source.txt' to 'destination.txt'
```
C# Signature: `Task CopyFile(string sourceFileName, string destFileName, bool createDirectoryIfNotExisting = false, bool overwriteFile = false)`

### Delete File
Delete a file from the filesystem.
```plang
- delete file 'old_data.txt'
```
C# Signature: `Task DeleteFile(string fileName, bool throwErrorOnNotFound = false)`

Note: This documentation provides examples for a subset of the methods available in the File module. For a complete list of examples and to see how each method can be used in plang, please refer to the source code at [PLang File Tests](https://github.com/PLangHQ/plang/tree/main/Tests/File).

## Source Code
The runtime code for the File module, including all the methods and their implementations, can be found at [PLang File Program.cs](https://github.com/PLangHQ/plang/tree/main/PLang/Modules/File/Program.cs).

## Step Options
For additional functionalities and handling specific scenarios, plang provides various handlers and options that can be used in conjunction with the File module:

- CacheHandler: [CacheHandler Documentation](/modules/cacheHandler.md)
- ErrorHandler: [ErrorHandler Documentation](/modules/ErrorHandler.md)
- RetryHandler: [RetryHandler Documentation](/modules/RetryHandler.md)
- CancellationHandler: [CancellationHandler Documentation](/modules/CancelationHandler.md)
- Run and Forget: [Run and Forget Documentation](/modules/RunAndForget.md)

These handlers allow for more robust and fault-tolerant file operations, ensuring that your plang goals can handle a variety of runtime situations effectively.