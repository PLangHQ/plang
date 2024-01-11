# Rules of plang

Welcome to the plang programming language documentation. This guide will provide you with the necessary information to understand and apply the rules of plang effectively. plang is designed to be intuitive and flexible, allowing developers to write code in a natural language-like syntax. Below are the foundational rules that govern the structure and execution of plang code.

## Goal Files

- **File Extension**: A plang goal file must have a `.goal` extension.
- **Goal Name**: The goal name should be declared at the beginning of the file. It acts as the entry point, similar to a function in traditional programming languages.
- **Multiple Goals**: A single `.goal` file can contain more than one goal. The first goal is public and can be initiated by a web server, while subsequent goals are private and cannot be web-initiated.

## Steps

- **Definition**: A step is defined as a line starting with a dash (`-`). It represents an action or a set of actions to be performed.
- **Multi-line Steps**: Steps can span multiple lines. In such cases, only the first line starts with a dash, and subsequent lines are indented for clarity.
- **Natural Language**: Steps should be described in simple terms, focusing on the intent of the action.

## Variables

- **Syntax**: Variables are enclosed within percentage signs (`%`). For example, `%username%`.

## Comments

- **Syntax**: A line starting with forward slash (`/`) is considered a comment and will not be executed.

## Conditional Logic

- **Indentation**: An `if` statement can include indented steps to define conditional actions. These should be indented by 4 spaces or a tab.

## Modules

- **Syntax**: To specify a module for a step, use square brackets with the module name (e.g., `[database]`).

## Example plang Code

Here is an example of a plang goal file named `MyApp.goal`:

```plang
MyApp
- if %user.isAdmin% is logged in then
    - write out 'Admin logged in'
- Retrieve the list of todos from %todos% table
    cache for 3 minutes
- Iterate through %todos%, calling !ProcessTodo for each item
/ This is a comment explaining the next steps
- Fetch data from https://example.org
    Bearer %Settings.ApiKey%
    {
        data: "some text"
    }
    write to %content%
- write out %content%
- [code] Generate a list of all 2-letter ISO country codes and store in %countryCodes%
```

## Writing Steps in Natural Language

plang allows you to write steps in various natural language forms, as long as the intent is clear. Here are multiple ways to perform the same action:

```plang
- read text file.txt into %content%
- file.txt should be read into %content%
- load file.txt and put it into %content%
```

Each of the above steps instructs plang to read the contents of `file.txt` and store it in the variable `%content%`.

## Conclusion

This document has outlined the core rules for writing plang code. 

By following these guidelines, developers can create `.goal` files that are both efficient and easy to understand. Remember that plang prioritizes the intent of your steps, and its natural language flexibility allows for various expressions of that intent.