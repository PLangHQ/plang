
# LocalOrGlobalVariable

## Introduction
The `LocalOrGlobalVariable` module is a fundamental part of the PLang programming language, designed to manage and manipulate variables within your code. Variables are essential for storing data that can be referenced and altered throughout the execution of a program.

## For Beginners
In programming, a variable is like a storage box where you can keep pieces of information. You can put something into the box, take it out, change it, or even throw it away. In PLang, `LocalOrGlobalVariable` helps you manage these boxes. Local variables are like your personal boxes that only you can use, while global (or static) variables are like community boxes that everyone can share.

## Best Practices for LocalOrGlobalVariable
When using `LocalOrGlobalVariable`, it's important to:

- Use meaningful names for your variables so you can easily remember what information they hold.
- Keep track of your variables to avoid using the same name for different things.
- Decide whether a variable should be local or static based on whether its information is meant to be shared across different parts of your program.

### Example
Let's say you're writing a program to greet users. You might have a local variable to store a user's name and a static variable to store the greeting message that everyone sees.

```plang
- set var 'username' to 'Alice'
- set static var 'greeting' to 'Welcome to PLang!'
- get static var 'greeting', write to %greetMsg%
- append %greetMsg% to var 'username', with separator ' '
- write out %username%
```

This code sets a local variable `username` to "Alice" and a static variable `greeting` to "Welcome to PLang!". It then appends the greeting to the username and outputs "Welcome to PLang! Alice".


# LocalOrGlobalVariable Module Documentation

The `LocalOrGlobalVariable` module in PLang provides functionality for setting and getting local and static variables, as well as binding events to variable lifecycle changes such as creation, change, and removal.

## Examples

### Set a Local Variable
To set a local variable, use the `SetVariable` or `SetStringVariable` method. This is commonly used to store data that will be used later in the program.

```plang
- set var 'username' to 'JohnDoe'
```

### Get a Local Variable
To retrieve the value of a local variable, use the `GetVariable` method. This is useful when you need to access data that has been stored previously.

```plang
- get var 'username', write to %userName%
- write out %userName%
```

### Set a Static Variable
Static variables are shared across all instances of the module. To set a static variable, use the `SetStaticVariable` method.

```plang
- set static var 'appVersion' to '1.0.0'
```

### Get a Static Variable
To retrieve the value of a static variable, use the `GetStaticVariable` method.

```plang
- get static var 'appVersion', write to %version%
- write out %version%
```

### Remove a Variable
To remove a variable, use the `RemoveVariable` or `RemoveStaticVariable` method. This is used when the data is no longer needed.

```plang
- remove 'username' var
```

### On Variable Creation
To execute a goal when a variable is created, use the `OnCreateVariableListener` method.

```plang
- when var 'newUser' is created, call !UserCreated
```

### On Variable Change
To execute a goal when a variable changes, use the `OnChangeVariableListener` method.

```plang
- when var 'theme' changes, call !ThemeChanged
```

### On Variable Removal
To execute a goal when a variable is removed, use the `OnRemoveVariableListener` method.

```plang
- on remove on var 'sessionToken' call !SessionEnded
```

### Set Default Values on Variables
To initialize multiple variables with default values, use the `SetDefaultValueOnVariables` method.

```plang
- set default values on vars: 'language' to 'English', 'timezone' to 'UTC'
```

### Append to a Variable
To append a value to an existing variable, use the `AppendToVariable` method. This is often used for building up strings or accumulating data.

```plang
- append 'Welcome ' to var 'greeting'
- append 'John!' to var 'greeting', with separator ' '
```

### Convert Variable to Base64
To convert the value of a variable to Base64, use the `ConvertToBase64` method.

```plang
- set var 'password' to 'myP@ssw0rd'
- convert 'password' to base64, write to %passwordBase64%
- write out %passwordBase64%
```

### Load Variables
To load variables from a storage or previous state, use the `LoadVariables` method.

```plang
- load vars with key 'sessionData', write to %sessionVars%
```

## Notes
- The examples provided are in the order of their expected popularity based on common programming tasks.
- When creating examples, parameters are described in natural language for better readability.
- To avoid redundancy, no method is called more than twice, and examples with parameters are provided.
- If a method returns a value, the result is written to a variable which can be used in subsequent steps.


## Examples
For a full list of examples, visit [PLang LocalOrGlobalVariable Examples](https://github.com/PLangHQ/plang/tree/main/Tests/LocalOrGlobalVariable).

## Step Options
When writing your PLang code, you can enhance your steps with additional functionalities. Click on the links below for more details on how to use each option:

- [CacheHandler](/modules/handlers/CachingHandler.md)
- [ErrorHandler](/modules/handlers/ErrorHandler.md)
- [RetryHandler](/modules/handlers/RetryHandler.md)



## Advanced
For more advanced information on `LocalOrGlobalVariable`, including how it interfaces with C#, check out the [advanced documentation](./PLang.Modules.LocalOrGlobalVariableModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:59:00.
