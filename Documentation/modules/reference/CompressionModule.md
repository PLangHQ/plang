# CompressionModule

Hint: `[compression]`  
Type: `PLang.Modules.CompressionModule.Program`

compress and decompress(extract) a file or folder. This can be various of file formats, zip, gz, or other custom formats. Example usage: `zip file.txt to file.zip`, or `unzip file.zip to file.txt`

## Methods

### CompressDirectory

```
CompressDirectory(String sourceDirectoryName, String destinationArchiveFileName, Int32 compressionLevel = 0, Boolean includeBaseDirectory = True, Boolean createDestinationDirectory = True, Boolean overwriteDestinationFile = False, String[] excludePatterns = null) : object
```


### CompressFile

```
CompressFile(String filePath, String saveToPath, Int32 compressionLevel = 0, Boolean overwrite = False) : object
```

compressionLevel: 0=Optimal, 1=Fastest, 2=No compression, 3=Smallest size(highest compression)


### CompressFiles

```
CompressFiles(String[] filePaths, String saveToPath, Int32 compressionLevel = 0, Boolean overwrite = False) : object
```


### DecompressFile

```
DecompressFile(String sourceArchiveFileName, String destinationDirectoryName, Boolean overwrite = False) : object
```


