# Plang Project Structure

## Standard Project Layout

```
MyPlangApp/                    # Root directory
├── Start.goal                 # Application entry point
├── Setup.goal                 # One-time initialization (OPTIONAL)
│
├── .build/                    # Generated build artifacts (AUTO-GENERATED)
│   ├── goals/
│   ├── Setup/
│   └── Start/
│
├── .db/                       # Database files (AUTO-GENERATED)
│   ├── system.sqlite          # System settings, API keys, private keys
│   └── data.sqlite            # Application data
│
├── user/                       
│   ├── Create.goal
│   ├── Get.goal
│   └── Update.goal
│
├── events/                    # Event handler goals (alternative to Events.goal)
│   ├── Events.goal
│   ├── OnError.goal
│   └── BeforeStep.goal
│
├── services/                  # Dependency injection DLLs
│   └── npgsql/
│       └── lib/
│
├── modules/                   # Custom modules (C# or Python)
│   ├── MyCustomModule/
│   │   ├── Builder.cs
│   │   └── Program.cs
│   └── ImageModule/
│
├── apps/                      # Third-party Plang apps
│   └── GoogleSearch/
│       ├── Start.goal
│       └── .db/
│
└── Setup/                     # Setup goals (alternative to Setup.goal)
    ├── CreateDatabase.goal
    ├── SeedData.goal
    └── CreateConfig.goal
```

## Core Files

### Start.goal
**Purpose**: Application entry point that runs ONLY when no goal file is specified at startup.

**Key behavior**:
- `plang` → runs Start.goal
- `plang MyGoal` → runs MyGoal.goal, **Start.goal is skipped**

**What belongs here**:
- Starting webserver
- Starting UI/window app
- Listening to message queues
- Scheduling recurring tasks
- Setting up injection of overriding modules

**Example**:
```plang
Start
- start webserver on port 8080
- every 5 minutes call SyncData
```

**What does NOT belong here**:
- Creating database tables (use Setup.goal)
- Creating configuration files (use Setup.goal)
- Seeding initial data (use Setup.goal)
- One-time initialization (use Setup.goal)

### Setup.goal (OPTIONAL but RECOMMENDED)
**Purpose**: ONE-TIME initialization. Each step is tracked and only runs once in the lifetime of the application.

**Key behavior**:
- Always checks if each step should run (tracked in system.sqlite)
- If a step has already run, it's skipped
- If you restart the app, already-executed steps won't run again
- Add new steps at the bottom for migrations 

**What belongs here**:
- Creating database tables
- Modifying database schema
- Creating configuration files
- Seeding initial data
- Setting up directory structure
- Generating encryption keys

**Example**:
```plang
Setup
- create table users, columns: id(int, pk), name(string, not null), email(string, not null), created(datetime, default now)
- create table products, columns: id(int, pk), name(string), price(decimal)
- when users table is updated, update column modified with current datetime
- create config.json with {"apiUrl":"https://api.example.com"} as data
```

**How it works**:
- Each step is tracked in system.sqlite
- Once a step runs successfully, it never runs again
- Add new steps to the bottom for schema migrations
- Great for event sourcing your setup history

**Migration example**:
```plang
Setup
- create table users, columns: name(string), email(string)
// Later, you need a password column:
- add column password(string, not null) to users table
// Even later, you rename it:
- rename column password to password_hash on table users
```

### Events.goal (OPTIONAL)
**Purpose**: Global event bindings for the entire application.

**What belongs here**:
- Authorization checks before API calls
- Logging/analytics before/after steps
- Error handling
- Application lifecycle events

**Example**:
```plang
Events
- before each goal in /.* call Authorize
- before each step call LogStart
- after each step call LogEnd
- on error call HandleError
- on app start call Initialize
```

## Folder Structure Rules

### Setup/ Folder (Alternative to Setup.goal)
Use when you want to separate databases for you data, e.g. keep analytical data separate from main db or store the user data in it's own database, isolated data storage, where each user gets it's own db.

