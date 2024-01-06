# plang Programming Language Documentation

Welcome to the plang programming language documentation. This guide is designed to help you understand the fundamental rules and syntax of plang, a language tailored for defining goals and automating tasks. Whether you're a beginner or an experienced developer, this documentation will provide you with the necessary information to write plang code effectively.

## Introduction to plang

plang is a versatile language that runs on Windows, Linux, and macOS. It is used to create `.goal` files, which are essentially scripts that automate a series of steps, similar to functions in traditional programming languages.

### Key Concepts

- **Goal File**: A plang script is saved in a text file with a `.goal` extension.
- **Goal Name**: The name of the goal is declared at the beginning of the file and serves as the entry point for the script.
- **Steps**: Each action in plang is defined as a step, which starts with a dash (`-`). Steps can span multiple lines if subsequent lines do not start with a dash.
- **Variables**: Variables in plang are enclosed within percentage signs (`%`), e.g., `%username%`.
- **Comments**: Comments are steps that start with a dash followed by a forward slash (`-/`) and are ignored during execution.
- **Conditional Statements**: `if` statements can include indented steps, which should be indented by either 4 spaces or a tab.
- **Modules**: Specific modules can be targeted using square brackets (`[module]`) to focus on building a particular step.

## plang Syntax

Below is an example of a plang goal file named `MyApp.goal`:

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

### Detailed Explanation

1. **Goal Name**: `MyApp` is the name of the goal and the entry point of the script.
2. **Conditional Step**: The `if` statement checks if `%user.isAdmin%` is logged in and then executes the indented step to write out 'Admin logged in'.
3. **Retrieval Step**: Retrieves a list of todos from the `%todos%` table.
4. **Iteration Step**: Goes through each item in `%todos%` and calls the `!ProcessTodo` function for each one.
5. **Comment**: `/ This is a comment...` is a comment that explains the purpose of the following steps.
6. **HTTP Request Step**: Fetches content from `https://example.org` and stores it in the `%content%` variable.
7. **Output Step**: Writes out the content of the `%content%` variable.

### Writing plang Code

When writing plang code, follow these guidelines:

- Start your `.goal` file with the goal name at the top.
- Define each step with a leading dash (`-`).
- Use variables to store and manipulate data within your goal.
- Comment your code to explain complex steps or logic.
- Use indentation for steps within conditional statements.
- Utilize modules to enhance the functionality of your steps.

### OS-Specific Examples

When providing examples that depend on the operating system, ensure that you include instructions for Windows, Linux, and macOS. For instance, if you need to execute a command that varies by OS, provide the equivalent command for each one.

## Conclusion

This documentation has outlined the basic structure and rules of the plang programming language. By following the guidelines provided, you should be able to create your own `.goal` files and automate tasks using plang. Remember to write clear and concise code, and make use of comments to maintain readability. Happy coding!