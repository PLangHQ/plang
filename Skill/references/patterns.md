# Common Plang Patterns

## Database Patterns

### User Management

```plang
LoadUser
- select * from users where id=%userId%, return 1, write to %user%
- if %user% is empty then
    - throw "User not found"

CreateUser
- insert into users, email=%email%, created=%now%, write to %userId%
- call goal SetupUserDatabase

UpdateUser
- update users set email=%newEmail% where id=%userId%
```

### Transaction Pattern

```plang
ProcessOrder
- begin transaction "users/%userId%"
- insert into orders, status='pending', amount=%total%, write to %orderId%
- foreach %cartItems% call CreateOrderItem
- end transaction
```

### Upsert Pattern

```plang
- upsert into table, id=%id%(unique), name=%name%, updated=%now%
```

## API Integration Patterns

### Authenticated API Call

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

### Pagination Pattern

```plang
GetAllPages
- set default value %page% = 1
- get %apiUrl%/data?page=%page%
    write to %result%
- if %result.page% != %result.total_pages% then
    - call NextPage page=%page+1%
```

### Retry Pattern

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

## Error Handling Patterns

### Global Error Handler

```plang
Events
- on app error, call goal HandleError

HandleError
- call goal InsertError
- if %!error.StatusCode% = 415 then call MimeTypeNotSupported, else DefaultError

InsertError
- if %!error.Key% is "UserDefinedError" then
    - end goal
- insert into errors, type=%!error.Key%, message=%!error.Message%, 
    error=%!error.ToString()%, identityId=%user.identityId%
```

### Try-Catch Pattern

```plang
ProcessData
- call goal FetchData
    on error message "file not found" call HandleFetchError    
    on error 404 call HandleFetchError
    on error call HandleFetchError
- call goal SaveData      
    on error key:"FileNotFound" call HandleFetchError  
    on error call HandleSaveError
```

## Payment Processing Patterns

### Payment Gateway Flow

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

### Refund Pattern

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

## UI Patterns

### Form Submission Flow

```plang
ShowForm
- [ui] render "form.html", navigate
- ask user template: "form.html"
    on callback: ValidateForm
    write to %formData%

ValidateForm
- validate %formData.email% is not empty, "Email required"
- validate %formData.terms% is true, "Must accept terms"
- call goal ProcessForm
```

### Modal Dialog Pattern

```plang
ConfirmAction
- ask user template: "confirm.html", open modal
    call back data: orderId=%orderId%
    write to %confirmation%
- if %confirmation.confirmed% then
    - call goal ExecuteAction
```

### Progressive Enhancement

```plang
LoadData
- [ui] render "loading.html", target="#container"
- call goal FetchData
- [ui] render "results.html", target="#container" and replace
```

## LLM Processing Patterns

### Structured Data Extraction

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

### Batch Processing with LLM

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

## File Processing Patterns

### CSV Import

```plang
ImportCSV
- read data.csv, first row is header, write to %rows%
- foreach %rows% call ProcessRow item=%row%

ProcessRow
- insert into table, column1=%row.header1%, column2=%row.header2%
```

### Excel Processing

```plang
ImportExcel
- read data.xlsx, first row is header, write to %sheets%
- foreach %sheets.SheetName% call ProcessSheet item=%row%
```

### Batch File Processing

```plang
ProcessFiles
- get all ".json" files in folder, write to %files%
- foreach %files%, call ProcessFile item=%file%

ProcessFile
- read %file.path%, write to %data%
- call goal HandleData
```

## Search Patterns

### Full-Text Search

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

### Filter Pattern

```plang
FilterProducts
- select * from products where status='published'
- if %request.query.category% is not empty then
    - filter %products% where "categoryId" = %request.query.category%, write to %products%
- if %request.query.minPrice% is not empty then
    - filter %products% where "price" >= %request.query.minPrice%, write to %products%
```

## Caching Patterns

### Cache with Fallback

```plang
GetData
- get cache "data_key", write to %data%
- if %data% is empty then
    - call goal FetchFreshData
    - set cache "data_key" = %data%, for 10 min

FetchFreshData
- select * from expensive_query, cache "data_key" for 10 min, write to %data%
```

## Authentication Patterns

### Login Flow

```plang
Login
- select * from users where email=%email%, return 1, write to %user%
- if %user% is empty then
    - throw "Invalid credentials"
- [code] verify password %password% against %user.passwordHash%, write to %valid%
- if %valid% then
    - set cache "User_%Identity%" = %user%, for 20 min from last access

CheckAuth
- get cache "User_%Identity%", write to %user%
- if %user% is empty then
    - redirect "/"
```

### Role-Based Access

```plang
Events
- before each goal(including private) in /admin/.*, call CheckAdmin

CheckAdmin
- if %user.role% does not contain "admin" then
    - redirect "/"
```

## Email Patterns

### Send Email

```plang
SendEmail
- render "email_template.html", write to %body%
- send email %recipient%, %subject%, %body%
    write to %result%
```

### Email Campaign

```plang
SendCampaign
- select email from users where subscribed=true limit 1000
    write to %recipients%
- foreach %recipients% call SendEmail item=%recipient%
```

## Scheduled Tasks Pattern

```plang
DailySync
- every day at 10am call SyncData

SyncData
- write out "Starting daily sync - %now%"
- call goal SyncData
- call goal CleanupOldData
- write out "Done daily sync - %now%"
```

## Testing Patterns

### Mock Setup

Mock will map to any module, then method and then parameter for that method.

```plang
SetupMocks
- set environment "test"
- mock http get url:https://api.example.com/*, call MockResponse

MockResponse
- read mock_data.json, write to %data%
- return %data%
```

### Test with Assertions

```plang
TestFunction
- set %input% = "test"
- call goal ProcessInput
- assert %result% equals "expected"
- assert %result% is not empty
```

## Data Migration Pattern

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

## Conditional Goal Calling

```plang
ProcessBasedOnType
- if %type% == "A" then call ProcessTypeA
- if %type% == "B" then call ProcessTypeB
- if %type% == "C" then call ProcessTypeC
```

## Variable Loading Pattern

```plang
LoadSettings
- load %lastCheck%, set %now% as default
- if %lastCheck% is empty then
    - set %lastCheck% = %now%
    - store %lastCheck%
```
