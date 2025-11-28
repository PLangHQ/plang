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
- write out %!error%"
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

## Retry Logic

### Basic Retry

**Retry HTTP requests**:
```plang
// Retry 5 times over 10 minutes
- get https://api.unreliable.com/data
    retry 5 times over 10 minute period
    write to %data%
```

**Retry with exponential backoff** (implicit):
```plang
// Plang automatically spaces retries
- get %externalApi%
    retry 3 times
    write to %result%
```

### Retry Step After Error

**Retry current step**:
```plang
GetData
- get https://api.example.com/data
    on error call RetryHandler
    write to %data%

RetryHandler
- wait 5 seconds
- retry step
```

**Limited retries**:
```plang
ProcessPayment
- set %retries% = 0
- call goal Charge %amount%, %card%
    on error HandleError

HandleError
- set %retries% = %retries% + 1
- if %retries% < 3
    - wait 2 seconds
    - retry step
- else
    - throw error "Payment failed after 3 attempts"
        
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