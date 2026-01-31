# PLang Error Handling Reference

Complete reference for error handling, validation, and error patterns.

## Errors Are Your Friends

**Important mindset shift:** Errors are not bad - they are helpful. They tell you something is wrong and need your attention. Good error handling makes your application robust and debuggable.

**Key principles:**

1. **Errors help you improve** - Each error is feedback about something that needs attention
2. **Handle errors gracefully** - Don't let errors crash your app; catch them and respond appropriately
3. **Trace back to the source** - Errors often originate several goals back from where they appear. When debugging, trace back through the call chain to find the root cause
4. **Fix at the source** - Don't just handle the symptom; fix the underlying issue if possible
5. **Log for debugging** - Always log detailed error info for developers while showing user-friendly messages to users

## Overview

PLang provides several mechanisms for error handling:
- Input validation with `make sure` / `validate`
- Throwing errors with status codes
- The error object (`%!error%`)
- Error callbacks and retry logic
- Goal termination control
- Debug mode for developers

## Input Validation

### Basic Validation

```plang
ProcessOrder
- make sure %orderId% is not empty
- make sure %customerId% is not empty
- make sure %amount% is not empty
```

### Check Format

```plang
ValidateOrder
- make sure %email% contains @
- make sure %amount% > 0
- make sure %quantity% >= 1
```

### Multiple Conditions

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

### Options Explained

| Option | Description | Example |
|--------|-------------|---------|
| **message** | User-friendly error message | `"Name cannot be empty"` |
| **statusCode** | HTTP status code | `400`, `401`, `403` |
| **key** | Identifier for client-side handling or i18n | `key: "EmptyName"` |
| **level** | Severity level | `level: error`, `level: warning`, `level: info` |

### Practical Example

```plang
ValidateOrder
- make sure %email% is not empty, "Email is required", 400, key: "EmailRequired", level: error
- make sure %email% contains @, "Please enter a valid email", 400, key: "InvalidEmail", level: error
- make sure %amount% > 0, "Amount must be positive", 400, key: "InvalidAmount", level: error
- make sure %terms% is true, "You must accept the terms", 400, key: "TermsNotAccepted", level: error
```

### Partial Options

You can use just what you need:

```plang
/ Just message
- make sure %name% is not empty, "Name is required"

/ Message and status code
- make sure %token% is not empty, "Unauthorized", 401

/ Message with key (for i18n)
- make sure %name% is not empty, "Name is required", key: "NameRequired"

/ Status code and key
- make sure %user.role% is "admin", "Forbidden", 403, key: "NotAdmin"
```

### Common Validation Rules

**String Validations:**
```plang
- make sure %name% is not empty
- make sure %title% length >= 3
- make sure %description% length <= 500
- make sure %email% contains @
- make sure %phoneNumber% is 7 numbers
```

**Number Validations:**
```plang
- make sure %age% >= 18
- make sure %quantity% >= 0
- make sure %quantity% <= 100
- make sure %price% > 0
```

**List/Enum Validations:**
```plang
- make sure %status% in ["pending", "active", "completed"]
- make sure %category% in ["Electronics", "Clothing", "Food", "Other"]
```

**Boolean Validations:**
```plang
- make sure %termsAccepted% is true, "Must accept terms", 400
- make sure %isBlocked% is false, "Account is blocked", 403
```

## Throwing Errors

### Basic Error Throwing

**Simple error:**
```plang
- if %user% is empty, throw error "User not found"
```

**HTTP status codes:**
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

**With error object:**
```plang
- throw 402 {amount:10, currency:"USD", address:"0x123..."}
```

### Error Messages

**User-friendly errors:**
```plang
❌ BAD:
- throw error "Null reference exception at line 42"

✅ GOOD:
- throw error "Unable to process request. Please try again."
```

**Informative but safe:**
```plang
❌ EXPOSES INTERNALS:
- throw error "SQL error: Invalid column 'password_hash' in table 'users'"

✅ SAFE:
- throw error "Unable to create user account"
```

**Developer vs User errors:**
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

**Stop execution early:**
```plang
GetUser
- select * from users where id=%userId%, write to %user%
- if %user% is empty, throw error "User not found"

// These lines only run if user exists:
- select * from orders where user_id=%userId%, write to %orders%
- write {user:%user%, orders:%orders%} to response
```

**Return with data:**
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

**Use return when:**
- ✅ Not an error, just different path
- ✅ Early exit from logic
- ✅ Multiple exit points with data

**Use throw when:**
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
/ product/Get.goal
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

