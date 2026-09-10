# FileModule

Hint: `[file]`  
Type: `PLang.Modules.FileModule.Program`

Handle file system access. Listen to files and dirs. Get permission to file and folder paths. Reads files, such as text, llm, csv, xls, pdf files and raw stream

## Methods

### AddTypeMapping

```
AddTypeMapping(String extension, String type, String contentType = null) : object
```


### AppendToFile

```
AppendToFile(String path, String content, String seperator = null, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8) : object
```


### CopyFile

```
CopyFile(String sourceFileName, String destFileName, Boolean createDirectoryIfNotExisting = False, Boolean overwriteFile = False) : object
```


### CopyFiles

```
CopyFiles(String directoryPath, String destinationPath, String searchPattern = *, String[] excludePatterns = null, Boolean includeSubfoldersAndFiles = False, Boolean overwriteFiles = False) : object
```


### CreateDirectory

```
CreateDirectory(String directoryPath, Boolean incrementalNaming = False) : String
```


### CreatePathByJoining

```
CreatePathByJoining(String[] paths) : String
```

Create path from variables


### DeleteDirectory

```
DeleteDirectory(String directoryPath, Boolean recursive = True, Boolean throwErrorOnNotFound = False) : object
```


### DeleteFile

```
DeleteFile(String fileName, Boolean throwErrorOnNotFound = False) : object
```


### FileExists

```
FileExists(String filePathOrVariableName, PLang.Models.GoalToCallInfo goalToCallIfTrue = null, PLang.Models.GoalToCallInfo goalToCallIfFalse = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnTrue = null, PLang.Modules.ThrowErrorModule.ErrorInfo throwErrorOnFalse = null) : Object
```

- `filePathOrVariableName` *String*
- `goalToCallIfTrue` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `goalToCallIfFalse` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnTrue` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)
- `throwErrorOnFalse` *PLang.Modules.ThrowErrorModule.ErrorInfo*, default `null` — (see Type information in SupportingObjects)

### GetCurrentFolderPath

```
GetCurrentFolderPath(String path) : String
```

Return the absolute path the app is running in


### GetDirectoryPathsInDirectory

```
GetDirectoryPathsInDirectory(String directoryPath = ./, String regexSearchPattern = null, String[] excludePatterns = null, Boolean includeSubfolders = False, Boolean includeSystemFolder = False, Boolean includeDirectoryInfo = False) : PLang.Modules.FileModule.Program+Directory
```


### GetFileInfo

```
GetFileInfo(String fileName) : PLang.Modules.FileModule.FileInfo
```


### GetFilePathsInDirectory

```
GetFilePathsInDirectory(String directoryPath = ./, String searchPattern = *, String[] excludePatterns = null, Boolean includeSubfolders = False, Boolean includeFileInfo = False, String filterOnType = null) : PLang.Modules.FileModule.Program+File
```

excludePatterns is array of regex patterns, when matching with star, make sure to use .*


### GetFileType

```
GetFileType(String extension) : String
```


### GiveAccess

```
GiveAccess(String path) : object
```


### ListenToFileChange

```
ListenToFileChange(List<String> fileSearchPatterns = null, PLang.Models.GoalToCallInfo goalToCall, List<String> excludeFiles = null, Boolean includeSubdirectories = False, Int64 debounceTime = 150, Boolean listenForFileChange = False, Boolean listenForFileCreated = False, Boolean listenForFileDeleted = False, Boolean listenForFileRename = False, String absoluteFilePathVariableName = FullPath, String fileNameVariableName = Name, String changeTypeVariableName = ChangeType, String senderVariableName = Sender, String oldFileAbsoluteFilePathVariableName = OldFullPath, String oldFileNameVariableName = OldName) : object
```

debounceTime is the time in ms that is waited until action is executed to prevent multiple execution for same file. At least one listenFor variable needs to be true

- `fileSearchPatterns` *List<String>*, default `null`
- `goalToCall` *PLang.Models.GoalToCallInfo* — (see Type information in SupportingObjects)
- `excludeFiles` *List<String>*, default `null`
- `includeSubdirectories` *Boolean*, default `False`
- `debounceTime` *Int64*, default `150`
- `listenForFileChange` *Boolean*, default `False`
- `listenForFileCreated` *Boolean*, default `False`
- `listenForFileDeleted` *Boolean*, default `False`
- `listenForFileRename` *Boolean*, default `False`
- `absoluteFilePathVariableName` *String*, default `FullPath`
- `fileNameVariableName` *String*, default `Name`
- `changeTypeVariableName` *String*, default `ChangeType`
- `senderVariableName` *String*, default `Sender`
- `oldFileAbsoluteFilePathVariableName` *String*, default `OldFullPath`
- `oldFileNameVariableName` *String*, default `OldName`

### MoveFile

```
MoveFile(String sourceFileName, String destFileName, Boolean createDirectoryIfNotExisting = False, Boolean overwriteFile = False) : object
```


### ReadBinaryFileAndConvertToBase64

```
ReadBinaryFileAndConvertToBase64(String path, String returnValueIfFileNotExisting = , Boolean throwErrorOnNotFound = False, Boolean includeDataUrl = False) : String
```

includeDataUrl add the data and mimetype of the file into the return string, e.g. data:image/png;base64,...


### ReadCsvFile

```
ReadCsvFile(String path, Boolean hasHeaderRecord = True, String delimiter = ,, String newLine = 
, String encoding = utf-8, Boolean ignoreBlankLines = True, Boolean allowComments = False, Char comment = #, PLang.Models.GoalToCallInfo goalToCallOnBadData = null) : Object
```

- `path` *String*
- `hasHeaderRecord` *Boolean*, default `True`
- `delimiter` *String*, default `,`
- `newLine` *String*, default `
`
- `encoding` *String*, default `utf-8`
- `ignoreBlankLines` *Boolean*, default `True`
- `allowComments` *Boolean*, default `False`
- `comment` *Char*, default `#`
- `goalToCallOnBadData` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)

