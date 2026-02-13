# PLang Runtime2 Migration - Full Roadmap (v4)

---
when saving this file, increase the version number, e.g. migration-roadmap-v4.md => migration-roadmap-v5.md => ...
---

## Context

PLang is migrating from Runtime1 (47+ modules, LightInject DI, BaseProgram pattern) to Runtime2 (OBP, strongly-typed Data, source-generated handlers, entity hierarchy).

Runtime2 currently has 6 modules (variable, file, output, condition, goal, event) with solid core infrastructure (Engine, Memory, Events, Caching, CallStack, TypeMapping, Source Generator). The builder still runs on Runtime1 but produces Runtime2 .pr artifacts via a bridge module. This roadmap covers the complete migration path.

### How We Work Each Phase

When starting each phase, the LLM takes a **software architect** mindset:
1. **Critically examine** the phase before jumping in - challenge assumptions, identify risks
2. **Ask hard questions** - don't be a yes man. Push back on design decisions that seem wrong. Ingi has final say, but the LLM should advocate for the best architecture.
3. **Get clarity first** - resolve open questions before writing code
4. **Write a detailed phase plan** - once questions are answered, produce a concrete plan for that specific phase
5. **Get confirmation** - Ingi approves the detailed phase plan before implementation begins

We are designing the next evolution of programming languages. Do this well and proper!

### Key Architectural Principles

1. **PLang over C# whenever possible** - If something can be written in PLang instead of C#, write it in PLang. Modules, error display, event bindings (debugger), etc. C# is only for when the underlying tech requires it.
2. **`/system/` layer** - PLang-written system code that ships with the runtime (error pages, debugger events, overrides). Needed early.
3. **`/app/` layer** - App-specific PLang code. Comes later when everything works.
4. **Override via goals, not DLLs** - Settings, behaviors, etc. should be overridable by calling PLang goals (e.g., `- override 'settings' call goal MySettings`).
5. **External libraries are good** - Use battle-tested libs: NCalc for math, MiniExcel for Excel, etc.
6. **Culture & Translation** - Culture info (dates, numbers, locale) must be considered from the start. TString type for translatable strings.
7. **Identity is built-in & multi-standard** - `%Identity%` gives user identity. Must support multiple signing standards at the core level: ed25519 (default) + web standards (see `SigningService/PlangSigningService.cs/VerifySignature()`). No separate auth module needed.
8. **Automatic type conversion** - Runtime converts types automatically when methods need them. No explicit convert module.

---

## Phase 0: Foundation Audit (1-2 days)

**Goal:** Ensure the foundation is rock-solid before building modules on top.

### 0.1 Strong Typing Audit
- Audit all `object?` usage in Runtime2 - ensure each has a justified reason or replace with typed alternative
- Verify TypeMapping covers all PLang primitives and their CLR counterparts
- Confirm Data.GetValue<T>() handles all TypeMapping conversions correctly
- Check that source generator handles all parameter types (primitives, lists, dicts, nested objects)

### 0.2 Data Flow & Type Preservation
- Trace a full execution: Goal load -> Step run -> Action execute -> Return -> MemoryStack store
  - Use `plang p !debug` for full trace, `plang p !debug=GoalName` per goal, `plang p !debug=GoalName:3` for specific step
- **Data.Type must be preserved through the entire pipeline** - e.g., `file.read('data.json')` returns Data with Type="application/json", and that type survives through variable storage and retrieval
- Ensure Data.Properties flow correctly between steps
- Validate that GoalCall parameters are strongly typed end-to-end
- **Type-aware conversion:** When PLang says `convert %data% to json`, the runtime sees Data.Type is "yaml", finds the right converter (yaml->json), and does it. Converters must be injectable/pluggable by the PLang programmer. If no converter exists, return error.
- Automatic type coercion: When a method needs `int` and gets a `string` that's numeric, runtime auto-converts. If it can't convert, error.

