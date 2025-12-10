
# Conditional

## Introduction
Conditionals are a fundamental concept in programming, allowing the flow of execution to change based on certain conditions. They are the decision-making backbone of a program, enabling it to react differently under varying circumstances.

## For Beginners
A conditional in programming is like a crossroad with signposts that guide you where to go based on your destination. In PLang, conditionals allow your program to make decisions. Think of it as asking a yes-or-no question, and depending on the answer, the program takes a different action. For example, if it's raining, you might choose to take an umbrella. In PLang, you can write a rule that checks if it's raining and then decides to remind you to take an umbrella.

## Best Practices for Conditional
When using conditionals, it's important to ensure that the conditions are clear and cover all possible scenarios. Avoid overly complex conditions that are hard to read and maintain. Always consider the 'else' case – what should your program do if none of the conditions are met?

### Example
Let's say you want to write a PLang program that gives advice on what to wear based on the weather:

```plang
- set var 'Weather' as 'sunny'
- if %Weather% equals 'rainy' then
    - write out 'Wear a raincoat'
```

In this example, the program checks if the weather is rainy. If it is, it advises wearing a raincoat. Otherwise, it suggests enjoying the sunshine.


# ConditionalModule Documentation

The `ConditionalModule` is a part of the PLang programming language that manages if conditions within user requests. It allows for the evaluation of boolean expressions and the execution of code based on the result of these conditions. Below are examples of how to use the `ConditionalModule` in PLang, sorted by their popularity and typical use cases.

## Examples

### Checking if a Variable is True
```plang
- set var 'Valid' as true
- if %Valid% is true then
    - write out 'It is valid'
```

### Checking if a Variable is False
```plang
- set var 'NotValid' as false
- if %NotValid% is false then
    - write out 'Not valid variable is set to false'
```

### Checking if a Variable is Empty
```plang
- set var 'Address' as ''
- if %Address% is empty then
    - write out 'Address is empty'
```

### Checking if a File Exists
```plang
- set var 'FilePath' as 'path/to/file.txt'
- if file at %FilePath% exists then
    - write out 'File exists'
```

### Checking if a Directory Exists
```plang
- set var 'DirectoryPath' as 'path/to/directory'
- if directory at %DirectoryPath% exists then
    - write out 'Directory exists'
```

### Checking if a Variable Equals a Specific Value
```plang
- set var 'StatusCode' as 200
- if %StatusCode% equals 200 then
    - write out 'Status code is OK'
```

### Checking if a Variable is Greater Than a Value
```plang
- set var 'DownloadSize' as 5mb
- if %DownloadSize% is greater than 4mb then
    - write out 'Download size exceeds limit'
```

### Checking if a Variable is Less Than a Value
```plang
- set var 'RemainingSpace' as 500mb
- if %RemainingSpace% is less than 1gb then
    - write out 'Not enough space remaining'
```

### Checking if a Variable is Within a Date Range
```plang
- set var 'EventDate' as '2023-05-01'
- if %EventDate% is between '2023-04-01' and '2023-06-01' then
    - write out 'Event is within the date range'
```

### Checking if a Variable is Before a Specific Date
```plang
- set var 'SubscriptionEnd' as '2023-12-31'
- if %SubscriptionEnd% is before %Now% then
    - write out 'Subscription has expired'
```

### Checking if a Variable is After a Specific Date
```plang
- set var 'NextMeeting' as '2023-04-15'
- if %NextMeeting% is after %Now% then
    - write out 'Next meeting is scheduled for the future'
```

These examples demonstrate the versatility of the `ConditionalModule` in handling various types of conditions. By using these patterns, developers can create robust and dynamic PLang applications that respond to different scenarios effectively.


## Examples
For a full list of conditional examples, visit [PLang Conditional Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Conditional).

## Step Options
Each step in PLang can be enhanced with additional options for more robust behavior. Click the links below for more details on how to use each option:

- [CacheHandler](/modules/cacheHandler.md)
- [ErrorHandler](/modules/ErrorHandler.md)
- [RetryHandler](/modules/RetryHandler.md)
- [CancellationHandler](/modules/CancelationHandler.md)
- [Run and Forget](/modules/RunAndForget.md)

## Advanced
For more advanced information on conditionals and how they map to underlying C# logic, refer to the [Advanced Conditional Module Documentation](./PLang.Modules.ConditionalModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:39:49.
