# PLang App Migration - Full Roadmap (v6)

---
when saving this file, increase the version number, e.g. migration-roadmap-v5.md => migration-roadmap-v6.md => ...
---

## Context

PLang is migrating from Runtime1 (47+ modules, LightInject DI, BaseProgram pattern) to App (OBP, strongly-typed Data, source-generated handlers, entity hierarchy).

App currently has 6 modules (variable, file, output, condition, goal, event) with solid core infrastructure (App, Memory, Events, Caching, CallStack, TypeMapping, Source Generator). The builder still runs on Runtime1 but produces App .pr artifacts via a bridge module. This roadmap covers the complete migration path.

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

## Phase 0: Foundation Audit ✅ COMPLETED

See [PHASE0-REPORT.md](phase0/PHASE0-REPORT.md) for details.

- 0.1 Rename `Data.Fail()` → `Data.FromError()` ✅
- 0.2 Error Categories (Application vs Runtime) ✅
- 0.3 `/system/error/` Layer ✅
- 0.4 Type Preservation Gaps ✅
- 0.5 TString & CultureInfo ✅

---

## Phase 0.6: Variable Resolution (NEW)

**Goal:** Bridge the gap between Runtime1's rich variable expressions and App's current simple path navigation. Then go beyond Runtime1 with user-extensible variable properties.

### What App Has Today

| Feature | Status | Implementation |
|---------|--------|---------------|
| Simple variables `%name%` | ✅ | Variables.Get() |
| Dot navigation `%user.name%` | ✅ | Data.GetChild() → ValueNavigators |
| Array indexing `%items[0]%` | ✅ | ListNavigator |
| Variable indices `%items[%idx%]%` | ✅ | Variables.ResolveVariablesInPath() |
| Named list accessors `%items.first%`, `%items.last%`, `%items.random%`, `%items.count%` | ✅ | ListNavigator |
| Dictionary access `%dict.key%` | ✅ | DictionaryNavigator |
| JSON string navigation `%jsonStr.key%` | ✅ | JsonStringNavigator |
| CLR property access `%obj.Prop%` | ✅ | ObjectNavigator (reflection) |
| Implicit first delegation `%addresses.street%` → `[0].street` | ✅ | ListNavigator |
| Dynamic system vars `%Now%`, `%NowUtc%`, `%GUID%` | ✅ | DynamicData in Variables |
| TString `%var%` resolution in strings | ✅ | TString.Resolve() |
| Case-insensitive lookups | ✅ | All navigators |

### What Runtime1 Has That App Lacks

| Feature | Runtime1 | Gap |
|---------|----------|-----|
| **CLR method calls** `%name.ToUpper()%`, `%date.AddDays(5)%` | MethodExtractor via reflection | Not implemented |
| **CLR property access on value types** `%now.Year%`, `%date.DayOfWeek%` | ObjectExtractor | ObjectNavigator exists but only on reference types stored in Variables — DynamicData returns the raw value, needs re-navigation |
| **Date arithmetic shorthand** `%now+1d%`, `%now-3months%`, `%date+5hours%` | CalculateDate() — regex parses `+/-` + time units | Not implemented |
| **Math in paths** `%quantity*price%`, `%a+b%` | MathPlan — supports +,-,*,/,^ on numerics | Not implemented |
| **Special prefix variables** `%!guid%`, `%!now%`, `%!Variables%`, `%!Goal%`, `%!Step%`, `%!Identity%`, `%!Error%` | ReservedKeywords with `!` prefix | Partially done — `%Now%`, `%NowUtc%`, `%GUID%` exist without `!` prefix. Missing: `%!Variables%`, `%!Goal%`, `%!Step%`, `%!StepIndex%`, `%!Error%`, `%!Identity%`, `%!CallStack%` |
| **Settings access** `%Settings.KEY%` | SettingsService lookup | Not implemented (needs Settings module in Phase 3) |
| **NCalc expressions** `calculate '(%price% * %qty%)'` | VariableModule + NCalc | Planned for Phase 1.5 Math module |
| **Recursive variables** `%user.%propName%%` | Multi-pass resolution | Not implemented |
| **Property extraction** `%result!properties%`, `%result!raw%` | `!` suffix accessor | Not implemented |

### What App Should Add Beyond Runtime1

#### 0.6.1 — Method Calls on Variables

Enable `%name.ToUpper()%`, `%date.AddDays(5)%`, `%now.Year%`, `%list.Count%` etc.

**Design:**
- Add a **MethodNavigator** to the ValueNavigators chain
- Parse method-call syntax: `key(args)` — detect trailing `()` or `(...)`
- Use reflection to invoke the method on the value object
- Parameter parsing: support literals (numbers, quoted strings, booleans)
- Also handles property access on value types (e.g., `%now.Year%` is a property, not a method)

**Scope:**
- New: `PLang/App/Memory/Navigators/MethodNavigator.cs`
- Modify: `ValueNavigators.cs` — add MethodNavigator to the chain
- Modify: `Data.GetChild()` — must detect `segment(args)` vs `segment` and route accordingly

