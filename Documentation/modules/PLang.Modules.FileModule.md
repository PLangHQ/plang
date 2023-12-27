# File

## What is File (Module)
The File module in PLang provides a suite of tools for performing file system operations such as reading, writing, copying, and deleting files, as well as working with Excel and CSV files.

### Another way to explain File (Module)

#### ELI5
Imagine you have a magic notebook that lets you create, read, change, and organize pages or entire sections easily. The File module is like that notebook for computers, helping you manage files and folders with simple commands.

#### Business Perspective
From a business standpoint, the File module is a productivity enhancer that automates file management tasks, ensuring data is accurately and efficiently processed, stored, and retrieved, which is crucial for maintaining operational workflows and data integrity.

# Examples

1. Read a binary file and convert it to Base64:
   ```
   - read '1px.png', into %base64%
   - write out %base64%
   ```

2. Read a text file into a variable:
   ```
   - read file.txt into %content%
   - write out %content%
   ```

3. Write text to a file:
   ```
   - write to file2.txt, 'This is a content'
   ```

4. Append text to a file:
   ```
   - append ', some more content' to file2.txt
   ```

5. Read an Excel file into a variable:
   ```
   - read Employees.xlsx into %excelData%
   ```

6. Write data to an Excel file with a header and overwrite if it exists:
   ```
   - write %excelData% to Employees.xlsx, has header, overwrite
   ```

7. Read a CSV file into a variable:
   ```
   - read Test5x2.csv into %csvData%
   ```

8. Write data to a CSV file:
   ```
   - write to Test5x2.csv, data %csvData%
   ```

9. Copy a file to a new location:
   ```
   - copy file2.txt to file3.txt
   ```

10. Delete files:
    ```
    - delete file file2.txt
    - delete file file3.txt
    ```

11. Retrieve file information and output specific properties:
    ```
    - get file info on Employees.xlsx into %fileInfo%
    - write out 'fileInfo: %fileInfo%, CreationTime: %fileInfo.CreationTime%, LastWriteTime: %fileInfo.LastWriteTime%'
    ```

12. Listen for changes in JSON files within a specific directory and call a goal when changes occur:
    ```
    - listen to 'files/*.json', call !ProcessJson
    ```

# Caching, Retries, Error Handling & Run and Forget

In PLang, developers have the ability to enhance the robustness and efficiency of their code by using caching, retries, error handling, and run-and-forget properties. These properties can be applied to individual steps to control their behavior during execution.

## Caching

Caching allows you to store the result of a step for a specified duration. Subsequent calls to the same step with the same parameters within the cache duration will return the cached result instead of executing the step again.

### Example:
```
- read Employees.xlsx into %excelData%
      cache for 10 minutes
```
This step reads data from "Employees.xlsx" into the variable `%excelData%` and caches the result for 10 minutes.

## Retry

Retry properties enable a step to be automatically retried a specified number of times with a delay between each attempt if it fails.

### Example:
```
- write to Test5x2.csv, data %csvData%
      retry 2 times over 30 sec period
```
This step attempts to write data to "Test5x2.csv" and will retry twice with a 30-second delay between retries if the initial attempt fails.

## Error Handling

Error handling properties allow you to specify actions to be taken if an error occurs during the execution of a step. You can define specific goals to be called when certain errors are encountered or choose to ignore all errors.

### Examples:
```
- copy file2.txt to file3.txt
      on error call !HandleCopyError
```
This step copies "file2.txt" to "file3.txt" and calls the `!HandleCopyError` goal if an error occurs.

```
- delete file file2.txt
      on error 'file not found', call !NotifyAdmin
      ignore all other errors
```
This step deletes "file2.txt" and calls the `!NotifyAdmin` goal if a 'file not found' error occurs. All other errors are ignored.

## Run and Forget

Run and forget allows you to call a goal and proceed without waiting for the response. This is useful for long-running processes where the immediate result is not necessary for the continuation of the program.

### Example:
```
- listen to 'files/*.json', call !ProcessJson, dont wait
```
This step starts listening to changes in JSON files and calls the `!ProcessJson` goal when a change is detected, without waiting for the goal to complete before moving on to the next step.

By utilizing these properties, developers can create more resilient and efficient PLang scripts that handle various scenarios gracefully.


# Best Practices for File

- Always validate file paths to prevent access to unauthorized directories.
- Use meaningful variable names when reading files into variables for better readability.
- When writing files, consider setting the `overwrite` flag to avoid unintentional data loss.
- Use the `listen to` feature to automate reactions to file changes, but ensure to handle events efficiently to avoid performance issues.
- Regularly back up important files to prevent data loss in case of accidental deletion or corruption.

# CSharp

## FileModule

Source code: [FileModule.cs](https://github.com/PLangHQ/Plang/modules/FileModule.cs)

- `RequestAccessToPath(string path)`: Requests access to a specified file system path.
- `ReadBinaryFileAndConvertToBase64(string path, ...)`: Reads a binary file and converts its content to a Base64 string.
- `ReadTextFile(string path, ...)`: Reads the content of a text file.
- `ReadFileAsStream(string path, ...)`: Reads a file as a stream.
- `ReadExcelFile(string path, ...)`: Reads an Excel file and optionally loads specified sheets into variables.
- `WriteExcelFile(string path, ...)`: Writes data to an Excel file.
- `WriteCsvFile(string path, ...)`: Writes data to a CSV file with various configuration options.
- `ReadCsvFile(string path, ...)`: Reads data from a CSV file and returns it as an object.
- `SaveMultipleFiles(List<FileInfo> files)`: Saves multiple files with their respective content.
- `ReadMultipleTextFiles(string folderPath, ...)`: Reads multiple text files from a specified folder path and search pattern.
- `WriteToFile(string path, ...)`: Writes content to a file.
- `AppendToFile(string path, ...)`: Appends content to a file.
- `CopyFiles(string directoryPath, ...)`: Copies files from one directory to another.
- `CopyFile(string sourceFileName, ...)`: Copies a single file to a destination.
- `DeleteFile(string fileName, ...)`: Deletes a file.
- `GetFileInfo(string fileName)`: Retrieves information about a file.
- `CreateDirectory(string directoryPath)`: Creates a directory.
- `DeleteDirectory(string directoryPath, ...)`: Deletes a directory.
- `GetFilePathsInDirectory(string directoryPath, ...)`: Retrieves file paths in a directory.
- `DirectoryExists(string directoryPath)`: Checks if a directory exists.
- `FileExists(string directoryPath)`: Checks if a file exists.
- `ListenToFileChange(string[] fileSearchPatterns, ...)`: Listens for file changes and calls a specified goal when changes occur.