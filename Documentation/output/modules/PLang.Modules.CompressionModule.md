
# Compression
## Introduction
Compression is a process that reduces the size of files and directories, making them easier to store and transfer. It's particularly useful when dealing with large amounts of data or when bandwidth is limited.

## For Beginners
Imagine you have a closet full of clothes that you need to pack into a suitcase. Compression is like folding and arranging your clothes in a way that takes up the least amount of space possible. In the digital world, compression works by finding and eliminating redundancies in data, allowing the same information to be stored using fewer bits and bytes.

## Best Practices for Compression
When using compression in plang, it's important to consider the following best practices:
- Choose the right compression level: Higher levels result in smaller files but take longer to compress.
- Be mindful of the file types: Some files, like JPEG images and MP3 audio files, are already compressed and may not benefit much from further compression.
- Clean up after decompression: Remove temporary files and archives to free up space.

### Example
Let's say you have a folder with text documents that you want to compress. You would use a high compression level because text files compress well. After compressing, you should delete the original folder if it's no longer needed to save space.

## Examples

# Compression Module Documentation

The Compression Module provides methods for compressing and decompressing files and directories. Below are examples of how to use the Compression Module in plang.

## Examples

### Compress a Single File
- Compress `report.txt` to `report.zip` with a standard compression level
```plang
- compress `report.txt` to `report.zip`
```

### Compress Multiple Files
- Compress `report.txt` and `summary.txt` to `documents.zip` with high compression
```plang
- compress `report.txt`, `summary.txt` to `documents.zip` with compression level 9
```

### Decompress a File
- Decompress `archive.zip` to the `./extracted` directory and overwrite existing files
```plang
- uncompress `archive.zip` to `./extracted/`, overwrite
```

### Compress a Directory
- Compress the entire `logs` directory to `logs-archive.zip` including the base directory with maximum compression
```plang
- compress directory `logs` to `logs-archive.zip` with compression level 9, include base directory
```

### Decompress and Read a File
- Decompress `data.zip` to `./extracted`, read `extracted/data.txt`, and check if it contains specific text
```plang
- uncompress `data.zip` to `./extracted/`, overwrite
- read `extracted/data.txt`, write to `%content%`
- if `%content%` contains 'expected data' then
    - write out 'The decompressed file contains the expected data.'
```

### Cleanup After Operations
- Delete the `archive.zip` and the `./extracted` directory after operations
```plang
- delete `archive.zip`
- delete directory `./extracted`
```

## Method Descriptions

### CompressFile
Compresses a single file to a specified archive file.
- Parameters: `filePath` (required), `saveToPath` (required), `compressionLevel` (optional, default is 0)
- Returns: Task

### CompressFiles
Compresses multiple files to a specified archive file.
- Parameters: `filePaths` (required, array of strings), `saveToPath` (required), `compressionLevel` (optional, default is 0)
- Returns: Task

### DecompressFile
Decompresses an archive file to a specified directory.
- Parameters: `sourceArchiveFileName` (required), `destinationDirectoryName` (required), `overwrite` (optional, default is false)
- Returns: Task

### CompressDirectory
Compresses an entire directory to a specified archive file.
- Parameters: `sourceDirectoryName` (required), `destinationArchiveFileName` (required), `compressionLevel` (optional, default is 0), `includeBaseDirectory` (optional, default is true)
- Returns: Task

Note: The examples provided are sorted based on the assumed popularity of usage, with single file compression being the most common, followed by decompression, and then directory compression. The cleanup example is included to demonstrate good practice after file operations.


For a full list of examples, visit [PLang Compression Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Compression).

## Step Options
When writing your plang code, you can enhance your steps with these options. Click on each for more details on how to use them:
- [CacheHandler](/modules/handlers/CachingHandler.md)
- [ErrorHandler](/modules/handlers/ErrorHandler.md)
- [RetryHandler](/modules/handlers/RetryHandler.md)
- [CancellationHandler](/moduels/CancelationHandler.md)


## Advanced
For more advanced information on how plang's compression module maps to underlying C# functionality, see [PLang.Modules.CompressionModule Advanced Documentation](./PLang.Modules.CompressionModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:37:49.
