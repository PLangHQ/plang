# Plang Modules Reference

## Overview

Plang comes with 40+ built-in modules that wrap C# functionality. Each module provides specific capabilities without requiring you to write low-level code.


## How Modules Work

1. You write a step in natural language
2. LLM analyzes the step and selects appropriate module
3. LLM asks the module which function can handle the step
4. Module generates instruction JSON (.pr file)
5. Runtime executes the instruction

**Module Hint Syntax**: Use `[moduleName]` to guide module selection. This is optional and only used when builder cannot determine which module to use.
```plang
- [file] read data.txt into %content%
- [db] get users from database
- [http] get https://api.example.com
```

## Core Modules

### CallGoalModule  
**Purpose**: Call other goal files (functions)

**Capabilities**:
- Call goals in same app
- Call goals in other apps
- Pass parameters
- Get return values

**Examples**:
```plang
// Basic goal call
- call ProcessUser %userData%

// Call with return value
- call CalculateTotal %items%, write to %total%

// Call goal in another app
- call GoogleSearch.Search %query%, write to %results%

// Call in folder
- call user/Get %userId%, write to %user%

// Don't wait for completion
- call SendAnalytics %data%, don't wait
```

**Rules**:
- Parameters passed automatically if variable names match
- Use `write to %variable%` to capture return values

### CodeModule
**Purpose**: Execute C# code for custom algorithms not covered by other modules. Use for complex operations with loops — NOT for simple math (use NCalc for that).

**Capabilities**:
- Custom algorithms with loops
- Complex string manipulations
- Data transformations
- Vector/matrix operations
- Performance-critical batch processing

**IMPORTANT: NCalc vs [code] Module**

PLang has NCalc built-in for simple calculations. Use `[code]` only for custom algorithms that require loops or complex logic.

**Simple math — use NCalc expressions directly (NOT [code]):**
```plang
- set %total% = %price% * %quantity%
- set %discount% = %subtotal% * 0.1
- set %nextPage% = %page% + 1
- calc %amount% * 1.21, write to %withVat%
```

**Custom algorithms with loops — use [code] module:**
```plang
- [code] compute dot product of %vectorA% and %vectorB%, write to %dot%
- [code] normalize vector %vector%, write to %normalized%
```

**Three approaches (simplest to most complex):**

#### 1. Simple Description (for well-known calculations)
```plang
- [code] calculate cosine similarity of %vectorA% and %vectorB%, write to %similarity%
- [code] compute dot product of %vec1% and %vec2%, write to %dot%
- [code] normalize vector %vector%, write to %normalized%
- [code] compute simhash for text %content%, write to %hash%
- [code] calculate hamming distance between %hash1% and %hash2%, write to %distance%
```

#### 2. Inline C# (for custom logic < 20 lines)
```plang
ComputeCosineSimilarity
- [code] compute cosine similarity between %vectorA% and %vectorB%:
    double dot = 0, normA = 0, normB = 0;
    for (int i = 0; i < vectorA.Length; i++) {
        dot += vectorA[i] * vectorB[i];
        normA += vectorA[i] * vectorA[i];
        normB += vectorB[i] * vectorB[i];
    }
    double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
    return denom > 0 ? dot / denom : 0;
    write to %similarity%
```

#### 3. External C# File (for complex/reusable code)
```plang
- [code] run "VectorMath.cs" vectorA=%vectorA%, vectorB=%vectorB%, write to %result%
```

**When to use each:**

| Scenario | Approach |
|----------|----------|
| Simple math (add, multiply, etc.) | NCalc: `- set %x% = %a% + %b%` |
| Standard vector math (sqrt, cos, normalize) | [code] with simple description |
| Custom algorithm, < 20 lines | [code] with inline C# |
| Complex logic, reusable across goals | [code] with external .cs file |

**❌ Bad - PLang loops (slow for large arrays):**
```plang
/ This is slow AND the correct loop syntax requires calling a goal
- set %dot% = 0
- set %i% = 0
- foreach %vectorA% call DoCalc

DoCalc
- set %dot% = %dot% + (%item% * %vectorB[i]%)
- calc %i% + 1, write to %i%
```

