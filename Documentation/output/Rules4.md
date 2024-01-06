# plang Programming Language Guide

Welcome to the plang programming language guide. This document is designed to provide you with a comprehensive understanding of the rules and syntax for writing plang code. plang is a powerful language for automating tasks and defining goals, and it is compatible with Windows, Linux, and macOS.

## Getting Started with plang

A plang script is a text file with a `.goal` extension that contains a series of steps to achieve a specific goal. Here's how to structure your plang code:

### Goal Definition

- **Goal Name**: The goal name is the first line in your `.goal` file and acts as the entry point, similar to a function in other programming languages.

### Steps

- **Basic Step**: Each step is a line of code that begins with a dash (`-`). If a step spans multiple lines, subsequent lines should not start with a dash.
- **Comments**: To add a comment, start the line with a dash followed by a forward slash (`-/`). Comments are not executed.
- **Variables**: Variables are placeholders for data and are wrapped in percentage signs (`%`), like `%variableName%`.
- **Conditional Steps**: Use an `if` statement to perform conditional logic. Indent the steps within the `if` block by 4 spaces or a tab.
- **Modules**: To target a specific module, use square brackets with the module name, such as `[moduleName]`.

## plang Syntax Examples

Here is a basic example of a plang goal file named `Example.goal`:

```plang
Example
- if %user.isAdmin% is logged in then
    - write out 'Admin logged in'
- Retrieve the list of todos from %todos% table
- Iterate through %todos%, calling !ProcessTodo for each item
-/ This is a comment explaining the next steps
- Fetch data from https://example.org and store in %content%
- Display the contents of %content%
```

### Writing Steps in Natural Language

plang allows you to write steps in a natural language style, focusing on the intent rather than strict syntax. Here are multiple ways to perform the same action:

```plang
- read text file.txt into %content%
- file.txt should be read into %content%
- load file.txt and put it into %content%
```

All of the above steps will read the contents of `file.txt` and store it in the `%content%` variable.

## Rules of plang

To ensure clarity and consistency in your plang scripts, adhere to the following rules:

- **File Extension**: The goal file must end with the `.goal` extension.
- **Goal Name**: The goal name should be at the start of the file.
- **Step Syntax**: A step begins with a dash (`-`) and can continue on multiple lines without a leading dash.
- **Variable Syntax**: Variables are enclosed in percentage signs (`%`).
- **Comment Syntax**: A comment starts with a dash followed by a forward slash (`-/`).
- **Conditional Indentation**: Indented steps within an `if` statement must be indented by 4 characters or a tab.
- **Module Targeting**: Use square brackets (`[moduleName]`) to specify a module for a step.

## Conclusion

This guide has introduced you to the fundamental rules and syntax of the plang programming language. By following these guidelines, you can create effective and efficient `.goal` files to automate your tasks. Remember to write steps in a way that clearly conveys their intent and to utilize comments for better understanding. Happy coding with plang!