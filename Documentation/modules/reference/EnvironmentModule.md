# EnvironmentModule

Hint: `[environment]`  
Type: `PLang.Modules.EnvironmentModule.Program`

Information about the environment that the software is running, such as machine name, os user name, process id, settings, debug modes, set culture, open file in default app, npm install

## Methods

### CanSeeErrorDetails

```
CanSeeErrorDetails() : Boolean
```


### EndApp

```
EndApp() : object
```


### GetAppName

```
GetAppName() : String
```


### GetCurrentCulture

```
GetCurrentCulture() : Globalization.CultureInfo
```


### GetEnvironmentVariable

```
GetEnvironmentVariable(String key) : String
```


### GetMachineName

```
GetMachineName() : String
```


### GetMemoryInfo

```
GetMemoryInfo(Boolean forceGarbageCollection = False) : PLang.Modules.EnvironmentModule.Program+MemoryInfo
```

Get current process memory usage details


### GetMocks

```
GetMocks() : PLang.Modules.MockModule.Program+MockData
```

Get active mocks on engine, used for testing


### GetOSDescription

```
GetOSDescription() : String
```


### GetProcessId

```
GetProcessId() : Int32
```


### GetSettings

```
GetSettings() : PLang.Models.Setting
```


### GetUserName

```
GetUserName() : String
```


### HideErrorDetails

```
HideErrorDetails() : object
```


### InstallNpm

```
InstallNpm(String packageName) : object
```


### IsInCSharpDebugMode

```
IsInCSharpDebugMode() : Boolean
```


### IsInDebugMode

```
IsInDebugMode() : Boolean
```


### KeepAlive

```
KeepAlive(String message = App KeepAlive) : object
```


### OpenFileInDefaultApp

```
OpenFileInDefaultApp(String filePath) : object
```


### RemoveDebugMode

```
RemoveDebugMode() : object
```


### SetCultureLanguageCode

```
SetCultureLanguageCode(String code = en-US) : object
```

Make sure to convert user code to valid BCP 47 code, language-country


### SetCultureUILanguageCode

```
SetCultureUILanguageCode(String code = en-US) : object
```

Make sure to convert user code to valid BCP 47 code, language-country


### SetDebugMode

```
SetDebugMode() : object
```


### SetEnvironment

```
SetEnvironment(String name) : object
```


### SetSettingsDbPath

```
SetSettingsDbPath(String path) : object
```


### ShowErrorDetails

```
ShowErrorDetails() : object
```