**Note:** Array index syntax uses `%array[i]%` where `i` is not wrapped in `%`.

**✅ Good - C# code module (fast):**
```plang
- [code] compute dot product of %vectorA% and %vectorB%, write to %dot%
```

**When to use [code]:**

The `[code]` module is for custom algorithms that can't be done with built-in modules or variable methods. Most common operations already have support:

| Don't use [code] for | Use instead |
|---------------------|-------------|
| Simple math (add, subtract, etc.) | NCalc: `%a% + %b%`, `%x% * %y%` |
| String uppercase/lowercase | `%text.ToUpper()%`, `%text.ToLower()%` |
| Date arithmetic | `%now+7days%`, `%date.AddMonths(1)%` |
| Type conversion | PLang runtime handles automatically |
| List sum/avg/max/min | `%items.price.sum%`, `%items.price.avg%` |
| List first/last/count | `%items.first%`, `%items.last%`, `%items.count%` |
| Filter/sort lists | FilterModule, ListDictionaryModule |
| Cryptographic hashing | CryptographicModule (`hash`, `encrypt`, `sign`) |

**Valid [code] use cases - custom algorithms:**
```plang
/ Vector math (no built-in module)
- [code] compute dot product of %vectorA% and %vectorB%, write to %dot%
- [code] calculate cosine similarity of %vec1% and %vec2%, write to %similarity%
- [code] normalize vector %vector%, write to %normalized%

/ Custom text processing
- [code] parse %rawText%, extract sections between "START" and "END", 
    trim whitespace, convert to html list items, write to %htmlList%

/ Custom data transformations
- [code] group %transactions% by month, calculate running total per group, 
    write to %monthlyTotals%

/ Performance-critical loops
- [code] for each item in %largeDataset%, apply custom scoring algorithm, 
    write to %scores%
```

**Example - Custom text processor (external file):**
```plang
- [code] run "TextProcessor.cs" text=%rawContent%, write to %processed%
```

```csharp
// TextProcessor.cs
public static string Process(string text) {
    var sections = text.Split("---");
    var items = sections
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => $"<li>{s.Trim()}</li>");
    return $"<ul>{string.Join("", items)}</ul>";
}
```

**When to use**:
- ✅ Custom algorithms not covered by built-in modules
- ✅ Vector/matrix math operations
- ✅ Complex text parsing with multiple transformations
- ✅ Performance-critical loops over large datasets
- ❌ Simple string/date/math operations (use built-in methods)
- ❌ Abstract/fuzzy operations (use LlmModule instead)
- ❌ Cryptographic operations (use CryptographicModule)

### ConditionModule
**Purpose**: If/else logic and conditional execution

**Capabilities**:
- If statements
- Comparisons
- Validation
- Early returns

**Examples**:
```plang
// Basic if statement
- if %age% >= 18
    - allow access

// If-else (single line)
- if %user.isAdmin% then call AdminPanel, else call UserPanel

// Validation with throw
- make sure %email% is not empty
- make sure %email% contains @
- if %password% length < 8, throw error "Password too short"

// Early return
- if %user% is empty then
    - return "There is no user"

// Multiple conditions (indent for grouping)
- if %user.role% is "admin"
    - call ShowAdminMenu
    - call LoadAdminData
    - enable admin features

// Comparison operators
- if %count% > 100
- if %status% is "active"  
- if %list% is empty
- if %value% is not null
```

**Syntax**:
- Indented content belongs to the if block
- Use `throw error` to stop execution
- Natural language conditions (>=, >, <, is, contains, etc.)

### CryptographicModule
**Purpose**: Encryption, hashing, signing

**Capabilities**:
- Hash passwords (BCrypt, Keccak256, SHA256)
- Encrypt/decrypt data (AES256)
- Sign and verify messages
- Generate secure random values

**Examples**:
```plang
// Hash password (BCrypt with salt)
- hash %password%, write to %hashedPassword%
- hash %password% into %hashed%

// Hash without salt (Keccak256)
- hash %address% without salt, write to %hash%

// Encrypt data (AES256 with app's private key)
- encrypt %sensitiveData%, write to %encrypted%
- encrypt file.txt

// Decrypt data
- decrypt %encrypted%, write to %plaintext%
- decrypt file.txt

// Sign message
- sign %message%, write to %signature%

// Verify signature
- verify %signature% for %message%, write to %isValid%
```

