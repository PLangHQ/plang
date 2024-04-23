# File

## Introduction
The File module in the PLang programming language provides a comprehensive suite of functionalities that allow you to manipulate files and directories. This includes operations such as reading, writing, appending, and deleting files, as well as managing directories and handling more complex file operations.

## For Beginners
In programming, a "File" refers to a document that stores data or information accessible by a computer. In PLang, the File module enables you to interact with these files directly from your code. This can be useful for a variety of tasks such as storing user data, logging information, or even reading configuration settings. The operations provided by the File module are designed to be straightforward so that even those with little to no programming experience can use them effectively.

## Best Practices for File
When working with files in PLang, it's important to follow certain best practices to ensure your code is efficient, safe, and easy to understand:

1. **Use Relative Paths**: Always prefer relative paths over absolute paths to ensure portability of your code. This means your file paths should be relative to the current working directory.
2. **Handle Variables in Files**: PLang allows dynamic loading of variables within files. This is particularly useful for customizing file content dynamically based on runtime data.
3. **Automatic Stream Management**: Remember, you do not need to manually close file streams; PLang handles that automatically, making your code cleaner and less error-prone.
4. **Check File Existence**: Files do not need to exist when attempting to read; the variable will simply remain empty if the file is not found. This can be used to your advantage to simplify your error handling code.

### Path Info

When specifying paths in the application, adhere to the following conventions to ensure correct file handling and compatibility across different environments:

#### Relative vs Absolute Paths
- **Relative Paths:** Use relative paths (e.g., `/this/is/a/path`) to reference files dynamically based on the application's current directory.
- **Absolute Paths:** Avoid using system-specific absolute paths (e.g., `C:\this\is\path`) to ensure cross-platform compatibility.

#### Working Directory
- The current working directory is determined by the location of the `.goal` file. All path references should be considered relative to this directory.

#### Path Prefixes
- `- read file.txt into %content%`: The `file.txt` should be located in the same folder as the `.goal` file.
- `- read /file.txt into %content%`: The `file.txt` should be located in the application folder (where `.build`, `.db`, and `Start.goal` are located).
- `- read //file.txt into %content%`: The `file.txt` should be located at the root of the drive, e.g., `C:\file.txt` on Windows or `/file.txt` on Linux/Macos.
- `- read ///shared/file.txt into %content%`: The `file.txt` should be located on the 'shared' network drive (note: this is applicable only on Windows systems).


### Example
Consider you have a text file named `settings.txt` that contains personalized settings or messages. You can dynamically load content from this file based on user-specific data:

```plang
Start
- set variable 'username' to 'Alice'
- read 'settings.txt' into %settings%, load variables
- write out %settings%
```

In this example, if `settings.txt` contains a reference to `%username%`, it will be replaced with "Alice" when the file is read.

## Examples
For practical applications and to see the File module in action, refer to the source code of the module [here](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/File/Program.cs).

# File Module Examples

## Reading and Writing Files
### Read a Text File
```plang
- read 'example.txt' into %content%
- write out %content%
```

### Write to a Text File
```plang
- write to 'example.txt', 'Hello, world!'
```

### Append to a Text File
```plang
- append ' Have a great day!' to 'example.txt'
```

## Handling Excel Files
### Read an Excel File
```plang
- read 'Employees.xlsx' into %excelData%
- loop through %excelData%, call !PrintOutExcel
```

### Write to an Excel File
```plang
- write %excelData% to 'UpdatedEmployees.xlsx', has header, overwrite
```

## Handling CSV Files
### Read a CSV File
```plang
- read 'data.csv' into %csvData%
- loop through %csvData%, call !PrintOutCSV
```

### Write to a CSV File
```plang
- write to 'output.csv', data %csvData%, has header, delimiter ',', overwrite
```

## File Management
### Copy a File
```plang
- copy 'source.txt' to 'destination.txt'
```

### Move a File
```plang
- move 'temp.txt' to 'final.txt'
```

### Delete a File
```plang
- delete 'old_file.txt'
```

### Get File Information
```plang
- get file info on 'report.xlsx' into %fileInfo%
- write out 'File Size: %fileInfo.Size%, Created On: %fileInfo.CreationTime%'
```

## Directory Management
### Create a Directory
```plang
- create directory 'new_folder'
```

### Delete a Directory
```plang
- delete directory 'unused_folder'
```

## Advanced File Operations
### Listen to File Changes
```plang
- listen to 'logs/*.log', call !HandleLogUpdate
```

### Read Binary File and Convert to Base64
```plang
- read 'image.png' into %base64%
- write out %base64%
```

These examples cover common tasks such as reading and writing text files, handling Excel and CSV files, managing files and directories, and responding to file changes. They are designed to be easily adaptable for various file handling needs in the PLang environment.

For a full list of examples demonstrating various file operations, visit [PLang File Examples](https://github.com/PLangHQ/plang/tree/main/Tests/File).

## Step Options
Each step in a PLang script can be enhanced with various handlers to manage execution flow, errors, retries, and more. Here are some useful handlers you can apply:

- [CacheHandler](/modules/handlers/CachingHandler.md)
- [ErrorHandler](/modules/handlers/ErrorHandler.md)
- [RetryHandler](/modules/handlers/RetryHandler.md)
- [Run and Forget](/modules/RunAndForget.md)

Click on the links for more details on how to use each handler.

## Advanced
For those interested in the deeper technical details or how the File module interfaces with underlying C# functionalities, refer to the advanced documentation [here](./PLang.Modules.FileModule_advanced.md).

## Created
This documentation was created on 2024-04-17T13:39:51, providing you with the latest and most accurate information to help you effectively use the File module in PLang.