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

- `path` *String*
- `content` *String*
- `seperator` *String*, default `null`
- `loadVariables` *Boolean*, default `False` — true only when the step says in words that the %variables% inside the content should be replaced with their values before it is written. A content that merely contains variables is not a reason: `content: {"a": %x%}` writes the variable through as it stands, which is the normal case and false
- `emptyVariableIfNotFound` *Boolean*, default `False` — true when the step says the variable should simply be empty if the file is missing, e.g. `empty variable if not found`. false otherwise
- `encoding` *String*, default `utf-8`

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


### GetFileHash

```
GetFileHash(String path, String algorithm = sha256) : String
```

Hash of the content of a file, as lower case hex. algorithm: sha256 (default), sha1 or md5. e.g. `get hash of file %path%, write to %hash%`, `get sha256 of file //shared/x.dll, write to %hash%`


Examples:

- get hash of file %path%, write to %hash% => path=%path%, algorithm="sha256"

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

Give the app access to a path outside the app folder, e.g. `give access to "/proc"`, `give access to "//etc"` (// is the root of the drive), `allow access to %folder%`. The app can then read and write under that path for the rest of the run without being asked.


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

- `path` *String*
- `throwErrorOnNotFound` *Boolean*, default `True` — false when the step says a missing file is not an error, e.g. `dont throw error on not found` or `empty variable if not found`. true otherwise
- `loadVariables` *Boolean*, default `False` — true only when the step says in words to load variables, e.g. `read file x.txt, load variables`. A path or a step containing variables is not a reason: false is the normal case and the file is read as it stands
- `emptyVariableIfNotFound` *Boolean*, default `False` — true when the step says the variable should simply be empty if the file is missing, e.g. `empty variable if not found`. false otherwise
- `encoding` *String*, default `utf-8`
- `allowReadingFromSystem` *Boolean*, default `False` — Almost always false. It is true only when the step says in words that it reads from plang's own installation folder, outside this app. Any path belonging to the app is false, including paths that begin with a dot folder such as /.db/ or /.build/, which are the app's own. A path that begins with /// is a path on the machine outside the app (e.g. ///proc/..., ///etc/...), not plang's installation: false

### ReadJsonLineFile

```
ReadJsonLineFile(String path, Boolean throwErrorOnNotFound = True, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8, String newLineSymbol = null, Boolean allowReadingFromSystem = False) : List<Object>
```

- `path` *String*
- `throwErrorOnNotFound` *Boolean*, default `True` — false when the step says a missing file is not an error, e.g. `dont throw error on not found` or `empty variable if not found`. true otherwise
- `loadVariables` *Boolean*, default `False` — true only when the step says in words to load variables, e.g. `read file x.txt, load variables`. A path or a step containing variables is not a reason: false is the normal case and the file is read as it stands
- `emptyVariableIfNotFound` *Boolean*, default `False` — true when the step says the variable should simply be empty if the file is missing, e.g. `empty variable if not found`. false otherwise
- `encoding` *String*, default `utf-8`
- `newLineSymbol` *String*, default `null`
- `allowReadingFromSystem` *Boolean*, default `False` — Almost always false. It is true only when the step says in words that it reads from plang's own installation folder, outside this app. Any path belonging to the app is false, including paths that begin with a dot folder such as /.db/ or /.build/, which are the app's own. A path that begins with /// is a path on the machine outside the app (e.g. ///proc/..., ///etc/...), not plang's installation: false

### ReadMultipleTextFiles

```
ReadMultipleTextFiles(String folderPath, String searchPattern = *, String[] excludePatterns = null, Boolean includeAllSubfolders = False) : List<FileInfo>
```


### ReadPdf

```
ReadPdf(String path, String format = md, String imagePath = null, String password = null) : PLang.Modules.FileModule.Program+Pdf
```

Reads pdf file and loads into return variable. format can be md|text|layout. layout keeps the horizontal position of words so table columns line up. imagePath can be null|base64|pathToFolder.


### ReadTextFile

```
ReadTextFile(String path, String returnValueIfFileNotExisting = , Boolean throwErrorOnNotFound = True, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8, String splitOn = null, Boolean allowReadingFromSystem = False) : Object
```

Reads a text file and write the content into a variable(return value)

- `path` *String*
- `returnValueIfFileNotExisting` *String*, default ``
- `throwErrorOnNotFound` *Boolean*, default `True` — false when the step says a missing file is not an error, e.g. `dont throw error on not found` or `empty variable if not found`. true otherwise
- `loadVariables` *Boolean*, default `False` — true only when the step says in words to load variables, e.g. `read file x.txt, load variables`. A path or a step containing variables is not a reason: false is the normal case and the file is read as it stands
- `emptyVariableIfNotFound` *Boolean*, default `False` — true when the step says the variable should simply be empty if the file is missing, e.g. `empty variable if not found`. false otherwise
- `encoding` *String*, default `utf-8`
- `splitOn` *String*, default `null`
- `allowReadingFromSystem` *Boolean*, default `False` — Almost always false. It is true only when the step says in words that it reads from plang's own installation folder, outside this app. Any path belonging to the app is false, including paths that begin with a dot folder such as /.db/ or /.build/, which are the app's own. A path that begins with /// is a path on the machine outside the app (e.g. ///proc/..., ///etc/...), not plang's installation: false

### ReadXml

```
ReadXml(String path) : Object
```


### ReplaceInFile

```
ReplaceInFile(String path, String oldText, String newText, String encoding = utf-8) : Int32
```

Replaces an exact piece of text in a file with another, like a coding assistant's targeted edit. oldText must match the file content exactly and must appear exactly once, otherwise it errors, so it never changes more than intended. Include enough surrounding text to make oldText unique. Returns 1 on success.


### RequestAccessToPath

```
RequestAccessToPath(String path) : Boolean
```

Check whether the app already may access a path, asking the user when it may not; returns true when access is allowed. Grants nothing by itself: for `give access to <path>` use GiveAccess. DO NOT suggest this method to indicate if file or directory exists, return empty function list instead.


### SaveMultipleFiles

```
SaveMultipleFiles(List<PLang.Modules.FileModule.FileInfo> files, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8) : object
```

- `files` *List<PLang.Modules.FileModule.FileInfo>* — (see Type information in SupportingObjects)
- `loadVariables` *Boolean*, default `False`
- `emptyVariableIfNotFound` *Boolean*, default `False` — true when the step says the variable should simply be empty if the file is missing, e.g. `empty variable if not found`. false otherwise
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

- `path` *String*
- `base64` *String*
- `overwrite` *Boolean*, default `False` — true when the step says to overwrite or replace the file. false when it says to append, or says nothing about an existing file

### WriteBytesToFile

```
WriteBytesToFile(String path, Byte[] content, Boolean overwrite = False) : object
```

- `path` *String*
- `content` *Byte[]*
- `overwrite` *Boolean*, default `False` — true when the step says to overwrite or replace the file. false when it says to append, or says nothing about an existing file

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

- `path` *String*
- `variableToWriteToExcel` *Object*
- `sheetName` *String*, default `Sheet1`
- `printHeader` *Boolean*, default `True`
- `overwrite` *Boolean*, default `False` — true when the step says to overwrite or replace the file. false when it says to append, or says nothing about an existing file

### WriteToFile

```
WriteToFile(String path, Object content, Boolean overwrite = False, Boolean loadVariables = False, Boolean emptyVariableIfNotFound = False, String encoding = utf-8, String identity = null, List<String> contracts = null) : object
```

- `path` *String*
- `content` *Object*
- `overwrite` *Boolean*, default `False` — true when the step says to overwrite or replace the file. false when it says to append, or says nothing about an existing file
- `loadVariables` *Boolean*, default `False` — true only when the step says in words that the %variables% inside the content should be replaced with their values before it is written. A content that merely contains variables is not a reason: `content: {"a": %x%}` writes the variable through as it stands, which is the normal case and false
- `emptyVariableIfNotFound` *Boolean*, default `False` — true when the step says the variable should simply be empty if the file is missing, e.g. `empty variable if not found`. false otherwise
- `encoding` *String*, default `utf-8`
- `identity` *String*, default `null` — Only when the step says to sign the file, e.g. `sign with identity default`. 'default' is the current identity. The signature is written next to the file as <file>.signature and covers the file's name and the sha256 of the bytes written. null when the step does not ask for a signature
- `contracts` *List<String>*, default `null`

