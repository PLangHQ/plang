# Plang Conventions and Best Practices

## Common Mistakes and How to Fix Them

### Setup vs Start - ONE-TIME vs ENTRY POINT

**CRITICAL RULE**: Setup is for ONE-TIME initialization, Start is for the application ENTRY POINT when no goal file is defined.

```bash
plang
```
This will run Start.goal

```bash
plang Send
```
This will run Send.goal

#### ❌ INCORRECT: Creating data/schema in Start.goal
```plang
// Start.goal - WRONG!
Start
- create table 'users', columns: id(int, pk), name(string)
- insert into users, values: 'John', 'john@example.com'
- start webserver on port 8080
```

**Problem**: Plang will throw build and runtime error when create table is executed in a non setup file

#### ✅ CORRECT: Separate Setup from Start
```plang
// Setup.goal or Setup/CreateDatabase.goal
Setup
- create table 'users', columns: id(int, pk), name(string), email(string, not null)

// Setup/SeedData.goal (optional)
SeedData
- insert into users, values: 'Admin', 'admin@example.com'

// Start.goal
Start
- start webserver, on start call AddRoutes
```

webserver default port is 8080

### Variable Naming Conventions

#### ❌ INCORRECT: Inconsistent variable naming
```plang
- read file.txt into data
- hash data write to hashedData
- get user from db, id=%userId%, write %User%
```

#### ✅ CORRECT: Consistent %variable% syntax
```plang
- read file.txt into %data%
- hash %data%, write to %hashedData%
- select * from users where id=%userId%, return 1, write to %user%
```

**Rule**: ALWAYS use `%variableName%` syntax for variables.

### Database Operations

#### ❌ INCORRECT: Using "create" for inserting records
```plang
- create user in database, name=%name%, email=%email%
```

**Problem**: "Create" can be interpreted as creating a table or database.

#### ✅ CORRECT: Use "insert" for records
```plang
- insert into users, name=%name%, email=%email%
```

### Goal Calling

#### ✅ CORRECT: Use call goal for goal calls
```plang
- call goal ProcessUser %userData%
```

### Error Handling

#### ❌ INCORRECT: No validation before operations
```plang
CreateUser
- hash %password%, write to %hashedPassword%
- insert into users, %email%, %hashedPassword%
```

#### ✅ CORRECT: Validate inputs first
```plang
CreateUser
- make sure %email% is not empty
- make sure %password% is not empty
- make sure %email% contains @
- hash %password%, write to %hashedPassword%
- insert into users, %email%, %hashedPassword%
```

See validation.md

### Module Hints

#### ❌ INCORRECT: Ambiguous step causing wrong module selection
```plang
- generate list and write it
```

#### ✅ CORRECT: Use module hints with []
```plang
- [code] generate list of number from 1 to 10, write to %numbers%
```

**Available hints**: `[file]`, `[db]`, `[http]`, `[llm]`, `[message]`, `[code]`, `[crypto]`, and more

### Async Operations

#### ❌ INCORRECT: Blocking on slow operations
```plang
ProcessOrder
- call goal AnalyzeOrder %order%  // Takes 30 seconds
- send confirmation email
```

#### ✅ CORRECT: Use "don't wait" for slow operations
```plang
ProcessOrder
- call goal AnalyzeOrder %order%, don't wait
- send confirmation email
```

### File Paths

#### ✅ CORRECT: Explicit paths or current directory
```plang
// read data.json located in a sub folder config
- read config/data.json into %config%
// OR
- read %fileName% into %data%
// OR read data.json from config located in root of app
- read /config/data.json into %config%
```

### Caching

#### ❌ INCORRECT: No caching for expensive operations
```plang
GetWeather
- get https://api.weather.com/forecast/%city%
- write out result
```

#### ✅ CORRECT: Cache expensive API calls
```plang
GetWeather
- get https://api.weather.com/forecast/%city%, write to %forecast%
    cache for 15 min, key "weather_%city%"
- write out %forecast%
```

### Template engine

plang uses Scriban template engine. Use Scriban syntax for UI (html)

See user-interface.md

## Anti-Patterns to Avoid

### 1. Mixing Concerns in Goals

**Anti-pattern**: One goal doing too many unrelated things
```plang
ProcessUser
- validate user data
- send welcome email
- update analytics
- charge credit card
- generate invoice
```

**Better**: Split into focused goals
```plang
ProcessUser
- call ValidateUser %userData%
- call ChargeUser %userData%
- call SendWelcomeEmail %userData%
- call UpdateAnalytics %userData%, don't wait
```

### 2. Hard-coding Configuration

**Anti-pattern**: Hard-coded values
```plang
- get https://api.production.com/data
- connect to database "server=prod.db.com"
```

**Better**: Use Settings
```plang
- get %Settings.ApiUrl%/data
- post http://..
    Bearer %Settings.ServiceNameBearerToken%
```

