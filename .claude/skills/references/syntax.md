# Plang Syntax Reference

## Core Concepts

Plang is a natural language programming language with goal-based architecture. Each file is called a "goal" and contains steps written in pseudo-natural language.

### Goal Structure

```plang
GoalName
- step 1
- step 2
- step 3
```

### Comments

```plang
/ This is a single-line comment
/* This is a
   multiline comment */
```

## Variables

Variables are wrapped with `%`, e.g. `%name%`. They can contain any type of object and sub-properties are accessible via dot notation.

The underlying object is C#, which means you can use C# properties and methods (case-insensitive):
```plang
%now.AddDays(1).ToString("g")%
%user.name.ToUpper()%
```

### Setting Variables

```plang
- set %variableName% = "value"
- set %number% = 42
- set int %amount% = 5000
```

### Default Values

```plang
- set default value %message% = %!error.Message%
- set default %limit% = 50 (max 100)
- set default value of %page% = 1
```

### Variable Operations

```plang
- calc %page%+1, write to %nextPage%
- hash "%text%", type="sha256", write to %signature%
- hash hmac sha256, "%phoneNumber%%amount%", secret: %Settings.AurWebSecret%, write to %hmac%
- hash sha256 %email%, write to %hashedEmail%
```

### Writing to Sub-Properties

You can select from database and write directly into a sub-property:

```plang
- select * from orders where id=%id%, return 1, write to %order%
- select * from orderItems where orderId=%id%, write to %order.items%
```

### System Variables

- `%now%` - Current datetime
- `%Now.ticks%` - Current timestamp
- `%Identity%` - Identity of the request (public key from signed request)
- `%MyIdentity%` - Identity of the PLang app itself
- `%request.query.*%` - Query parameters
- `%request.body.*%` - POST body data
- `%request.form.*%` - Form data
- `%!error.*%` - Error information (in error handlers)
- `%!event.*%` - Event metadata
- `%!step.*%` - Current step metadata
- `%!goal.*%` - Current goal metadata
- `%!Signature%` - Signature of the current request

### Debug & Runtime Variables

These variables provide runtime introspection for debugging and advanced scenarios:

- `%!memoryStack%` - Current memory state (all variables as JSON)
- `%!callStack%` - Call stack object
- `%!callStack.Depth%` - Current call depth (number)
- `%!callStack.Frames%` - List of goal frames in the call stack

**Example - Debug output:**
```plang
DebugStep
- write out "Memory: %!memoryStack%"
- write out "Call depth: %!callStack.Depth%"
- write out "Current step: %!step.Text%"
```

### Time Operations

```plang
- %now.AddDays(-3)%
- %now.AddMonths(-3)%
- %now-10m% (10 minutes ago)
- %now-15min%
- %now.Year%
```

### Variable Scope

Variables are global for the running context. Be careful not to overwrite them:

```plang
/ ❌ INCORRECT: Overwriting variable
ProcessData
- read "config.json", write to %data%
- call goal LoadMetadata
- write out %data%  / Only contains metadata, since LoadMetadata overwrote %data%

LoadMetadata
- read "metadata.json", write to %data%  / Overwrites!

/ ✅ CORRECT: Use distinct variable names
ProcessData
- read "config.json", write to %data%
- call goal LoadMetadata
- write out %data%  / Contains config data as expected

LoadMetadata
- read "metadata.json", write to %metadata%
```

### Route Parameters (Preferred Pattern)

Instead of extracting from request body, use route parameters:

```plang
/ In Start.goal
- add route /product/%product.id%, call goal Product

/ In Product.goal
Product
/ %product.id% comes from route, then %product% gets populated by query
- select * from products where id=%product.id%, return 1, write to %product%
/ Now %product% has id (from route) + all columns from query
```

With validation:
```plang
- add route /product/%product.id%(number > 0), call goal Product
- add route /product/%product.id%(number > 0), POST, call goal SaveProduct
- add route /activate/%status%(bool), POST, call goal Activate
```

### Variable Syntax Rules

**Always use `%variableName%` syntax:**

```plang
/ ❌ INCORRECT
- read file.txt into data
- hash apiKey write to hashedKey

/ ✅ CORRECT
- read file.txt into %data%
- hash %apiKey%, write to %hashedKey%
```

**Don't create unnecessary intermediate variables:**

```plang
/ ❌ INCORRECT
- set %name% = %contract.name%
- write out "%name%"

/ ✅ CORRECT
- write out "%contract.name%"
```

## Identity Variable

`%Identity%` is the public key from the client's cryptographic key pair, used for authentication.

### How Identity Works

1. **Private key on client**: Browser/app holds the private key
2. **Requests are signed**: Every request is signed with the private key
3. **Server validates**: PLang framework validates the signature
4. **`%Identity%` set**: If valid, `%Identity%` = public key; if invalid, request rejected

**Key concept:**
- `%Identity%` not empty = **Authenticated** (valid signed request)
- Authenticated ≠ Authorized (authorization is your business logic)

