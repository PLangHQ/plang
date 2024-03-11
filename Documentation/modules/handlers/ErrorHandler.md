# ErrorHandler in plang

The ErrorHandler in plang is a powerful feature that allows developers to gracefully manage and respond to errors that occur during the execution of their plang code. Below, we'll explore how to implement error handling in various scenarios.

## Basic Error Handling

To handle errors in plang, you can define an error handler that will be called when an error occurs. Here's a simple example:

```plang
Start
- get http://example.org
    on error call ManageError

ManageError
- write out error %____Exception__%
```

In this example, if an error occurs during the `get` request to `http://example.org`, the `ManageError` goal will be called, which outputs the error message.

## Conditional Error Handling

plang allows you to handle different errors with specific handlers based on the error message. This can be useful for providing more context-specific error messages or actions.

```plang
Start
- get http://example.org
    on error 'timeout' call ManageTimeoutError
    on error 'host not found' call InternetDownError

ManageTimeoutError
- write out error 'There was a timeout'

InternetDownError
- write out error 'Internet is down'
```

In the above code, if a timeout error occurs, `ManageTimeoutError` is called, and if a 'host not found' error occurs, `InternetDownError` is called.

## Ignoring Errors

There may be situations where you want to ignore all errors and not have any error handling logic. This can be done using the `ignore all errors` directive.

```plang
Start
- get http://example.org
    ignore all errors
```

When this code is executed, any errors that occur during the `get` request will be ignored.

## Integration with RetryHandler

ErrorHandler works seamlessly with the RetryHandler, allowing you to attempt a request multiple times before handling the error.

```plang
Start
- get http://example.org
    retry 2 times over 1 min
    on error call ManageError

ManageError
- write out error %____Exception__%
```

In this scenario, if the `get` request fails, plang will retry the request twice over the span of one minute before calling the `ManageError` goal.

## Event-Based Error Handling

plang also supports event-based error handling, where you can define handlers for errors that occur at the step or goal level.

```plang
Events
- on error for step, call HandleErrorOnStep
- on error for goal, call HandleErrorOnGoal

HandleErrorOnStep
- write out error 'Error on step: %____Exception__%'

HandleErrorOnGoal
- write out error 'Error on goal: %____Exception__%'
```

With event-based error handling, you can have a centralized error management system that responds to errors as they occur in different parts of your plang code.

By utilizing these error handling techniques, you can ensure that your plang applications are robust and can handle unexpected situations gracefully.