**Security**:
- BCrypt for passwords (includes salt automatically)
- Keccak256 for blockchain addresses
- SHA256 also available
- AES256 for data encryption
- Each app uses its own encryption keys

### DbModule
**Purpose**: Database operations (SQLite, PostgreSQL, MySQL, SQL Server)

**Capabilities**:
- Create tables
- Insert, update, delete records
- Select queries
- Transactions
- Schema modifications
- Event sourcing (automatic for SQLite)

**Examples**:
```plang
// Create table
- create table users, columns: id(int, pk), name(string, not null), email(string, not null), created(datetime, default now)

// Insert
- insert into users, name=%name%, email=%email%
- insert into users, %user.name%, %user.email%, %user.password%

// Select
- select * from users, write to %users%
- select * from users where id=%userId%, write to %user%
- select name, email from users where age > 18, write to %adults%

// Select with pagination
- select * from users, paginate page %page%, 50 per page, write to %users%

// Update
- update users set name=%newName% where id=%userId%
- update users, name=%name%, email=%email% where id=%id%

// Delete
- delete from users where id=%userId%

// Check existence
- select id from users where email=%email%, return 1, write to %userId%

// Transactions
- begin transaction
- insert into orders, %order%
- insert into order_items, %items%
- commit transaction

// Schema modifications (in Setup.goal)
- add column phone(string) to users table
- rename column password to password_hash on users
- create index on users(email)

// Triggers
- when users table is updated, update column modified with current datetime
```

**Connection**:
- Default: SQLite (data.sqlite in .db folder)
- Configure other databases in Setup or Start
- Event sourcing automatic for SQLite
- Multiple database support

### FileModule
**Purpose**: File system operations

**Capabilities**:
- Read/write files
- Create/delete directories
- Copy/move files
- List directory contents
- Check file existence

**Examples**:
```plang
// Read file
- read file.txt into %content%
- read ./config/settings.json into %config%
- read bytes of image.png, write to %imageData%

// Write file
- write %content% to file.txt
- write %data% to ./output/result.json

// Create directory
- create folder %folderName%
- create directory ./output/reports

// Copy/Move
- copy file.txt to backup/file.txt
- move old.txt to archive/old.txt

// Delete
- delete file.txt
- delete folder ./temp

// List contents
- list files in ./data, write to %files%
- list directories in %path%, write to %folders%

// Check existence
- check if file.txt exists, write to %exists%

// File info
- get file info for data.txt, write to %info%
  // %info% contains: size, created, modified, extension
```

**Security**:
- Apps can only access files in their own folder
- Accessing parent folders requires user permission
- SafeFileSystem enforces boundaries

### HttpModule
**Purpose**: HTTP requests (GET, POST, PUT, DELETE)

**Capabilities**:
- Make HTTP requests
- Send JSON/form data
- Custom headers
- Handle responses
- Automatic request signing

**Examples**:
```plang
// GET request
- get https://api.example.com/users, write to %users%
- get %apiUrl%/data, write to %response%

// POST request
- post https://api.example.com/users
    body: {name:%name%, email:%email%}
    write to %response%

// PUT request
- put https://api.example.com/users/%userId%
    body: {name:%newName%}

// DELETE request
- delete https://api.example.com/users/%userId%

// Custom headers
- post https://api.example.com/data
    bearer: %Settings.ApiKey%
    body: %data%
    write to %response%

// External service (LLM figures out the API)
- create user in Brevo
    bearer %Settings.BrevoApiKey%
    parameters FNAME=%firstName%, LNAME=%lastName%, EMAIL=%email%

// Retry on failure
- get https://api.unreliable.com, retry 5 times over 10 minutes, write to %data%

// Error handling
- get %apiUrl%
    on error call HandleApiError
    write to %result%
```

**Signing**:
- All requests automatically signed with %Identity%
- Server can verify signature
- Prevents tampering
- Enables passwordless authentication

### LlmModule
**Purpose**: Call LLM for abstract operations