### First Visit (Empty Identity)

```plang
Landing
- if %Identity% is empty then
    / First visit - render page so browser can create identity
    - [ui] render "landing.html", navigate
    - end goal
/ Identity exists from signed request
- call goal LoadUser
```

### Loading User from Identity

```plang
/ Auto-create user if not exists
LoadUser
- select * from users where identity=%Identity%, return 1, write to %user%
- if %user% is empty then
    - insert into users, identity=%Identity%, created=%now%, write to %user.id%
    - select * from users where id=%user.id%, return 1, write to %user%
```

See [security.md](security.md) and [patterns-and-conventions.md](patterns-and-conventions.md) for multi-device patterns with email verification.

## Database Operations

### Select Queries

```plang
- select * from users where id=%userId%, return 1, write to %user%
- select id, name from products where status='published' limit 100, write to %products%
- select count(*) as total from orders, return 1, write to %count%
```

### Insert Operations

```plang
- insert into users, email=%email%, created=%now%, write to %userId%
- upsert into table, id=%id%(unique), name=%name%
```

### Update Operations

```plang
- update users set email=%email% where id=%userId%
- update products set status='published' where status is null, write to %affectedRows%
```

### Delete Operations

```plang
- delete from orders where status='pending'
```

### Transactions

```plang
- begin transaction "data", "analytics"
- insert into orders ...
- insert into orderItems ...
- end transaction
- commit
- roll back transaction
```

### Data Sources

```plang
- create datasource "name"
- create datasource "/users/%user.id%"
- select * from table, datasource: "users/%userId%"
- ds: "data"
```

### Table Creation

```plang
- create table users, columns:
    email(string, not null, unique),
    created(datetime, now),
    role(string, default '["customer"]')
- create table orders, columns:
    userId(number, foreign key to users.id),
    amount(number, not null),
    status(enum('pending', 'paid', 'failed'))
```

## HTTP Operations

### GET Requests

```plang
- get https://api.example.com/data
    x-api-key: %apiKey%
    write to %response%
```

### POST Requests

```plang
- post https://api.example.com/endpoint
    headers:
        "X-API-key": "%apiKey%"
    data: {
        "field": "value",
        "amount": %amount%
    }
    write to %result%
```

### Download

```plang
- [http] download "https://example.com/file.csv", write to %content%
```

### Error Handling

```plang
- get https://api.example.com/endpoint
    on error status code = 404, call HandleNotFound
    on error status code = 423, call HandleLocked
    on error 'timeout', call WaitAndRetry
    write to %result%
```

## File Operations

### Reading Files

```plang
- read file.txt, write to %content%
- read /path/to/data.xlsx, first row is header, write to %data%
- read /path/to/data.csv, delimiter=";", first row is header, write to %rows%
- read jsonl "file.txt", write to %lines%
- read base64 of %path%, include data url, write to %base64%
```

### Writing Files

```plang
- write %data% to "output.json"
- write %data% to "output.json", overwrite
- write %content% to file.txt
```

### File System

```plang
- get all ".csv" files in "csv", write to %files%
- get all ".json" files in folder, write to %jsonFiles%
- copy source.sqlite, to dest.sqlite
- delete file.txt
- zip %file%, to %output%.zip
```

## Control Flow

### Conditionals

**Note**: PLang has NO standalone `else` blocks. Use inline else with goal calls.

```plang
- if %value% is empty then
  - call goal HandleEmpty
  - end goal
- if %status% == "Completed" then MarkOrder, else RedirectToFrontPage
- if %user.role% contains 'admin' then call RenderAdmin
- if %result.code% == 0 then call Success, else Error
```

