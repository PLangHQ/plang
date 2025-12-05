# Plang Error Handling & Validation

## Overview

Plang provides several mechanisms for error handling:
- Input validation
- Conditional error throwing
- Try-catch patterns (via Events)
- Early returns
- Error callbacks
- Retry logic

## Input Validation

### Basic Validation

**Check for empty values**:
```plang
CreateUser
- make sure %name% is not empty
- make sure %email% is not empty
- make sure %password% is not empty
```

**Check format**:
```plang
ValidateUser
- make sure %email% contains @
- make sure %password% length >= 8
- make sure %age% >= 18
```

**Multiple conditions**:
```plang
ValidateProduct
- make sure %productName% is not empty
- make sure %price% > 0
- make sure %quantity% >= 0
- make sure %category% in ["Electronics", "Clothing", "Food"]
```

### Validation with Full Options

Validation can include message, status code, key, and level:

```plang
/ Full syntax: make sure <condition>, "<message>", <statusCode>, key: "<key>", level: <level>

- make sure %name% is not empty, "Name cannot be empty", 400, key: "EmptyName", level: error
- make sure %email% contains @, "Invalid email format", 400, key: "InvalidEmail", level: error
- make sure %age% >= 18, "Must be 18 or older", 403, key: "Underage", level: warning
```

**Options explained:**
- **message**: User-friendly error message
- **statusCode**: HTTP status code (400, 401, 403, etc.)
- **key**: Identifier for client-side error handling or i18n
- **level**: `error`, `warning`, `info`

**Practical example:**
```plang
ValidateRegistration
- make sure %email% is not empty, "Email is required", 400, key: "EmailRequired", level: error
- make sure %email% contains @, "Please enter a valid email", 400, key: "InvalidEmail", level: error
- make sure %password% length >= 8, "Password must be at least 8 characters", 400, key: "WeakPassword", level: error
- make sure %terms% is true, "You must accept the terms", 400, key: "TermsNotAccepted", level: error
```

**Partial options** (you can use just what you need):
```plang
/ Just message
- make sure %name% is not empty, "Name is required"

/ Message and status code
- make sure %token% is not empty, "Unauthorized", 401

/ Message with key (for i18n)
- make sure %name% is not empty, "Name is required", key: "NameRequired"
```

### Validation Patterns

**Pattern 1: Validate First**
```plang
ProcessOrder
// Validate inputs
- make sure %orderId% is not empty
- make sure %amount% > 0
- make sure %customerId% is not empty

// Then process
- select * from orders where id=%orderId%, write to %order%
- if %order% is empty, throw error "Order not found"
- process order
```

**Pattern 2: Centralized Validation**
```plang
CreateUser
- call ValidateUserInput %userData%
- hash %userData.password%, write to %hashedPassword%
- insert into users, %userData.name%, %userData.email%, %hashedPassword%

// ValidateUserInput.goal
ValidateUserInput
- make sure %userData.name% is not empty
- make sure %userData.email% is not empty
- make sure %userData.email% contains @
- make sure %userData.password% length >= 8
```

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
- end goal and 2 levels up

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

**Combined pattern:**
```plang
ShowProduct
- select * from products where id=%productId%, return 1, write to %product%
- if %product% is empty then
    - throw "Product not found", 404   / Returns 404 status to browser
/ Execution stops here if throw happened
- [ui] render "product.html", navigate
```

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
- write {success:false, error:"Unable to fetch data"} to response
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

**Termination levels:**
```plang
/ Stop only the error handler goal
- end goal

/ Stop error handler AND the goal that triggered the error
- end goal and previous

/ Stop multiple levels up (e.g., nested goal calls)
- end goal, 4 levels up

/ Stop all execution completely
- end goal and terminate
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

## Validation Strategies

### Strategy 1: Fail Fast

```plang
CreateUser
// Validate everything first
- make sure %name% is not empty
- make sure %email% is not empty
- make sure %email% contains @
- make sure %password% length >= 8

// Then proceed (all validation passed)
- hash %password%, write to %hashedPassword%
- insert into users, %name%, %email%, %hashedPassword%
```

### Strategy 2: Database Constraints

```plang
// In Setup.goal
Setup
- create table users, columns:
    name(string, not null),
    email(string, not null, unique),
    created(datetime, default now)

// In CreateUser.goal
CreateUser
- insert into users, %name%, %email%
    on error UserInsertError

UserInsertError
- if %!error.message% contains "UNIQUE constraint"
    - throw 409 "Email already exists"
