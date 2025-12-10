# PLang Error Patterns Reference

Common error patterns, response formats, and best practices.

**Related references:**
- [error-handling.md](error-handling.md) - Core error handling, throwing errors, callbacks
- [validation.md](validation.md) - Input validation syntax and strategies

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
- before each goal in api/* call Authenticate

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
- before api/* call CheckRateLimit

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

### Payment Error (Special)

```plang
- throw 402 {amount: 10, currency: "USD", address: "0x123..."}

// Client can handle payment flow based on this response
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
- on error in api/* call HandleApiError

❌ BAD:
// Duplicated error handling in every API goal
```

### 8. Provide Actionable Feedback

```plang
✅ GOOD:
- throw 400 "Password must be at least 8 characters"
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

## Summary

Key principles:
1. **Validate early** - Check inputs first
2. **Use correct status codes** - 400, 401, 403, 404, etc.
3. **Log for debugging** - Detailed logs, safe messages to users
4. **Handle gracefully** - Don't crash, provide fallbacks
5. **Be specific** - Clear, actionable error messages
6. **Centralize** - Use Events.goal for common handling
7. **Don't expose internals** - Keep stack traces in logs only