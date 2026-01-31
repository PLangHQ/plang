# Plang Patterns and Conventions

Best practices, common patterns, and mistakes to avoid.

## Critical: Setup vs Start

**CRITICAL RULE**: Setup is for ONE-TIME initialization, Start is the application ENTRY POINT when no goal is specified.

```bash
plang          # Runs Start.goal (no goal specified)
plang Send     # Runs Send.goal (Start.goal is NOT run)
```

### How They Work

**Setup.goal:**
- Always checks if each step should run (tracked in system.sqlite)
- Each step only executes ONCE in the lifetime of the application
- If a step has run before, it's skipped
- Great for migrations — add new steps at the bottom

**Start.goal:**
- Entry point ONLY if no goal is specified at startup
- If you run `plang MyGoal`, Start.goal is skipped entirely
- Runs every time (when applicable)

### ❌ INCORRECT: Creating data/schema in Start.goal
```plang
// Start.goal - WRONG!
Start
- create table 'users', columns: id(int, pk), name(string)
- start webserver on port 8080
```

**Problem**: Plang will throw build and runtime error when create table is executed in a non-setup file.

### ✅ CORRECT: Separate Setup from Start
```plang
// Setup.goal
Setup
- create table 'users', columns: id(int, pk), name(string), email(string, not null)

// Start.goal
Start
- start webserver, on start call AddRoutes
```

Webserver default port is 8080.

## Common Patterns

### Database Patterns

#### User Management

**Simple approach (one identity per user)** - suitable for desktop apps:
```plang
LoadUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user% is empty then
    - throw "User not found"

CreateUser
- insert into users, identity=%Identity%, created=%now%, write to %user.id%

UpdateUser
- update users set updated=%now% where id=%user.id%
```

**Multi-identity approach (recommended for web apps)** - allows multiple identities per user:

Schema:
```plang
Setup
- create table users, columns: id(int, pk), created(datetime, default now)
- create table identities, columns: 
    identity(string, not null, unique), 
    userId(number, foreign key to users.id)
```

How identities are connected and validated (OTP, email code, etc.) is up to the developer.

#### Transaction Pattern
```plang
ProcessOrder
- begin transaction "users/%userId%"
- insert into orders, status='pending', amount=%total%, write to %orderId%
- foreach %cartItems% call CreateOrderItem
- end transaction
```

#### Upsert Pattern
```plang
- upsert into table, id=%id%(unique), name=%name%, updated=%now%
```

### API Integration Patterns

#### Authenticated API Call
```plang
CallApi
- post %apiUrl%/endpoint
    headers:
        "X-API-key": "%Settings.ApiKey%"
    data: {
        "field": "%value%"
    }
    on error call HandleApiError
    write to %result%
```

#### Pagination Pattern
```plang
GetAllPages
- set default value %page% = 1
- get %apiUrl%/data?page=%page%
    write to %result%
- if %result.page% != %result.total_pages% then
    - call NextPage page=%page+1%
```

#### Retry Pattern
```plang
FetchWithRetry
- get %url%
    set timeout 5 min
    on error 'timeout', retry 3 times every 10 sec
    on error 'host', WaitAndRetry
    write to %data%

WaitAndRetry
- wait 5 seconds
- retry step
```

### Payment Processing Patterns

#### Payment Gateway Flow
```plang
InitiatePayment
- validate %amount% is not empty and larger than 0
- insert into transactions, userId=%user.id%, orderId=%order.id%, 
    status='pending', write to %transactionId%
- post %Settings.PaymentGatewayUrl%/charge
    headers:
        "X-API-key": "%Settings.PaymentApiKey%"
    data: {
        "amount": %amount%,
        "reference": "%order.id%"
    }
    write to %result%

CheckPaymentStatus
- select * from transactions where status='pending' and created < %now-15min%
    write to %pendingTransactions%
- foreach %pendingTransactions% call CheckTransaction item=%transaction%
```

