# RetryHandler Documentation

The `RetryHandler` is a powerful feature in plang that enables developers to implement a retry mechanism for steps that may fail during execution. This functionality is particularly useful when dealing with operations that have a chance of temporary failure, such as network requests or external service calls.

## Overview

When a step in your plang code encounters an error, you might want to attempt to execute it again rather than immediately failing the process. The `RetryHandler` allows you to specify the number of retry attempts and the time period over which these attempts should occur.

## Syntax

To use the `RetryHandler`, you need to follow the plang syntax for retries. Here's the basic structure:

```plang
Start
- your_step
    retry X times over Y minute period
```

- `your_step` is the operation you want to perform, such as an HTTP GET request.
- `X` is the number of times you want to retry the step if it fails.
- `Y` is the total time period, over which the retry attempts should be spread out. In it stored in milliseconds, but you can use any timescale you like.

## Example

Let's consider an example where you want to make an HTTP GET request to `http://example.org`. If the request fails, you want to retry it 3 times over a 3-minute period.

```plang
Start
- get http://example.org
    retry 3 times over 3 minute period
```

In this example, if the `get` operation fails, plang will automatically retry the step up to 3 additional times, with the retries spread out over a total of 3 minutes.

## Notes

- Ensure that the retry parameters (`X` times and `Y` time period) are chosen based on the expected error recovery time. For instance, if you're dealing with a service that has a rate limit, you'll want to space out your retries accordingly.
- The retry mechanism is designed to handle transient errors. If the error persists beyond the specified retry attempts and period, the step will ultimately fail.

By incorporating the `RetryHandler` into your plang code, you can make your code more robust and resilient to temporary issues. This feature is essential for creating reliable automation workflows that can handle unexpected errors gracefully.