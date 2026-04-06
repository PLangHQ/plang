# Libraries Refactor — Branch Summary

**Branch:** `plang-coder-_interactive-v1`
**Date:** 2026-02-14
**Commits:** `c10fb8d5`, `44e2fee0`, `04371b68`, `af12b38d`

---

## What Changed

ActionRegistry was replaced by a two-class design: `Library` (one assembly) and `Libraries` (smart collection). This makes external DLL loading a first-class feature and unifies handler resolution across built-in and external modules.

### Before

```
Engine
  .Actions  →  ActionRegistry (single ConcurrentDictionary, one assembly)
```

- All handlers discovered from `Assembly.GetExecutingAssembly()`
- No path for loading external DLLs at runtime
- `RegisterBuiltInModules()` called in Engine constructor

### After

```
Engine
  .Libraries  →  Libraries (list of Library instances)
                    [0] = built-in (PLang's own handlers)
                    [1] = external DLL loaded at runtime
                    [n] = ...
```

- Built-in handlers auto-discovered in `Libraries` constructor
- External DLLs loadable via `library.load` handler or `Libraries.Add()`
- Resolution walks all libraries — first match wins

---

## New Files

| File | Purpose |
|------|---------|
| `PLang/App/modules/Library.cs` | Single library — one assembly's handlers. Owns discovery, registration, lookup. |
| `PLang/App/modules/Libraries.cs` | Smart collection. Walk-the-list resolution. Built-in always `[0]`. |
| `PLang/App/modules/library/load.cs` | `library.load` action handler — loads external DLLs at runtime |
| `PLang/App/modules/library/types.cs` | Return type for `library.load` (name + action count) |
| `PLang.Tests/App/Modules/LibrariesTests.cs` | 40+ tests for Library and Libraries |
| `PLang.Tests/App/Modules/library/LibraryLoadTests.cs` | 7 tests for the load handler |

## Deleted Files

| File | Replaced By |
|------|-------------|
| `PLang/App/modules/ActionRegistry.cs` | `Library.cs` + `Libraries.cs` |

---

## API Changes

### Engine

| Before | After |
|--------|-------|
| `engine.Actions` (`ActionRegistry`) | `engine.Libraries` (`Libraries`) |
| `engine.Actions.GetCodeGenerated(module, action)` | `engine.Libraries.GetCodeGenerated(module, action, context)` |
| `engine.Actions.Register(ns, cls, handler)` | `engine.Libraries.Register(module, action, handler)` |
| Constructor calls `RegisterBuiltInModules()` | `Libraries` constructor auto-discovers |

### Resolution

```csharp
// Old
ICodeGenerated? handler = engine.Actions.GetCodeGenerated("variable", "set");

// New — returns tuple with error info
var (handler, error) = engine.Libraries.GetCodeGenerated("variable", "set", context);
```

### Loading External DLLs

```csharp
// C# API
var lib = new Library("mylib", Assembly.LoadFrom("mylib.dll"));
lib.Discover("MyCompany.Modules");
engine.Libraries.Add(lib);

// PLang syntax
// - use library 'mylib.dll'
```

### Engine.Property (key-value store, also added in this branch)

```csharp
// Store a GoalCall in the engine's Property store
engine.Property["Summary"] = new GoalCall { Name = "GetSummary" };

// Sync access returns the raw GoalCall
var raw = engine.Property["Summary"]; // GoalCall object

// Async Get detects GoalCall and executes the goal
var result = await engine.Property.Get("Summary"); // runs GetSummary, returns result
var typed = await engine.Property.Get<string>("Summary"); // typed variant
```

---

## Library.Discover — How It Works

`Discover(baseNamespace)` scans the library's assembly for types that:
1. Have the `[Action]` attribute
2. Implement `ICodeGenerated`
3. Are not abstract
4. Have a namespace starting with `baseNamespace + "."`

The module name is derived from the namespace segment after `baseNamespace`. For example, `App.modules.variable.Set` registers as module `"variable"`, action `"set"`.

If `baseNamespace` is null, it defaults to `"App.modules"`.

---

## Documentation Updated

All active App docs were updated to replace ActionRegistry references:

| File | Key Changes |
|------|-------------|
| `engine.md` | Properties table, constructors, ResolveAsync docs |
| `modules.md` | Full rewrite: ActionRegistry section → Library/Libraries |
| `README.md` | Architecture diagram, execution flow, file structure |
| `goals-steps.md` | Execution flow references |
| `complete-example.md` | Handler example updated to `[Action]` + `IContext` pattern |
| `plang_object_based_pattern.md` | Engine graph, navigation examples |
| `object_pattern_formal.md` | Example names |
| `good_to_know.md` | Added Libraries refactor architectural note |
| `todos.md` | Marked "Libraries Replaces ActionRegistry" as done |

---

## Test Coverage

**Total: 1167 tests, 0 failures**

### Libraries/Library tests (LibrariesTests.cs)
- Constructor discovers built-in handlers
- Register/Get/Contains — case-insensitive
- GetCodeGenerated — built-in, explicit, type-based, not-found, multi-library first-match-wins
- Library standalone: Discover with correct/wrong namespace, null assembly
- Aggregate queries: Modules, GetActions, Count across libraries (no duplicates)

### library.load handler tests (LibraryLoadTests.cs)
- Nonexistent path → error with "Library not found"
- Valid assembly → adds to `engine.Libraries`
- Discovered actions accessible on added library
- Custom namespace filter → only matching types found
- Null namespace → defaults to built-in namespace
- Return value contains library info
- Added library resolvable via `GetCodeGenerated`

### Engine.ResolveAsync tests (EngineTests.cs)
- Normal value returns as-is
- Null key returns null
- GoalCall value → executes goal, returns result
- Sync indexer returns raw GoalCall (no execution)
- Generic typed variant
- Type mismatch returns default
