# Action Catalog

The action catalog is the structured list of modules, actions, parameters, and examples that the builder sends to the LLM as part of the system prompt. It's what the LLM uses to translate a natural-language step into a `{module, action, parameters}` mapping.

This doc is for anyone adding or editing action handlers — what metadata you attach, how it flows into the prompt, and how to keep the catalog useful.

## Where the catalog lives

1. **Source**: C# action handler classes in `PLang/app/modules/<module>/<action>.cs`. Attributes on these classes (and their properties) are the single source of truth.
2. **Builder pulls it**: `app.modules.Describe()` walks the registered handlers, reads the attributes, and emits a collection of `Action` records with: `Module`, `ActionName`, `Cacheable`, `Parameters`, `Description`, `ModuleDescription`, `IsModifier`, `Examples`, `ReturnType`.
3. **Template renders it**: `system/actions/v2/summary.md` is a Liquid template that groups the records into a `# Actions` section and a `# Modifiers` section and formats each entry.
4. **Prompt injects it**: `system/builder/llm/BuildGoal.llm` pulls the rendered output in via `{{ actionSummary }}`, right under the `## Available Actions` heading.

So: an attribute you add on a class today changes what the LLM sees the next time any goal is built.

## What the LLM sees

Rendered output (excerpt):

```
Notation: `module.action Name([type?=default])` — `?` = optional, `=val` is default, `%var%` = runtime variable reference.
`tstring` = translatable string; `any` = untyped/dynamic value.

# Actions

## file — Read, write, list, and delete files through the configured filesystem
- file.read — Read a file's bytes or text, returned in %!data%
  (Path([path]), Encoding([string?]))
  e.g. `read file.txt, write to %content%` → file.read Path([path] file.txt) | variable.set Name([string] %content%), Value([object] %!data%)

## error — Error handling: throw errors from a step, or wrap the preceding action with retry/handle-goal/ignore semantics
- error.throw [no-cache] — Immediately fail the step with a structured error message, status code, and optional key
  (Message([string]), StatusCode([int = 500]), Key([string?]))

# Modifiers

Modifiers wrap the preceding action in the same step. They never stand alone — a step that starts with a modifier is invalid…

## error — Error handling: throw errors from a step, or wrap the preceding action with retry/handle-goal/ignore semantics
- error.handle [no-cache] — Intercept errors from the preceding action; optionally retry, call a goal, or suppress the error
  (StatusCode([int?]), Key([string?]), Message([string?]), Goal([goal.call?]), RetryCount([int?]), RetryOverMs([int?]), Order([errororder?(GoalFirst|RetryFirst)]), IgnoreError([bool = false]))
```

A module with a mix of action and modifier classes (like `error` here) gets its `##` header in both sections, each listing only the actions that belong there. This is intentional — it keeps module context present while cleanly separating standalone actions from wrappers.

## Attributes you apply

All attributes live in `app.modules` (plus the built-in `System.ComponentModel.Description`).

### On the action class

| Attribute | Required? | Purpose |
|---|---|---|
| `[Action]` or `[Action("name")]` | Yes | Marks the class as a PLang action handler. Without this the class is ignored by the source generator and `Describe()`. `Cacheable = false` opts out of builder-side LLM caching. |
| `[System.ComponentModel.Description("…")]` | Strongly recommended | One-sentence, present-tense, verb-first description. Rendered inline after the action name. Must add information the signature doesn't — "Stores a value" on `variable.set` is noise; "Stores a value under the named key in the current variable scope" teaches. |
| `[ModuleDescription("…")]` | One per module namespace | One sentence describing the whole module. Apply to exactly one class per namespace (alphabetically first by convention). `Describe()` finds it once per module and attaches it to every action record in that namespace. |
| `[Modifier(Order = N)]` | Modifier actions only | Marks an action as a modifier — it wraps the preceding action instead of standing alone. Drives both the runtime fold order (lower N = outer wrapper) and the catalog routing. Modules can mix modifiers and non-modifiers (`error.throw` is not a modifier, `error.handle` is). |
| `[Example(plang, mapping)]` | Optional; repeatable | Natural-language step on the left, the formal mapping on the right. See the Examples section below for the shape rules. |

### On action properties (parameters)

| Attribute | Purpose |
|---|---|
| `[IsNotNull]` | Fails validation if the parameter is missing at build time. The LLM sees this reflected implicitly — non-nullable properties render without the trailing `?`. |
| `[Default(value)]` | Provides a default. Renders as `Name([type = default])` in the catalog so the LLM knows it's optional. |
| `[Code]` | Hides the property from the catalog — it's injected by the source generator at runtime from `app.Code.Get<T>()`. |
| `[IsInitiated]` | Source-generator hint for Data properties that must be non-null before Run() (runtime concern, not catalog). |

### On the parameter's type

