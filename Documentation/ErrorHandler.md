# ErrorHandler Documentation

## Overview

ErrorHandler in plang is a powerful feature that allows developers to manage and handle errors effectively within their plang scripts. This document provides a comprehensive guide on how to utilize ErrorHandler to improve the robustness and reliability of your plang applications.

## Basic Usage

ErrorHandler enables you to define specific actions based on the type of error encountered. Below is a simple example to illustrate its usage:

```plang
Start
- get http://example.org
    on error call ManageError

ManageError
- log error %!error%
- throw 'Error happened'
```

In this example, if an error occurs during the `get` operation, the `ManageError` goal is invoked. This goal logs the error and throws an exception to halt further execution.

## ErrorHandler Properties

ErrorHandler supports various properties to customize the error handling behavior:

- **IgnoreError**: By default, this is set to `false`. If set to `true`, the error is caught but the execution continues to the next step.
  - Example: `on error 'element not found', ignore`
- **Message**: Checks if the specified text is present in the error message (case insensitive).
  - Example: `on error 'timeout', call HandleTimeout`
- **StatusCode**: Similar to HTTP status codes, where 400 indicates a user error and 500 indicates a server/system error.
  - Example: `on error 402, call PayForService`
- **Key**: Identifies the type of error (e.g., StepError, ServiceError, ProgramError).
  - Example: `on error key:'ProgramError', call HandleProgramError`
- **GoalToCall**: Specifies which goal to call when an error occurs, with the option to pass parameters.
  - Example: `on error call HandleError %variable%`
- **RetryHandler**: Defines how many times to retry the failed step over a specified period.
  - Example: `retry 5 times over 3 minutes`

## Error Object

The error object (`%!error%`) is automatically populated by the runtime and contains details about the error, including:

- **Message**: The error message.
- **Key**: The type of error.
- **StatusCode**: The error status code.
- **Additional Properties**: Depending on the error type, additional properties may be available.

## Practical Examples

Here are some practical examples of using ErrorHandler in plang:

**Handling Timeouts**

```plang
Start
- get https://example.org
    on error 'timeout' call HandleTimeout, retry 5 times over 3 minutes

HandleTimeout
- log warning "Got timeout %!error%"
```

**Handling Specific Error Keys**

```plang
- get https://example.org
    on error key 'ProgramError', call HandleProgramError
```

**Handling Payment Required Errors**

```plang
- get https://example.org/use_service_that_costs
    on error 402, call HandlePayment
```

**Multiple Error Handlers**

```plang
Start
- get http://example.org
    on error 'timeout', retry 2 times over 30 seconds then call ManageTimeoutError
    on error 'host not found' call InternetDownError, retry 5 times over 5 minutes
    on error 402, call ExecutePayment
```

## Order of Error Handling

The order in which error handlers are defined is crucial. Handlers earlier in the order can preempt later ones:

```plang
Start
- read file.txt into %content%
    on error ignore
    on error 'timeout', call HandleTimeout
```

In the above example, the `on error ignore` handler will catch and ignore all errors, preventing the `on error 'timeout'` handler from ever being triggered.

## Ignoring All Errors

To ignore all errors in a goal:

```plang
Start
- get http://example.org
    ignore all errors
```

## Using Events for Error Handling

You can also use events to handle errors:

```plang
Events
- on error for step, call HandleErrorOnStep
- on error for goal, call HandleErrorOnGoal

HandleErrorOnStep
- write out error, 'Error on step: %!error%'

HandleErrorOnGoal
- write out error, 'Error on goal: %!error%'
```

This guide should provide you with the necessary knowledge to implement and manage error handling in your plang scripts effectively, ensuring more robust and reliable applications.