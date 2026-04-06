# PLang App Migration Roadmap (v7)

## Context

PLang is migrating from Runtime1 (47+ modules, LightInject DI, BaseProgram pattern) to App (OBP, strongly-typed Data, source-generated handlers, entity hierarchy).

App currently has 6 modules (variable, file, output, condition, goal, event) with solid core infrastructure (App, Memory, Events, Caching, CallStack, TypeMapping, Source Generator). The builder still runs on Runtime1 but produces App .pr artifacts via a bridge module.

### How We Work Each Phase

When starting each phase, the LLM takes a **software architect** mindset:
1. **Critically examine** the phase before jumping in - challenge assumptions, identify risks
2. **Ask hard questions** - don't be a yes man. Push back on design decisions that seem wrong. Ingi has final say, but the LLM should advocate for the best architecture.
3. **Get clarity first** - resolve open questions before writing code
4. **Write a detailed phase plan** - once questions are answered, produce a concrete plan for that specific phase
5. **Get confirmation** - Ingi approves the detailed phase plan before implementation begins

We are designing the next evolution of programming languages. Do this well and proper!

### Key Architectural Principles

1. **PLang over C# whenever possible** - If something can be written in PLang instead of C#, write it in PLang.
2. **`/system/` layer** - PLang-written system code that ships with the runtime.
3. **`/app/` layer** - App-specific PLang code.
4. **Override via goals, not DLLs** - Settings, behaviors, etc. should be overridable by calling PLang goals.
5. **External libraries are good** - Use battle-tested libs: NCalc for math, MiniExcel for Excel, etc.
6. **Culture & Translation** - CultureInfo + TString type for translatable strings.
7. **Identity is built-in & multi-standard** - `%Identity%` gives user identity. ed25519 (default) + web standards.
8. **Automatic type conversion** - Runtime converts types automatically when methods need them.

---

## Architecture (High Level)

> Full detail: [architecture-overview.md](architecture-overview.md) (living document)

```
┌─────────────────────────────────────────────────────────────────┐
│                           ENGINE                                │
│  Id, Name, RootPath                                             │
│                                                                 │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────────┐  │
│  │ AppContext   │  │ FileSystem   │  │ Cache (ICache)         │  │
│  │  Culture     │  │ (IPLangFS)   │  │  MemoryStepCache       │  │
│  │  Events      │  └──────────────┘  └────────────────────────┘  │
│  │  Serializers │                                               │
│  └─────────────┘                                                │
│                                                                 │
│  ┌─────────────────┐  ┌──────────────────┐  ┌───────────────┐  │
│  │ Actions          │  │ Serializers       │  │ Goals         │  │
│  │ (ActionRegistry) │  │ (SerializerReg)   │  │  Get/Load/Run │  │
│  │  module.action   │  │  Json, Text, ...  │  │  .pr files    │  │
│  │  → ICodeGenerated│  └──────────────────┘  └───────────────┘  │
│  └─────────────────┘                                            │
│                                                                 │
│  ┌──────────────┐                                               │
│  │     IO       │   App-level router → delegates to actor IO │
│  └──────────────┘                                               │
│                                                                 │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                      │
│  │  System  │  │ Service  │  │   User   │  ← Actors (lazy)     │
│  │  Actor   │  │  Actor   │  │  Actor   │                      │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘                      │
│       │              │              │                            │
│       ▼              ▼              ▼                            │
│  ┌─────────────────────────────────────────────┐                │
│  │              PLangContext                    │                │
│  │  Variables  (variables, %var%, !system)    │                │
│  │  CallStack    (goal→step tracking)           │                │
│  │  Events       (System scope + User scope)    │                │
│  │  IO           (per-actor channels)           │                │
│  │  Goal/Step    (current execution pointers)   │                │
│  └─────────────────────────────────────────────┘                │
└─────────────────────────────────────────────────────────────────┘

Execution: Goal → Steps → Actions
┌────────┐     ┌─────────────────┐     ┌──────────────────────┐
│  Goal  │────▶│  Steps : List   │────▶│  Actions : List      │
│  .pr   │     │  [0] Step       │     │  module: "variable"  │
│  file  │     │  [1] Step       │     │  action: "set"       │
│        │     │  ...            │     │  params: List<Data>  │
└────────┘     └─────────────────┘     │  return: List<Data>  │
                                       └──────────┬───────────┘
                                                  │
                                                  ▼
                                       ┌──────────────────────┐
                                       │  Action ([Action])     │
                                       │  partial class         │
                                       │  partial properties    │
                                       │  Run() → Data          │
                                       └──────────────────────┘

Modules (13 implemented):
  variable (set,get,exists,remove,clear)    condition (if)
  file (read,save,copy,move,delete,...)     goal (call)
  output (write)                            event (before/after/skip/remove)
  loop (foreach)                            error (throw)
  list (add,remove,sort,filter,...)         math (add,round,random,...)
  convert (toInt,toJson,fromJson,...)       assert (equals,isTrue,...)
  mock (intercept,verify,reset)
```

---

## Phase Overview

| Phase | Title | Status | Detail |
|-------|-------|--------|--------|
| **0** | [Foundation Audit](phase0/README.md) | COMPLETED | Core typing, errors, /system/, TString, tests |
| **1** | [Core Language Completeness](phase1/README.md) | IN PROGRESS | Loop, error throw, retry, list, math, test, mock, variable resolution |
| **2** | [Essential I/O & Processing](phase2/README.md) | NOT STARTED | HTTP, terminal, serializer, template |
| **3** | [Data & Persistence](phase3/README.md) | NOT STARTED | Database, settings, environment, caching |
| **4** | [Builder Self-Hosting](phase4/README.md) | NOT STARTED | LLM module, builder recompilation, V1 bridge |
| **5** | [Advanced Modules](phase5/README.md) | NOT STARTED | Priority/network/domain tiers, PLang-written modules |
| **6** | [Webserver & Infrastructure](phase6/README.md) | NOT STARTED | Webserver, middleware, UI |
| **7** | [Cleanup & Deprecation](phase7/README.md) | NOT STARTED | Remove V1, remove bridge, final integration, release |

## Dependency Graph

```
Phase 0 (DONE)
  └── Phase 1 (loop, error, retry, list, math, test, mock, variable resolution)
        └── Phase 2 (HTTP, terminal, serializer, template)
              └── Phase 3 (database, settings, environment, caching)
                    ├── Phase 4 (builder self-hosting)
                    └── Phase 5 (advanced modules)
                          └── Phase 6 (webserver, middleware, UI)
                                └── Phase 7 (cleanup & release)
```

## Cross-Cutting Concerns

See [cross-cutting-concerns.md](cross-cutting-concerns.md) for standards that apply to all phases: strong typing, OBP compliance, source generator compatibility, error patterns, PLang-first philosophy, identity, and culture.

## Archive

Previous roadmap versions are in [archive/](archive/).

## Testing

See [test_plan.md](test_plan.md) for the testing app design (Phase 1.7).
