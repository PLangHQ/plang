# File

## Introduction
The File module in plang programming language is a powerful tool that allows you to interact with the file system. It provides a range of functionalities to read from and write to files, manage directories, and monitor file changes. This module is essential for tasks such as data storage, report generation, and dynamic content management within your plang applications.

## For beginners
If you're new to programming, think of a File as a digital document on your computer where you can store information. Just like you can create, read, edit, and organize paper documents, the File module in plang lets you do the same with digital files. You can create new files to store data, read data from existing files, add more data to them, and even keep an eye on a file for any changes, all through simple plang commands.

## Best Practices for File
When working with files in plang, it's important to follow best practices to ensure your code is efficient, secure, and easy to maintain. Here's an example to illustrate a best practice:

```plang
- read 'config.json' into %configData%
- if %configData% is not empty then call !ProcessConfig
```

In this example, we first read the contents of 'config.json' into a variable called `%configData%`. Before proceeding, we check if `%configData%` is not empty, which is a good practice to avoid errors in case the file doesn't exist or is empty. If there is data, we then call a goal named `!ProcessConfig` to handle the configuration data.

# File Module Examples Documentation

## Read and Write Text Files
### Read Text File
```plang
- read 'example.txt' into %content%
- write out %content%
```

### Write to Text File
```plang
- write to 'example.txt', 'This is a content', overwrite if exists
```

### Append to Text File
```plang
- append ', some more content' to 'example.txt'
```

## Read and Write Excel Files
### Read Excel File
```plang
- read 'Employees.xlsx' into %excelData%
- loop through %excelData%, call !PrintOutExcel
```

### Write to Excel File
```plang
- write %excelData% to 'Employees.xlsx', has header, overwrite if exists
```

## Read and Write CSV Files
### Read CSV File
```plang
- read 'Test5x2.csv' into %csvData%
- loop through %csvData%, call !PrintOutCSV
```

### Write to CSV File
```plang
- write to 'Test5x2.csv', data %csvData%, overwrite if exists
```

## File Operations
### Copy File
```plang
- copy 'file2.txt' to 'file3.txt', overwrite if exists
```

### Delete File
```plang
- delete file 'file2.txt'
- delete file 'file3.txt'
```

### Get File Information
```plang
- get file info on 'Employees.xlsx' into %fileInfo%
- write out 'FileInfo: %fileInfo%, CreationTime: %fileInfo.CreationTime%, LastWriteTime: %fileInfo.LastWriteTime%'
```

## Directory Operations
### Create Directory
```plang
- create directory 'new_folder'
```

### Delete Directory
```plang
- delete directory 'old_folder', include all contents
```

## File Monitoring
### Listen to File Changes
```plang
- listen to 'files/*.json', call !ProcessJson
```

## Auxiliary Goals
### PrintOutExcel
```plang
PrintOutExcel
- write out %item.Name% - %item.Email%
```

### PrintOutCSV
```plang
PrintOutCSV
- write out %item%
```

Note: The examples provided are based on the most common use cases for file operations, such as reading and writing text, Excel, and CSV files, as well as performing file and directory operations. The examples are sorted by popularity and frequency of use. If a method from the `FileModule` class does not have a corresponding example, a natural language example has been created to demonstrate its usage.

For a full list of examples, visit [PLang File Examples](https://github.com/PLangHQ/plang/tree/main/Tests/File).

## Step options
When writing your plang code, you can enhance the functionality of each step with these options:

- [CacheHandler](/modules/handlers/CachingHandler.md): Manage caching of data to improve performance.
- [ErrorHandler](/modules/handlers/ErrorHandler.md): Handle errors gracefully without stopping your program.
- [RetryHandler](/modules/handlers/RetryHandler.md): Automatically retry a step if it fails initially.
: Cancel a running step under certain conditions.
: Execute a step without waiting for its completion.

Click on the links for more details on how to use each option.

## Advanced
For those who want to dive deeper into the File module and understand how it maps to underlying C# functionalities, you can explore the [advanced documentation](./PLang.Modules.FileModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:51:35.