#### Refund Pattern
```plang
ProcessRefund
- begin transaction
- select * from orders where id=%orderId%, return 1, write to %order%
- insert into orders, status='pending', type='credit', 
    amount=%refundAmount%, originalOrderId=%orderId%
    write to %creditOrderId%
- call goal ExecuteRefund
- end transaction
```

### LLM Processing Patterns

#### Structured Data Extraction
```plang
AnalyzeContent
- read file system.llm, write to %system%
- [llm] system: %system%
    user: %content%
    scheme: {
        category: string,
        summary: string,
        tags: string[]
    }
    write to %analysis%
```

**When .llm file contains %variables%**, use `load vars` to substitute them:
```plang
ProcessWithContext
- read file analyzer.llm, load vars, write to %system%
- [llm] system: %system%
    user: %userInput%
    scheme: {result:string}
    write to %response%
```

#### Batch Processing with LLM
```plang
ProcessDocuments
- select * from documents where processed is null
    write to %documents%
- foreach %documents%, split into 20 items, call AnalyzeBatch item=%batch%

AnalyzeBatch
- [llm] system: %system%
    user: %batch%
    scheme: [{id:number, category:string, summary:string}]
    write to %results%
- foreach %results% call SaveAnalysis item=%result%
```

### File Processing Patterns

#### CSV Import
```plang
ImportCSV
- read data.csv, first row is header, write to %rows%
- foreach %rows% call ProcessRow item=%row%

ProcessRow
- insert into table, column1=%row.header1%, column2=%row.header2%
```

#### Excel Processing
```plang
ImportExcel
- read data.xlsx, first row is header, write to %sheets%
- foreach %sheets.SheetName% call ProcessSheet item=%row%
```

#### Batch File Processing
```plang
ProcessFiles
- get all ".json" files in folder, write to %files%
- foreach %files%, call ProcessFile item=%file%

ProcessFile
- read %file.path%, write to %data%
- call goal HandleData
```

### Search Patterns

#### Full-Text Search
```plang
Search
- set default %q% = %request.query.q%
- SELECT * FROM products_fts
    JOIN products p ON p.id = products_fts.rowid
    WHERE products_fts MATCH %q%
    ORDER BY bm25(products_fts)
    LIMIT 50
    write to %results%
```

#### Filter Pattern
```plang
FilterProducts
- select * from products where status='published'
- if %request.query.category% is not empty then
    - filter %products% where "categoryId" = %request.query.category%, write to %products%
- if %request.query.minPrice% is not empty then
    - filter %products% where "price" >= %request.query.minPrice%, write to %products%
```

### Caching Patterns

#### Cache with Fallback
```plang
GetData
- get cache "data_key", write to %data%
- if %data% is empty then
    - call goal FetchFreshData
    - set cache "data_key" = %data%, for 10 min

FetchFreshData
- select * from expensive_query, cache "data_key" for 10 min, write to %data%
```

### Authentication Patterns

#### Understanding %Identity%

In PLang, authentication is handled through cryptographic signing:
- The private key lives on the **client**
- Every request is signed with this private key
- The PLang framework parses the signature and sets `%Identity%` to the public key
- If the signature is invalid, the request is rejected
- **If `%Identity%` is not empty, the request is authenticated** (but not necessarily authorized)

**Key distinction:**
- **Authenticated** = valid signed request (`%Identity%` is not empty)
- **Authorized** = user has permission to perform the action (your business logic)

See [security.md](security.md) for comprehensive `%Identity%` documentation.

#### Simple Setup (One Identity Per User)

For simple apps where each identity maps to one user. See [security.md](security.md) for more details.

```plang
/ In Events.goal or on webserver request
Events
- before each goal, call LoadUser

/ Or in Start.goal
Start
- start webserver, on request call LoadUser

LoadUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user% is empty then
    - insert into users, identity=%Identity%, created=%now%, write to %user.id%
    - select * from users where id=%user.id%, return 1, write to %user%
/ %user% is now available throughout the request
```

