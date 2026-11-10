
# Terminal
## Introduction
The Terminal module in plang is a powerful feature that allows you to interact with the system's command line interface (CLI) directly from your plang scripts. It enables you to execute external commands, scripts, and programs, and to handle their input and output within your plang code.

## For Beginners
If you're new to programming, the Terminal can be thought of as a way to communicate with your computer's operating system using text-based commands. It's like giving your computer a set of instructions to perform specific tasks, such as managing files, running programs, or accessing network resources.

In plang, the Terminal module lets you write these instructions in a structured way, allowing you to automate processes and integrate external tools into your workflows. You don't need to understand all the technical details to get started; you can simply follow the examples and gradually learn how to tailor them to your needs.

## Best Practices for Terminal
When writing plang code that interacts with the Terminal, it's important to keep a few best practices in mind:

1. **Validate Command Outputs**: Always check the output of your commands to ensure they executed successfully and handle any errors that may occur.
2. **Use Variables Wisely**: Store command outputs in variables when you need to use them later in your script.
3. **Handle Large Outputs**: Be cautious when dealing with commands that produce large amounts of output. Use conditions to handle these cases efficiently.
4. **Secure User Input**: When reading user input, ensure that it's sanitized to prevent unintended command execution.
5. **Comment Your Code**: Use comments to explain what each terminal command does, making your script easier to understand and maintain.

Here's an example that incorporates these best practices:

```plang
BackupLogs
- run 'tar' with parameters ['-czf', 'logs_backup.tar.gz', '/var/log'], write to %backupResult%
- if %backupResult% contains 'error' then call !HandleBackupError, else !HandleBackupSuccess
- write out 'Backup completed successfully.'
```

In this example, we're creating a compressed backup of the `/var/log` directory. We store the result of the `tar` command in a variable `%backupResult%`, check for errors, and call appropriate handlers based on the outcome.


# Terminal Module Examples

The Terminal module provides access to the terminal/console for running external applications. Below are examples of how to use the Terminal module in plang, sorted by the most common use cases.

## 1. Running a Terminal Command

This example demonstrates how to run a simple terminal command and capture its output.

```plang
Terminal
- run 'ping' with parameters 'google.com', write to %pingResult%
- write out '\nPing Output:\n%pingResult%'
```

## 2. Running a Terminal Command with Parameters

Here we run a command with multiple parameters and capture both the output and error information.

```plang
Terminal
- run 'ffmpeg' with parameters ['-i', 'input.mp4', '-codec', 'copy', 'output.mkv'], write to %ffmpegOutput%
- if %ffmpegOutput% contains 'error' then call !HandleError, else !HandleSuccess
```

## 3. Running a Terminal Command with a Working Directory

This example shows how to run a command in a specific working directory.

```plang
Terminal
- run 'git' with parameters ['pull'], in directory '/path/to/repo', write to %gitPullResult%
- write out 'Git Pull Result:\n%gitPullResult%'
```

## 4. Capturing Output and Error Streams Separately

In this example, we capture the standard output and error streams separately.

```plang
Terminal
- run 'node' with parameters ['script.js'], output delta %outputDelta%, error stream delta %errorDelta%, write to %nodeResult%
- write out 'Script Output:\n%outputDelta%'
- write out 'Script Errors:\n%errorDelta%'
```

## 5. Reading User Input

This example demonstrates how to read user input from the terminal.

```plang
Terminal
- read 'Please enter your name: ', write to %userName%
- write out 'Hello, %userName%!'
```

## 6. Running a Long-Running Process and Monitoring Output Changes

Here we monitor the output of a long-running process and react to changes.

```plang
Terminal
- run 'tail' with parameters ['-f', '/var/log/syslog'], output delta %logDelta%, write to %tailResult%
- when var %logDelta% changes, call !ProcessLogDelta
```

## 7. Handling Errors from Terminal Commands

This example includes error handling for terminal commands.

```plang
Terminal
- run 'cp' with parameters ['/missing/file', '/destination'], write to %copyResult%
- if %copyResult% contains 'error' then call !HandleCopyError, else !HandleCopySuccess
```

## 8. Running a Command with a Large Output

When dealing with large outputs, it's important to handle the data efficiently.

```plang
Terminal
- run 'find' with parameters ['/home/user', '-type', 'f'], write to %findResult%
- if %findResult% size is greater than '50MB' then call !HandleLargeOutput, else !HandleNormalOutput
```

## 9. Running a Command and Capturing Exit Code

Capturing the exit code of a command can be crucial for conditional logic based on the success or failure of the command.

```plang
Terminal
- run 'grep' with parameters ['-r', 'pattern', '/some/dir'], write to %grepResult%
- if %grepResult% exit code is '0' then call !PatternFound, else !PatternNotFound
```

## 10. Running a Command with Environment Variables

This example shows how to run a command with specific environment variables set.

