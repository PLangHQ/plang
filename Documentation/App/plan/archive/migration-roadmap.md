# PLang App Migration - Full Roadmap

---
when saving this file, increase the version number, e.g. migration-roadmap-v1.md => migration-roadmap-v2.md => ...
---

## Context

PLang is migrating from Runtime1 (47+ modules, LightInject DI, BaseProgram pattern) to App (OBP, strongly-typed Data, source-generated handlers, entity hierarchy). 

App currently has 6 modules (variable, file, output, condition, goal, event) with solid core infrastructure (Engine, Memory, Events, Caching, CallStack, TypeMapping, Source Generator). The builder still runs on Runtime1 but produces App .pr artifacts via a bridge module. This roadmap covers the complete migration path.

---

## Phase 0: Foundation Audit (1-2 days)

**Goal:** Ensure the foundation is rock-solid before building modules on top.

### 0.1 Strong Typing Audit
- Audit all `object?` usage in App - ensure each has a justified reason or replace with typed alternative
- Verify TypeMapping covers all PLang primitives and their CLR counterparts
- Confirm Data.GetValue<T>() handles all TypeMapping conversions correctly
- Check that source generator handles all parameter types (primitives, lists, dicts, nested objects)

### 0.2 Data Flow Verification
- Trace a full execution: Goal load -> Step run -> Action execute -> Return -> Variables store
- Verify type preservation through the entire pipeline (value set as `long` stays `long`)
- Ensure Data.Properties flow correctly between steps
- Validate that GoalCall parameters are strongly typed end-to-end

Ingi comments: 
- make sure Data.Type keep it´s type, e.g. when I read a file.json, that it keep Type=json.
- I want to be sure I understand Data object flow through everything. We should be able to say some thing like '- convert %data% to json, write to %json%', the internal convert would see that data.type is yaml, and that use the correct converter to convert to json. a converter that need to be injectable by plang programmer. If he doesn´t know how to convert that is fine, just give error


### 0.3 Error Infrastructure
- **Design decision needed:** Define the App error pattern before any modules use it
- Current: Data.Fail(IError) + step-level OnErrorGoal
- Missing: retry logic, error propagation rules, error event firing
- Create `IError` hierarchy: ActionError, StepError, GoalError, ValidationError
- Define how errors bubble up: Action -> Step -> Goal -> Engine
- **Files:** `PLang/App/Core/ErrorHandler.cs` (new), existing error types in `Errors/`

Ingi comments:
- I dont like Data.Fail, I feel it should be Data.Error because that is what it is.

### 0.4 Tests
- C# tests: Type round-trip tests, Data conversion edge cases, error propagation tests
- PLang tests: `Tests/App/TypeSafety/` - verify types survive variable set/get cycles

**Complexity:** Medium
**Depends on:** Nothing (this is the base)

---

Ingi comments:
I feel we are missing the /system/ and /app/ portion of the runtime. 
we need /system early, /apps when everything is working

/system is anything we can write in plang and not use c#, if there is a chance to do it in plang code instead of c# code then we do it plang

example of this are errors and how we display them
another example are modules can be written in plang if the underlaying tech that is needed exists, such as llm module which just needs http and have some structure data

another example is event binding, e.g. debugger which is just sendin data to a channel. there are many that can be written in plang and not c#. think about it, lot of classes in c# use c# to create it self.

I feel like this a code thing to get right from start, to build strong foundation. Strong foundation is everything!

Culture
- lets keep in mind culture info, so people can define that. this is date, numbers, etc.

Translation
- plang doesn't support multiple languages, my idea is to have string type called tstring, anywhere a translation is needed, such as 'write out..', it would take in tstring, and tstring would then do lookup in hash table and format the output according to it.

External libraries:
External libraries are good to use, they are battle tested. like NCalc for calcuations, or MiniExcel for excel file, etc.

Overriding behaviour
Often we can override something in plang,such as settings and some others. lets call a goal to do the overriding not some dll if possible. like for settings it is goal, beause it's just returning string from say http or some special storage, in that goal it's fine to use dll.


## Phase 1: Core Language Completeness (3-5 days)

**Goal:** Make App a complete language runtime - all control flow and basic data operations.

### 1.1 Loop Module (`loop/foreach`)
- **Handler:** `loop/foreach.cs` - iterate collection, call goal per item
- **Parameters:** collection (list/dict), goalToCall (GoalCall), itemVariableName (string)
- **PLang syntax:** `foreach %items%, call ProcessItem item=%item%`
- **Design:** foreach always calls a goal (PLang v0.1 limitation - no sub-steps)
- **Edge cases:** empty collection, null collection, nested foreach
- **Complexity:** Medium

Ingi comments:
- I like the foreach only calling a goal, no substep, lets keep that. 
- Future loop module version, allow multithreaded. discuss when we are there.
- when collection is empty|null, that is just list.count = 0, when variable is just an object it is list.count = 1.