### 0.3 Error Infrastructure
- **Design decision:** Define the Runtime2 error pattern before any modules use it
- **Rename `Data.Fail()` to `Data.Error()`** - that's what it is
- **Two error categories:**
  - **PLang developer errors** - The developer is telling the user something is wrong (e.g., `validate %name% is not empty`). These propagate to the user (e.g., in a web request, this is a 400-style response).
  - **C# runtime errors** - Unexpected system-level failures the PLang dev didn't anticipate. In web requests this is a 500 error page. In console, just show the error.
- **Error display in PLang** - Error rendering should be PLang code in `/system/`:
  - `/system/error/500.html` for server errors
  - `/system/error/error.html` as fallback
  - Console errors just output directly
- Error propagation: Action -> Step -> Goal -> Engine
- `IError` hierarchy: ActionError, StepError, GoalError, ValidationError
- **Files:** `PLang/Runtime2/Core/ErrorHandler.cs` (new), `/system/error/` (PLang goals)

### 0.4 /system/ Layer Foundation
- Define the `/system/` directory structure that ships with the runtime
- Initial content:
  - `/system/error/` - error display templates/goals
  - `/system/debug/` - debugger event bindings (sends data to channel)
- Establish the pattern: C# core provides primitives, `/system/` PLang code provides behavior
- This is the foundation for later: modules written in PLang, overridable settings, etc.

### 0.5 Culture & Translation Foundation
- **CultureInfo support:** Allow PLang apps to set culture (affects date formatting, number formatting, etc.)
- **TString type:** Translatable string type
  - Anywhere translation is needed (e.g., `write out`, error messages), accept TString
  - TString does hash-table lookup and formats output according to locale
  - Register TString in TypeMapping
- This doesn't need to be fully implemented now, but the design must be in place so modules build on it

### 0.6 Tests
- C# tests: Type round-trip tests, Data conversion edge cases, error propagation tests, Data.Error() replaces Data.Fail()
- PLang tests: `Tests/Runtime2/TypeSafety/` - verify types survive variable set/get cycles

**Complexity:** Medium-Large (more work than originally planned due to /system/ and error redesign)
**Depends on:** Nothing (this is the base)

---

## Phase 1: Core Language Completeness (3-5 days)

**Goal:** Make Runtime2 a complete language runtime - all control flow and basic data operations.

### 1.1 Loop Module (`loop/foreach`)
- **Handler:** `loop/foreach.cs` - iterate collection, call goal per item
- **Parameters:** collection (list/dict), goalToCall (GoalCall), itemVariableName (string)
- **PLang syntax:** `foreach %items%, call ProcessItem item=%item%`
- **Design:** foreach always calls a goal (no sub-steps - this is intentional, keep it)
- **Collection handling:**
  - empty/null collection = count 0, just skip (no error)
  - single object (not a list) = treated as list with count 1
- **Future:** multithreaded foreach (discuss when we get there, not now)
- **Complexity:** Medium

### 1.2 Error Handling Module (`error/throw`)
- **Handler:** `error/throw.cs` - PLang developer throws an error
- **Parameters:** message (string), statusCode (int?), type (string?)
- **This is a PLang developer error** - intentional, user-facing (e.g., validation failure)
- Uses `Data.Error()` (renamed from Data.Fail())
- **Step-level:** Wire up Step.OnErrorGoal to catch and handle errors
- **Error display:** Delegates to `/system/error/` PLang goals for rendering
- **Complexity:** Medium

### 1.3 Retry Logic
- **Not a module** - it's Step infrastructure (like Cache)
- **Add:** `Step.RetrySettings` (maxRetries, delayMs, backoffMultiplier)
- **Integration:** StepMethods.RunAsync checks retry on Data.Error()
- **Complexity:** Small

### 1.4 List/Dictionary Module (`list/*`)
- Handlers: `add`, `remove`, `get`, `set`, `count`, `contains`, `indexOf`, `sort`, `filter`, `first`, `last`, `join`, `split`, `unique`, `reverse`, `range`, `flatten`
- **Important:** Operations must preserve strong typing through Data.Type
- **Chained operations:** Support `filter %list% where name = 'Ingi', sort by reverse date` - use `IEnumerable<T>` internally for lazy evaluation, only materialize when needed (e.g., stored to variable or iterated)
- **Complexity:** Large (many handlers, but each is small)