## Error Callbacks

### On Error Handlers

**Inline error handling:**
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

**Call another goal on error:**
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

**Global error handling:**
```plang
// Events.goal
Events
- on error call HandleApiError

// HandleApiError.goal  
HandleApiError
/ %!callingGoal% is the goal that triggered the error, not HandleApiError
- log error "API error: %!error.message% in %!callingGoal.GoalName%"
- if %!error.code% is 404
    - write out {error:"Not found"}
    - end goal
- if %!error.code% is 401
    - write {error:"Unauthorized"}
    - end goal
- write {error:"Server error"}
```

**Goal-specific error handling:**
```plang
// Events.goal
Events
- on error in CreateUser call HandleUserCreationError
- on error in ProcessPayment call HandlePaymentError
```

## Common Error Patterns

### Pattern 1: Resource Not Found (404)

```plang
GetUser
- select * from users where id=%userId%, write to %user%
- if %user% is empty, throw 404 "User not found"
- write out %user%
```

### Pattern 2: Unauthorized Access (401)

```plang
// In Events.goal
Events
- before each goal call Authenticate

// Authenticate.goal
Authenticate
- select user_id from users where identity=%Identity%, write to %userId%
- if %userId% is empty, throw 401 "Unauthorized"
```

### Pattern 3: Forbidden - Insufficient Permissions (403)

```plang
DeleteUser
- select role from users where identity=%Identity%, write to %role%
- if %role% is not "admin", throw 403 "Forbidden"
- delete from users where id=%userId%
```

### Pattern 4: Invalid Input (400)

```plang
CreateProduct
- make sure %name% is not empty, "Product name required", 400
- make sure %price% > 0, "Price must be positive", 400
- insert into products, %name%, %price%
```

### Pattern 5: External Service Failure

```plang
SendEmail
- post https://email-service.com/send
    bearer %Settings.EmailApiKey%
    body: {to:%recipient%, subject:%subject%, body:%body%}
    on error call HandleEmailFailure
    write to %result%

// HandleEmailFailure.goal
HandleEmailFailure
- log error "Email service failed: %!error%"
- insert into email_queue, recipient=%recipient%, subject=%subject%, body=%body%, status="pending"
- write out {queued:true}
```

### Pattern 6: Race Conditions (409 Conflict)

```plang
ReserveTicket
- begin transaction
- select available from tickets where id=%ticketId%, write to %available%
- if %available% is false, throw 409 "Ticket already reserved"
- update tickets set available=false where id=%ticketId%
- commit transaction
```

### Pattern 7: Rate Limiting (429)

```plang
// In Events.goal
Events
- before each goal call CheckRateLimit

// CheckRateLimit.goal
CheckRateLimit
- select count(*) as count from requests 
    where identity=%Identity% 
    and created > %now.AddMinutes(-1)%, return 1, write to %result%

- if %result.count% > 60
    - throw 429 "Rate limit exceeded. Try again later."

- insert into requests, identity=%Identity%, created=%now%
```

### Pattern 8: Payment Required (402)

```plang
AccessPremiumContent
- select subscription from users where id=%userId%, return 1, write to %user%
- if %user.subscription% is not "premium"
    - throw 402 {
        message: "Premium subscription required",
        plans: ["monthly", "yearly"],
        upgradeUrl: "/upgrade"
      }
- call goal ServePremiumContent
```

### Pattern 9: Service Unavailable with Retry (503)

```plang
CallExternalAPI
- get https://api.external.com/data
    on error status code 503, retry 3 times over 30 seconds
    on error call HandleServiceDown
    write to %data%

HandleServiceDown
- log error "External service unavailable: %!error%"
- throw 503 "Service temporarily unavailable. Please try again later."
```

### Pattern 10: Validation Error Collection

Collect multiple validation errors before responding:

```plang
ValidateOrder
- set %errors% = []

- if %items% is empty
    - add "At least one item required" to %errors%
    
- if %shippingAddress% is empty
    - add "Shipping address required" to %errors%
    
- if %paymentMethod% is empty
    - add "Payment method required" to %errors%

- if %errors% is not empty
    - throw 400 {error: "Validation failed", details: %errors%}
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

### Error with Status Code
```plang
- throw 404 "User not found"

// Response:
{
  "error": "User not found",
  "statusCode": 404
}
```

### Structured Error
```plang
- throw 400 {
    error: "Validation failed",
    details: ["Name required", "Invalid email"]
  }

