
# File

## What is File(Module)
The File module in plang is a set of functionalities that allow you to interact with the file system. It provides you with the ability to read, write, copy, and delete files, as well as listen for file changes. This module is essential for handling file operations within your plang scripts.

## Best Practices for File
When working with the File module in plang, consider the following best practices:

1. **Use Absolute Paths**: To avoid confusion and ensure that your file operations target the correct files, use absolute paths whenever possible.
2. **Handle Errors Gracefully**: Implement error handling to manage scenarios where files may not exist or access is denied.
3. **Manage File Resources**: Ensure that file streams are properly closed after use to prevent resource leaks.
4. **Use Variables**: Store file contents and file information in variables for easy manipulation and access throughout your script.
5. **Secure Sensitive Data**: If your files contain sensitive information, implement appropriate security measures to protect the data.
6. **Optimize Performance**: For large files or operations on many files, consider performance implications and optimize your code accordingly.
7. **Test File Operations**: Always test file operations in a safe environment to prevent accidental loss of data.


# File Module Examples

## Read Binary File and Convert to Base64
```plang
- read '1px.png', into %base64%
- write out %base64%
```

## Read Text File
```plang
- read 'document.txt', write to %content%
- write out %content%
```

## Read Excel File
```plang
- read 'Employees.xlsx' into %excelData%
- loop through %excelData%, call !PrintOutExcel
```

## Write Excel File
```plang
- write %excelData% to 'Employees.xlsx', has header, overwrite
```

## Read CSV File
```plang
- read 'Test5x2.csv' into %csvData%
- loop through %csvData%, call !PrintOutCSV
```

## Write CSV File
```plang
- write to 'Test5x2.csv', data %csvData%
```

## Write to Text File
```plang
- write to 'file2.txt', 'This is a content'
```

## Read Text File and Output Content
```plang
- read 'file2.txt', into %newContent%
- write out %newContent%
```

## Append to Text File
```plang
- append ', some more content' to 'file2.txt'
```

## Copy File
```plang
- copy 'file2.txt' to 'file3.txt'
```

## Read and Output Copied File Content
```plang
- read 'file3.txt' into %file3Content%
- write out %file3Content%
```

## Delete File
```plang
- delete file 'file2.txt'
- delete file 'file3.txt'
```

## Get File Information
```plang
- get file info on 'Employees.xlsx' into %fileInfo%
- write out 'fileInfo: %fileInfo%, CreationTime: %fileInfo.CreationTime%, LastWriteTime: %fileInfo.LastWriteTime%'
```

## Write Data to Excel and CSV
```plang
- write to 'demo.xlsx', %csvData%, overwrite file
- write to 'demo.csv', %csvData%, overwrite file
```

## Write Multiple Data Sets to Excel
```plang
- write to 'demo2.xlsx', data: %excelData%, %csvData%, overwrite file
```

## Delete Excel Files
```plang
- delete 'demo.xlsx'
- delete 'demo2.xlsx'
```

## Listen to File Changes
```plang
- listen to 'files/*.json', call !ProcessJson
```

## Print Out Excel Data
```plang
PrintOutExcel
- write out %item.Name% - %item.Email%
```

## Print Out CSV Data
```plang
PrintOutCSV
- write out %item%
```


For a full list of examples, visit [PLang File Module Examples](https://github.com/PLangHQ/plang/tree/main/Tests/File).

# Step options
These options are available for each step. Click the links for more detail on how to use them:

- [CacheHandler](/modules/cacheHandler.md)
- [ErrorHandler](/modules/ErrorHandler.md)
- [RetryHandler](/modules/RetryHandler.md)
- [CancellationHandler](/modules/CancelationHandler.md)
- [Run and forget](/modules/RunAndForget.md)

# Advanced
For more advanced information, if you want to understand how the underlying mapping works with C#, check out [FileModule_advanced.md](./FileModule_advanced.md).
