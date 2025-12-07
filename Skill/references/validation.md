# PLang Validation Reference

Input validation syntax, options, and strategies.

**Related references:**
- [error-handling.md](error-handling.md) - Core error handling, throwing errors, callbacks
- [error-patterns.md](error-patterns.md) - Common error patterns and best practices

## Basic Validation

### Check for Empty Values

```plang
CreateUser
- make sure %name% is not empty
- make sure %email% is not empty
- make sure %password% is not empty
```

### Check Format

```plang
ValidateUser
- make sure %email% contains @
- make sure %password% length >= 8
- make sure %age% >= 18
```

### Multiple Conditions

```plang
ValidateProduct
- make sure %productName% is not empty
- make sure %price% > 0
- make sure %quantity% >= 0
- make sure %category% in ["Electronics", "Clothing", "Food"]
```

## Validation with Full Options

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
ValidateRegistration
- make sure %email% is not empty, "Email is required", 400, key: "EmailRequired", level: error
- make sure %email% contains @, "Please enter a valid email", 400, key: "InvalidEmail", level: error
- make sure %password% length >= 8, "Password must be at least 8 characters", 400, key: "WeakPassword", level: error
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

## Validation Patterns

### Pattern 1: Validate First (Fail Fast)

Validate all inputs at the start before any processing:

```plang
ProcessOrder
// Validate inputs first
- make sure %orderId% is not empty
- make sure %amount% > 0
- make sure %customerId% is not empty

// Then process (only runs if all validations pass)
- select * from orders where id=%orderId%, write to %order%
- if %order% is empty, throw error "Order not found"
- call goal ProcessPayment
```

### Pattern 2: Centralized Validation

Create reusable validation goals:

```plang
CreateUser
- call ValidateUserInput %userData%
- hash %userData.password%, write to %hashedPassword%
- insert into users, %userData.name%, %userData.email%, %hashedPassword%

// ValidateUserInput.goal
ValidateUserInput
- make sure %userData.name% is not empty, "Name is required", 400
- make sure %userData.email% is not empty, "Email is required", 400
- make sure %userData.email% contains @, "Invalid email format", 400
- make sure %userData.password% length >= 8, "Password too short", 400
```

### Pattern 3: Database Constraints

Let the database enforce constraints and handle errors:

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

### Pattern 4: Conditional Validation

Different validation rules based on context:

```plang
ValidateUser
- make sure %email% is not empty, "Email required", 400

/ Additional validation for new users
- if %isNewUser% then
    - make sure %password% is not empty, "Password required", 400
    - make sure %password% length >= 8, "Password too short", 400
    - make sure %confirmPassword% == %password%, "Passwords don't match", 400

/ Additional validation for admins
- if %role% == "admin" then
    - make sure %adminCode% is not empty, "Admin code required", 400
```

### Pattern 5: Cross-Field Validation

Validate relationships between fields:

```plang
ValidateDateRange
- make sure %startDate% is not empty, "Start date required", 400
- make sure %endDate% is not empty, "End date required", 400
- make sure %endDate% > %startDate%, "End date must be after start date", 400

ValidatePayment
- make sure %amount% > 0, "Amount must be positive", 400
- make sure %amount% <= %balance%, "Insufficient balance", 400
```

## Common Validation Rules

### String Validations

```plang
/ Not empty
- make sure %name% is not empty

/ Minimum length
- make sure %password% length >= 8

/ Maximum length
- make sure %description% length <= 500

/ Contains substring
- make sure %email% contains @

/ Matches pattern (basic)
- make sure %phoneNumber% is 7 numbers
```

### Number Validations

```plang
/ Greater than
- make sure %age% >= 18

/ Range
- make sure %quantity% >= 0
- make sure %quantity% <= 100

/ Positive number
- make sure %price% > 0
```

### List/Enum Validations

```plang
/ Value in list
- make sure %status% in ["pending", "active", "completed"]

/ Value in category
- make sure %category% in ["Electronics", "Clothing", "Food", "Other"]
```

### Boolean Validations

```plang
/ Must be true
- make sure %termsAccepted% is true, "Must accept terms", 400

/ Must be false
- make sure %isBlocked% is false, "Account is blocked", 403
```

## Validation Best Practices

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
- if %email% is empty, throw error  / Too late!
```

### 2. Use Descriptive Messages

```plang
✅ GOOD:
- make sure %password% length >= 8, "Password must be at least 8 characters"

❌ BAD:
- make sure %password% length >= 8, "Invalid"
```

### 3. Use Appropriate Status Codes

```plang
✅ GOOD:
- make sure %email% is not empty, "Email required", 400        / Bad request
- make sure %token% is valid, "Invalid token", 401             / Unauthorized  
- make sure %user.role% is "admin", "Forbidden", 403           / Forbidden

❌ BAD:
- make sure %email% is not empty, "Email required", 500        / Wrong code!
```

### 4. Use Keys for i18n

```plang
✅ GOOD:
- make sure %email% is not empty, "Email is required", 400, key: "validation.email.required"

/ Client can look up translated message using the key
```

### 5. Group Related Validations

```plang
✅ GOOD:
ValidateAddress
- make sure %street% is not empty, "Street required", 400
- make sure %city% is not empty, "City required", 400
- make sure %postalCode% is not empty, "Postal code required", 400
- make sure %country% is not empty, "Country required", 400

CreateOrder
- call ValidateAddress %shippingAddress%
- call ValidateAddress %billingAddress%
```