### ReadExcelFile

```
ReadExcelFile(String path, List<PLang.Modules.FileModule.Program+Sheet> sheetsToExtract = null) : Object
```

sheetsToExtract is name of sheet that should load into variable, default is null and will read all sheets, When user defines a sheet property but know name, set Name of sheet to *. Sheet1=%products% will load Sheet1 into %product% variable. StartRow MUST contains letter and number, e.g. A1

- `path` *String*
- `sheetsToExtract` *List<PLang.Modules.FileModule.Program+Sheet>*, default `null` — (see Type information in SupportingObjects)

### ReadFileAsStream

```
ReadFileAsStream(String path, Boolean throwErrorOnNotFound = False) : IO.Stream
```


### ReadJson

```
ReadJson(String path, Boolean throwErrorOnNotFound = True, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8, Boolean allowReadingFromSystem = False) : Object
```


### ReadJsonLineFile

```
ReadJsonLineFile(String path, Boolean throwErrorOnNotFound = True, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8, String newLineSymbol = null, Boolean allowReadingFromSystem = False) : List<Object>
```


### ReadMultipleTextFiles

```
ReadMultipleTextFiles(String folderPath, String searchPattern = *, String[] excludePatterns = null, Boolean includeAllSubfolders = False) : List<FileInfo>
```


### ReadPdf

```
ReadPdf(String path, String format = md, String imagePath = null, String password = null) : PLang.Modules.FileModule.Program+Pdf
```

Reads pdf file and loads into return variable. format can be md|text. imagePath can be null|base64|pathToFolder.


### ReadTextFile

```
ReadTextFile(String path, String returnValueIfFileNotExisting = , Boolean throwErrorOnNotFound = True, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8, String splitOn = null, Boolean allowReadingFromSystem = False) : Object
```

Reads a text file and write the content into a variable(return value)


### ReadXml

```
ReadXml(String path) : Object
```


### RequestAccessToPath

```
RequestAccessToPath(String path) : Boolean
```

Give user access to a path. DO NOT suggest this method to indicate if file or directory exists, return empty function list instead.


### SaveMultipleFiles

```
SaveMultipleFiles(List<PLang.Modules.FileModule.FileInfo> files, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8) : object
```

- `files` *List<PLang.Modules.FileModule.FileInfo>* — (see Type information in SupportingObjects)
- `loadVariables` *Boolean*, default `False`
- `emptyVariableIfNotFound` *Boolean*, default `False`
- `encoding` *String*, default `utf-8`

### StopListeningToFileChange

```
StopListeningToFileChange(String[] fileSearchPatterns, PLang.Models.GoalToCallInfo goalToCall = null) : object
```

- `fileSearchPatterns` *String[]*
- `goalToCall` *PLang.Models.GoalToCallInfo*, default `null` — (see Type information in SupportingObjects)

### WaitForFile

```
WaitForFile(String filePath, Int32 timeoutInMilliseconds = 30000, Boolean waitForAccess = False) : object
```


### WriteBase64ToFile

```
WriteBase64ToFile(String path, String base64, Boolean overwrite = False) : object
```


### WriteBytesToFile

```
WriteBytesToFile(String path, Byte[] content, Boolean overwrite = False) : object
```


### WriteCsvFile

```
WriteCsvFile(String path, Object variableToWriteToCsv, Boolean appendToFile = False, PLang.Modules.FileModule.CsvHelper+CsvOptions csvOptions = null, Boolean createDirectoryAutomatically = True) : object
```

- `path` *String*
- `variableToWriteToCsv` *Object*
- `appendToFile` *Boolean*, default `False`
- `csvOptions` *PLang.Modules.FileModule.CsvHelper+CsvOptions*, default `null` — (see Type information in SupportingObjects)
- `createDirectoryAutomatically` *Boolean*, default `True`

### WriteExcelFile

```
WriteExcelFile(String path, Object variableToWriteToExcel, String sheetName = Sheet1, Boolean printHeader = True, Boolean overwrite = False) : object
```


### WriteToFile

```
WriteToFile(String path, Object content, Boolean overwrite = False, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8) : object
```