### 1.5 Math Module (`math/*`)
- **Use NCalc library** - battle-tested, supports complex formula strings
- Handlers: `calculate` (NCalc expression), plus convenience: `round`, `floor`, `ceiling`, `min`, `max`, `random`, `abs`
- NCalc allows users to write formulas like `- calculate '(%price% * %quantity%) * (1 + %taxRate%)', write to %total%`
- **Complexity:** Medium (NCalc does the heavy lifting)

### ~~1.6 Convert Module~~ - REMOVED
- **Not needed.** PLang runtime handles type conversion automatically. When a method needs `int` and receives a string, the runtime auto-converts via TypeMapping. If it can't convert, it returns an error.
- Type-aware format conversion (yaml->json, etc.) is handled by the serializer/Data.Type system in Phase 0.

### Tests per module
- C# unit tests: Each handler tested with valid/invalid inputs, type preservation
- PLang integration: `Tests/Runtime2/Loop/`, `Tests/Runtime2/ErrorHandling/`, `Tests/Runtime2/ListOps/`, `Tests/Runtime2/Math/`

**Depends on:** Phase 0 (error pattern and Data.Type flow must be defined first)

---

## Phase 2: Essential I/O & Processing (3-5 days)

**Goal:** Enable Runtime2 apps to interact with the outside world.

### 2.1 HTTP Module (`http/*`)
- Handlers: `get`, `post`, `put`, `delete`, `patch`, `head`, `options`
- **Parameters:** url, body, headers (dict), contentType, timeout, bearerToken
- **Returns:** Data with Value=response body, Properties={statusCode, headers, contentType}
- **Data.Type set from response Content-Type** (e.g., "application/json")
- **Design decision:** Use HttpClient (injected via Engine or singleton with pool)
- **Complexity:** Large (many handlers + HTTP client management)

### 2.2 Terminal Module (`terminal/*`)
- Handlers: `execute` (run shell command)
- **Parameters:** command (string), args (list?), workingDirectory (string?), timeout (int?)
- **Returns:** Data with Value=stdout, Properties={exitCode, stderr}
- **Security:** Consider sandboxing/approval model
- **Complexity:** Medium

### 2.3 Serializer Module (`serializer/*`)
- Handlers: `toJson`, `fromJson`, `toXml`, `fromXml`, `toCsv`, `fromCsv`, `toYaml`, `fromYaml`
- **Leverages:** Existing SerializerRegistry infrastructure
- **Pluggable converters:** PLang programmer can register custom converters via goals
- **Key role:** Enables the type-aware conversion pipeline from Phase 0 (Data.Type drives which serializer is used)
- **Complexity:** Medium

