# PLang Error Handling Reference

Core error handling mechanics: throwing errors, the error object, callbacks, and goal termination.

**Related references:**
- [validation.md](validation.md) - Input validation syntax and strategies
- [error-patterns.md](error-patterns.md) - Common error patterns, best practices, response formats

## Overview

PLang provides several mechanisms for error handling:
- Throwing errors with status codes
- The error object (`%!error%`)
- Error callbacks and retry logic
- Goal termination control
- Debug mode for developers

## Throwing Errors

### Basic Error Throwing

**Simple error**:
```plang
- if %user% is empty, throw error "User not found"
```

**HTTP status codes**:
```plang
// 400 Bad Request
- if %input% is invalid, throw 400 "Invalid input"

// 401 Unauthorized
- if %token% is invalid
    - throw 401 "Unauthorized"

// 403 Forbidden
- if %user.role% is not "admin", throw 403 "Forbidden"

// 404 Not Found
- if %resource% is empty, throw 404, key: NotFound, "Resource not found"

// 500 Internal Server Error
- if %criticalError%
    - throw 500 "Internal server error"
```

**With error object**:
```plang
- throw 402 {amount:10, currency:"USD", address:"0x123..."}
```

### Error Messages

**User-friendly errors**:
```plang
❌ BAD:
- throw error "Null reference exception at line 42"

✅ GOOD:
- throw error "Unable to process request. Please try again."
```

**Informative but safe**:
```plang
❌ EXPOSES INTERNALS:
- throw error "SQL error: Invalid column 'password_hash' in table 'users'"

✅ SAFE:
- throw error "Unable to create user account"
```

**Developer vs User errors**:
```plang
ProcessPayment
- read file %fileName% to %content%
    on error call HandleError

HandleError
// Log detailed error for debugging
- log error "Reading file failed: %!error.message%"
// Show generic error to user
- throw error "Could not read the file %fileName%. Please try again."
```

## Early Returns

### Return on Error

**Stop execution early**:
```plang
GetUser
- select * from users where id=%userId%, write to %user%
- if %user% is empty, throw error "User not found"

// These lines only run if user exists:
- select * from orders where user_id=%userId%, write to %orders%
- write {user:%user%, orders:%orders%} to response
```

**Return with data**:
```plang
CalculateDiscount
- if %orderAmount% < 50
    - return {discount:0, message:"Minimum not met"}
- if %orderAmount% < 100
    - return {discount:5, message:"5% discount"}
- if %orderAmount% < 200
    - return {discount:10, message:"10% discount"}
- return {discount:15, message:"15% discount"}
```

### Return vs Throw

**Use return when**:
- ✅ Not an error, just different path
- ✅ Early exit from logic
- ✅ Multiple exit points with data

**Use throw when**:
- ✅ Actual error condition
- ✅ Should stop execution
- ✅ Need to propagate error up

```plang
// Return - not an error
ValidateAge
- if %age% < 18
    - return {allowed:false, reason:"Too young"}
- return {allowed:true}

// Throw - actual error
ValidateAge
- if %age% < 0, throw error "Invalid age"
- if %age% < 18, throw 403 "Access denied"
```

## Goal Termination

### End Goal

Use `end goal` to stop the current goal's execution:

```plang
ShowProduct
- select * from products where id=%productId%, return 1, write to %product%
- if %product% is empty then
    - [ui] render "404.html", navigate
    - end goal
/ This only runs if product exists
- [ui] render "product.html", navigate
```

### End Goal and Parent Goals

When a goal is called from another goal, `end goal` only stops the current goal. The parent goal continues. To stop parent goals too:

```plang
/ Stop current goal only
- end goal

/ Stop current goal AND the goal that called it
- end goal and previous

/ Stop current goal and multiple levels up
- end goal, 4 levels up

/ Stop ALL goal execution (full termination)
- end goal and terminate
```

**Example - API returning 404:**
```plang
/ api/GetProduct.goal
GetProduct
- call goal /product/LoadProduct
- [ui] render "product.html"   / This should NOT run if product not found

/ product/LoadProduct.goal
LoadProduct
- select * from products where id=%productId%, return 1, write to %product%
- if %product% is empty then
    - [ui] render "404.html", navigate
    - end goal and previous    / Stops LoadProduct AND GetProduct
```

### Throw vs End Goal

Both can stop execution, but they serve different purposes:

```plang
/ Throw - signals an error, can be caught by error handlers
- throw "Product not found", 404

/ End goal - graceful exit, no error, just stops execution
- end goal
```

**When to use throw:**
- Error condition that should be logged
- Need to return HTTP status code
- Want error handlers to catch it
- Abnormal termination

**When to use end goal:**
- Normal early exit (e.g., already rendered response)
- Conditional branching that doesn't continue
- After rendering error pages to user
- Graceful termination

**Note:** When you render a template like "404.html" and the filename contains a status code pattern, PLang automatically returns that status code to the browser.

