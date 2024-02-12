
# Code
## Introduction
Code is a powerful module within the plang programming language designed to facilitate the automatic generation of code snippets based on natural language instructions. It leverages the capabilities of language models to interpret user requests and produce secure, validated code.

## For beginners
Code is like a smart assistant that helps you write programming code. You tell it in simple words what you want to do, and it writes the code for you. It's designed to be easy for people who are new to programming and might not know all the technical terms yet.

## Best Practices for Code
When writing plang code, it's important to follow certain best practices to ensure your code is clear and effective:
- Always use `[code]` at the beginning of your statements to signal the use of the code module.
- Be precise in your instructions to avoid ambiguity.
- Validate the output of the code module to ensure it meets your expectations.

Here's an example to illustrate:
```plang
- set %fileName% as 'report.pdf'
- [code] extract file name without extension from %fileName%, write to %nameOnly%
- write out %nameOnly%
```
In this example, we're telling the code module to remove the file extension from 'report.pdf' and then output the result.

## Examples

Following are examples of how to use the Code module. These are very limited examples. If you can describe your intent, the Code module should be able to generate the code. Just make sure to validate the code generated.

### Get First Name from Full Name
```plang
- set %name% as 'Toby Flenderson'
- [code] get first name of %name%, write to %firstName%
- write out %firstName%
```

### Get Last Name from Full Name
```plang
- set %name% as 'Toby Flenderson'
- [code] get last name of %name%, write to %lastName%
- write out %lastName%
```

### Convert Name to Uppercase
```plang
- set %name% as 'Toby Flenderson'
- [code] uppercase %name%, write to %uppercaseName%
- write out %uppercaseName%
```

### Create a String of a List with Random Data
```plang
- [code] create string of list with 10 rows of random list data, write to %list%
- write out %list.ToJson()%
```

### Remove File Extension from Filename
```plang
- set %fileNameWithExtension% as 'video.mp4'
- [code] remove file extension from %fileNameWithExtension%, write to %fileName%
- write out %fileName%
```

#### Create a New GUID
```plang
- [code] create a new GUID, write to %guid%
- write out %guid%
```

#### Format Bytes to Human Readable Form
```plang
- set %fileSizeBytes% as 10485760 /10MB/
- [code] format %fileSizeBytes% to human readable form, write to %readableSize%
- write out %readableSize%
```


For a comprehensive list of examples, visit [PLang Code Examples](https://github.com/PLangHQ/plang/tree/main/Tests/Code).

## Step options
Each step in your plang code can be enhanced with additional options for handling various scenarios:
- [CacheHandler](/moduels/cacheHandler.md) - Manages caching of data for faster access.
- [ErrorHandler](/moduels/ErrorHandler.md) - Handles errors that occur during code execution.
- [RetryHandler](/moduels/RetryHandler.md) - Attempts to retry a step if it fails initially.
- [CancelationHandler](/moduels/CancelationHandler.md) - Manages the cancellation of long-running steps.
- [Run and forget](/moduels/RunAndForget.md) - Executes a step without waiting for its completion.

## Advanced
For those who want to delve deeper into the Code module and understand how it maps to underlying C# code, please refer to the [advanced documentation](./PLang.Modules.CodeModule_advanced.md).

## Created
This documentation was created on 2024-02-10T13:50:51.