| Type shape | Catalog render |
|---|---|
| `string` / `int` / `long` / `bool` / `double` / `decimal` / `DateTime` / `Guid` | Lowercased: `string`, `int`, etc. |
| Nullable (`string?`, `int?`) | Trailing `?`. |
| `Data<T>` (LazyParam) | Unwrapped to `T`'s rendering. |
| `Data<app.variables.Variable>` | Renders as `string` — the slot names a variable rather than carrying a value. `Variable` implements `IRawNameResolvable`, so `Data.As<T>` resolves it from the raw slot string (`%x%` and bare `x` both produce `Variable { Name = "x" }`). The handler reads `Foo.Value` to get the `Variable`; implicit conversion to `string` covers method-call boundaries. |
| Enum or type with a `static string[] ValidValues` | Type name plus inline enumeration: `operator(==|!=|>|<|…)`. |
| `List<T>`, `IList<T>`, arrays | `list<T>`. |
| `Dictionary<K,V>`, `IDictionary<K,V>`, `ConcurrentDictionary<K,V>`, `ReadOnlyDictionary<K,V>`, etc. | `dict<K,V>`. |
| `HashSet<T>`, `ISet<T>`, `IEnumerable<T>`, `ICollection<T>`, `IReadOnlyList<T>` | `list<T>`. |
| `byte[]` | `bytes`. |

Type mapping lives in `PLang/app/utils/TypeMapping.cs`. If you add a new generic collection type and see it leak as `somecollection<…>` in the catalog, add a branch to `GetTypeName()`.

## Writing good `[Example]` attributes

The `mapping` string must use **formal syntax** — the same shape the LLM is asked to emit in its `formal` response field:

```
module.action Name([type] value), Name2([type] value) | next-action Name([type] value)
```

- Params comma-separated within an action; actions pipe-separated (` | `) within a step.
- `[type]` tag precedes each value. Use the same type token the catalog renders (`string`, `path`, `object`, `int`, `bool`, `goal.call`, etc.). Variable-name slots render as `[string]` with a `%var%` value (e.g. `Name([string] %foo%)`).
- String values with spaces or commas are quoted; numbers, bools, and `%var%` refs are unquoted.
- Structured values (e.g. `goal.call` payloads) stay as JSON after the type tag: `GoalName([goal.call] {"name":"Greet","parameters":[...]})`.
- When the plang step captures output with `write to %var%`, include the implicit `variable.set` as the last pipe segment: `| variable.set Name([string] %var%), Value([object] %!data%)`.

Concrete example from `crypto/verify.cs`:

```csharp
[Example("verify %content% against %hash%, write to %isValid%",
         "crypto.verify Data([object] %content%), Hash([string] %hash%), Algorithm([string] keccak256) | variable.set Name([string] %isValid%), Value([object] %!data%)")]
```

**Keep examples at one per action**, and only when the mapping teaches something the signature alone doesn't:

- Non-obvious param wiring (which natural phrase maps to which param)
- A multi-action pipe chain (shows how `%!data%` flows)
- Structured JSON values
- Enum selection

Drop examples that restate the signature (`"set %x% = 1 → Name=x, Value=1"` adds nothing over `variable.set Name([string] %x%), Value([object] 1)`).

## Adding a new action

1. Create `PLang/app/modules/<module>/<action>.cs`. Class should be `public partial class YourAction : IContext` (or the appropriate base/interfaces for your module pattern).
2. Apply `[Action("<action-name>")]`. If action name matches the class name lowercased, the string argument is optional.
3. Apply `[System.ComponentModel.Description("…")]` — one short sentence.
4. If this is the first class in a brand-new module, apply `[ModuleDescription("…")]` too.
5. If this is a modifier, apply `[Modifier(Order = N)]` — pick N so the fold order makes sense (current: timeout=1, cache=2, error=3).
6. Add properties for each parameter with the appropriate attributes.
7. Add at most one `[Example]` if the mapping is non-obvious.
8. `dotnet build PlangConsole/PLangConsole.csproj`.

Nothing else needs updating — the builder picks up the new action the next time it runs.

## Adding a new module

Apart from the usual action setup, the **first** class you add in the namespace should carry `[ModuleDescription("…")]`. Convention: whichever action sorts alphabetically first. `Describe()` caches the module description per namespace — the first type with the attribute wins, and all other actions in the namespace inherit it.

Modules composed entirely of modifier actions (e.g. `cache`, `timeout`) render only in the `# Modifiers` section. Modules with only non-modifier actions render only in `# Actions`. Mixed modules render their header in both, which is a feature, not a bug — it keeps module context next to each group.

## A fully-annotated action (end to end)

This is `variable.set` — a good reference because it exercises most of the attributes in a compact class. Comments are annotations for this doc; they're not part of the source file.