## The Error Object (%!error%)

`%!error%` is a reserved keyword containing error details:

```plang
HandleError
- write out %!error%                    / Writes full error info
- write out %!error.Message%            / Just the message
- write out %!error.StatusCode%         / HTTP status code
- write out %!error.Level%              / error, warning, info
- write out %!error.Key%                / Error key for i18n
- write out %!error.Exception%          / Full exception details (debug mode)
```

### Debug Mode

Enable debug mode for detailed error output (useful for developers):

```plang
OnRequest
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user.role% contains "admin" then call EnableDebug

EnableDebug
- set debug mode
```

When debug mode is enabled, `write out %!error%` includes full exception details, stack traces, and internal information helpful for debugging.

```plang
HandleError
/ In debug mode, this shows detailed error info
/ In normal mode, shows user-friendly message only
- write out %!error%
```

## Error Callbacks

### On Error Handlers

**Inline error handling**:
```plang
GetData
- get https://api.example.com/data
    on error call ApiError
    write to %data%
/ only runs if not error since error handler ends the goal (using 'and previous')
- write out %data%

ApiError
- log error "API call failed: %!error%"
- write out {success:false, error:"Unable to fetch data"}
- end goal and previous  
```

**Call another goal on error**:
```plang
ProcessOrder
- call goal ChargePayment %paymentInfo%
    on error call HandlePaymentError

// HandlePaymentError.goal
HandlePaymentError
- log error "Payment failed: %!error%"
- write out %!error%
```

### Conditional Error Handling

Match specific error conditions using partial string matching (case-insensitive):

```plang
FetchData
- get https://api.example.com/data
    on error 'timeout', call HandleTimeout
    on error 'connection refused', call HandleConnectionError
    on error 'unauthorized', call HandleAuth
    on error call HandleGenericError
    write to %data%
```

### Retry on Error

Automatically retry on specific errors:

```plang
/ Retry on timeout - 5 attempts over 30 seconds
- get https://api.example.com/data
    on error 'timeout', retry 5 times over 30 seconds
    write to %data%

/ Retry on any error
- get https://api.unreliable.com/data
    retry 3 times over 1 minute
    write to %data%

/ Retry with specific status codes
- post https://api.example.com/process
    on error status code 503, retry 5 times over 2 minutes
    write to %result%
```

### Fix and Retry Step

Handle an error, fix the problem, then retry the same step:

```plang
ReadConfig
- read config.txt, write to %config%
    on error call CreateDefaultConfig

CreateDefaultConfig
- write '{"setting": "default"}' to config.txt
- retry step   / Retries the read operation
```

**Example - ensure file exists:**
```plang
LoadData
- read data.json, write to %data%
    on error 'not found', call CreateDataFile

CreateDataFile
- write '[]' to data.json
- retry step
```

### Error Handler Termination

Control execution flow from error handlers:

```plang
ProcessRequest
- get https://api.example.com/data
    on error call HandleError
- process %data%   / Only runs if HandleError doesn't terminate

HandleError
/ Check status code and decide what to do
- if %!error.StatusCode% == 401 then
    - [ui] render "login.html", navigate
    - end goal and previous    / Stop HandleError AND ProcessRequest

- if %!error.StatusCode% == 403 then
    - [ui] render "forbidden.html", navigate
    - end goal and previous

- if %!error.StatusCode% == 503 then
    - retry step               / Retry the HTTP request

/ For other errors, log and continue
- log error "API error: %!error.Message%"
```

### Try-Catch Pattern via Events

**Global error handling**:
```plang
// Events.goal
Events
- on error in api/* call HandleApiError

// HandleApiError.goal  
HandleApiError
- log error "API error: %!error.message% in %!goal.GoalName%"
- if %!error.code% is 404
    - write out {error:"Not found"}
    - end goal
- if %error.code% is 401
    - write {error:"Unauthorized"}
    - end goal
- write {error:"Server error"}
```

**Goal-specific error handling**:
```plang
// Events.goal
Events
- on error in CreateUser call HandleUserCreationError
- on error in ProcessPayment call HandlePaymentError
```

## Logging Errors

### Basic Logging

```plang
ProcessData
- process %data%
    on error
        - log error "Processing failed: %error.message%"
        - throw error "Unable to process data"
```

### Detailed Logging

```plang
// Events.goal
Events
- on error call LogError

// LogError.goal
LogError
- insert into error_log
    goal=%!goal%
    step=%!step%
    error=%!error.message%
    stack_trace=%!error.stackTrace%
    timestamp=%Now%
    identity=%Identity%

- log error "Error in %!goal.GoalName%: %!error.message%"
```

### Debug Logging

```plang
ProcessOrder
- log debug "Processing order %orderId%"
- get order from database
- log debug "Order data: %order%"
- validate order
- log debug "Validation passed"
- call goal ProcessOrder
    on error WriteToLog

WriteToLog
- log error "Order processing failed: %!error%"
```