```plang
Terminal
- run 'python' with parameters ['script.py'], with env 'ENV_VAR=value', write to %pythonResult%
- write out 'Python Script Result:\n%pythonResult%'
```

Note: The examples above are based on the provided plang language rules and the method descriptions of the TerminalModule class. They are intended to illustrate the usage of the Terminal module in a plang script.


For a full list of examples, visit [PLang Terminal Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Terminal).

## Parsing the output with a template (`parse with`)
A program writes text. `parse with "<template file>"` turns that text into plang objects, so the goal does not have to Trim and Split. The template is a JSON file, its path is app relative like other plang file paths. A program that already prints JSON (`--json`) needs no template: plang resolves a JSON string on key paths.

```plang
- run 'df', parameters: ['-P'], parse with '/parsers/df.json', write to %disks%
- write out '%disks[0].mount% uses %disks[0].capacity%'
```

Both `RunTerminal` and `RunTerminalStreaming` take it. A missing file, invalid JSON or an invalid regex stops the step with an error before the program is started.

### Template format

```json
{
  "rules": [ ],
  "skip": 0,
  "trim": true,
  "types": "auto",
  "ignore": "<regex>"
}
```

| Key | Meaning |
|---|---|
| `rules` | Optional list of rules. A template without `rules` is itself one rule (it carries `mode` and the mode options directly). |
| `skip` | The first N lines are dropped (a header). Blank lines never reach the parser, so they are not counted. |
| `trim` | Default `true`: lines are trimmed and empty lines dropped. |
| `types` | `"auto"` (default): a value that is wholly a number or `true`/`false` becomes a number or bool, `53%` and `00:01:30` stay text, and a value that was quoted and unquoted by `unquote` stays text. `"string"`: everything is text. |
| `ignore` | Regex. Matching lines are dropped silently, they are not counted as unmatched. |

A rule is `{ "as": "name", "when": "<regex>", "mode": "...", "once": false, ... }`.

- `as`: the name of the result (required when the template has `rules`, names must be unique).
- `when`: optional regex. The rule only takes lines it matches. Every line is tried against the rules in order, the first rule whose `when` matches (no `when` = always) takes the line. If that rule cannot parse the line, the line is unmatched, the next rule is not tried. So give each rule of a multi rule template a `when`.
- `once`: `true` merges all lines of the rule into one object (later keys win), otherwise the rule gives a list of objects.

Modes:

- `columns`: `"separator"` is `"whitespace"` (default) or a single character. `"columns"` maps a name to a 1-based index, `{"name": 1, "size": 3}`. `"lastColumnRest": true` makes the last named column take the rest of the line, including spaces. A line with fewer columns than the highest index is unmatched.
- `match`: `"pattern"` is a .NET regex with named groups. A matching line becomes an object keyed by the group names (a group that did not take part is left out). A non matching line is unmatched.
- `keyvalue`: `"separator"` (default `=`), `"pairsOn"` is `"line"` (default, one pair per line, split on the first separator) or `"whitespace"` (several pairs on a line, the value of a key runs to the next key, e.g. ffmpeg `frame=  100 fps= 25`). `"trimValues"` (default `true`), `"unquote": true` strips surrounding double quotes, `"blockEnd": "<key>"` closes the current object when that key appears and starts a new one, which gives a list of objects (ffmpeg `-progress pipe:1`).

### Result shape

| Template | Result |
|---|---|
| no `rules`, `columns` or `match` | a list of objects |
| no `rules`, `keyvalue` without `blockEnd` | one object |
| no `rules`, `keyvalue` with `blockEnd` | a list of objects |
| `rules` | an object keyed by each rule's `as`: one object for a `once` rule, otherwise a list of objects |

Every parsed object carries `!Raw`, the original line (for a `once` object or a `blockEnd` block, the joined lines of that object). Read it with `%row!Raw%`.

Lines no rule took (and not `ignore`d) are the unmatched lines. They are reachable as `%result!Unmatched%`, a list of strings, for both result shapes. When the result is a list, `!Unmatched` is a property of the variable and is always there (possibly empty). When the result is one object it is a `!Unmatched` member of that object, present only when there are unmatched lines.

### Standard error and exit codes

Without `parse with` nothing is checked beyond the old rule: any text on standard error makes the step return an error, and the exit code is ignored. With `parse with` the template says how the program behaves, through two top level options:

```json
{
  "stderr": "output",
  "okExitCodes": [0]
}
```

| Key | Meaning |
|---|---|
| `stderr` | `"error"` (default): any text on standard error is an error, exactly as without a template. A non ok exit code is also an error, even with empty standard error. `"output"`: standard error lines are ordinary output lines, they go through the same rules, columns and match as standard output, and stderr text alone never makes an error. `"ignore"`: standard error lines are dropped. |
| `okExitCodes` | List of whole numbers, default `[0]`. An exit code outside the list is an error. |

