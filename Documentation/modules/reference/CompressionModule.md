# CompressionModule

Hint: `[compression]`  
Type: `PLang.Modules.CompressionModule.Program`

compress and decompress(extract) a file or folder. This can be various of file formats, zip, gz, or other custom formats. Example usage: `zip file.txt to file.zip`, or `unzip file.zip to file.txt`

## Methods

### CompressDirectory

```
CompressDirectory(String sourceDirectoryName, String destinationArchiveFileName, Int32 compressionLevel = 0, Boolean includeBaseDirectory = True, Boolean createDestinationDirectory = True, Boolean overwriteDestinationFile = False, String[] excludePatterns = null) : object
```

- `sourceDirectoryName` *String*
- `destinationArchiveFileName` *String*
- `compressionLevel` *Int32*, default `0`
- `includeBaseDirectory` *Boolean*, default `True` — true (default): the archive holds the folder itself, folder/file.txt. false when the step says without the folder, the content only, not the base folder: the archive holds file.txt, and extracting it puts the files straight into the destination
- `createDestinationDirectory` *Boolean*, default `True`
- `overwriteDestinationFile` *Boolean*, default `False`
- `excludePatterns` *String[]*, default `null`

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