**Capabilities**:
- Ask questions to LLM
- Parse unstructured data
- Generate text
- Classify/categorize
- Extract information

**Examples**:
```plang
// Sentiment analysis
- [llm] system: Determine if the text is positive, negative, or neutral
    user: %text%
    scheme: {sentiment:string}
    write to %result%

// Extract information
- [llm] system: Extract email and phone from the text
    user: %contactInfo%
    scheme: {email:string, phone:string}
    write to %contact%

// Classification
- [llm] system: Categorize this product into one of: Electronics, Clothing, Food, Other
    user: %productDescription%
    model: "o1"
    write to %category%

// Fix/clean data
- [llm] system: Fix the CSV data, remove duplicates and format properly
    user: %csvData%
    temperature: 5
    ptop: 3
    write to %cleanedData%

// Generate text
- [llm] system: Write a professional email response
    user: Customer complaint: %complaint%
    write to %emailResponse%

// Decision making
- [llm] system: Based on the user request, which goal should be called: %__Goals__%
    user: %userInput%
    scheme: {goalName:string}
    write to %goalToCall%
```

**When to use**:
- ✅ Abstract/fuzzy operations
- ✅ Natural language understanding
- ✅ Unstructured data parsing
- ✅ Text generation
- ❌ Simple string operations (use [code] instead)
- ❌ Math calculations (use MathModule instead)

**Performance**: Slower than other modules, use wisely.

### LoopsModule
**Purpose**: Iterate through collections

**Capabilities**:
- Loop through lists
- Call goals for each item
- Access loop variables

**Examples**:
```plang
// Basic loop
- go through %users%, call ProcessUser

// Loop with custom variable names
- go through %products%, call UpdateProduct product=%item%

// Nested loops
- go through %orders%, call ProcessOrder
  
ProcessOrder
- go through %order.items%, call ProcessItem
```

**Automatic variables**:
- `%list%` - The collection being iterated
- `%item%` - Current item
- `%position%` - Current index (0-based)
- `%listCount%` - Total count

**In called goal**:
```plang
ProcessUser
- set %item.processed% = true
- update users set processed=true where id=%item.id%
```

### MessageModule (Nostr)
**Purpose**: Send private messages via Nostr protocol

**Capabilities**:
- Send private messages
- Listen for messages
- Nostr relay integration

**Examples**:
```plang
// Send private message
- send private message to npub1abc...
    content: Hi, how are you?

// Send message to variable address
- send message to %recipientPublicKey%
    content: %messageText%

// Listen for messages (in Start.goal)
Start
- listen for new message, call HandleMessage

// Handle received message
HandleMessage
// Automatic variables: %messageContent%, %sender%
- write out "Received: %messageContent%"
- send message to %sender%, content: "Got your message!"
```

**Keys**:
- Each app has its own Nostr key pair
- Keys stored in .db/system.sqlite
- Private messages encrypted automatically

### OutputModule
**Purpose**: Display output to user

**Capabilities**:
- Write to console
- Write to HTTP response
- Format output

**Examples**:
```plang
// Write to console
- write out %message%
- write out "Hello, %name%!"

// Write object (formatted JSON)
- write out %user%

// Write to HTTP response
- write %data% to web response
- write %jsonData% to response, content type application/json

// Write success message
- write out "User created successfully: %user.name%"
```

### PythonModule
**Purpose**: Execute Python scripts

**Capabilities**:
- Run Python scripts
- Pass parameters
- Get return values
- Auto-install requirements

**Examples**:
```plang
// Run Python script
- run analyze.py, write to %results%

// With parameters
- run process.py, input=%data%, output_path=%path%, write to %result%

// Requirements
// If requirements.txt exists in the goal folder, it runs:
// pip install -r requirements.txt

// Python script location
// Script must be in the same folder as the goal file
```

**Python script example** (analyze.py):
```python
import sys
import json

# Get parameters from Plang
data = sys.argv[1]

# Process data
result = {"status": "success", "count": len(data)}

# Return to Plang
print(json.dumps(result))
```

### ScheduleModule
**Purpose**: Schedule recurring tasks

**Capabilities**:
- Run goals at specific times
- Recurring schedules
- Cron-like syntax