```
Setup/
├── CreateDatabase.goal      # Database schema
├── SeedData.goal           # Initial data
├── CreateConfig.goal       # Configuration files
└── SetupWebhooks.goal      # External service setup
```

**Execution order**:
1. Setup.goal (if it exists) runs FIRST
2. All individual .goal files in Setup/ folder (alphabetically)

### Events/ Folder (Alternative to Events.goal)
Use when you have many event handlers.

```
events/
├── Events.goal             # Main event bindings
├── Authorize.goal          # Authorize handler
├── LoggingHandlers.goal    # Logging handlers
└── ErrorHandlers.goal      # Error handlers
```


### modules/ Folder
Custom modules written in C#.

**Structure**:
```
modules/
└── ImageModule/
    ├── Builder.cs          # Optional: custom build instructions
    └── Program.cs          # Required: module implementation
```

**Requirements**:
- Program.cs must inherit from `BaseProgram`
- All public async Task methods are callable
- See Module Development documentation for details

### services/ Folder
Dependency injection DLLs for overriding default implementations.

**Example**:
```
services/
├── npgsql/                 # PostgreSQL database driver
│   └── lib/
│       └── net7.0/
│           └── Npgsql.dll
└── redis/                  # Redis cache driver
    └── StackExchange.Redis.dll
```

**Usage**:
```plang
// In Start.goal or Events.goal
- inject db, npgsql/lib/net7.0/Npgsql.dll, global
```

### apps/ Folder
Third-party Plang apps that your app uses.

**Structure**:
```
apps/
├── GoogleSearch/
│   ├── Start.goal
│   ├── Search.goal
│   └── .db/
│       └── system.sqlite   # App's own private keys
└── ImageProcessor/
    ├── Start.goal
    └── Process.goal
```

**Security**: Each app runs in its own container with:
- Own memory space
- Own private keys
- Own file system (can only access its own folder)
- Requires permission to access parent app resources

**Usage**:
```plang
- call app GoogleSearch.Search %query%, write to %results%
```

## Hidden Folders (Auto-Generated)

### .build/ Folder
Contains compiled Plang code (.pr files - JSON instruction files).

**Structure**:
```
.build/
├── goals/
│   └── CreateUser/
│       ├── 0.Goal.pr
│       ├── 1.ValidateInput.pr
│       ├── 2.HashPassword.pr
│       └── 3.InsertUser.pr
├── Setup/
│   └── CreateDatabase/
│       ├── 0.Goal.pr
│       └── 1.CreateTable.pr
└── Start/
    └── StartWebserver/
        ├── 0.Goal.pr
        └── 1.StartServer.pr
```

**Important**: 
- Never edit .pr files directly
- Always modify .goal files and rebuild
- .pr files are for debugging and understanding execution
- These are JSON files - human readable for verification

### .db/ Folder
SQLite databases for the application.

**Files**:
- `system.sqlite`: Settings, API keys, private keys, secrets
- `data.sqlite`: Application data tables
- `__Events__` table: Automatic event sourcing

**Security**:
- Private keys stored unencrypted (as of Sept 2024)
- Future: Will support encryption with bio/pin/face unlock
- Each app has its own .db folder with separate keys

## File Naming Conventions

### Goal Files
- **Format**: `PascalCase.goal`
- **Examples**: 
  - `CreateUser.goal`
  - `ProcessOrder.goal`
  - `SendEmail.goal`
  - `UpdateProduct.goal`

### Folders
- **Format**: `lowercase`
- **System folders**: lowercase (api, ui, events, setup)
- **Custom folders**: PascalCase (MyFeature, OrderProcessing)

## When to Use What

### Single File vs Folder

**Use single Setup.goal when**:
- Simple setup (< 10 steps)
- All setup is related located in .db/data/data.sqlite
- Quick prototyping

**Use Setup/ folder when**:
- Complex setup (many steps)
- Multiple related but separate concerns
- To separate data from one another, e.g. main database might not need to have analytical data in it.

**Use events/Events.goal folder when**:
- When you want to bind events to app, goal or steps
- Complex event handling
- Many event handlers
- Want to organize handlers by concern

## Project Organization Patterns