// Response:
{
  "error": "Validation failed",
  "details": ["Name required", "Invalid email"],
  "statusCode": 400
}
```

### Error with Key (for i18n)
```plang
- throw 404, key: "UserNotFound", "User not found"

// Response:
{
  "error": "User not found",
  "key": "UserNotFound",
  "statusCode": 404
}
```

## HTTP Status Codes Reference

| Code | Meaning | When to Use |
|------|---------|-------------|
| 400 | Bad Request | Invalid input, validation failed |
| 401 | Unauthorized | Not logged in, invalid token |
| 403 | Forbidden | Logged in but no permission |
| 404 | Not Found | Resource doesn't exist |
| 409 | Conflict | Race condition, duplicate |
| 429 | Too Many Requests | Rate limit exceeded |
| 500 | Server Error | Unexpected internal error |
| 503 | Service Unavailable | Temporary outage |

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
/ Use %!callingGoal% to get the goal that caused the error
- insert into error_log
    goal=%!callingGoal.GoalName%
    step=%!step%
    error=%!error.message%
    stack_trace=%!error.stackTrace%
    timestamp=%Now%
    identity=%Identity%

- log error "Error in %!callingGoal.GoalName%: %!error.message%"
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
ProcessOrder
- make sure %orderId% is not empty
- make sure %amount% > 0
- call goal ChargePayment
- insert into orders

❌ BAD:
ProcessOrder
- call goal ChargePayment
- insert into orders
- if %orderId% is empty, throw error  / Too late!
```

### 2. Be Specific with Error Messages
```plang
✅ GOOD:
- if %user% is empty, throw 404 "User not found"

❌ BAD:
- if %user% is empty, throw error
```

### 3. Don't Expose Internal Details
```plang
✅ GOOD:
- on error throw error "Payment processing failed"

❌ BAD:
- on error throw error %!error.stackTrace%
```

### 4. Log Before Throwing
```plang
✅ GOOD:
ProcessPayment
- call goal Charge %amount%
    on error HandleError
    
HandleError
- log error "Payment error: %!error%"      / Log detailed error
- throw error "Payment failed"              / Show safe message

❌ BAD:
ProcessPayment
- call goal Charge %amount%
    on error throw error "Error"            / No logging, vague message
```

### 5. Use Appropriate HTTP Status Codes
```plang
✅ GOOD:
- if %user% is empty, throw 404 "User not found"
- if not authorized, throw 401 "Please log in"
- if validation fails, throw 400 "Invalid input"

❌ BAD:
- if %user% is empty, throw 500 "User not found"  / Wrong code!
- if not authorized, throw 400 "Not authorized"   / Should be 401
```

### 6. Handle Expected Failures Gracefully
```plang
✅ GOOD:
GetExternalData
- get https://external-api.com/data
    retry 3 times
    on error HandleError
    write to %data%

HandleError
- log error "External API failed: %!error%"
- read data.cache, write to %data%    / Fallback to cache

❌ BAD:
GetExternalData
- get https://external-api.com/data
// If fails, entire app crashes
```

### 7. Centralize Error Handling
```plang
✅ GOOD:
// Events.goal - Central error handling
Events
- on error call HandleApiError

❌ BAD:
// Duplicated error handling in every API goal
```

### 8. Provide Actionable Feedback
```plang
✅ GOOD:
- throw 400 "Amount must be greater than zero"
- throw 429 "Rate limit exceeded. Try again in 60 seconds."

❌ BAD:
- throw 400 "Invalid"
- throw 429 "Error"
```

## Error Handling Checklist

### Validation
- [ ] Validate all inputs at start of goal
- [ ] Check for empty/null values
- [ ] Validate data types and formats
- [ ] Validate ranges and constraints
- [ ] Use descriptive error messages

### Error Messages
- [ ] User-friendly error messages
- [ ] Don't expose internal details
- [ ] Include appropriate status codes
- [ ] Provide actionable feedback
- [ ] Use keys for i18n where needed

### Error Logging
- [ ] Log all errors for debugging
- [ ] Include context (goal, step, identity)
- [ ] Log before throwing to user
- [ ] Use different log levels (error, warning, debug)

### Error Recovery
- [ ] Implement retry for transient failures
- [ ] Provide fallback options where possible
- [ ] Graceful degradation
- [ ] Transaction rollbacks where needed

### Error Propagation
- [ ] Use Events.goal for global handling
- [ ] Use `on error` for local handling
- [ ] Use `end goal and previous` when needed
- [ ] Throw errors for critical failures