**Examples**:
```plang
// In Start.goal
Start
- every 60 seconds call CheckStatus
- every 5 minutes call SyncData
- every day at 09:00 call SendDailyReport
- every monday at 08:00 call SendWeeklyReport

// Complex schedules (cron)
- at 13:23 every day except sundays, call ProcessOrders
- every 1st of month at 00:00, call MonthlyCleanup
```

**Cron conversion**:
Plang converts natural language to cron patterns automatically.

### WebServerModule
**Purpose**: Run HTTP server

**Capabilities**:
- Start web server
- Serve static files
- Map URLs to goals (routes must be explicitly defined)

**Examples**:
```plang
// In Start.goal
Start
- start webserver on port 8080, on start call MapUrls
- run webserver, port 9090

MapUrls
- add route /user, call goal /user/User
- add route /product/%product.slug%, call goal /products/Product
- add route /category/%category.id%(number > 0), call goal /products/Category
- add route /admin/product/%product.id%(number > 0), POST, call goal /admin/SaveProduct
```

**URL Mapping**:
- GET is default
- Add POST, PUT, DELETE after constraints for other methods
- Use object.property pattern (e.g. `%product.id%`) for smooth variable flow in goals
- Parameters from route are automatically available in the called goal

### WebbrowserModule
**Purpose**: Automate web browser (Selenium)

**Capabilities**:
- Navigate to URLs
- Fill forms
- Click buttons
- Extract data
- Wait for elements

**Examples**:
```plang
// Navigate and extract
- go to https://example.com
- #search should be "Plang programming"
- click button "Search"
- wait 5 seconds
- extract .results into %data%

// Form filling
- go to https://login.example.com
- #email should be %userEmail%
- #password should be %userPassword%
- click <button type="submit">Login</button>

// Wait for element
- wait for element .profile-loaded
- extract .username into %username%
```

## Less Common Modules

### BlockchainModule
**Purpose**: EVM blockchain interactions, payments, smart contracts

**Capabilities**:
- Transfer tokens (ETH, USDC, etc.)
- Listen for blockchain events
- Interact with smart contracts
- Execute transactions

**Examples**:
```plang
// Transfer tokens
- transfer 10 usdc to 0x742d35Cc6634C0532925a3b844Bc9e7595f0bEb
- transfer 0.5 eth to %recipientAddress%, wait for confirmation, write to %receipt%

// Check balance
- what is my balance on usdc, write to %balance%
- what is balance of 0x123... on eth, write to %ethBalance%

// Listen for events
- listen for transfer event on contract 0x123.., call ProcessTransfer

// Smart contract interaction
- call function 'mint' on contract 0x456.., with amount=%amount%
```

**Private Keys**:
- Each app has its own blockchain private key
- Keys stored in .db/system.sqlite
- Public address generated from private key

### ArchiveModule
**Purpose**: Compress/decompress files

**Examples**:
```plang
- compress %files% to archive.zip
- extract archive.zip to ./extracted
```

### CachingModule  
**Purpose**: Manual cache control

**Examples**:
```plang
- cache %data% with key "user_%userId%", expire in 10 minutes
- get cache with key "user_%userId%", write to %cached%
- remove cache with key "user_%userId%"
```

### CompressionModule
**Purpose**: Compress data

**Examples**:
```plang
- compress %data%, write to %compressed%
- decompress %compressed%, write to %original%
```

### IdentityModule
**Purpose**: Manage %Identity% and keys

**Examples**:
```plang
- get my identity, write to %myIdentity%
- get my public key, write to %publicKey%
- sign %message%, write to %signature%
```

### InjectModule
**Purpose**: Dependency injection

**Examples**:
```plang
// In Start.goal or Events.goal
- inject db, npgsql/lib/net7.0/Npgsql.dll, global
- inject cache, redis, global
- inject llm, OpenAIService
```

### LocalOrGlobalVariableModule
**Purpose**: Variable management and persistence

**Examples**:
```plang
/ Load and store persist variables to __Variables__ table
- load %lastSync%
- store %lastSync%

/ Set default value if variable is empty
- set default %limit% = 50
```

### LoggerModule
**Purpose**: Logging

**Examples**:
```plang
- log info "User created: %userId%"
- log error "Failed to process: %error%"
- log debug %debugData%
```