### Pattern 1: Simple App
```
MyApp/
├── Start.goal
├── Setup.goal
├── ProcessData.goal
└── SendEmail.goal
```

### Pattern 2: Web Application
```
MyApp/
├── Start.goal              # Start webserver
├── Setup.goal              # Create tables
├── Events.goal             # Authentication
├── user/
│   ├── Create.goal
│   ├── Get.goal
│   └── Update.goal
└── product/
    ├── List.goal
    └── View.goal
```

### Pattern 3: Complex Application
```
MyComplexApp/
├── Start.goal
├── Events.goal
│
├── Setup/
│   ├── Database.goal
│   ├── SeedData.goal
│   └── Configuration.goal
│
├── user/
│   ├── users/
│   │   ├── templates/
│   │   │   ├── create.html
│   │   │   ├── view.html
│   │   ├── Create.goal
│   │   └── View.goal
│   └── products/
│   │   ├── templates/
│   │   │   ├── list.html
│       └── List.goal

```

## Best Practices

### 1. Keep Root Clean
Only put commonly-called goals in root. Use folders for organization.

✅ **Good**:
```
MyApp/
├── Start.goal
├── Setup.goal
├── user/
│   └── [user goals]
├── product/
│   └── [product goals]
└── services/
    └── [service goals]
```

❌ **Bad**:
```
MyApp/
├── Start.goal
├── Setup.goal
├── CreateUser.goal
├── UpdateUser.goal
├── DeleteUser.goal
├── CreateProduct.goal
├── UpdateProduct.goal
└── [50 more goals...]
```

### 2. Group Related Goals
```
user/                   # User-related endpoints
├── Create.goal
├── Update.goal
└── Delete.goal
product/                # Product-related endpoints
├── Create.goal
└── List.goal
order/                  # Order-related endpoints
├── Create.goal
└── View.goal
```

### 3. Separate Concerns
```
MyApp/
├── user/               # User endpoints
├── product/            # Product endpoints
├── services/           # Business logic
├── data/               # Data access
└── external/           # Third-party integrations
```

### 4. Use Descriptive Names, Folder structure helps
```
✅ user/Create.goal
✅ product/List.goal
✅ services/EmailService.goal

❌ Create.goal              # Create what?
❌ List.goal                # List what?
❌ Service.goal             # Which service?
```

## Migration Guide

### Converting Start.goal with Setup Logic

**Before**:
```plang
// Start.goal
Start
- create table users, columns: id, name, email
- create config.json
- start webserver
```

**After**:
```plang
// Setup.goal
Setup
- create table users, columns: id(int, pk), name(string, not null), email(string, not null)
- create config.json with {"api":"https://example.com"}

// Start.goal
Start
- start webserver on port 8080
```

**Steps**:
1. Create Setup.goal
2. Move one-time initialization from Start.goal to Setup.goal
3. Run `plang exec Setup` once
4. Remove Setup logic from Start.goal
5. Run `plang exec` normally

### Organizing Growing Codebase

**Before** (everything in root):
```
MyApp/
├── Start.goal
├── CreateUser.goal
├── GetUser.goal
├── CreateProduct.goal
├── GetProduct.goal
└── [20 more goals...]
```

**After** (organized):
```
MyApp/
├── Start.goal
├── Setup.goal
├── users/
│   ├── CreateUser.goal
│   └── GetUser.goal
└── products/
│   ├── CreateProduct.goal
│   └── GetProduct.goal
└── services/
    ├── EmailService.goal
    └── PaymentService.goal
```

## Summary

- ✅ **Start.goal**: Entry point, runs ONLY when no goal file is specified (`plang` runs it, `plang MyGoal` skips it)
- ✅ **Setup.goal**: One-time initialization (each step tracked, runs only once ever)
- ✅ **Events.goal**: Global event bindings
- ✅ **modules/**: Custom C# code
- ✅ **services/**: Dependency injection DLLs
- ✅ **apps/**: Third-party Plang apps
- ✅ **.build/**: Generated build artifacts (don't edit)
- ✅ **.db/**: Databases and settings (auto-generated)