### 3. Not Using %Identity% for Authentication

**Anti-pattern**: Username/password in Plang apps
```plang
Login
- get %email% and %password% from form
- check credentials in database
```

**Better**: Use %Identity% for authentication

A good convention is to create a users table in Setup.goal, with a column identity. it can then be used to register user or check if he is allowed

Here is only registered users are allowed

```plang
AuthenticateUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user.id% is empty, throw 401 "Unauthorized"
```

Here we register user if he does not exists

```plang
AuthenticateUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user.id% is empty then
    - insert into users, identity=%Identity%, write to %userId%
    - select * from users where identity=%Identity%, return 1, write to %user%
```

### 4. Simple Authentication

create role column in users table, with the content ["user"] or ["user", "admin"]

```plang
AuthorizeUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user.role% contains "admin" then call goal IsAdmin
```


### 5. Over-using LLM Module

**Anti-pattern**: Using [llm] for simple operations
```plang
- [llm] convert %text% to uppercase, write to %upper%
```

**Better**: Use [code] for simple operations
```plang
- [code] convert %text% to uppercase, write to %upper%
```

**Rule**: Use [llm] only for abstract/complex parsing. Use [code] for simple transformations.


### Refactoring: Breaking Up Large Goals

**Before**:
```plang
// ProcessOrder.goal - 50+ steps
ProcessOrder
- [50+ steps of mixed logic]
```

**After**:
```plang
// ProcessOrder.goal
ProcessOrder
- call goal ValidateOrder %order%
- call goal CalculateTotals %order%
- call goal ChargePayment %order%
- call goal CreateInvoice %order%
- call goal SendConfirmation %order%

// ValidateOrder.goal
// CalculateTotals.goal
// etc.
```

## Performance Best Practices

### 1. Keep Steps Simple

**Wrong And will not work**: Complex multi-part steps will NOT work and fails at build time.

```plang
- get user from db, calculate their age, check if over 18, and send email if true
```

**Correct**: Separate steps
```plang
- select birthdate from users where id=%userId%, write to %birthdate%
- [code] calculate age from %birthdate%, write to %age%
- if %age% >= 18
    - send email to %userEmail%
```

### 2. Use Caching Appropriately

```plang
// Cache expensive operations
- get https://api.slow.com/data, write to %data%
    cache for 1 hour, key "api_data"

// Don't cache rapidly changing data
- select balance from accounts where id=%accountId%  // No cache
```

### 3. Batch Database Operations

**Slow**: One at a time
```plang
- go through %users%, call goal UpdateUser

UpdateUser
- update users set processed=true where id=%item.id%
```

**Fast**: Batch update
```plang
- update users set processed=true where id in (%userIds%)
```

## Security Best Practices

### 1. Always Validate Input

```plang
CreateUser
- make sure %email% is not empty
- make sure %email% contains @
- make sure %password% length > 8
```
See validation.md

### 2. Use %Identity% Not Passwords

```plang
// All HTTP requests are automatically signed
// Server-side:
AuthenticateRequest
- select user_id from users where identity=%Identity%
```

### 3. Don't Store Sensitive Data

**Bad**: Storing plain passwords
```plang
- insert into users, email=%email%, password=%password%
```

**Good**: Hash sensitive data
```plang
- hash %password%, write to %hashedPassword%
- insert into users, email=%email%, password=%hashedPassword%
```

## Code Organization

### Goal Size Guidelines

- **Small goals**: 1-5 steps (ideal for reusable operations)
- **Medium goals**: 6-15 steps (typical business logic)
- **Large goals**: 16-30 steps (complex workflows, consider breaking up)
- **Too large**: 30+ steps (definitely break into smaller goals)

### Naming Conventions

- **Goals**: PascalCase (CreateUser, ProcessOrder, SendEmail)
- **Variables**: camelCase (%userId%, %orderTotal%, %emailAddress%)
- **Folders**: lowercase (api, events, setup)
- **Files**: PascalCase.goal (CreateUser.goal, ProcessOrder.goal)

### Comments

```plang
CreateUser
// This validates the user input
- make sure %email% is not empty
- make sure %password% is not empty

// Hash the password for security  
- hash %password%, write to %hashedPassword%

// Store in database
- insert into users, %email%, %hashedPassword%
```

**Rule**: Use `/` for single-line comments. Comments explain *why*, not *what*. /* */ for multiline comment

## Summary of Critical Rules

1. ✅ **Setup.goal** = ONE-TIME initialization (tables, config)
2. ✅ **Start.goal** = Application ENTRY POINT (webserver, listeners)
3. ✅ Always use `%variable%` syntax
4. ✅ Use `insert` not `create` for database records
5. ✅ Validate inputs before processing
6. ✅ Use `don't wait` for slow operations
7. ✅ Cache expensive operations
8. ✅ Use %Identity% for authentication
9. ✅ Keep steps simple (one operation per step)