#### Recommended Setup (Multiple Identities Per User)

For web apps where users may access from multiple devices, use separate `identities` and `users` tables:

**Schema (Setup.goal):**
```plang
Setup
- create table users, columns:
    email(string, not null, unique),
    created(datetime, default now),
    lastAccess(datetime, default now),
    role(string, default '["user"]', not null),
    termsSignature(string)

- create table identities, columns:
    identity(string, not null, unique),
    userId(number, foreign key to users.id, null),
    accessCode(string),
    accessCodeCreated(datetime),
    created(datetime, default now)
```

**Flow:**
1. User visits site → identity created in `identities` table (no user yet)
2. User wants to access account → check if identity has userId
3. If no userId → ask for email, send verification code
4. User enters code → create user row, link identity to user
5. Same user on new device → new identity, verify email+code, link to existing user

See [security.md](security.md) for more details on `%Identity%`.

**Load identity on every request:**
```plang
LoadIdentity
- select * from identities where identity=%Identity%, return 1, write to %identity%
- if %identity% is empty then
    - insert into identities, identity=%Identity%, created=%now%
    - select * from identities where identity=%Identity%, return 1, write to %identity%
- if %identity.userId% is not empty then
    - select * from users where id=%identity.userId%, return 1, write to %user%
    - update users set lastAccess=%now% where id=%user.id%
```

**Email verification flow:**
```plang
RequestAccess
- make sure %email% is not empty
- make sure %email% contains @
- [code] generate 6 digit code, write to %code%
- update identities set accessCode=%code%, accessCodeCreated=%now% where identity=%Identity%
- send email to %email%, subject: "Your access code", body: "Code: %code%"
- ask user "enter-code.html"
    call back data: %code%, %email%
    validate: ValidateCode
    write to %userCode%
/ Code validated - find or create user
- select * from users where email=%email%, return 1, write to %user%
- if %user% is empty then
    - insert into users, email=%email%, write to %user.id%
    - select * from users where id=%user.id%, return 1, write to %user%
/ Link identity to user
- update identities set userId=%user.id%, accessCode=null where identity=%Identity%
- [ui] render "dashboard.html", navigate

ValidateCode
- if %code% != %userCode% then throw error "Invalid code"
- select * from identities where identity=%Identity%, return 1, write to %identity%
- if %identity.accessCodeCreated% < %now.AddMinutes(-15)% then
    - throw 400 "Code expired"
```

**Recording user consent (terms):**
```plang
AcceptTerms
- update users set termsSignature=%!Signature% where id=%user.id%
```

The `%!Signature%` from the request provides cryptographic proof of consent.

#### Authorization Check

Once authenticated, check authorization for protected actions:

```plang
Events
- before each goal in /admin/.*, call CheckAdmin

CheckAdmin
- if %user% is empty, throw 401 "Not authenticated"
- if %user.role% does not contain "admin", throw 403 "Not authorized"
```

#### Role-Based Access
```plang
Events
- before each goal(including private) in /admin/.*, call CheckAdmin

CheckAdmin
- if %user.role% does not contain "admin" then
    - redirect "/"
```

### Email Patterns

#### Send Email
```plang
SendEmail
- render "email_template.html", write to %body%
- send email %recipient%, %subject%, %body%
    write to %result%
```

#### Email Campaign
```plang
SendCampaign
- select email from users where subscribed=true limit 1000
    write to %recipients%
- foreach %recipients% call SendEmail item=%recipient%
```

### Scheduled Tasks Pattern
```plang
DailySync
- every day at 10am call SyncData

SyncData
- write out "Starting daily sync - %now%"
- call goal SyncData
- call goal CleanupOldData
- write out "Done daily sync - %now%"
```

### Testing Patterns

#### Mock Setup
Mock will map to any module, then method and then parameter for that method.

```plang
SetupMocks
- set environment "test"
- mock http get url:https://api.example.com*, call MockResponse

MockResponse
- read mock_data.json, write to %data%
- return %data%
```