### SettingsModule
**Purpose**: Manage application settings

**Examples**:
```plang
- set setting ApiKey to %apiKey%
- get setting ApiKey, write to %key%

// Or use %Settings.% variables
- get %Settings.ApiKey%
- set %Settings.DatabaseUrl% = %newUrl%
```

### ThrowErrorModule
**Purpose**: Error handling

**Examples**:
```plang
- throw error "Invalid user data"
- throw 404 "User not found"
- throw 500 error %errorMessage%
```

## Module Selection Process

1. **Step Analysis**: LLM reads your step
2. **Module Hint**: If you use `[module]`, only that module considered
3. **Module List**: LLM given list of available modules
4. **Selection**: LLM suggests best module
5. **Function Match**: Module's functions analyzed
6. **Instruction**: JSON instruction generated
7. **Fallback**: If no match, try next module or generate code

## Custom Modules

When built-in modules don't cover your needs, create custom C# modules.

**Structure**:
```
modules/
└── MyModule/
    ├── Builder.cs          # Optional - build-time validation/customization
    └── Program.cs          # Required - runtime methods
```

### Program.cs Template

```csharp
using PLang.Modules;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Models;
using System.ComponentModel;

namespace PLang.Modules.MyModule
{
    [Description("Brief description of what this module does - used by LLM for module selection")]
    public class Program : BaseProgram
    {
        public Program() : base() { }

        // All PUBLIC ASYNC methods become callable from PLang
        // Use [Example] attributes to help LLM map natural language to method calls
        [Description("Description of what this method does")]
        [Example("call goal Process %name%", @"GoalToCallInfo.Name=Process, GoalToCallInfo.Parameters={""name"":""%name%""}")]
        [Example("run Process with name %userName%", @"GoalToCallInfo.Name=Process, GoalToCallInfo.Parameters={""name"":""%userName%""}")]
        public async Task<string> MyFunction(string input)
        {
            // Implementation
            return result;
        }
        
        // Return with Properties (additional metadata accessible in PLang via ! syntax)
        // Properties is a class that inherits from List<ObjectValue>
        public async Task<(MyResult? Result, IError? Error, Properties? Properties)> GetData(
            MyParams parameters)
        {
            var result = new MyResult { ... };
            var props = new Properties();
            props.Add(new ObjectValue("sql", actualSql));
            props.Add(new ObjectValue("executionTime", elapsed));
            return (result, null, props);
        }
        
        // Calling another goal from within a module
        public async Task<(object?, IError?)> ProcessWithGoal(GoalToCallInfo goalToCall)
        {
            // goal is the current executing goal (from BaseProgram)
            var result = await engine.RunGoal(goalToCall, goal, context);
            return result;
        }
    }
}
```

### The [Example] Attribute

Use `[Example]` attributes to help the LLM correctly map natural language steps to your methods:

```csharp
[Example("send email to %recipient%", @"EmailMessage.To=%recipient%")]
[Example("email %address% with subject %subj%", @"EmailMessage.To=%address%, EmailMessage.Subject=%subj%")]
public async Task SendEmail(EmailMessage message) { ... }
```

You can have multiple `[Example]` attributes on a single method to cover different phrasings.

### Parameter Types

**Preferred: Custom classes for multiple parameters**
```csharp
// Custom classes are PREFERRED - they allow better descriptions for LLM mapping
// When property names change, descriptions help the LLM self-correct
[Description("Email message configuration")]
public class EmailMessage
{
    [Description("Recipient email address")]
    public string To { get; set; }
    
    [Description("Email subject line")]
    public string Subject { get; set; }
    
    [Description("Email body content")]
    public string Body { get; set; }
}

public async Task SendEmail(EmailMessage message) { ... }
```

**Also valid:**
- C# primitives (string, int, double, bool, etc.)
- List<T> and arrays
- Dictionary<string, object>
- GoalToCallInfo (for callbacks)

### Return Types