### 2.4 Template Module (`template/*`)
- Handlers: `render` (Scriban template rendering)
- **Parameters:** template (string), data (object), outputPath (string?)
- **Returns:** rendered string
- **Template vs UI problem:** Need to resolve the distinction:
  - `render 'email.html', write to %email%` - renders to a variable (template use)
  - `render 'page.html' to #someTarget` - renders to a UI target (UI use)
  - **Proposed:** Same render engine, but the *target* determines behavior. Variable target = template module. UI target (#selector) = UI module. Builder distinguishes based on target syntax.
- **Important for builder:** Builder goals use `render template` heavily
- **Complexity:** Medium

### Tests
- C# + PLang integration for each module
- HTTP tests may need mock server or real endpoints

**Depends on:** Phase 1 (error handling needed for HTTP errors, timeouts, etc.)

---

## Phase 3: Data & Persistence (3-4 days)

**Goal:** Enable stateful applications with database access and configuration.

### 3.1 Database Module (`db/*`)
- Handlers: `select`, `insert`, `update`, `delete`, `execute`, `beginTransaction`, `commitTransaction`, `rollbackTransaction`
- **Parameters:** sql (string), parameters (dict?), connectionString (string?)
- **Design:** Support SQLite (built-in) + pluggable providers
- **Build-time validation:** When user writes a SELECT statement, validate it's valid SQL at build time
- **Returns:** Data with Value=results (list of dicts for select)
- **Complexity:** Large (SQL parsing, parameter binding, connection management, transactions, build-time validation)

### 3.2 Settings Module (`settings/*`)
- Handlers: `get`, `set`, `remove`, `exists`
- **Storage:** SQLite-backed by default (like Runtime1's SettingsService)
- **Pluggable via goal override:** `- override 'settings' call goal MySettings`
  - Developer can replace settings backend with HTTP calls, special storage, etc.
  - Override goal receives the operation (get/set/remove) and key, returns value
- **Complexity:** Small-Medium (small handler + override infrastructure)

### 3.3 Environment Module (`environment/*`)
- Handlers: `get`, `set`
- **Wraps:** System.Environment with PLang conventions
- **Complexity:** Small

### 3.4 App-Level Caching
- Step caching is done (MemoryStepCache). Consider if app-level caching is needed as a separate module.
- Handlers: `cache/get`, `cache/set`, `cache/remove`, `cache/clear`
- **Complexity:** Small (reuse ICache infrastructure)

### Tests
- DB tests: SQLite in-memory, CRUD operations, transactions, SQL validation
- Settings: persistence + override via goal

**Depends on:** Phase 2 (serializer needed for DB result formatting)

---

## Phase 4: Builder Self-Hosting (3-5 days)

**Goal:** Make the builder run natively on Runtime2.

### 4.1 LLM Module - Written in PLang
- **Key insight:** LLM module is just HTTP + structured data. It can be written as a PLang module in `/system/` rather than C#!
- `/system/modules/llm/ask.goal` - makes HTTP POST to OpenAI API, parses JSON response
- Needs: HTTP module (Phase 2), serializer (Phase 2), settings (Phase 3 for API keys)
- **Caching:** LLM response caching via step cache or app cache
- **Complexity:** Medium (PLang code, not C#)

### 4.2 Builder Goal Recompilation
- Take existing `system/builder/*.goal` files
- Build them with `plang p build` to produce Runtime2 .pr files
- Verify each builder goal works on Runtime2
- **This is the milestone:** builder runs on Runtime2 engine

### 4.3 Simplify V1 Bridge
- Once builder self-hosts, the PlangModule bridge (Program.cs) can be simplified
- Executor.Build2() can use Runtime2 engine directly
- **Caution:** Keep V1 path available as fallback during transition

### Tests
- LLM module: mock HTTP responses for unit tests, real API for integration
- Builder: recompile and run sample .goal files, compare .pr output

**Depends on:** Phase 2 (HTTP, template), Phase 3 (settings for API keys), Phase 1 (foreach, error handling, list ops)

---

## Phase 5: Advanced Modules (5-7 days)

**Goal:** Port remaining Runtime1 modules needed for production apps.

### 5.1 Priority Tier (commonly used)
- **Crypto** (`crypto/*`): hash, encrypt, decrypt, sign, verify - Medium
- **Compression** (`compression/*`): zip, unzip - Small
- **Schedule** (`schedule/*`): delay, interval, cron - Medium
- **Logger** (`logger/*`): log at levels - Small
- **Assert** (`assert/*`): assertions for testing - Small
- **Validate** (`validate/*`): input validation - Small

### 5.2 Network Tier
- **WebSocket** (`websocket/*`): connect, send, receive, close - Medium
- **TCP** (`tcp/*`): connect, send, receive - Medium
- **UDP** (`udp/*`): send, receive - Small

### 5.3 Domain Tier (port as needed)
- **Image** (`image/*`): resize, crop, convert - Medium
- **HTML** (`html/*`): parse, select, extract - Medium
- **WebCrawler** (`webcrawler/*`): navigate, scrape - Large
- **XML** (`xml/*`): parse, query, transform - Medium
- **Code** (`code/*`): dynamic C# compilation - Large
- **Python** (`python/*`): Python script execution - Medium

### 5.4 PLang-Written Modules (in /system/)
- Consider which of these can be written as PLang goals instead of C#:
  - Logger (could be event binding + output.write)
  - Assert (could be condition + error/throw)
  - Validate (could be condition + error/throw)
  - Others where the underlying primitives exist

**Depends on:** Phase 3 (many need DB, settings)

---

## Phase 6: Webserver & Infrastructure (3-5 days)

**Goal:** Enable Runtime2 to serve HTTP endpoints.

### 6.1 Webserver Module
- HTTP server using ASP.NET Core Kestrel (like Runtime1)
- Route registration, request handling, response writing
- Middleware pipeline
- SSL/TLS support
- **Largest single module** in Runtime1 - careful port needed
- **Complexity:** Very Large

### 6.2 Middleware System
- Request/response interceptors
- CORS, compression, logging
- Note: Authentication is NOT needed as a middleware - Identity is built into PLang (`%Identity%` gives ed25519/web-standard identity)

### 6.3 UI Module (Critical)
- DOM instructions, layouts, dialogs, notifications
- **Ingi has ideas for this** - will bring specifics when the time comes
- Interplays with Template module (same render engine, different targets)
- **Complexity:** Very Large

**Depends on:** Phase 5 (crypto for SSL)

---

## Phase 7: Cleanup & Deprecation (2-3 days)

**Goal:** Remove Runtime1 code paths and finalize migration.

### 7.1 Remove V1 Engine
- Remove `PLang/Runtime/` once all features work in Runtime2
- Remove `PLang/Modules/` (old module implementations)
- Remove LightInject DI container (Runtime2 uses Engine object graph)

### 7.2 Remove Bridge Code
- Simplify `PlangModule/Program.cs`
- Remove `GoalMapper.cs` (no more Building.Model -> Runtime2 mapping needed)
- Remove old Building models

### 7.3 Final Integration
- Run all 48 Runtime1 test goal directories on Runtime2
- Performance benchmarks: Runtime2 vs Runtime1
- Documentation update

### 7.4 Release
- Merge runtime2 branch to main
- Remove `p` flag requirement (Runtime2 becomes default)

**Depends on:** All previous phases

---

## Cross-Cutting Concerns (Address Throughout)

### Strong Typing Discipline
- Every new module must declare explicit parameter types (never `object` without justification)
- Data.Type must be set correctly on all return values
- TypeMapping must be extended for any new types
- Automatic type coercion where possible, error where not

### OBP Compliance
- Collections own their operations (no external iteration)
- Navigate through object graph, don't decompose
- Per-request state as parameters, per-object state as properties

### Source Generator Compatibility
- Every handler must be `partial class` with `[Action]` attribute
- Parameter records must have virtual properties for source generator
- Test mocks must implement `ICodeGenerated` manually

### Error Pattern Consistency
- All modules return Data.Ok() or Data.Error() - never throw exceptions
- PLang dev errors (validation) vs C# runtime errors (unexpected) are distinct
- Step-level OnErrorGoal catches failures
- Error display via `/system/error/` PLang goals
- Retry logic wraps Step execution

### PLang-First Philosophy
- If it can be written in PLang, write it in PLang (not C#)
- C# only for underlying tech primitives
- Overrides via goal calls, not DLL injection
- `/system/` ships with runtime, `/app/` is user space

### Identity & Signing
- Multi-standard: ed25519 (default) + web standards
- Core-level support (see `SigningService/PlangSigningService.cs/VerifySignature()`)
- `%Identity%` available everywhere, no separate auth module

### Culture & i18n
- CultureInfo support for formatting
- TString type for translatable strings
- Consider culture in all user-facing output

---

## Suggested First 3-Day Sprint

**Day 1:** Phase 0 - Foundation Audit (typing audit, Data.Error() rename, Data.Type flow, /system/ structure, error categories design)
**Day 2:** Phase 1.1 (foreach) + Phase 1.2-1.3 (error handling + retry)
**Day 3:** Phase 1.4 (list operations with IEnumerable chaining) + Phase 1.5 (math with NCalc)

This gives you a feature-complete language runtime in 3 days, ready for I/O modules in the next sprint.