#### Test with Assertions
```plang
TestFunction
- set %input% = "test"
- call goal ProcessInput
- assert %result% equals "expected"
- assert %result% is not empty
```

### Data Migration Pattern
```plang
Migrate
- select * from old_table, write to %records%
- begin transaction "new_datasource"
- foreach %records% call MigrateRecord item=%record%
- end transaction

MigrateRecord
- insert into new_table, 
    field1=%record.old_field1%,
    field2=%record.old_field2%
```

### Variable Loading Pattern

The `load` and `store` commands read/write from the `__Variables__` table in the current datasource:

```plang
LoadSettings
- load %lastCheck%
- if %lastCheck% is empty then
    - set %lastCheck% = %now%
    - store %lastCheck%
```

**Note:** `load %variable%` reads from `__Variables__` table. `store %variable%` writes to it. This persists values across goal executions.

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

**Better**: Use %Identity% for authentication. Create a users table in Setup.goal with an identity column:

```plang
/ Only registered users allowed
AuthenticateUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user.id% is empty, throw 401 "Unauthorized"

/ Auto-register users
AuthenticateUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user.id% is empty then
    - insert into users, identity=%Identity%, write to %userId%
    - select * from users where identity=%Identity%, return 1, write to %user%
```

### 4. Simple Authorization
Create role column in users table with content like `["user"]` or `["user", "admin"]`:

```plang
AuthorizeUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user.role% contains "admin" then call goal IsAdmin
```

### 5. Over-using LLM Module

**Anti-pattern**: Using [llm] for operations that can be done with built-in methods or [code]
```plang
/ ❌ Don't use LLM for simple string operations
- [llm] convert %text% to uppercase, write to %upper%
```

**Better**: Use built-in C# methods on variables
```plang
/ ✅ Use built-in method
- set %upper% = %text.ToUpper()%
```

**When to use [code]** - for conditional logic that's too complex for simple expressions:
```plang
/ Complex conditional mapping
- [code] if %status% contains "pending" then "waiting", 
    if contains "approved" then "ready", 
    if contains "rejected" then "failed",
    else "unknown", 
    write to %displayStatus%
```

**Rule**: Use [llm] only for abstract/complex parsing where human-like understanding is needed. Use built-in methods or [code] for deterministic transformations.

## Performance Best Practices

### 1. Calculations: NCalc vs [code] Module

PLang has NCalc built-in for simple calculations. Use `[code]` module only for custom algorithms with many loops.

**Simple calculations — use NCalc expressions directly:**
```plang
- set %total% = %price% * %quantity%
- set %discount% = %subtotal% * 0.1
- set %nextPage% = %page% + 1
- if %amount% > %threshold% then...
```

**Custom algorithms with loops — use [code] module with C#:**
```plang
/ Vector operations, nested loops, complex data transformations
- [code] compute dot product of %vectorA% and %vectorB%, write to %dot%
- [code] normalize %vector%, write to %normalized%
```

**❌ Bad - PLang loops for heavy calculations (slow):**
```plang
- set %dot% = 0
- set %i% = 0
- foreach %vectorA% call DoCalc

DoCalc
- set %dot% = %dot% + (%item% * %vectorB[i]%)
- calc %i% + 1, write to %i%
```

**Note:** Array index syntax uses `%array[i]%` where `i` is not wrapped in `%`. PLang expects an integer there - if it's not a literal integer, it assumes it's a variable name.

Use `[code]` for:
- Custom algorithms with many loops
- Vector/array math (dot products, cosine similarity, normalization)
- Any element-by-element array operations
- Nested loops over large datasets

**Do NOT use [code] for:**
- Simple math (use NCalc: `%a% + %b%`, `%x% * %y%`)
- Cryptographic operations (use CryptographicModule: `hash`, `encrypt`, `sign`)