**Key decisions:**
- Security: Which methods are allowed? Runtime1 allows *any* public method. We should do the same for now but consider a whitelist/blocklist in future.
- Async: Some methods return Task — need to handle. But Variables.Get is sync. **Decision needed:** keep Get sync and disallow async methods for now, or make Get async?

#### 0.6.2 — Date Arithmetic Shorthand

Enable `%now+1d%`, `%date-3months%`, `%now+5hours%`.

**Design:**
- Detect `+` or `-` in variable names after the root resolves to a DateTime
- Parse: `{number}{unit}` where units are: `micro`, `ms`, `sec`/`second`/`seconds`, `min`/`minute`/`minutes`, `hour`/`hours`, `d`/`day`/`days`, `month`/`months`, `year`/`years`
- Call the appropriate DateTime method (AddDays, AddMonths, etc.)

**Scope:**
- New: `PLang/App/Memory/Navigators/DateArithmeticNavigator.cs` — or integrate into Variables.Get() since `+` isn't a dot-path segment
- Modify: `Variables.Get()` — detect `+/-` pattern after root resolution

#### 0.6.3 — Numeric Math in Paths

Enable `%quantity*price%`, `%a+b%`, `%total/count%`.

**Design:**
- When a variable name contains `+`, `-`, `*`, `/`, `^` and the operands resolve to numbers, compute the result
- Parse left and right operands, resolve each from Variables, apply operator
- This overlaps with 0.6.2 (date arithmetic uses `+`/`-`) — date check takes priority

**Scope:**
- Integrate into Variables.Get() — detect operators, resolve operands, compute

#### 0.6.4 — System Context Variables (`!` prefix)

Enable `%!Goal%`, `%!Step%`, `%!StepIndex%`, `%!Error%`, `%!Variables%`, `%!Identity%`, `%!CallStack%`.

**Design:**
- The `!` prefix pattern from Runtime1 is useful for separating system state from user variables
- These need runtime context (current goal, step, etc.) which Variables doesn't have
- **Option A:** Register DynamicData for each, where the factory function reaches into App/Context
- **Option B:** Variables.Get() intercepts `!` prefix and delegates to a context provider
- **Recommendation:** Option B — cleaner separation, Variables stays focused on user data

**Scope:**
- New interface: `ISystemVariableProvider` with `object? Resolve(string name)`
- Register provider in Variables (or inject via App)
- Provider looks up: Goal, Step, StepIndex, Error, Identity, CallStack from the current PLangContext

#### 0.6.5 — Extensible Variable Properties (NEW — Beyond Runtime1)

**This is the big new capability.** Allow PLang developers to register custom transformations on variables.

**PLang syntax:**
```plang
- add variable property 'md', call goal ConvertToMd
- add variable property 'summary', call goal Summarize
```

**Usage after registration:**
```plang
- set %content% = 'Hello **world**'
- write out %content.md%      / calls ConvertToMd with %content% value
- write out %content.summary%  / calls Summarize with %content% value
```

**Design:**
- This makes variable property access **async** — calling a goal is async
- Registered properties are stored in a dictionary: `name → GoalCall`
- When a path segment matches a registered property name, the runtime calls the goal instead of doing normal navigation
- The goal receives the current value as a parameter and returns the transformed value
- **Caching:** Results can be cached per value+property to avoid repeated goal calls

**Key questions:**
1. **Where does the registry live?** On App? On Variables? Recommendation: App-level, since it's a cross-cutting concern
2. **Async implications:** `Data.GetChild()` is currently sync. Variable property extension makes it async. This ripples up through `Variables.Get()` and `TString.Resolve()` — all need async variants.
3. **Naming collision:** What if a CLR property has the same name as a user-registered property? User registration should win (explicit override).
4. **Scope:** Registration is per-app (not per-goal). Registered in Start.goal or a setup goal.

**Scope:**
- New: `PLang/App/Memory/VariablePropertyRegistry.cs` — registry of name → GoalCall mappings
- New: `PLang/App/modules/variable/addproperty.cs` — handler for `add variable property` action
- Modify: `Data.GetChild()` → add async variant `GetChildAsync()` that checks registry
- Modify: `Variables.Get()` → async variant for when extensible properties are involved
- Modify: `TString.Resolve()` → async variant
- Modify: `App` — hold the VariablePropertyRegistry

**This is a Phase 2+ implementation** — requires goal.call module and async infrastructure. The design should be locked in Phase 0.6 so the foundation supports it.

#### 0.6.6 — Recursive Variable Resolution

Enable `%user.%propName%%` — variable names inside variable names.

**Design:**
- Already partially handled by `ResolveVariablesInPath()` for bracket indices
- Extend to also resolve `%var%` inside the path itself before navigation
- Multi-pass: resolve inner `%...%` first, then navigate the resulting path

**Scope:**
- Modify: `Variables.Get()` or `CleanName()` — detect and resolve nested `%var%` references

### Implementation Order

The sub-phases have dependencies:

