# Rules of Plang

Welcome to the documentation for the Plang programming language. This guide will help you understand the fundamental rules and syntax of Plang, enabling you to write effective and efficient Plang code. Plang is designed to be intuitive, allowing you to express your intentions clearly and concisely.

## Basic Structure

### Goal Files
- **File Extension**: Plang code is written in text files with the `.goal` extension.
- **Goal Name**: Each file begins with a goal name, which functions similarly to a function in other programming languages.
- **Multiple Goals**: A single goal file can contain multiple goals. The first goal is publicly visible, while subsequent goals are private.
- **Visibility**: Private goals cannot be initiated by a web server.

### Steps
- **Step Definition**: Each step in a goal is a line starting with a dash (`-`). Steps can span multiple lines, provided that continuation lines are indented.
- **Intent Description**: Steps should clearly describe the intended action in simple terms.
- **Comments**: Lines starting with a slash (`/`) are comments, e.g., `/ this is a comment`.

### Variables
- **Syntax**: Variables are enclosed in percentage signs (`%`), e.g., `%name%`, `%user.email%`. For more details, refer to the [Variables documentation](./Variables.md).

### Conditional Logic
- **If Statements**: Conditional steps can be indented by four spaces or a tab. Note that steps cannot start with `- else` or `- else if`. Instead, use goal calls for conditional logic. For more information, see the [Conditions documentation](./Conditions.md).

### Goal Calls
- **Calling Goals**: You can call other goals using the syntax `- call goal XXX`.

### Loops
- **Looping Through Items**: Developers can loop through items and call goals using the syntax `- go through %list%, call ProcessItem`. For more details, refer to the [Loop documentation](./Loop.md).

### Time Access
- **Current Date & Time**: Use `%Now%` and `%NowUtc%` to access the current date and time. More information is available in the [Time documentation](./Time.md).

### Modules
- **Module Focus**: Use square brackets (`[...]`) to specify a module for a step, e.g., `[llm]` uses the LlmModule.

### Escape Characters
- **Escaping**: Use the backslash (`\`) as an escape character for `%`, e.g., `\%ThisWillNotBeVariable\%`.

## Example of Plang Source Code

Below is an example of a Plang source code file, demonstrating various features and syntax:

```plang
MyApp
- if %user.isAdmin% is logged in then
    - write out 'Admin logged in'
- Retrieve the list of todos for %todos% database table
    cache for 3 minutes
- go through %todos%, call !ProcessTodo
/ This is a comment explaining the next steps
- get https://example.org
    Bearer %Settings.ApiKey%
    {
        data: "some text"
    }
    write to %content%
- write out %content%
- [code] create list of all 2 letter ISO country codes, write to %countryCodes%
- call goal WriteHello

WriteHello
- write out "Hello plang world"
```

### Explanation
- The code begins with a goal named `MyApp`.
- It checks if a user is an admin and logs a message if true.
- It retrieves a list of todos, caches it, and processes each item.
- A comment is included to explain the subsequent steps.
- It makes an HTTP GET request and writes the response to a variable.
- It demonstrates the use of modules and goal calls.

## Writing Steps in Natural Language

Plang allows you to express steps in multiple ways, as long as the intent remains clear. Here are different ways to achieve the same result:

```plang
- read text file.txt into %content%
- file.txt should be read into %content%
- load file.txt and put it into %content%
```

Each of these steps reads a file and loads its content into a variable, demonstrating Plang's flexibility in expressing actions.

By following these guidelines, you can effectively utilize Plang to create clear and maintainable code. Happy coding!