**❌ Wrong — Standalone else block (doesn't exist):**
```plang
- if %value% is empty then
    - call goal HandleEmpty
- else
    - call goal HandleValue
```

**✅ Correct — Inline else with goal calls:**
```plang
- if %value% is empty then call HandleEmpty, else call HandleValue
```

### Loops

**CRITICAL**: Loops MUST call a separate goal - inline code is not allowed.

```plang
- foreach %items% call ProcessItem item=%item%
- foreach %items%, split into 20 items per list, call Process
- foreach %items%, split in 100, call ProcessUsers item=%groupedUsers%
- go through %items%, split into 1000, call ProcessBatch item=%ids%
```

**Automatic loop variables** (available in the called goal):
- `%item%` — Current item being processed
- `%position%` — Current index (0-based)

**❌ Wrong - No inline code in loops:**
```plang
- foreach %items%
    - insert into table, field=%item.value%
```

**✅ Correct - Call a goal:**
```plang
- foreach %items%, call ProcessItem

ProcessItem
- insert into table, field=%item.value%
```

## UI Operations

### Rendering Templates

```plang
- [ui] render "template.html", navigate and scroll to top
- [ui] render template "path.html", cssSelector:"#main", action:append
- [ui] render "template.html", target="#container" and replace
- [ui] set "%cssFramework%/layout.html" as default layout
```

### DOM Manipulation

```plang
- [ui] set checked="checked" attribute on "#checkbox"
- [ui] remove '#element'
- set element "#id"="<em>%value%</em>"
- set '#text' = "Value: %value%"
- set class="slide-up" to #element
```

### User Interaction

```plang
/ Simple form
- ask user template: "form.html"
    write to %result%

/ With validation callback
- ask user template: "signup.html"
    on callback: ValidateEmail
    write to %answer%

/ With callback data (use variable directly - keeps its name after callback)
- ask user template: "confirm.html"
    call back data: %order.id%
    show as modal
    write to %confirmation%

/ Multiple callback data
- ask user template: "details.html"
    call back data: %order.id%, %customer.name%
    show as modal
    write to %result%
```

**Note:** Variables in `call back data` keep their original names after the form submission.

## LLM Integration

### Basic LLM Call

```plang
- [llm] system: %system%
    user: %userPrompt%
    write to %result%
```

### Structured Output

```plang
- [llm] system: %system%
    user: %input%
    model: "gpt-4o"
    scheme: {field1:string, field2:number}
    write to %response%
```

### Image Input

```plang
- [llm] system: %system%
    image: %base64%
    model: 'o1'
    scheme: {modules:[{name:string, html:string}]}
    write to %page%
```

## Goal Control

### Calling Goals

```plang
- call goal ProcessData
- call goal /path/to/Goal param1=%value%
- call goal HandleError, dont wait
- call goal UpdateStatus item=%item%, method="Aur"
```

### Goal Ending

```plang
- end goal
- end goal, and previous
- end goal and 2 levels up
```

### Error Handling

```plang
- call goal Process
    on error call HandleError
- call goal Fetch
    on error status code = 404, call NotFound
```

## Events System

```plang
Events
- before each goal(including private) in /admin/.*, call CheckAdmin
- on app error, call goal HandleError
- after each step, call LogStep
```

## Special Operations

### Validation

```plang
- validate %email% is not empty, "Email is required"
- validate that %type%, %message% is not empty
- validate %phoneNumber% is not empty and 7 numbers, "Phone must be 7 digits"
```

### Assertions (Testing)

```plang
- assert %guid% equals "expected"
- assert %value% is not empty
```

### Code Execution

```plang
- [code] run "Script.cs", write to %result%
- run csharp "ParseQuery.cs" q=%q%, write to %parsed%
```

### Terminal

```plang
- terminal git pull
    working dir: "../../"
    on error ignore error
    write to %output%
- run terminal "command", write to %result%
```

## Data Manipulation

### Filtering

```plang
- filter %items% where "status" = "active", get parent, write to %filtered%
- filter %prices% where "type"="digital-list-price", extract "value" property, first, write %dlp%
```

### Grouping

```plang
- group %items% on "category", write to %grouped%
- group %orders% by "id", write to %orders%
```

### Sorting

```plang
- select * from products order by created desc
```

### List Aggregation & Access

PLang provides powerful list operations directly on variables:

```plang
/ Aggregation on list properties
- %items.price.sum%         / Sum of all price values
- %items.price.avg%         / Average (also: average, mean)
- %items.price.max%         / Maximum value
- %items.price.min%         / Minimum value
- %items.price.range%       / max - min
- %items.price.median%      / Median value
- %items.price.mode%        / Most common value
- %items.price.stddev%      / Standard deviation
- %items.price.variance%    / Variance

/ List access
- %items.count%             / Number of items
- %items.first%             / First item
- %items.last%              / Last item
- %items.random%            / Random item
- %items.elementat(3)%      / Item at index 3

/ Percentile (0-100)
- %items.price.percentile(90)%  / 90th percentile
```

**Examples:**
```plang
/ Get total order amount
- set %total% = %order.items.price.sum%

/ Get random product for featured section
- set %featured% = %products.random%

/ Calculate average rating
- set %avgRating% = %reviews.rating.avg%
```

## Web Server

```plang
Start
- start webserver, port: %port%, host: %host%,
    on start call OnStartWebserver
    on poll start call OnConnect
    on begin request call OnRequestBegin
```

## Output

### Console Output

```plang
- write out "Message: %value%"
- write out to system "Debug info"
- write out to user log "Processing..."
```

### Return Values

```plang
- return %result%
- return variable %value%
```

## Environment & Settings

```plang
- [plang] set environment "Test"
- get cache "key", write to %value%
- set cache "key" = %value%, for 20 min from last access
- remove cache "key"
- [env] get all settings, write to %settings%
- load %variable%, set = 1 if empty
- store %variable%
```

## String Operations

```plang
- convert 'md' to "html" on %text%, write to %html%
- render "template.html", write to %html%
- %text.ClearHtml()%
- split on new line, %text%, write to %lines%
- %text.noformatting()%
```
