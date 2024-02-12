
# Output
## Introduction
Output in plang is a fundamental concept that allows your program to communicate with the end-user by displaying text or other information on the screen.

## For beginners
Output is the way your program talks back to you. When you ask a question or give a command, the output is how the program responds, showing you messages, errors, or any other kind of information.

## Best Practices for Output
When using output in plang, it's important to follow certain best practices to ensure clear communication with the end-user. For instance, always use meaningful messages and consider the status codes to indicate the success or failure of an operation.

### Example:
```plang
- write out 'Processing your request...'
- if %success% then
  / status code will be 200, status text='text'
  - write out 'Operation completed successfully'
- if %success% is false, then
  / status code will be 400, status text='error'
  - write out error 'Operation had error'
```

The output module might handle each status code diffrently.

## Examples
For practical examples of how to use the Output module in plang, please refer to the source code of the module located at [Output Module Source](https://github.com/PLangHQ/plang/blob/main/PLang/Modules/Output/Program.cs).



### Example 1: Simple Text Output
```plang
- write out 'This is a text shown to user'
```

### Example 2: Buffered Output
```plang
- write out '{', use buffer
- write out '"name":"John"', use buffer
- write out '}', use buffer
```

## Ask Method Examples

### Example 1: Asking for a Number
```plang
- ask 'How old are you?', should be number, write to %age%
```

### Example 2: Asking for Non-empty Input
```plang
- ask 'What is your name?', cannot be empty, write to %name%
```

### Example 3: Asking for a Valid URL
```plang
- ask 'Type in a URL', must be valid URL, write to %url%
```

## Additional Examples

### Example 1: Asking with a Regular Expression Pattern
```plang
- ask 'Enter your email', must match pattern '\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b', write to %email%
```

### Example 2: Writing Out a Variable
```plang
- ask 'What is your favorite color?', cannot be empty, write to %favoriteColor%
- write out 'Your favorite color is %favoriteColor%'
```

### Example 3: Writing Out with a Specific Status Code
```plang
- write out 'Operation completed successfully', with status code 200
```


For a comprehensive list of examples, visit the [Output Examples Repository](https://github.com/PLangHQ/plang/tree/main/Tests/Output).

## Step options
Each step in plang can be enhanced with additional options for more complex scenarios. Click on the links below for detailed usage instructions:
- [CacheHandler](/moduels/cacheHandler.md)
- [ErrorHandler](/moduels/ErrorHandler.md)
- [RetryHandler](/moduels/RetryHandler.md)
- [CancelationHandler](/moduels/CancelationHandler.md)
- [Run and forget](/moduels/RunAndForget.md)

## Advanced
For developers seeking a deeper understanding of the Output module's integration with C#, please refer to the [advanced documentation](./PLang.Modules.OutputModule_advanced.md).

## Created
This documentation was created on 2024-02-10T14:26:32.