See [modules.md](modules.md#codemodule) for detailed examples.

### 2. Keep Steps Simple

**Wrong (will fail at build time)**: Complex multi-part steps
```plang
- get user from db, calculate their age, check if over 18, and send email if true
```

**Correct**: Separate steps
```plang
- select birthdate from users where id=%userId%, write to %birthdate%
- [code] calculate age from %birthdate% (yyyy-mm-dd), write to %age%
- if %age% >= 18
    - send email to %userEmail%
```

### 3. Use Caching Appropriately
```plang
// Cache expensive operations
- get https://api.slow.com/data, write to %data%
    cache for 1 hour, key "api_data"

// Don't cache rapidly changing data
- select balance from accounts where id=%accountId%  // No cache
```

### 4. Batch Database Operations

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

## PLang-Specific Coding Rules

### String Interpolation — Never Concatenate

**CRITICAL**: Never concatenate strings with `+`. Embed variables directly in the string.

**❌ Wrong — String concatenation:**
```plang
- set %path% = "/folder/" + %name% + ".goal"
- write out "Total: " + %count% + " items"
```

**✅ Correct — Direct interpolation:**
```plang
- set %path% = "/folder/%name%.goal"
- write out "Total: %count% items"
```

Works with object properties too:
```plang
- write out "User: %user.name% (%user.email%)"
- set %message% = "Order #%order.id% - %order.status%"
```

### Empty Value Handling

PLang handles empty/null values naturally. Don't add unnecessary checks:

**❌ Unnecessary:**
```plang
- if %items% is not empty then
    - append %newItem% to %items%
```

**✅ Just do it:**
```plang
- append %newItem% to %items%
```

### Variable Initialization

Variables don't need pre-initialization. However, for aggregation you need to use a goal:

**❌ Unnecessary initialization and wrong syntax:**
```plang
- set %total% = 0
- foreach %items% call AddToTotal

AddToTotal
- set %total% = %total% + %item.price%
```

**✅ Let PLang handle it:**
```plang
- foreach %items% call AddToTotal

AddToTotal
- set %total% = %total% + %item.price%
```

**Note:** For simple aggregations, consider using `[code]` module for better performance:
```plang
- [code] sum price property of %items%, write to %total%
```

### Error Handling Must Call Goals

The `on error` clause must always call a goal, not inline code:

**❌ Invalid:**
```plang
- get https://api.example.com
    on error set %result% = "failed"
```

**✅ Valid:**
```plang
- get https://api.example.com
    on error call HandleApiError
```

### Foreach Doesn't Need Empty Guards

**❌ Unnecessary:**
```plang
- if %users% is not empty then
    - foreach %users% call ProcessUser
```

**✅ Just iterate:**
```plang
- foreach %users% call ProcessUser
```

Foreach on empty list simply does nothing.

### Use Scriban Templates for Complex Conditionals

When building LLM prompts with complex conditional logic, use Scriban templates:

**✅ In template file (prompt.txt):**
```
{{ if tables.size > 0 }}
Available tables:
{{ for table in tables }}
- {{ table.name }}: {{ table.columns | array.join ", " }}
{{ end }}
{{ end }}
```

## Module Hints

Use module hints `[text]` when the builder cannot determine which module to use. The hint does a **contains** match on module names - so `[file]` matches `FileModule`, `[db]` matches `DbModule`, etc.

```plang
- [file] read data.txt
- [db] select * from users
- [http] get https://api.example.com
- [code] generate list of number from 1 to 10, write to %numbers%
```

**How it works**: The text inside `[]` is matched against module names using contains. For example:
- `[file]` → matches `FileModule`
- `[db]` → matches `DbModule`  
- `[crypt]` → matches `CryptographicModule`
- `[l]` → would match any module containing "l" (LlmModule, FileModule, etc.)

**Common hints**: `[file]`, `[db]`, `[http]`, `[llm]`, `[message]`, `[code]`, `[crypt]`, `[ui]`

## Async Operations

Use "don't wait" for slow operations:

**❌ Blocking:**
```plang
ProcessOrder
- call goal AnalyzeOrder %order%  // Takes 30 seconds
- send confirmation email
```

**✅ Non-blocking:**
```plang
ProcessOrder
- call goal AnalyzeOrder %order%, don't wait
- send confirmation email
```

## Security Best Practices

### 1. Always Validate Input
```plang
ProcessOrder
- make sure %orderId% is not empty
- make sure %amount% > 0
- make sure %items.count% > 0
```

### 2. Use %Identity% - Never Passwords
```plang
// All HTTP requests are automatically signed
AuthenticateRequest
- select user_id from users where identity=%Identity%
```

**CRITICAL**: PLang is designed for passwordless authentication. Never implement password-based login flows. Use `%Identity%` which is cryptographically signed and unforgeable.

### 3. Hash Sensitive Data When Required

If you must store sensitive data (API keys from users, etc.), hash it:
```plang
- hash %sensitiveData%, write to %hashedData%
- insert into secrets, data=%hashedData%
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
ProcessOrder
/ Validate the order before processing
- make sure %orderId% is not empty
- make sure %items% is not empty

/ Calculate totals including tax  
- call goal CalculateTotals

/ Save to database
- insert into orders, %orderId%, %total%, %tax%
```

**Rule**: Use `/` for single-line comments. Use `/* */` for multiline. Comments explain *why*, not *what*.

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
```

## Advanced Build Patterns

### Builder for Custom Module Logic

Each module can have a `Builder.cs` (or `Builder.goal`) file that runs during the build process. Builders can run:
- **Before LLM analysis** - validate constraints, gather context
- **After method selection** - if there's a `Builder{MethodName}` method, it runs after that specific method is selected

This enables:
- Build-time validation of step constraints
- Gathering context (datasources, table schemas)
- Creating `%assistant%` variable appended to LLM request
- Post-selection validation and customization

**Example - DbModule/Builder.goal:**
```plang
Builder
/ Use LLM for intent detection - NEVER string matching!
/ PLang is natural language and may not be in English
- [llm] system: Determine if this step creates a new datasource/database
    user: %step.Text%
    scheme: {isCreateDatasource: bool, datasourceName: string?}
    write to %analysis%
    
- if %analysis.isCreateDatasource% then
    / Validate setup constraint
    - if %step.IsSetup% is not true then
        - append "Warning: create datasource only allowed in Setup.goal" to %step.LlmComments%

/ Get table schemas for SQL validation
- get database tables, write to %tables%

/ Build assistant context for LLM
- read file assistantContext.llm, load vars, write to %assistant%
```

**Key principle:** Never use string matching like `if %step% contains "create"` - PLang is natural language supporting multiple human languages. Always use LLM for intent detection.

## Summary of Critical Rules

1. ✅ **Setup.goal** = ONE-TIME initialization (tables, config) - each step tracked, runs only once
2. ✅ **Start.goal** = Entry point ONLY when no goal specified (`plang` runs it, `plang MyGoal` skips it)
3. ✅ Always use `%variable%` syntax
4. ✅ **Never concatenate strings** — embed variables directly: `"/path/%name%.goal"`
5. ✅ **foreach MUST call a goal** — no inline code in loops
6. ✅ **Goal names have no prefix** — `call goal ProcessItem` (not `call goal !ProcessItem`)
7. ✅ Use `insert` not `create` for database records
8. ✅ Validate inputs before processing
9. ✅ Use `don't wait` for slow operations
10. ✅ Cache expensive operations
11. ✅ Use %Identity% for authentication - **NEVER passwords**
12. ✅ Keep steps simple (one operation per step)
13. ✅ **Use NCalc for simple math, [code] module for custom algorithms with loops**
14. ✅ **on error must call a goal** (not inline code)
15. ✅ **Don't pre-check empty values** — PLang handles them naturally
16. ✅ **Loop provides %item% and %position%** — use %position% for index (not %idx%)
17. ✅ **Never use string matching for intent** — use LLM (PLang is multi-lingual)