With `"output"` and `"ignore"` the step is an error only when the exit code is not in `okExitCodes`. The error message holds the command, the exit code and the last 5 lines of standard error, so the failure can be diagnosed. A value other than `"error"`, `"output"` or `"ignore"` for `stderr`, or an `okExitCodes` that is not a non empty list of whole numbers, stops the step before the program is started, like the other template errors.

With `"output"` the two streams are read on separate threads and share one buffer under a lock, so the order between a standard error line and a standard output line is the order they arrive, not necessarily the order the program wrote them (the order within each stream is always kept). In streaming mode the standard error lines are put to the variable that receives standard output, as parsed objects with `!Raw` and `!Rule`, not to the error variable. With `"ignore"` they are not put anywhere.

The exit code is a property of the result variable, read as `%result!ExitCode%`, for any step that waits for the program (also without `parse with`). It does not exist when the step does not wait for the program (run and forget), and then neither the exit code check nor the parse is done.

### Streaming
With `RunTerminalStreaming` (a step that names a variable for the running output) the template applies per line. The variable receives the parsed object of that line, with `!Raw`, and `!Rule` (the rule's `as`) when the template has `rules`. A line no rule took is put as the plain text line. Skipped and ignored lines are not put. `once` and `blockEnd` only apply to the final result, in streaming each line is its own object. The value returned by the step (`write to`) is the parse of the whole output, as above. The streaming goal can use `%line.frame%`:

```plang
Encode
- on change %line%, call goal Progress
- terminal ffmpeg, parameters: ['-i', 'in.mp4', 'out.mp4'], %line%, parse with '/parsers/ffmpeg.json', write to %info%

Progress
- write out 'frame %line.frame%'
```

### Example 1: `df -P` (columns)
`/parsers/df.json`
```json
{
  "mode": "columns",
  "skip": 1,
  "lastColumnRest": true,
  "columns": {"filesystem": 1, "blocks": 2, "used": 3, "available": 4, "capacity": 5, "mount": 6}
}
```
gives `[ {"filesystem": "overlay", "blocks": 1055762868, "used": 108793816, "available": 893265580, "capacity": "11%", "mount": "/", "!Raw": "..."}, ... ]`.

### Example 2: ffmpeg (rules)
`/parsers/ffmpeg.json`
```json
{
  "rules": [
    {"as": "input", "when": "^Input #", "mode": "match", "once": true, "pattern": "from '(?<file>[^']+)'"},
    {"as": "duration", "when": "^Duration:", "mode": "match", "once": true, "pattern": "^Duration: (?<duration>[\\d:.]+), start: (?<start>[\\d.]+), bitrate: (?<bitrate>\\d+) kb/s"},
    {"as": "streams", "when": "^Stream #", "mode": "match", "pattern": "^Stream #(?<id>[\\d:]+): (?<type>\\w+): (?<codec>[^,]+)"},
    {"as": "progress", "when": "^frame=", "mode": "keyvalue", "separator": "=", "pairsOn": "whitespace"}
  ]
}
```
gives `{ "input": {"file": "in.mp4", ...}, "duration": {"duration": "00:01:30.50", "bitrate": 1200, ...}, "streams": [ ... ], "progress": [ {"frame": 100, "fps": 25, "q": 28.0, "size": "256kB", ...}, ... ] }`, so `%info.duration.duration%` and `%info.progress.Count%` work. Note that ffmpeg writes its normal output to standard error, so add `"stderr": "output"` at the top of this template (next to `rules`). Then the step parses ffmpeg's progress and info lines and is an error only when ffmpeg exits with a non zero code:
```json
{
  "stderr": "output",
  "okExitCodes": [0],
  "rules": [ ... as above ... ]
}
```
```plang
- terminal ffmpeg, parameters: ['-i', 'in.mp4', 'out.mp4'], parse with '/parsers/ffmpeg.json', write to %info%
- write out 'ffmpeg exit code %info!ExitCode%, %info.progress.Count% progress lines'
```

## Step Options
When writing steps in your plang script, you have several options to enhance functionality and handle different scenarios:

- [CacheHandler](/modules/cacheHandler.md): Use this to cache command outputs and improve performance.
- [ErrorHandler](/modules/ErrorHandler.md): Implement this to manage and respond to errors in your terminal commands.
- [RetryHandler](/modules/RetryHandler.md): This option allows you to retry a command if it fails initially.
- [CancellationHandler](/modules/CancelationHandler.md): Use this to gracefully cancel long-running commands if necessary.
- [Run and Forget](/modules/RunAndForget.md): This is useful for running commands whose outputs you don't need to capture or monitor.

Click on the links above for more details on how to use each option.

## Advanced
For those who are interested in diving deeper into the Terminal module and understanding how it interfaces with C#, you can explore more advanced topics [here](./PLang.Modules.TerminalModule_advanced.md).

## Created
This documentation was created on 2024-01-02T22:30:39.