### 1.2 Error Handling Module (`error/throw`)
- **Handler:** `error/throw.cs` - throw an error that stops step/goal execution
- **Parameters:** message (string), statusCode (int?), type (string?)
- **Integration:** Sets Data.Error and returns Data.Fail()
- **Step-level:** Wire up Step.OnErrorGoal to catch and handle errors
- **Complexity:** Medium

Ingi comments:
- when c# returns error, it is different from plang returns error. When plang returns error, it is the developer telling the user that something is wrong, e.g. 'validate %name% is not empty' this propigates to the user, think for example in web request. but when c# returns error, it is more of underlaying issue, the plang dev didn't expect it, this is more of general error page to the user in web requests. in console we just give him the error.
- Error display should be in plang, e.g. if there is 500 error we use /system/error/500.html to display or fallback /system/error/error.html

### 1.3 Retry Logic
- **Not a module** - it's Step infrastructure (like Cache)
- **Add:** `Step.RetrySettings` (maxRetries, delayMs, backoffMultiplier)
- **Integration:** StepMethods.RunAsync checks retry on Data.Fail()
- **Complexity:** Small

### 1.4 List/Dictionary Module (`list/*`)
- Handlers: `add`, `remove`, `get`, `set`, `count`, `contains`, `indexOf`, `sort`, `filter`, `first`, `last`, `join`, `split`, `unique`, `reverse`, `range`, `flatten`
- **Important:** Operations must preserve strong typing through Data.Type
- **Design:** Lists are `List<object?>` internally but tracked via Data.Type for element type
- **Complexity:** Large (many handlers, but each is small)

Ingi comments:
- in list, chained actions are important, you want to be able to say '- filter %list% where name = 'Ingi', sort by reverse date'. Is it possible to use IEnumerable instead of creating a ToList() each time?

### 1.5 Math Module (`math/*`)
- Handlers: `add`, `subtract`, `multiply`, `divide`, `modulo`, `power`, `sqrt`, `abs`, `round`, `floor`, `ceiling`, `min`, `max`, `random`
- **Design:** Accept numeric types, return appropriate numeric type
- **Complexity:** Medium

Ingi comments:
- see if ncalc library should be used here, I think that the most stable. It allows users also to create complex formulas in string.

### 1.6 Convert Module (`convert/*`)
- Handlers: `toInt`, `toLong`, `toDouble`, `toString`, `toBool`, `toDateTime`, `toJson`, `fromJson`, `toBase64`, `fromBase64`
- **Leverages:** TypeMapping.ConvertTo() for most conversions
- **Complexity:** Medium

Ingi comments:
- I dont really see point in this convert module, plang runtime handles conversion when needed, there is no benefit of saying '- convert %zip% to int' because if %zip% needs to be int when calling a method, it should automatically convert to int so that method can be called, if it cant be converted, then error.

### Tests per module
- C# unit tests: Each handler tested with valid/invalid inputs, type preservation
- PLang integration: `Tests/App/Loop/`, `Tests/App/ErrorHandling/`, `Tests/App/ListOps/`, `Tests/App/Math/`, `Tests/App/Convert/`

**Depends on:** Phase 0 (error pattern must be defined first)

---

## Phase 2: Essential I/O & Processing (3-5 days)

**Goal:** Enable App apps to interact with the outside world.

### 2.1 HTTP Module (`http/*`)
- Handlers: `get`, `post`, `put`, `delete`, `patch`, `head`, `options`
- **Parameters:** url, body, headers (dict), contentType, timeout, bearerToken
- **Returns:** Data with Value=response body, Properties={statusCode, headers, contentType}
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
- **Note:** Some overlap with Convert module - serializer is for structured formats
- **Complexity:** Medium

### 2.4 Template Module (`template/*`)
- Handlers: `render` (Scriban template rendering)
- **Parameters:** template (string), data (object), outputPath (string?)
- **Returns:** rendered string
- **Note:** Already have Scriban in Runtime1's TemplateEngineModule; port the core
- **Important for builder:** Builder goals use `render template` heavily
- **Complexity:** Medium

Ingi comments:
template and ui, is kind of similiar and this is confusing when writing plang code, we need to solve this somehow, e.g. when I just want to render some template for email for instance I do '- render 'email.html', write to %email%', but then I have a website '- render 'email.html' to #someTarget', it's same but targets are different, one is variable the other is some target in ui.

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
- **Returns:** Data with Value=results (list of dicts for select)
- **Complexity:** Large (SQL parsing, parameter binding, connection management, transactions)

Ingi comments:
Here we also need to be aware of the db, so that when user write e.g. a select statement, we can validate that it's a valid sql statement. Yes this is Large.

