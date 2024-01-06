# Rules

Welcome to the Rules guide for plang, the programming language designed for creating goal-oriented scripts. This guide will help you understand the syntax and structure of plang so that you can start writing your own `.goal` files effectively.

## Getting Started with plang

Before you dive into writing plang code, it's important to understand the basic components of a plang script, also known as a goal file.

### Goal Files

A goal file is a text file with a `.goal` extension. It contains all the instructions needed to achieve a specific task or goal. Think of it as a function in traditional programming languages.

### Goal Name

The goal name is the identifier for your goal file. It should be placed at the beginning of the file and serves as the entry point for the execution of your script.

### Steps

Steps are the individual instructions within your goal file. Each step starts with a dash (`-`) and represents an action to be taken. If a step spans multiple lines, subsequent lines should not start with a dash.

### Variables

Variables in plang are placeholders for data and are denoted by a percentage sign (`%`) at the beginning and end of the variable name.

### Comments

Comments are non-executable lines that help explain the code. In plang, a comment starts with a dash followed by a forward slash (`-/`).

### Conditional Statements

plang supports conditional statements (`if` statements) that can execute steps based on certain conditions. The steps within an `if` statement are indented by either four spaces or a tab to denote that they are part of the conditional block.

## Example Structure of a plang Goal File

Here's a basic example of what a plang goal file might look like:

```plang
MyApp
- if %user.isAdmin% is logged in then
    - write out 'Admin logged in'
- Retrieve the list of todos for %todos% table
- go through %todos%, call !ProcessTodo
/ This is a comment explaining the next steps
- get https://example.org, write to %content%
- write out %content%
```

## Conclusion

With this guide, you should now have a basic understanding of how to write a plang goal file. Remember to follow the syntax rules closely, and use comments to keep your code understandable. Happy coding!