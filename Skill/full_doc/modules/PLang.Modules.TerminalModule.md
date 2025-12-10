
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