- throw 500 "Unable to create user"
```

## Common Error Patterns

### Pattern 1: Resource Not Found

```plang
GetUser
- select * from users where id=%userId%, write to %user%
- if %user% is empty, throw 404 "User not found"
- write out %user%
```

### Pattern 2: Unauthorized Access

```plang
// In Events.goal
Events
- on all goals in api/* call Authenticate

// Authenticate.goal
Authenticate
- select user_id from users where identity=%Identity%, write to %userId%
- if %userId% is empty, throw 401 "Unauthorized"
```

### Pattern 3: Forbidden (Insufficient Permissions)

```plang
DeleteUser
- select role from users where identity=%Identity%, write to %role%
- if %role% is not "admin", throw 403 "Forbidden"
- delete from users where id=%userId%
```

### Pattern 4: Invalid Input

```plang
CreateProduct
- make sure %name% is not empty, else throw 400 "Product name required"
- make sure %price% > 0, else throw 400 "Price must be positive"
- insert into products, %name%, %price%
```

### Pattern 5: External Service Failure

```plang
SendEmail
- post https://email-service.com/send
    bearer %Settings.EmailApiKey%
    body: {to:%recipient%, subject:%subject%, body:%body%}
    on error call HandleEmailFailure %error%
    write to %result%

// HandleEmailFailure.goal
HandleEmailFailure
- log error "Email service failed: %error%"
- insert into email_queue, recipient=%recipient%, subject=%subject%, body=%body%, status="pending"
- write out {queued:true}
```

### Pattern 6: Race Conditions

```plang
ReserveTicket
- begin transaction
- select available from tickets where id=%ticketId%, write to %available%
- if %available% is false, throw 409 "Ticket already reserved"
- update tickets set available=false where id=%ticketId%
- commit transaction
```

### Pattern 7: Rate Limiting

```plang
// In Events.goal
Events
- before api/* call CheckRateLimit

// CheckRateLimit.goal
CheckRateLimit
- select count from requests 
    where identity=%Identity% 
    and time > %oneMinuteAgo%, write to %count%

- if %count% > 60
    - throw 429 "Rate limit exceeded. Try again later."

- insert into requests, identity=%Identity%, time=%Now%
```

## Error Response Formats

### Simple Error

```plang
- throw error "Something went wrong"

// Response:
{
  "error": "Something went wrong"
}
```

### Structured Error

```plang
- throw 400 {
    error:"Validation failed",
    details:["Name required", "Invalid email"]
  }

// Response:
{
  "error": "Validation failed",
  "details": ["Name required", "Invalid email"],
  "statusCode": 400
}
```

### Payment Error (Special)

```plang
- throw 402 {amount:10, currency:"USD", address:"0x123..."}

// Client can handle payment
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

## Best Practices

### 1. Validate Early

```plang
✅ GOOD:
CreateUser
- make sure %email% is not empty
- make sure %password% is not empty
- hash %password%
- insert into users

❌ BAD:
CreateUser
- hash %password%
- insert into users
- if %email% is empty, throw error
```

### 2. Be Specific

```plang
✅ GOOD:
- if %user% is empty, throw 404 "User not found"

❌ BAD:
- if %user% is empty, throw error
```

### 3. Don't Expose Internals

```plang
✅ GOOD:
- on error throw error "Payment processing failed"

❌ BAD:
- on error throw error %error.stackTrace%
```

### 4. Log Errors for Debugging

```plang
✅ GOOD:
ProcessPayment
- call goal Charge %amount%
    on error HandleError
    
HandleError
- log error "Payment error: %!error%"
- throw error "Payment failed"

❌ BAD:
ProcessPayment
- call goal Charge %amount%
    on error throw error "Error"
```

### 5. Use Appropriate Status Codes

```plang
✅ GOOD:
- if %user% is empty, throw 404
- if not authorized, throw 401
- if validation fails, throw 400

❌ BAD:
- if %user% is empty, throw 500
- if not authorized, throw 400
```

### 6. Handle Expected Failures

```plang
✅ GOOD:
GetExternalData
- get https://external-api.com/data
    retry 3 times
    on error HandleError
    write to %data%

HandleError
- log error "External API failed: %!error%"
- read data.cache, write to %data%

❌ BAD:
GetExternalData
- get https://external-api.com/data
// If fails, entire app crashes
```

### 7. Centralize Error Handling

```plang
✅ GOOD:
// Events.goal
Events
- on error in api/* call HandleApiError

❌ BAD:
// Duplicated error handling in every API goal
```

## Error Handling Checklist

✅ **Validation**
- [ ] Validate all inputs at start of goal
- [ ] Check for empty/null values
- [ ] Validate data types and formats
- [ ] Validate ranges and constraints

✅ **Error Messages**
- [ ] User-friendly error messages
- [ ] Don't expose internal details
- [ ] Include appropriate status codes
- [ ] Provide actionable feedback

✅ **Error Logging**
- [ ] Log all errors for debugging
- [ ] Include context (goal, step, identity)
- [ ] Log before throwing to user
- [ ] Different log levels (error, warning, debug)

✅ **Error Recovery**
- [ ] Implement retry for transient failures
- [ ] Provide fallback options
- [ ] Graceful degradation
- [ ] Transaction rollbacks where needed

✅ **Error Propagation**
- [ ] Use Events.goal for global handling
- [ ] Use on error for local handling
- [ ] Return early when appropriate
- [ ] Throw errors for critical failures

## Summary

Key error handling concepts:
1. ✅ **Validate early** - Check inputs first
2. ✅ **Throw appropriately** - Use status codes
3. ✅ **Log for debugging** - Track all errors
4. ✅ **Handle gracefully** - Don't crash
5. ✅ **Return early** - Exit when needed
6. ✅ **Retry failures** - Handle transient issues
7. ✅ **Use Events** - Centralize error handling
8. ✅ **Be specific** - Clear error messages
9. ✅ **Don't expose internals** - Safe errors
10. ✅ **Provide fallbacks** - Alternative paths

**Critical**: Always validate inputs, log errors for debugging, but show safe messages to users.