### 3.2 Settings Module (`settings/*`)
- Handlers: `get`, `set`, `remove`, `exists`
- **Storage:** SQLite-backed (like Runtime1's SettingsService)
- **Complexity:** Small

Ingi comments:
- settings must be pluggable, '- override 'settings' call goal MySettings'. 


### 3.3 Environment Module (`environment/*`)
- Handlers: `get`, `set`
- **Wraps:** System.Environment with PLang conventions
- **Complexity:** Small

### 3.4 App-Level Caching
- Step caching is done (MemoryStepCache). Consider if app-level caching is needed as a separate module.
- Handlers: `cache/get`, `cache/set`, `cache/remove`, `cache/clear`
- **Complexity:** Small (reuse ICache infrastructure)

### Tests
- DB tests: SQLite in-memory, CRUD operations, transactions
- Settings: persistence across goal calls

**Depends on:** Phase 2 (serializer needed for DB result formatting)

---

## Phase 4: Builder Self-Hosting (3-5 days)

**Goal:** Make the builder run natively on App.

### 4.1 LLM Module (`llm/*`)
- Handlers: `ask` (send prompt to LLM, get response)
- **Parameters:** system (string), user (string), model (string?), temperature (double?), maxTokens (int?), responseFormat (string?)
- **Returns:** Data with Value=response text, Properties={model, tokens, cost}
- **Design:** Abstract ILlmService interface, default OpenAI implementation
- **Port from:** Runtime1's LlmService/OpenAiService (significant code)
- **Caching:** LLM response caching (like Runtime1's LlmCaching)
- **Complexity:** Large

### 4.2 Builder Goal Recompilation
- Take existing `system/builder/*.goal` files
- Build them with `plang p build` to produce App .pr files
- Verify each builder goal works on App
- **This is the milestone:** builder runs on App engine

### 4.3 Remove V1 Builder Bridge
- Once builder self-hosts, the PlangModule bridge (Program.cs) can be simplified
- Executor.Build2() can use App engine directly
- **Caution:** Keep V1 path available as fallback during transition

### Tests
- LLM module: mock LLM service for unit tests, real API for integration
- Builder: recompile and run sample .goal files, compare .pr output

**Depends on:** Phase 2 (template module), Phase 1 (foreach, error handling, list ops)

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

### 5.4 App Tier
- **Identity** (`identity/*`): user auth, keys - Medium
- **Message** (`message/*`): messaging (Nostr, etc.) - Medium
- **Blockchain** (`blockchain/*`): Ethereum interactions - Large
- **Desktop** (`desktop/*`): desktop app operations - Medium

**Depends on:** Phase 3 (many need DB, settings)

---

## Phase 6: Webserver & Infrastructure (3-5 days)

**Goal:** Enable App to serve HTTP endpoints.

### 6.1 Webserver Module
- HTTP server using ASP.NET Core Kestrel (like Runtime1)
- Route registration, request handling, response writing
- Middleware pipeline
- SSL/TLS support
- **Largest single module** in Runtime1 - careful port needed
- **Complexity:** Very Large

### 6.2 Middleware System
- Request/response interceptors
- Authentication middleware
- CORS, compression, logging

### 6.3 UI Module
- If needed: DOM instructions, layouts, dialogs
- May be deferred if not critical

Ingi comments: 
yes, UI module is critical, I have ideas here, I will bring when time is.

**Depends on:** Phase 5 (crypto for SSL, identity for auth)

Ingi comments:
What do you identity for auth?? Identity is built into plang, no need for auth. developer does %Identity% and he gets the user identity(edd25519)

---

## Phase 7: Cleanup & Deprecation (2-3 days)

**Goal:** Remove Runtime1 code paths and finalize migration.

### 7.1 Remove V1 Engine
- Remove `PLang/Runtime/` once all features work in App
- Remove `PLang/Modules/` (old module implementations)
- Remove LightInject DI container (App uses Engine object graph)

### 7.2 Remove Bridge Code
- Simplify `PlangModule/Program.cs`
- Remove `GoalMapper.cs` (no more Building.Model -> App mapping needed)
- Remove old Building models

### 7.3 Final Integration
- Run all 48 Runtime1 test goal directories on App
- Performance benchmarks: App vs Runtime1
- Documentation update

### 7.4 Release
- Merge runtime2 branch to main
- Remove `p` flag requirement (App becomes default)

**Depends on:** All previous phases

---

## Cross-Cutting Concerns (Address Throughout)

### Strong Typing Discipline
- Every new module must declare explicit parameter types (never `object` without justification)
- Data.Type must be set correctly on all return values
- TypeMapping must be extended for any new types

### OBP Compliance
- Collections own their operations (no external iteration)
- Navigate through object graph, don't decompose
- Per-request state as parameters, per-object state as properties

### Source Generator Compatibility
- Every handler must be `partial class` with `[Action]` attribute
- Parameter records must have virtual properties for source generator
- Test mocks must implement `ICodeGenerated` manually

### Error Pattern Consistency
- All modules return Data.Ok() or Data.Fail(error) - never throw exceptions
- Step-level OnErrorGoal catches failures
- Retry logic wraps Step execution

---

## Suggested First 3-Day Sprint

**Day 1:** Phase 0 (Foundation Audit) + Phase 1.1 (foreach)
**Day 2:** Phase 1.2-1.3 (error handling + retry) + Phase 1.4 (list operations - start)
**Day 3:** Phase 1.4 (list operations - finish) + Phase 1.5-1.6 (math + convert)

This gives you a feature-complete language runtime in 3 days, ready for I/O modules in the next sprint.
