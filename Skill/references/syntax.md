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
```

## Variable Handling

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

### System Variables

- `%now%` - Current datetime
- `%Now.ticks%` - Current timestamp
- `%Identity%` - User identity
- `%user.id%` - User ID
- `%user.role%` - User role
- `%request.query.*%` - Query parameters
- `%request.body.*%` - POST body data
- `%request.form.*%` - Form data
- `%!error.*%` - Error information
- `%!event.*%` - Event metadata
- `%!step.*%` - Step metadata
- `%!goal.*%` - Goal metadata

### Time Operations

```plang
- %now.AddDays(-3)%
- %now.AddMonths(-3)%
- %now-10m% (10 minutes ago)
- %now-15min%
- %now.Year%
```

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

```plang
- if %value% is empty then
  - call goal HandleEmpty
  - end goal
- if %status% == "Completed" then MarkOrder, else RedirectToFrontPage
- if %user.role% contains 'admin' then call RenderAdmin
- if %result.code% == 0 then call Success, else Error
```

### Loops

```plang
- foreach %items% call ProcessItem item=%item%
- foreach %items%, split into 20 items per list, call Process
- foreach %items%, split in 100, call ProcessUsers item=%groupedUsers%
- go through %items%, split into 1000, call ProcessBatch item=%ids%
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
- ask user template: "form.html"
    write to %result%
- ask user
    render "template.html"
    on callback: ValidateEmail
    call back data: %data%
    write to %answer%
```

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

### Aggregation

```plang
- %items.count%
- %amounts.sum%
- SUM(field) in SQL queries
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
