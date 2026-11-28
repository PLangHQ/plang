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
RunTerminal(String appExecutableName, List<String> parameters = null, String pathToWorkingDirInTerminal = null, Boolean hideTerminal = False, String parseWith = null) : Object
```

Run an executable and return its output. Parameter strings should not be escaped.

- `appExecutableName` *String* — The program to run, e.g. git in `terminal git --status`, or a full path such as /usr/bin/ffmpeg
- `parameters` *List<String>*, default `null`
- `pathToWorkingDirInTerminal` *String*, default `null` — The directory the program runs in, stated by the step as e.g. `working dir: "/srv/app"`
- `hideTerminal` *Boolean*, default `False` — true when the step says the terminal window should not be shown, e.g. `hide terminal`. false otherwise
- `parseWith` *String*, default `null` — Path to a JSON template file that turns the program's text output into objects, stated by the step as e.g. `parse with "/parsers/df.json"`. Null when the step does not say parse with

Examples:

- terminal git --status, write to %output% => appExecutableName=git, parameters="--status", ReturnValues = %output%
- terminal /usr/bin/du, parameters: [%dir%] working dir: "/srv", hide terminal, write to %usage% => appExecutableName="/usr/bin/du", parameters="%dir%", pathToWorkingDirInTerminal="/srv", hideTerminal=true, ReturnValues = %usage%
- terminal df, parameters: ["-P"], parse with "/parsers/df.json", write to %disks% => appExecutableName="df", parameters="-P", parseWith="/parsers/df.json", ReturnValues = %disks%

### RunTerminalStreaming

```
RunTerminalStreaming(String appExecutableName, String variableNameForDeltaOnStandardStream, List<String> parameters = null, String pathToWorkingDirInTerminal = null, String variableNameForDeltaOnErrorStream = null, Boolean hideTerminal = False, String parseWith = null) : Object
```

Run an executable that streams its output while it runs, for a step that names a variable to receive each line as it arrives. A plain `write to %variable%` is the finished output and belongs to RunTerminal, not here: this method is only for a step that names a separate variable for the running output, e.g. `terminal ffmpeg ..., %delta%`.

- `appExecutableName` *String* — The program to run, e.g. ffmpeg, or a full path such as /usr/bin/ffmpeg
- `variableNameForDeltaOnStandardStream` *String* — The variable that receives standard output line by line as it arrives
- `parameters` *List<String>*, default `null`
- `pathToWorkingDirInTerminal` *String*, default `null` — The directory the program runs in, stated by the step as e.g. `working dir: "/srv/app"`
- `variableNameForDeltaOnErrorStream` *String*, default `null` — The variable that receives the error stream line by line, when the step names a second one
- `hideTerminal` *Boolean*, default `False` — true when the step says the terminal window should not be shown, e.g. `hide terminal`. false otherwise
- `parseWith` *String*, default `null` — Path to a JSON template file that turns each output line into an object for the streaming variable, and the whole output into the return value, stated by the step as e.g. `parse with "/parsers/ffmpeg.json"`. Null when the step does not say parse with

Examples:

- terminal /usr/bin/rsync -av src dst, %line%, %problem%, write to %summary% => appExecutableName="/usr/bin/rsync", parameters="-av","src","dst", variableNameForDeltaOnStandardStream="%line%", variableNameForDeltaOnErrorStream="%problem%", ReturnValues = %summary%
- terminal ffmpeg -i in.mp4 out.mp4, %line%, parse with "/parsers/ffmpeg.json", write to %info% => appExecutableName="ffmpeg", parameters="-i","in.mp4","out.mp4", variableNameForDeltaOnStandardStream="%line%", parseWith="/parsers/ffmpeg.json", ReturnValues = %info%

### StartProcess

```
StartProcess(String appExecutableName, List<String> parameters = null, String pathToWorkingDirInTerminal = null, PLang.Models.GoalToCallInfo onOutputLine = null, PLang.Models.GoalToCallInfo onErrorLine = null, PLang.Models.GoalToCallInfo onExit = null, String name = null) : PLang.Modules.TerminalModule.PlangProcess
```

Start a program that keeps running beside the app, and return it, so the step writes it to a %variable%. Each line the program writes is handed to onOutputLine (standard output) or onErrorLine (standard error) as %line%, one at a time and in order, with the process as %process%; onExit gets %exitCode% when it ends. Write to its standard input with `write out %text% to %variable%`, end it with `stop process %variable%`. Not for a command whose output the step waits for: that is RunTerminal.

- `appExecutableName` *String* — The program to run, e.g. claude, or a full path such as /usr/bin/node
- `parameters` *List<String>*, default `null`
- `pathToWorkingDirInTerminal` *String*, default `null` — The directory the program runs in, stated by the step as e.g. `working dir: "/srv/app"`
- `onOutputLine` *PLang.Models.GoalToCallInfo*, default `null` — The goal called for each line on standard output, `on output line call X` (see Type information in SupportingObjects)
- `onErrorLine` *PLang.Models.GoalToCallInfo*, default `null` — The goal called for each line on standard error, `on error line call X` (see Type information in SupportingObjects)
- `onExit` *PLang.Models.GoalToCallInfo*, default `null` — The goal called when the program has ended, `on exit call X` (see Type information in SupportingObjects)
- `name` *String*, default `null` — A name for the process, shown in runtime stats; the program name when the step gives none

Examples:

- start process "claude", parameters ["-p", "--verbose"], on output line call OnLine, on exit call OnExit, write to %bot% => appExecutableName="claude", parameters=["-p","--verbose"], onOutputLine={Name="OnLine"}, onExit={Name="OnExit"}, ReturnValues = %bot%

### StopProcess

```
StopProcess(Object process) : object
```

End a program started with `start process`, e.g. `stop process %bot%`. Its onExit goal is called.


Examples:

- stop process %bot% => process="%bot%"

