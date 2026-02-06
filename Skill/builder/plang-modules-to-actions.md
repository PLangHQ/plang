# PLang: Modules → Actions Rename

## What this is

A rename and conceptual shift in PLang's architecture. What we currently call "Modules" should be called "Actions." This isn't cosmetic — it changes how we think about the builder, the docs, and the runtime interface.

## Why

PLang has goals. Each goal has steps. Each step maps to an **action** — something the user wants to do. Calling the backing code "modules" is wrong because "module" describes how *we* organized the C# code. "Action" describes what the *user* is doing. The user writes `- select * from users`, and that's a `db.select` action. They don't care that it lives in a DbModule class.

This matters most for the builder (the LLM that compiles .goal files into .pr files). When the builder prompt says "pick a module," the LLM thinks about code organization. When it says "pick an action," the LLM thinks about intent — which is what the user wrote in the first place. Better alignment between user intent and builder behavior.

## The naming convention

`action.method` — e.g., `db.select`, `http.get`, `var.set`, `file.read`

The dot notation groups by domain (db, http, file, var, output, condition, loop, etc.) and the method is the specific operation. This maps directly to C# — there's still a class per domain with a method per operation. The rename is at the interface level: what the builder sees, what the docs call them, what the .pr files reference.

## Current builder flow (two LLM passes)

1. **Pass 1 (BuildGoal):** LLM sees the entire goal + list of module descriptions (from .md files). Returns `[{stepIndex, module, intent}]` for each step.
2. **Pass 2 (BuildStep):** For each step individually, LLM sees the step text + intent + methods available on the assigned module. Returns `{method, data, return}`.

See the .goal files in the builder: `Build.goal`, `BuildGoal.goal`, `BuildStep.goal`, `AssignModule.goal`, `HandleBuildStepFailure.goal`, `RetryBuildStep.goal`.

## New builder flow (single smart pass + on-demand detail)

### Pass 1 — the primary builder

LLM sees the entire goal + a compact action summary. The action summary is a generated .md listing all actions, their methods, DTOs, and DTO inheritance. Example:

```md
# db
Database operations
## select - Query data from tables, parameters: db.Query
## execute - Add/modify/delete records, parameters: db.Execute

# http
HTTP requests
## get - Fetch from URL, parameters: http.Get
## post - Send data to URL, parameters: http.Post

# var
Variable operations
## set - Create or update a variable, parameters: var.SetValue
## set_default - Set only if empty, parameters: var.SetDefault
## remove - Remove from memory, parameters: var.Remove
## load - Read from persistence, parameters: var.Load
## store - Write to persistence, parameters: var.Store

DTOs
db.IDbExecute : {sql: string, parameters?: dict<string, object>}
db.Query : IDbExecute
db.Execute : IDbExecute
http.IRequest : {url: string, headers?: dict<string, string>, timeout?: int}
http.Get : IRequest
http.Post : IRequest & {body?: object, contentType?: string}
var.SetValue : {name: string, value: object, type?: string}
...
```

For each step, the LLM returns either:

- **Complete result:** `{action, method, data, return}` — it had enough info from the summary to build the full data object. This is the common case for well-understood actions like db, http, var, conditions, loops, output.
- **Needs detail:** `{action, method, needsDetail: true}` — it picked the action and method but needs the full DTO spec to construct the data object correctly. This should be rare.

### Pass 2 — on-demand only

Only for steps that returned `needsDetail: true`. Load the full .md for just that specific action (with complete DTO field descriptions, validation rules, examples) and ask the LLM to build the data object for that one step.

### Why this is better

