# ErrorHandler Documentation

## Introduction

The `ErrorHandler` in Plang is a powerful feature that allows developers to manage and respond to errors effectively within their Plang scripts. This documentation will guide you through the basics of error handling, the properties of the `ErrorHandler`, and how to implement error handling in your Plang code.

## Basics of Error Handling

In Plang, you can handle errors using the `on error` statement. This allows you to define specific actions to take when an error occurs. Here's a simple example:

```plang
Start
- get http://example.org
    on error call ManageError

ManageError
- log error %!error%
- throw 'Error happened'
```

In this example, if an error occurs during the `get` request, the `ManageError` goal is called. The error is logged, and an error message is thrown.

## ErrorHandler Properties

The `ErrorHandler` has several properties that allow you to customize how errors are handled:

- **IgnoreError**: Default is `false`. Allows you to catch an error but ignore it and continue to the next step. Example: `on error 'element not found', ignore`.

- **Message**: The message of the error. You can check if the defined text is in the message (case insensitive). Example: `on error 'timeout', call HandleTimeout`.

- **StatusCode**: The status code of the error, similar to HTTP status codes. Example: `on error 402, call PayForService`.

- **Key**: The key of the error, which can vary. Common keys include `StepError`, `ServiceError`, and `ProgramError`. Example: `on error key:'ProgramError', call HandleProgramError`.

- **GoalToCall**: Specifies which goal to call on the error. You can send parameters, e.g., `on error call HandleError %variable%`.

- **RetryHandler**: Allows you to define how many times to retry the step over a specified period.

## Error Object

The error object, defined as `%!error%` in Plang, is thrown by the runtime and sent to the error handler. It has the following properties:

- **Message**: The error message. It checks if the message contains a user-defined message (case insensitive).

- **Key**: The key of the error, which can vary. Common keys include `StepError`, `ServiceError`, and `ProgramError`.

- **StatusCode**: The status code (HTTP status codes) of the error. Codes 400 are user errors, and 500 are system errors.

- **Other Properties**: Depending on the error type, you can see the properties by writing out the `%!error%` variable. Example: `- write out %!error%`.

## Defining Error Handling

You can define error handling in your Plang code like this:

```plang
Start
- get https://example.org
    on error 'timeout' call HandleTimeout, retry 5 times over 3 minutes

HandleTimeout
- log warning "Got timeout %!error%"
```

In this example, the error handler will search for the word 'timeout' in the `Message` property.

## Handling Status Codes

You can handle specific status codes using the `StatusCode` property:

```plang
- get https://example.org/use_service_that_costs
    on error 402, call HandlePayment
```

In this example, the error handler will call `HandlePayment` if the status code is 402.

## Multiple Error Handlers

You can define multiple error handlers for different scenarios:

```plang
Start
- get http://example.org
    on error 'timeout', retry 2 times over 30 seconds then call ManageTimeoutError
    on error 'host not found' call InternetDownError, retry 5 times over 5 minutes
    on error 402, call ExecutePayment

ManageTimeoutError
- write out error 'There was a timeout'
- if %isProduction%
    - throw error 'There was a time out'

InternetDownError
- write out error 'Internet is down'

ExecutePayment
- transfer 50 usdc to 0x123..
```

In this example, different error handlers are defined for different error messages and status codes.

## Ignoring Errors

You can choose to ignore all errors and continue executing the next step:

```plang
Start
- get http://example.org
    ignore all errors
```

## Order of Retry Statement

You can define when the retry statement should be executed, either before or after handling the error. For example:

```plang
- open in browser, http://slow_website.com/
    on error 'timeout', retry 10 times over 10 minutes, if that fails call HandleTimeoutError
- write out 'yes, go connected'
```

In this example, Plang tries 10 times to connect to the website. If it fails, it calls the `HandleTimeoutError` goal.

## Order of Error Handling

The order of error handling matters. If error handling is defined like this:

```plang
Start
- read file.txt into %content%
    on error ignore
    on error 'timeout', call HandleTimeout
```

The `on error 'timeout'` will never be called because `on error ignore` catches all errors and ignores them.

## Handling Error in Call Stack

When you want to handle an error but not stop the execution of a step, you can use the `end goal` command. For example:

```plang
Start
- select subscriberId from users, write to %users%
- for each %users%, call ChargeUser %user%=item

ChargeUser
- get http://localhost:100/ChargeUser?userId=%user.id%, 
    on error call HandleError
- write out 'if error happens, this will not be written out'

HandleError
- write out %!error%
- end goal and previous // you can also say 'end goal and 1 level more'
```

This will take the execution back to the `Start` goal and process the next user in the loop.

## Continue to Next Step

You can catch an error and decide to continue to the next step:

```plang
Start
- get https://doesNotExists,
    on error call HandleError, continue to next step
- write out 'This will run even tho prev step got error'
```

## Global Error Handling

You can use events to handle errors globally:

```plang
Events
- on error for step, call HandleErrorOnStep
- on error for goal, call HandleErrorOnGoal

HandleErrorOnStep
- write out error, 'Error on step: %!error%'

HandleErrorOnGoal
- write out error, 'Error on goal: %!error%'
```

This allows you to define global error handling strategies for steps and goals.

For more information on error types, visit the [Plang Errors](https://github.com/PLangHQ/plang/tree/main/PLang/Errors) page.