1. **0.6.1 Method Calls** — standalone, highest value (enables .ToUpper(), .Year, .Count, .AddDays() etc.)
2. **0.6.4 System Variables** — standalone, needed for debugging and error handling
3. **0.6.2 Date Arithmetic** — depends on 0.6.1 being skippable (they overlap in parsing `+`)
4. **0.6.3 Numeric Math** — depends on 0.6.2 (shared operator parsing)
5. **0.6.6 Recursive Variables** — standalone, small
6. **0.6.5 Extensible Properties** — depends on goal.call async infrastructure, designed now, built later

### Tests

- **C# tests:** `PLang.Tests/App/Memory/VariableResolutionTests.cs` — method calls, date arithmetic, math, system vars, recursive resolution
- **PLang tests:** `Tests/App/Variables/` — end-to-end: set variable, access via method call, date math, etc.

**Complexity:** Large (many sub-features, async implications for extensible properties)
**Depends on:** Phase 0 complete ✅. Extensible properties (0.6.5) depends on Phase 1 (goal.call async).

---

## Phase 1: Core Language Completeness (3-5 days)

**Goal:** Make App a complete language runtime - all control flow and basic data operations.

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
- Uses `Data.FromError()`
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

### 1.7 Testing App (COMPLETED)
- **Assert module** (`assert/*`): 9 handlers (equals, notEquals, isTrue, isFalse, isNull, isNotNull, contains, greaterThan, lessThan)
- **Test runner** (`plang p !test`): discovers `*.test.goal` files, runs each in isolated app, reports pass/fail summary
- 12 test suites passing across all modules

### 1.8 Mock Module (COMPLETED)
- **`mock.intercept`** — Register a mock for a module.action pattern. Supports return value, goal-based, and spy mode
- **`mock.verify`** — Assert mock was called exactly N times (uses AssertionError)
- **`mock.reset`** — Clear a specific mock or all mocks
- **Call tracking** — MockHandle.Calls records parameters and timestamps for each invocation
- **Parameter matching** — Regex-based matching with `*` wildcard sugar
- **Architecture:** Uses BeforeAction events + EventOverride mechanism. No new event infrastructure needed.
- 13 test suites passing (12 existing + Mock)

### Tests per module
- C# unit tests: Each handler tested with valid/invalid inputs, type preservation
- PLang integration: `Tests/App/Loop/`, `Tests/App/ErrorHandling/`, `Tests/App/ListOps/`, `Tests/App/Math/`, `Tests/App/Mock/`

**Depends on:** Phase 0 (error pattern and Data.Type flow must be defined first)

---

## Phase 2: Essential I/O & Processing (3-5 days)

**Goal:** Enable App apps to interact with the outside world.

### 2.1 HTTP Module (`http/*`)
- Handlers: `get`, `post`, `put`, `delete`, `patch`, `head`, `options`
- **Parameters:** url, body, headers (dict), contentType, timeout, bearerToken
- **Returns:** Data with Value=response body, Properties={statusCode, headers, contentType}
- **Data.Type set from response Content-Type** (e.g., "application/json")
- **Design decision:** Use HttpClient (injected via App or singleton with pool)
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
  - **Proposed:** Same render app, but the *target* determines behavior. Variable target = template module. UI target (#selector) = UI module. Builder distinguishes based on target syntax.
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

**Goal:** Make the builder run natively on App.

### 4.1 LLM Module - Written in PLang
- **Key insight:** LLM module is just HTTP + structured data. It can be written as a PLang module in `/system/` rather than C#!
- `/system/modules/llm/ask.goal` - makes HTTP POST to OpenAI API, parses JSON response
- Needs: HTTP module (Phase 2), serializer (Phase 2), settings (Phase 3 for API keys)
- **Caching:** LLM response caching via step cache or app cache
- **Complexity:** Medium (PLang code, not C#)

### 4.2 Builder Goal Recompilation
- Take existing `system/builder/*.goal` files
- Build them with `plang p build` to produce App .pr files
- Verify each builder goal works on App
- **This is the milestone:** builder runs on App app

### 4.3 Simplify V1 Bridge
- Once builder self-hosts, the PlangModule bridge (Program.cs) can be simplified
- Executor.Build2() can use App app directly
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
- ~~**Assert** (`assert/*`): assertions for testing~~ - COMPLETED in Phase 1.7
- ~~**Mock** (`mock/*`): test isolation/mocking~~ - COMPLETED in Phase 1.8
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
- CORS, compression, logging
- Note: Authentication is NOT needed as a middleware - Identity is built into PLang (`%Identity%` gives ed25519/web-standard identity)

### 6.3 UI Module (Critical)
- DOM instructions, layouts, dialogs, notifications
- **Ingi has ideas for this** - will bring specifics when the time comes
- Interplays with Template module (same render app, different targets)
- **Complexity:** Very Large

**Depends on:** Phase 5 (crypto for SSL)

---

## Phase 7: Cleanup & Deprecation (2-3 days)

**Goal:** Remove Runtime1 code paths and finalize migration.

### 7.1 Remove V1 App
- Remove `PLang/Runtime/` once all features work in App
- Remove `PLang/Modules/` (old module implementations)
- Remove LightInject DI container (App uses App object graph)

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
- All modules return Data.Ok() or Data.FromError() - never throw exceptions
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