- Most builds become a single LLM call. Common patterns (db, http, conditions, loops, var, output) are well-understood — the LLM handles them from the summary alone.
- The action summary is compact. All actions + DTOs fit in one prompt because each method is one line + the DTOs use inheritance.
- Pass 1 still sees the full goal for context (step 3 might reference step 2's output).
- The two-pass overhead only applies to unusual/complex steps.

## C# side

Each action is still a C# class with methods. The rename is:
- `BaseModule` → `BaseAction` (or similar)
- `ModuleRegistry` → `ActionRegistry`
- Class names: `DbModule` → `DbAction`, `HttpModule` → `HttpAction`, etc.
- The .md files that describe available actions are **generated from C#** using reflection or source generators. The C# is the source of truth, the .md is its projection for the builder.

### File and class naming

Current pattern: `VariableModule.Program.cs` with class `VariableModule` containing methods like `SetVariable(...)`.

New pattern: `variable.cs` with class named after the action domain. Methods are lowercase, named after the action method, and take a single typed Data object, returning `object`. Example:

- `VariableModule.Program.cs` → `variable.cs`
- `DbModule.Program.cs` → `db.cs`
- `HttpModule.Program.cs` → `http.cs`
- `FileModule.Program.cs` → `file.cs`
- etc.

Method signatures follow the same pattern:

```csharp
// OLD
public async Task<object?> SetVariable(string name, object value, string? type) { ... }
public async Task<object?> GetHttp(string url, Dictionary<string,string>? headers, int timeout) { ... }

// NEW
public object set(SetValue data) { ... }
public object get(Get data) { ... }
```

The method name matches the action method (`var.set` → method `set`), the parameter is always a single typed DTO, and the return is `object`. No more parameter explosion.

Each method has its own strictly typed DTO parameter object. DTOs use inheritance where methods share structure (e.g., all db methods share `IDbExecute` with `sql` and `parameters`). Example:

```csharp
public class DbAction : BaseAction
{
    public async Task<GoalResult> Select(Query query) { ... }
    public async Task<GoalResult> Execute(Execute execute) { ... }
}

// DTOs
public interface IDbExecute {
    string Sql { get; set; }
    Dictionary<string, object>? Parameters { get; set; }
}

public class Query : IDbExecute { ... }
public class Execute : IDbExecute { ... }
```

## Documentation side

The docs split into two sections:

**Language** — how PLang works (goals, steps, variables syntax, scope, dot notation, events, error handling, identity). These are concepts, not actions.

**Actions** — what you can tell PLang to do. One page per action domain (var, db, http, file, etc.). Each page lists methods with DTOs. The action docs mirror the generated .md the builder uses — same names, same structure.

The sidebar changes from:

```
Modules          →    Language
  Variables             Goals & Steps
  Database              Variables
  HTTP                  Events
  ...                   Error Handling
                        ...
Concepts
  Goals & Steps        Actions
  Variable Scope         var
  Events                 db
  ...                    http
                         file
                         ...
```

## What to rename / refactor

This is a summary of the areas affected. Explore the codebase to find all references.

1. **Builder .goal files** — rename module references to action references. `AssignModule.goal` → `AssignAction.goal`. The LLM prompts in `llm/SelectModules.llm` → `llm/SelectActions.llm` (or similar). The builder scheme changes from `{stepIndex, module, intent}` to the new format.

2. **C# runtime classes** — `BaseModule` → `BaseAction`, `ModuleRegistry` → `ActionRegistry`, all `*Module` classes → `*Action`. Namespaces too.

3. **Generated .md files** — the code that generates module descriptions for the builder needs to output action descriptions instead. Format: `action.method` with DTOs and inheritance.

4. **Step model** — `Step.ModuleName` → `Step.ActionName` (or `Step.Action`). The .pr JSON files will reference actions instead of modules.

5. **Documentation** — restructure from module-organized to language + actions split.

6. **Builder flow** — explore collapsing the two-pass build into single-pass-with-on-demand-detail as described above. This is the bigger change and can be a separate phase.

## Suggested phasing

**Phase 1: Rename.** Pure rename of Module→Action across C#, .goal files, .pr format, and docs. No behavior change. Everything still works the same way, just called "actions" instead of "modules."

**Phase 2: Builder consolidation.** Rework the builder flow from two-pass to single-pass-with-on-demand-detail. Restructure the generated .md format to support the compact action summary with DTOs and inheritance. This is where the real benefit kicks in.

**Phase 3: Docs restructure.** Split documentation into Language + Actions sections. Generate action docs from the same C# source that generates the builder .md files.
