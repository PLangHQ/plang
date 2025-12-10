# Plang Runtime and Builder Parameters Guide

Welcome to the Plang runtime and builder parameters guide. This document provides detailed instructions on how to start the Plang runtime with various parameters and how to build your Plang projects effectively.

## Starting the Plang Runtime

### Default Execution
To run Plang without any specific parameters, which by default executes the `Start.goal` file, use the following command:

```bash
plang
```

### Running a Specific Goal File
If you wish to execute a specific goal file, specify the path to the file as follows:

```bash
plang /path/to/file
```

## Building Your Project

To build your Plang project, use the following command:

```bash
plang build
```

## Optional Parameters

Plang supports several optional parameters that enhance its functionality and debugging capabilities:

- **`--debug`**: Initiates a debug session. This parameter creates an event folder at `/events/external/plang/runtime` and binds an event before each step. Debug data is sent to `localhost:60877`, which is typically the web server run by VS Code. For more details, refer to the [Debug documentation](Debug.md).

- **`--csdebug`**: Starts a debug session using the CLR engine, allowing you to debug the Plang source code. Ensure your project is set up in Visual Studio. For more information, see the [Debug documentation](Debug.md).

- **`--detailerror`**: By default, the runtime displays limited error information. Use this parameter to obtain more detailed error messages when running from the console.

- **`--llmservice`**: Specifies the language model service to use, either `plang` or `openai`. The default is `plang`. If you have an OpenAI key, you can use this parameter. For further details, see the [Plang or OpenAI documentation](PlangOrOpenAI.md).

- **`--version`**: Displays the version of Plang you are running. No Plang code is executed with this parameter.

- **`--logger`**: Sets the logging level. Options include `error`, `warning`, `info`, `debug`, and `trace`. The default is `warning` at runtime and `information` at builder runtime.

- **`--strictbuild`**: Ensures that every line number in goal files matches exactly. If they do not match, the step is rebuilt.

## Summary

This guide provides you with the necessary commands and parameters to effectively run and build your Plang projects. Whether you are debugging, building, or running specific files, the above parameters will help you tailor the Plang environment to your needs.