# Plang Compiler/Runtime Project

This skill provides context for working on the plang compiler and runtime codebase.

## Project Overview

Plang is a **natural language programming language** that compiles `.goal` files into executable instructions using LLM-powered code generation and reflection-based runtime execution.

## Architecture Layers

```
User Code (.goal files)
    ↓
Builder (LLM-based Compiler)
    ↓
Step Builder + Instruction Builder
    ↓
C# Runtime Execution (Reflection-based)
    ↓
Module System (20+ built-in modules)
    ↓
Services Framework (Database, Caching, LLM, etc.)
```

## Build Process (Compiler)

### Build Order
1. **Events Folder** - All `.goal` files in `Events/` compiled first
2. **Setup.goal** - System initialization
3. **Start.goal** - Main entry point
4. **Remaining Goal Files** - Other files

### Three-Stage Builder Workflow

**Stage 1: Module Selection**
- Step text sent to LLM with list of available modules
- LLM determines best-fit module (e.g., FileModule, HttpModule)
- Response: JSON with `ModuleType`

**Stage 2: Instruction Building**
- LLM receives available methods in selected module
- Maps method parameters from user intent
- Response: JSON with `Action`, `FunctionName`, `Parameters`, `ReturnValue`

**Stage 3: Validation & Generation**
- Builder validates function existence and parameter matching
- If validation fails, requests additional info from LLM with error details
- Generates executable code using reflection

### Key Builder Classes
- `StepBuilder.cs` - Process individual steps
- `InstructionBuilder.cs` - Build instructions from steps
- Located in `PLang/Building/` directory

## Runtime Execution

### Execution Sequence
1. Load Events - Execute `Events.goal` definitions
2. Before Start Events - Run pre-startup hooks
3. Run Setup.goal - Initialize application (one-time only)
4. Start Scheduler - Activate background task scheduler
5. Run Goal File - Execute `Start.goal` by default
6. After Start Events - Run post-startup hooks

### Goal Execution Flow
1. Register dependency injection
2. Run "before goal" events
3. Execute each step:
   - Run "before step" events
   - Execute step
   - Run "after step" events (unless error)
4. Run "after goal" events (unless error)
5. On error: Skip "after" events, run "on error" event instead

## Module System (20+ Modules)

### Core Modules
- **FileModule** - Read/write files, directory operations
- **DbModule** - CRUD operations, schema management, transactions
- **HttpModule** - HTTP requests (GET, POST, PUT, DELETE, PATCH)
- **CodeModule** - String operations, task execution
- **LlmModule** - LLM integration for text analysis, generation
- **ConditionalModule** - If statements, variable checking
- **LoopModule** - Iterate lists/dictionaries
- **CallGoalModule** - Invoke other goals with parameters
- **ScheduleModule** - Task scheduling, cron expressions
- **CachingModule** - In-memory/distributed caching
- **OutputModule** - Console display, user input
- **TerminalModule** - Execute system commands
- **WebserverModule** - Start web server, handle requests
- **CryptographicModule** - AES256 encryption, hashing
- **CompressionModule** - ZIP compression/decompression
- **PythonModule** - Execute Python scripts
- **BlockchainModule** - Wallet management, smart contracts
- **WebCrawlerModule** - Browser automation
- **MessageModule** - Encrypted messaging
- **LocalOrGlobalVariableModule** - Variable management

## Services Framework (Dependency Injection)

### Injectable Services
1. **db** - Database connections (PostgreSQL, MySQL, SQL Server, SQLite)
2. **settings** - Configuration storage
3. **caching** - Distributed caching (Redis, memcached)
4. **llm** - Alternative LLM services
5. **askuser** - Custom user input handling
6. **encryption** - Alternative encryption
7. **archiver** - Custom compression formats
8. **logger** - Custom logging

### Service Implementation
- Implement required interface (e.g., `IDbConnection`, `ILlmService`)
- Place DLL in `.services/ServiceName/` folder
- Reference in code or at build time

## Language Syntax

### Goal Files
- Extension: `.goal`
- First goal in file: Public (callable from web server)
- Subsequent goals: Private

### Steps
- Start with dash: `- step description`
- Multi-line: Use indentation for continuation
- Module hint: `[module_name]`

### Variables
- Syntax: `%variable_name%` or `%object.property%`
- Built-in: `%Now%`, `%NowUtc%`, `%Identity%`, `%MyIdentity%`, `%Settings.Key%`
- Loop variables: `%item%`, `%list%`, `%position%`, `%listCount%`
- Error variable: `%!error%`
- Goal/Step context: `%!goal%`, `%!step%`

### Comments
- Format: `/ comment text`
- Log level: `/ text [trace|debug|info|warning|error]`

### Caching
- Absolute: `cache for X minutes`
- Sliding: `cache for X minutes from last usage`

### Error Handling
- Syntax: `on error 'text' call Goal` or `on error 402 call Goal`
- Retry: `retry N times over X duration`

## Identity System

- No passwords needed - User identity via Ed25519 cryptographic keys
- All requests signed with identity headers
- Headers: `X-Signature-*` (method, URL, created time, nonce, body hash, public key, signature)
- Keys stored in `.db/system.sqlite`

## Project Folder Structure

```
PLang/
├── Building/           # Builder/compiler code
│   ├── StepBuilder.cs
│   └── InstructionBuilder.cs
├── Modules/            # Module implementations
├── Services/           # Service interfaces and implementations
├── Runtime/            # Runtime execution code
└── ...
```

### Standard Plang App Structure
```
app_root/
├── Start.goal          # Main entry point
├── Setup.goal          # One-time initialization
├── .build/             # Auto-generated build artifacts
├── .db/                # Databases (system.sqlite, data.sqlite)
├── api/                # REST API endpoints
├── ui/                 # UI definitions
├── events/             # Event handlers
├── .modules/           # Custom modules
└── .services/          # Custom services
```

## Startup Parameters

```bash
plang                    # Run Start.goal
plang build              # Build all goals
plang --debug            # Enable debugger
plang --csdebug          # C# debugger
plang --detailerror      # Verbose errors
plang --llmservice=openai # Use OpenAI directly
plang --logger=trace     # Set log level
plang --strictbuild      # Rebuild if line numbers mismatch
```

## Key Design Decisions

1. **LLM-Based Compilation** - Natural language input, requires LLM service
2. **Reflection-Based Execution** - Dynamic method invocation, flexible but has performance overhead
3. **Identity-Centric** - No password management, strong cryptographic security
4. **Event-Driven** - Lifecycle hooks at multiple points (build-time and runtime)

## Performance Characteristics

- Priority: Simplicity over raw speed
- Use case: Business applications, not computational algorithms
- Well-suited for: Web services, APIs, SaaS, automation
- Not optimized for: Tight loops processing thousands of items
