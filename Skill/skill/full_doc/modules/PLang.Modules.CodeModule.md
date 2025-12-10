
# Code
## Introduction
The Code module in PLang is a versatile tool designed to bridge the gap between high-level goal descriptions and the generation of executable C# code. It allows users to articulate their programming needs in a simplified manner, which the PLang interpreter then translates into functional code snippets.

## For Beginners
If you're new to programming, think of Code as a friendly translator. You tell it what you want to do in simple terms, and it turns those instructions into a language that computers understand. Code in PLang helps you perform tasks without needing to know the intricacies of programming languages like C#.

## Best Practices for Code
When writing PLang code, clarity and simplicity are key. Always start with a clear goal, break down your tasks into small steps, and use descriptive variable names. Here's an example to illustrate:

```plang
CreateGreetingCard
- set %occasion% as 'Birthday'
- set %recipientName% as 'John'
- [code] generate greeting for %occasion% to %recipientName%, write to %greetingMessage%
- write out %greetingMessage%
```

In this example, we've defined a goal to create a greeting card. We've set two variables, `%occasion%` and `%recipientName%`, and used a code action to generate a greeting message. Finally, we output the message. This approach keeps your code readable and maintainable.

## Examples

# PLang Code Module Documentation

The Code Module in PLang is designed to generate C# code based on user descriptions. It should be used when no other module fits the requirements. Below are examples of how to use the Code Module in PLang, sorted by their expected popularity based on common coding tasks.

## Examples

### Getting Parts of a Name

```plang
Code
- set %name% as 'Toby Flenderson'
- [code] get first name of %name%, write to %firstName%
- write out %firstName%
- [code] get last name of %name%, write to %lastName%
- write out %lastName%
```

### Converting to Uppercase

```plang
Code
- set %name% as 'Toby Flenderson'
- [code] uppercase %name%, write to %uppercaseName%
- write out %uppercaseName%
```

### Generating Random Data List

```plang
Code
- [code] create string of list with 10 rows of random data, write to %randomDataList%
- write out %randomDataList%
```

### Removing File Extension

```plang
Code
- set %fileNameWithExtension% as 'video.mp4'
- [code] remove file extension from %fileNameWithExtension%, write to %fileName%
- write out %fileName%
```

### Additional Examples

#### Formatting File Sizes

```plang
Code
- set %fileSizeBytes% as 3145728
- [code] format %fileSizeBytes% as 'mb', write to %fileSizeReadable%
- write out %fileSizeReadable%
```

#### Setting Expiration

```plang
Code
- set %duration% as 1209600
- [code] format %duration% as 'expires in 2 weeks', write to %expiration%
- write out %expiration%
```

Please note that the examples provided are for illustration purposes and may not directly correspond to actual methods in the CodeModule class. The examples are written in natural language to demonstrate how the PLang Code Module can be used to perform common coding tasks.


For a full list of examples, visit [PLang Code Examples on GitHub](https://github.com/PLangHQ/plang/tree/main/Tests/Code).

## Step Options
Each step in your PLang code can be enhanced with additional options for robustness and functionality. Click the links below for more details on how to use each option:

- [CacheHandler](/modules/cacheHandler.md)
- [ErrorHandler](/modules/ErrorHandler.md)
- [RetryHandler](/modules/RetryHandler.md)
- [CancellationHandler](/modules/CancelationHandler.md)
- [Run and Forget](/modules/RunAndForget.md)

## Advanced
For those who wish to delve deeper into the mechanics of PLang and its interaction with C#, refer to the [advanced documentation](./PLang.Modules.CodeModule_advanced.md).

## Created
This documentation was created on 2024-01-02T21:34:52.
