
# Output

## Introduction
The Output module in PLang is a crucial component that allows programs to communicate with users. It provides the functionality to display messages, request input, and show results. Understanding how to use the Output module effectively is key to creating interactive and user-friendly applications.

## For Beginners
If you're new to programming, think of the Output module as your program's way of talking to the world. It's how your code can send messages to the screen, ask questions, and get responses from a user. It's like having a conversation where your program can say "Hello," ask "What's your name?" and remember the answer for later use.

## Best Practices for Output
When using the Output module, it's important to keep your messages clear and concise. Users should easily understand what information is being displayed or what is being asked of them. Always ensure that variable content is properly formatted and that any user input is validated to prevent errors.

### Example:
```plang
Output
- write out 'Welcome to PLang! Please enter your name:'
- ask 'Name:', write to %userName%
- write out 'Hello, %userName%! Nice to meet you.'
```
In this example, we first output a welcome message, then ask for the user's name and store it in a variable called `%userName%`. Finally, we use the stored name to greet the user personally.


# Output Module Documentation

The Output module in PLang is designed to interact with the user interface, such as a console, by outputting text or variables and by asking the user for input. Below are examples of how to use the Output module in PLang, sorted by the most common usage scenarios.

## 1. Writing Output to the Console

The `Write` method is used to output content to the UI. It can output text, variables, and even write to a buffer if needed.

### Example 1: Simple Text Output

```plang
Output
- write out 'Hello PLang world'
```

### Example 2: Writing with Buffer

```plang
Output
- write out '{', use buffer
- write out '}'
```

### Example 3: Writing a Variable

```plang
Output
- read file.txt, write to %content%
- write out %content%
```

## 2. Asking for User Input

The `Ask` method is used to prompt the user for input. The method can specify the type of input expected and can also provide a status code.

### Example 1: Asking for Text Input

```plang
Output
- ask 'What is your name?', write to %userName%
```

### Example 2: Asking for Numeric Input

```plang
Output
- ask 'Enter your age:', type 'number', write to %userAge%
```

## Additional Examples

### Example: Writing a Variable with a Specific Type

```plang
Output
- calculate size in mb, write to %fileSize%
- write out 'The file size is %fileSize% MB', type 'info'
```

### Example: Asking for Input with a Custom Status Code

```plang
Output
- ask 'Do you agree to the terms and conditions? (yes/no)', type 'confirm', status code 400, write to %userConsent%
```

Note: When creating examples for methods that were not provided, natural language has been used to ensure the compiler can map them with module methods. Large numbers are represented in a human-readable form, and parameters are described in natural language for clarity.


For a full list of examples, visit [PLang Output Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Output).

## Step Options
When writing your PLang code, you can enhance the functionality of each step with these options. Click on the links below for more detailed information on how to use them:

- [CacheHandler](/modules/cacheHandler.md)
- [ErrorHandler](/modules/ErrorHandler.md)
- [RetryHandler](/modules/RetryHandler.md)
- [CancellationHandler](/modules/CancelationHandler.md)
- [Run and Forget](/modules/RunAndForget.md)

## Advanced
For those who are ready to dive deeper into the Output module and understand how it maps to underlying C# code, check out the [advanced documentation](./PLang.Modules.OutputModule_advanced.md).

## Created
This documentation was created on 2024-01-02T22:11:49.