```csharp
using app.attributes;
using app.variables;

// Namespace segment = module name. "app.modules.variable" ⇒ module "variable".
namespace app.modules.variable;

// System.ComponentModel.Description — rendered inline after the action name
// in the catalog. Must add info the signature doesn't: the ", optionally
// coercing to a type or setting only when unset" clause teaches behaviour
// you can't read off the parameter list.
[System.ComponentModel.Description("Assign a value to a named variable, optionally coercing to a type or setting only when unset")]

// [Action] marks this as a PLang action handler. "set" is the action name
// the LLM uses in the .pr file (module.action). Cacheable = false opts
// this action out of builder-side LLM caching — variable writes should
// always be re-evaluated.
[Action("set", Cacheable = false)]

// One [Example] per action. Left side = a natural step a developer might
// write. Right side = the formal mapping using the catalog's own notation.
// Actions are pipe-separated; params are comma-separated with [type] tags.
[Example(
    "set %data% = {\"name\": \"%user%\", \"age\": 30}, type=json",
    "variable.set Name([string] %data%), Value([json] {\"name\":\"%user%\",\"age\":30}), Type([string] json)")]

public partial class Set : IContext, IBuildValidatable
{
    // Optional build-time validator: the builder calls this on the LLM's
    // parameter mapping and can return an error message the LLM uses to
    // self-correct. Not part of the catalog — this is runtime-adjacent.
    public static string? ValidateBuild(List<Data.@this> parameters) { /* ... */ }

    // Data<Variable> — slot names a variable, not a value. The catalog
    // renders it as "Name([string] %var%)"; Data.As<Variable> resolves
    // the raw slot string into a Variable (app.variables.Variable implements
    // IRawNameResolvable, so %x% and bare x both yield Name="x").
    // Use sites read Name.Value (Variable→string implicit fires at
    // Variables.Set boundaries).
    public partial Data.@this<Variable> Name { get; init; }

    // No attribute → renders as "Value([object])". The source generator
    // wraps `partial Data.@this` properties in LazyParam behaviour.
    public partial Data.@this Value { get; init; }

    // Nullable type → renders with trailing "?": "Type([string?])".
    public partial Data.@this<string>? Type { get; init; }

    // [Default] → renders with "= false": "AsDefault([bool = false])".
    // Tells the LLM this parameter is optional and what it gets if omitted.
    [Default(false)]
    public partial Data.@this<bool> AsDefault { get; init; }
}
```

This class renders in the catalog as:

```
## variable — Read, write, and inspect PLang runtime variables in the current scope
- variable.set [no-cache] — Assign a value to a named variable, optionally coercing to a type or setting only when unset
  (Name([string]), Value([object]), Type([string?]), AsDefault([bool = false]))
  e.g. `set %data% = {"name": "%user%", "age": 30}, type=json` → variable.set Name([string] %data%), Value([json] {"name":"%user%","age":30}), Type([string] json)
```

Note: the `## variable — …` module header is contributed by whichever class in this namespace carries `[ModuleDescription(...)]`, not this one. By convention that's the alphabetically first action's class — for `variable`, that happens to be `clear.cs`.

## Current modifier classification

As of this writing, these are the only actions tagged `[Modifier]`:

| Action | Order | Role |
|---|---|---|
| `timeout.after` | 1 | Outermost wrapper. Cancels the decorated action when the deadline hits. |
| `cache.wrap` | 2 | Caches the decorated action's result; on a hit, the inner delegate is skipped. |
| `error.handle` | 3 | Innermost wrapper. Catches errors from the decorated action and decides retry / call-error-goal / ignore. |

Lower `Order` = outer wrapper in the runtime fold, so a step declaring all three runs as `timeout → cache → error → action`.

Every other action in the codebase is standalone. Notable mixed modules:

- **`error`** — `error.handle` is a modifier (above); `error.throw` is a standalone action that raises an error directly. The `## error` module header appears in both the `# Actions` and `# Modifiers` sections of the catalog, each listing only the appropriate action.

Future mixed modules follow the same pattern — add `[Modifier(Order = N)]` to the wrapper actions, leave standalones unmarked, and the catalog routes them automatically. Example the team has discussed: `cache.set` as a direct "put this value in the cache" action alongside the existing `cache.wrap` modifier.

## Viewing the rendered catalog

Two ways:

1. Build any goal with `plang build …`. The full LLM system prompt (including the rendered catalog) is saved in `.build/traces/<ticks>_<goal>.json` under `pass1.system`. Open the trace viewer at `system/builder/web/index.html` (start the server with `python3 system/builder/web/server.py 8080`) and select the trace to read the prompt directly.
2. Grep the fresh trace's `pass1.system` for `Notation:` and read downward.

Whenever you change an attribute, rebuild, and verify the catalog renders the way you expect before committing.

## Don't

- Don't rebuild `system/builder/BuildGoal.goal` as a way to test catalog changes. The catalog renders on *any* build (it's a step inside `BuildGoalCore`). Rebuilding the builder risks LLM drift on its own `.pr` — the builder building the builder is the most fragile build in the system.
- Don't add a description that restates the signature. If your description starts with the action's name verb-for-verb, rewrite it.
- Don't edit `system/actions/v2/summary.md` to work around missing metadata — add the attribute to the C# class instead.
- Don't manually edit `.pr` files to change catalog behaviour. `.pr` files never reference catalog metadata; they only carry `{module, action, parameters}`.
