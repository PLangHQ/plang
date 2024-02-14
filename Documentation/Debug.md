# Debugging with plang

Welcome to the plang debugging guide! In this document, we'll cover the essentials of debugging your plang programs, as well as the generated C# code. Let's get started by setting up your environment and then dive into the debugging process.

> **Warning**: Running your code in debug mode can slow down execution by approximately 10 times.

## Environment Setup

To ensure a smooth debugging experience, you'll need to have a few tools and extensions in place:

- **Visual Studio Code**: Make sure you've followed the [IDE setup guide](./IDE.md) to install and configure Visual Studio Code for plang development.
- **plang Extension for VS Code**: Enhance your development workflow by installing the plang extension from the Visual Studio Code marketplace. This extension provides valuable features such as debugging support.

## Debugging Steps in Visual Studio Code

### Initiating the Debugger

To start the debugger within Visual Studio Code, you can either press `F5` or navigate to the menu and select `Run -> Start Debugging`.

### Adding Breakpoints

Breakpoints allow you to halt the execution of your program at specific points. You can add them by clicking on the left margin next to the line numbers within your code.

### Stepping Through the Code

To step through your code line by line and examine the execution flow, use the `F10` key. This will help you understand how your program operates and identify any issues.

## Debug Mode in plang

To enable debug mode in plang, append the `--debug` flag to your terminal command like so:

```bash
plang exec --debug
```

## Debugging Generated C# Code & Plang source code

If you need to debug the C# code that plang generates or you want to debug the plang source code, here's what you should do:

### Environment Setup
- **Visual Studio Community Edition**: For those who need to debug the generated C# code, you can download and install the free Visual Studio Community Edition from [this link](https://visualstudio.microsoft.com/vs/community/).

### Debugging C#
1. Enable C# debugging by adding the `--csdebug` flag to your terminal command.
2. Download the plang project from the official GitHub repository at [PLangHQ/plang](https://github.com/PLangHQ/plang/).
3. With Visual Studio installed, you can proceed to debug the generated C# code:
   - Enter debug mode and press `Ctrl+Alt+U`, or go to `Debug -> Windows -> Modules` from the menu.
   - Look for the module named in the format `01. NameOfModule.dll`.
   - Right-click on the module and select "Extract code" to view and debug the C# code.

## Debugger Workflow Explained

When you use the `--debug` parameter, an `Events` folder is created within your project. This folder contains bindings that are executed before each step in your goal files, triggering the `!SendDebug.goal`. 

The `SendDebug` goal sends a POST request to `http://localhost:60877/`, which corresponds to a server initiated by the plang extension in Visual Studio Code. The extension then receives the debugging data and displays it within the editor, allowing you to track and inspect your program's execution in real-time.

By adhering to these steps and understanding the debugger workflow, you'll be well-equipped to debug your plang programs and the C# code they generate. Happy debugging!