```csharp
// Simple return
public async Task<string> GetName() { ... }

// With error handling
public async Task<(MyResult?, IError?)> GetData() { ... }

// With Properties (metadata accessible via ! syntax in PLang)
// Properties inherits from List<ObjectValue> where ObjectValue is the PLang variable type
public async Task<(MyResult?, IError?, Properties?)> Query() 
{
    // Properties allow attaching metadata to results
    var props = new Properties();
    props.Add(new ObjectValue("sql", actualSql));
    props.Add(new ObjectValue("url", requestUrl));
    return (result, null, props);
}
```

**Accessing Properties in PLang:**
```plang
- get http://example.com/data, write to %response%
- write out %response!properties%    / write out all available properties
- write out %response!url%           / access specific property (e.g., actual URL used)
- write out %response!sql%           / access SQL that was executed (for db queries)
```
```

### Error Handling

**Always use ProgramError in modules:**
```csharp
public async Task<(object?, IError?)> MyMethod()
{
    if (invalid)
    {
        return (null, new ProgramError("Validation failed", goalStep, function));
    }
    return (result, null);
}
```

### Important Attributes

### Important Attributes

- `[Description("...")]` - Helps LLM understand the purpose (on classes, methods, properties)
- `[Example(stepText, mapping)]` - Helps LLM map natural language to method calls (can have multiple)
- `[LlmIgnore]` - Hides property from LLM during build time

### BaseProgram Available Properties

When inheriting from `BaseProgram`, you have access to:
```csharp
protected MemoryStack memoryStack;      // Variable storage
protected Goal goal;                     // Current executing goal
protected GoalStep goalStep;            // Current step
protected Instruction instruction;       // Current instruction
protected PLangAppContext appContext;   // App-level context
protected PLangContext context;         // Request-level context
protected IEngine engine;               // For running other goals
protected ISettings settings;           // App settings
protected IPLangFileSystem fileSystem;  // File operations
protected ILogger logger;               // Logging
protected IAppCache appCache;           // Caching
protected HttpContext? HttpContext;     // Web request context (if applicable)
```

### GoalToCallInfo Class

Used for callback parameters:
```csharp
public class GoalToCallInfo
{
    public string Name { get; set; }                           // Goal name/path
    public Dictionary<string, object?> Parameters { get; set; } // Parameters to pass
    public bool IsAsync { get; set; } = false;                 // Run and forget
    public int WaitBeforeExecutingInMs { get; set; } = 0;      // Delay before execution
    public GoalToCallInfo? AfterExecution { get; set; }        // Chain another goal
    
    [LlmIgnore]
    public string Path { get; set; }                           // Resolved file path
}

// Calling a goal from module code:
await engine.RunGoal(goalToCall, goal, context);
```

### Builder.cs (Optional)

For build-time validation or customization:
```csharp
public class Builder : BaseBuilder
{
    public override async Task<Instruction?> Build(GoalStep step)
    {
        // Validate step, modify instruction, etc.
        return await base.Build(step);
    }
}
```

## Tips for Module Usage

### 1. Use Module Hints
```plang
- [file] read data.txt           // Forces FileModule
- [db] get users                 // Forces DbModule
- [http] get api.example.com     // Forces HttpModule
```

### 2. Keep Steps Simple

Each step has one specific action

```plang
✅ Good:
- select everything from users db where id=%userId%, write to %user%
- hash %user.password%, write to %hashed%
- update users set password=%hashed% where id=%userId%

❌ Bad:
- get user, hash password, and update in database
```

### 3. Be Specific
```plang
✅ Good:
- insert into users, name=%name%, email=%email%

❌ Ambiguous:
- create user %name% %email%
```

### 4. Help the LLM
```plang
// Use comments
- get weather data  // From weather API

// Be explicit
- select id from users where email=%email%, return 1

// Specify output
- calculate total, write to %total% (decimal)
```

## Summary

- ✅ 40+ built-in modules cover most needs
- ✅ Use `[module]` hints to guide selection
- ✅ Keep steps simple (one operation per step)
- ✅ Create custom modules when needed
- ✅ **Custom classes are preferred** for module parameters
- ✅ Use `[Description]` attributes to help LLM
- ✅ **Always use ProgramError** in custom modules
- ✅ **Use [code] for heavy calculations** (vectors, arrays, loops)
- ✅ All requests are automatically signed
- ✅ Module selection is automatic via LLM