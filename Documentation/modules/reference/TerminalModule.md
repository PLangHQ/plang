# TerminalModule

Hint: `[terminal]`  
Type: `PLang.Modules.TerminalModule.Program`

Terminal/Console access to run external applications

## Methods

### Read

```
Read(String variableName) : object
```


### RunTerminal

```
RunTerminal(String appExecutableName, List<String> parameters = null, String pathToWorkingDirInTerminal = null, String variableNameForDeltaOnStandardStream = null, String variableNameForDeltaOnErrorStream = null, Boolean hideTerminal = False) : Object
```

Run a executable. Parameters string should not be escaped. variableNameForDeltaOnStandardStream and variableNameForDeltaOnErrorStream must to be clearly defined by the user either in it's name or with parameter variableNameForDeltaOnStandardStream: or variableNameForDeltaOnErrorStream:. When user write to a %variable%, this is the whole standard output stream, NOT delta.


Examples:

- terminal git --status, write to %output% => appExecutableName=git, parameters="--status", variableNameForDeltaOnStandardStream=null, variableNameForDeltaOnErrorStream=null, ReturnValues = %output%
- terminal ffmpeg -i input.mp4 output.avi, %delta%, %errorDelta%, write to %data% => appExecutableName=ffmpeg, parameters="-i","input.mp4","output.avi", variableNameForDeltaOnStandardStream=%delta%, variableNameForDeltaOnErrorStream=%errorDelta%, ReturnValues should be %data%

