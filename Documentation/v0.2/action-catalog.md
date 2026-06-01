# Action Catalog

The action catalog is the structured list of modules, actions, parameters, and per-action teaching that the builder sends to the LLM. It's what the LLM uses to translate a natural-language step into a `{module, action, parameters}` mapping.

This doc is for anyone adding or editing action handlers — what goes where, how it flows into the prompt, and how to keep the catalog useful.

## Where the catalog lives

1. **Source (shape)**: C# action handler classes in `PLang/app/module/<module>/<action>.cs`. Class attributes declare structure (action name, modifier role, parameter defaults). Class shape is the single source of truth for what parameters exist and what types they take.
2. **Source (prose)**: Markdown files under `os/system/modules/<module>/`. Description, Notes, and Examples are read from disk per action — tuning LLM teaching ships without a C# rebuild.
3. **Builder pulls both**: `app.Module.Describe()` walks the registered handlers, reads the class attributes, then calls `MarkdownTeaching.Load(...)` for each one. Output is a collection of `Action` records carrying `Module`, `ActionName`, `Cacheable`, `Parameters`, `Description` + `ModuleDescription`, `Notes` + `ModuleNotes`, `ExamplesMd` + `ModuleExamplesMd`, `IsModifier`, `ReturnType`.
4. **Renderer**: a step-action-details template renders **per-action blocks only for the actions the planner picked** — the Notes and Examples blocks fire in the user message of the Compile call, not in the cross-cutting system prompt. The system prompt keeps only the cross-cutting kernel (modifier vs peer classification, `write to %var%` → peer `variable.set`, formal-mirroring rule, type-name conventions, etc.).
5. **Prompt injects it**: `system/builder/llm/Compile.llm` carries the kernel; per-action blocks render through `stepActionDetails` against the planner's set.

So: changing the markdown in `os/system/modules/<module>/` changes what the LLM sees on the next build — no `dotnet build` required.

## What the LLM sees

Rendered output (excerpt):

```
Notation: `module.action Name([type?=default])` — `?` = optional, `=val` is default, `%var%` = runtime variable reference.
`tstring` = translatable string; `any` = untyped/dynamic value.

# Actions

## file — Read, write, list, and delete files through the configured filesystem
- file.read — Read a file's bytes or text, returned in %!data%
  (Path([path]), Encoding([string?]))

## error — Error handling: throw errors from a step, or wrap the preceding action with retry/handle-goal/ignore semantics
- error.throw [no-cache] — Immediately fail the step with a structured error message, status code, and optional key
  (Message([string]), StatusCode([int = 500]), Key([string?]))

# Modifiers

## error — Error handling: throw errors from a step, or wrap the preceding action with retry/handle-goal/ignore semantics
- error.handle [no-cache] — Intercept errors from the preceding action; optionally retry, call a goal, or suppress the error
  (StatusCode([int?]), Key([string?]), Message([string?]), Goal([goal.call?]), …)
```

A module with a mix of action and modifier classes (like `error`) gets its `##` header in both sections, each listing only the actions that belong there. This is intentional — it keeps module context present while cleanly separating standalone actions from wrappers.

The cross-cutting catalog lists the **shape** of every action. The **teaching** for a specific action (Notes block, Examples block, longer Description) appears only when the planner selects that action — rendered alongside that step's compile call. That's the whole point: per-action prose lives with the action it constrains, and the planner-set scoping means each compile call sees only the rules that matter to its actions.

## Markdown teaching layout

Per-action prose lives at `os/system/modules/<module>/`. Three files per concern, action-prefixed for action-specific text, `module.`-prefixed for module-wide text:

```
os/system/modules/
  <module>/
    module.notes.md           # applies to every action in this module
    module.examples.md
    module.description.md
    <action>.notes.md         # applies only to this action
    <action>.examples.md
    <action>.description.md
```

Module folder names are lowercase and match `PLang/app/module/<module>/` (`assert/`, `error/`, `output/`, …).

### Merge semantics

When both `module.<file>.md` and `<action>.<file>.md` exist, the renderer **concats — module-level first, blank line, action-specific.** No override semantics; if every action in a module needs the same rule, write it once at `module.*.md` and don't repeat it per action. (The override shape is what produces drift cycles — every action having to re-state a family rule.)

Empty or missing files are fine. An action with no notes contributes no Notes block to its rendered catalog entry.

### `module` is reserved

No action may be literally named `module`. The `module.*.md` stem is reserved for the module-wide layer; an action named "module" would collide with that convention.

### Orphan validation

At catalog load, `MarkdownTeaching.ScanOrphans` enumerates each module folder and surfaces any `*.{notes,examples,description}.md` whose stem is neither `module` nor a registered action. One warning per orphan, written to the developer's `Output` channel. Orphans don't block the build — they're loud, not fatal. This is the replacement for "the C# compiler catches typos in attribute argument strings" — which it didn't, really, but file-system validation is at least explicit.

## Attributes you apply (class shape, not prose)

All attributes live in `app.Attributes`. They declare **structure** — prose belongs in markdown.

### On the action class

| Attribute | Required? | Purpose |
|---|---|---|
| `[Action]` or `[Action("name")]` | Yes | Marks the class as a PLang action handler. Without it the class is ignored by the source generator and `Describe()`. `Cacheable = false` opts out of builder-side LLM caching. |
| `[Modifier(Order = N)]` | Modifier actions only | Marks an action as a modifier — it wraps the preceding action instead of standing alone. Drives both the runtime fold order (lower N = outer wrapper) and the catalog routing. Modules can mix modifiers and non-modifiers (`error.throw` is standalone, `error.handle` is a modifier). |

There is **no `[Description]` attribute on action classes any more**, and no `[Example]` or `[ModuleDescription]` either. All three moved to markdown when this branch shipped. C# `///` xmldoc remains useful as developer documentation in the source file, but it's not part of the catalog.

### On action properties (parameters)

| Attribute | Purpose |
|---|---|
| `[IsNotNull]` | Fails build-time validation if the parameter is missing. The LLM sees this reflected implicitly — non-nullable properties render without the trailing `?`. |
| `[Default(value)]` | Provides a default. Renders as `Name([type = default])` in the catalog so the LLM knows it's optional. |
| `[Code]` | Hides the property from the catalog — it's injected by the source generator at runtime from `app.Code.Get<T>()`. (Renamed from `[Provider]` on this branch — both the attribute and the PLNG001 diagnostic text now say `[Code]`.) |
| `[IsInitiated]` | Source-generator hint for Data properties that must be non-null before `Run()` (runtime concern, not catalog). |

### On the parameter's type

| Type shape | Catalog render |
|---|---|
| `string` / `int` / `long` / `bool` / `double` / `decimal` / `DateTime` / `Guid` | Lowercased: `string`, `int`, etc. |
| Nullable (`string?`, `int?`) | Trailing `?`. |
| `Data<T>` (LazyParam) | Unwrapped to `T`'s rendering. |
| `Data<app.variable.Variable>` | Renders as `string` — the slot names a variable rather than carrying a value. `Variable` implements `IRawNameResolvable`, so `Data.As<T>` resolves it from the raw slot string (`%x%` and bare `x` both produce `Variable { Name = "x" }`). The handler reads `Foo.Value`; implicit conversion to `string` covers method-call boundaries. |
| Enum or type with valid values (`static string[] ValidValues`) | Type name only: `operator`, `trigger`. The members are **not** inlined per parameter — they're declared once in the Type Information block, derived live from the enum via `app.type.GetValidValues`. The type name alone points the LLM to that entry. |
| `List<T>`, `IList<T>`, arrays | `list<T>`. |
| `Dictionary<K,V>` and its variants | `dict<K,V>`. |
| `HashSet<T>`, `ISet<T>`, `IEnumerable<T>`, `ICollection<T>`, `IReadOnlyList<T>` | `list<T>`. |
| `byte[]` | `bytes`. |

Type mapping lives in `PLang/app/utils/TypeMapping.cs`. If you add a new generic collection type and see it leak as `somecollection<…>` in the catalog, add a branch to `GetTypeName()`.

## Writing good Notes

Notes is the file you'll touch most often. It's the LLM rulebook for **this specific action**: the things that aren't visible from the signature and aren't general enough to belong in the cross-cutting kernel.

A good Notes file:

- **Targets one action's drift modes.** "Omit `Message` from `parameters` and from `formal` unless the step text names a custom error message" is a fact about `assert.*`. It does not belong in `Compile.llm`.
- **Shows the correct shape with a tiny JSON example.** The LLM learns better from a worked example than from a paragraph of prose. See `os/system/modules/output/write.notes.md` for the pattern.
- **Names the failure mode you're guarding against.** "DO NOT emit a peer `channel.set` action when the user names a routing channel" beats "use channel routing carefully" — the explicit-bad-shape framing is what the LLM picks up.
- **Lives at the right layer.** If every action in the module needs the rule, put it in `module.notes.md`. If only one action needs it, put it in `<action>.notes.md`. The renderer concats module-first; you do not have to repeat the family rule on each action.

## Writing good Examples

`<action>.examples.md` is one example per paragraph (paragraphs separated by blank lines). Two-line shape that mirrors the old `[Example]` pairs:

```
Step text: `verify %content% against %hash%, write to %isValid%`
Mapping: `crypto.verify Data([object] %content%), Hash([string] %hash%), Algorithm([string] keccak256) | variable.set Name([string] %isValid%), Value([object] %!data%)`
```

The mapping uses **formal syntax** — the same shape the LLM is asked to emit in its `formal` response field:

- Params comma-separated within an action; actions pipe-separated (` | `) within a step.
- `[type]` tag precedes each value. Use the same type token the catalog renders (`string`, `path`, `object`, `int`, `bool`, `goal.call`, etc.). Variable-name slots render as `[string]` with a `%var%` value.
- String values with spaces or commas are quoted; numbers, bools, and `%var%` refs are unquoted.
- Structured values (e.g. `goal.call` payloads) stay as JSON after the type tag.
- When the plang step captures output with `write to %var%`, include the implicit `variable.set` as the last pipe segment.

**Keep examples sparse.** One example per action, only when the mapping teaches something the signature alone doesn't (non-obvious param wiring, multi-action pipe chain showing `%!data%` flow, structured JSON values, enum selection). Drop examples that restate the signature.

## Writing good Descriptions

One-sentence, present-tense, verb-first. Must add information the signature doesn't — "Stores a value" on `variable.set` is noise; "Stores a value under the named key in the current variable scope" teaches.

`<action>.description.md` is the action sentence. `module.description.md` is the module sentence (rendered once per module header).

## Adding a new action

1. Create `PLang/app/module/<module>/<action>.cs`. Class should be `public partial class YourAction : IContext` (or the appropriate base/interfaces for your module pattern).
2. Apply `[Action("<action-name>")]`. If the action name matches the class name lowercased, the string argument is optional.
3. Apply `[Modifier(Order = N)]` if this is a modifier (current orders: `timeout.after = 1`, `cache.wrap = 2`, `error.handle = 3` — outer first).
4. Add properties for each parameter with the appropriate attributes.
5. Write `os/system/modules/<module>/<action>.description.md` — one sentence.
6. Write `os/system/modules/<module>/<action>.notes.md` if this action has drift-prone behaviour the signature doesn't capture (optional, but the right place for "rule about this action").
7. Write `os/system/modules/<module>/<action>.examples.md` if a non-obvious mapping needs to be shown (optional, sparse).
8. `dotnet build PlangConsole/PLangConsole.csproj`.

Nothing else needs updating — the builder picks up the new action the next time it runs. Builds **do not** need to be re-run after editing only the markdown files; the loader re-reads them per build.

## Adding a new module

Create the C# `PLang/app/module/<module>/` folder with at least one `[Action]` class. Create `os/system/modules/<module>/module.description.md` (one sentence for the module). If there's a rule that applies to every action in the module, add `os/system/modules/<module>/module.notes.md`.

Modules composed entirely of modifier actions (e.g. `cache`, `timeout`) render only in the `# Modifiers` section. Modules with only non-modifier actions render only in `# Actions`. Mixed modules render their header in both, which is a feature, not a bug — it keeps module context next to each group.

## A fully-annotated action (end to end)

`variable.set` — compact, exercises most of the attributes.

```csharp
using app.Attributes;
using app.variable;

namespace app.module.variable;

/// <summary>
/// Sets a variable in the current context's variable store.
/// When AsDefault is true, only sets if the variable doesn't already exist.
/// </summary>
[Action("set", Cacheable = false)]
public partial class Set : IContext, IBuildValidatable
{
    // Optional build-time validator: the builder calls this on the LLM's
    // parameter mapping and can return an error message the LLM uses to
    // self-correct. Not part of the catalog — this is runtime-adjacent.
    public static string? ValidateBuild(List<data.@this> parameters) { /* ... */ }

    // Data<Variable> — slot names a variable, not a value. Catalog renders as
    // "Name([string] %var%)"; Data.As<Variable> resolves the raw slot string
    // into a Variable (IRawNameResolvable; %x% and bare x both yield Name="x").
    public partial data.@this<Variable> Name { get; init; }

    // No attribute → renders as "Value([object])".
    public partial data.@this Value { get; init; }

    // Nullable type → renders with trailing "?": "Type([string?])".
    public partial data.@this<string>? Type { get; init; }

    // [Default] → renders with "= false": "AsDefault([bool = false])".
    [Default(false)]
    public partial data.@this<bool> AsDefault { get; init; }
}
```

The prose for this action lives at:

```
os/system/modules/variable/
  module.description.md    # "Read, write, and inspect PLang runtime variables in the current scope"
  set.description.md       # "Assign a value to a named variable, optionally coercing to a type or setting only when unset"
  set.notes.md             # AsDefault flag rule, code.setDefault distinction
  set.examples.md          # `set %data% = {…}, type=json` worked mapping
```

Rendered into the catalog header:

```
## variable — Read, write, and inspect PLang runtime variables in the current scope
- variable.set [no-cache] — Assign a value to a named variable, optionally coercing to a type or setting only when unset
  (Name([string]), Value([object]), Type([string?]), AsDefault([bool = false]))
```

And when the planner picks `variable.set` for a step, the per-step block in the user message includes the Notes paragraph and the worked Example.

## Current modifier classification

As of this writing, these are the only actions tagged `[Modifier]`:

| Action | Order | Role |
|---|---|---|
| `timeout.after` | 1 | Outermost wrapper. Cancels the decorated action when the deadline hits. |
| `cache.wrap` | 2 | Caches the decorated action's result; on a hit, the inner delegate is skipped. |
| `error.handle` | 3 | Innermost wrapper. Catches errors from the decorated action and decides retry / call-error-goal / ignore. |

Lower `Order` = outer wrapper in the runtime fold, so a step declaring all three runs as `timeout → cache → error → action`.

Every other action is standalone. Notable mixed modules:

- **`error`** — `error.handle` is a modifier (above); `error.throw` is a standalone action that raises an error directly. The `## error` module header appears in both the `# Actions` and `# Modifiers` sections of the catalog, each listing only the appropriate action.

## Viewing the rendered catalog

Two ways:

1. Build any goal with `plang build …`. The full LLM system prompt and per-step user messages are saved in `.build/traces/<ticks>_<goal>.json`. Open the trace viewer at `system/builder/web/index.html` (start the server with `python3 system/builder/web/server.py 8080`) and select the trace to read the prompt directly. The per-action Notes/Examples blocks appear in the **user** message for each step (under `pass1.user` or similar), not in the system prompt — that's the whole architecture.
2. Grep the fresh trace's `pass1.system` for `Notation:` and read downward to inspect the kernel; grep `pass1.user` for `## ` to find the per-action blocks.

Whenever you change markdown or attributes, rebuild a sample goal and verify the catalog renders the way you expect before committing.

## Don't

- Don't rebuild `system/builder/BuildGoal/Start.goal` as a way to test catalog changes. The catalog renders on *any* build (the per-action notes/examples render in `BuildStep/Start.goal` for the planner's picked action set). Rebuilding the builder risks LLM drift on its own `.pr` — the builder building the builder is the most fragile build in the system.
- Don't add a Description that restates the signature. If your description starts with the action's name verb-for-verb, rewrite it.
- Don't enumerate a closed set's members in prose — enum values, valid-value lists, type-kind vocabularies, MIME maps. The catalog already injects them live from the source of truth (`app.type.GetValidValues` → the enum's own members) into the Type Information block; per-parameter inlining was removed (`PLang/app/module/this.cs:336`). A prose copy goes stale the moment a member is added — today `os/system/modules/event/on.description.md` lists 11 of `Trigger`'s 21. Name the type (`trigger`, `operator`) and teach shape and behavior — how the value is emitted, what it must not be confused with — and let Type Information carry the members. At most one representative member as illustration, never "the valid set is X, Y, Z."
- Don't put a rule that constrains one action into `Compile.llm`. The cross-cutting system prompt keeps only the kernel — modifier vs peer classification, the formal-mirroring rule, type conventions, the `%!data%`-never-as-fallback rule. Per-action rules go in `<action>.notes.md`.
- Don't repeat a family rule across every `<action>.notes.md` in a module. Put it once in `module.notes.md` — the renderer concats module-first automatically.
- Don't add a stem named `module.<foo>.md` for anything other than module-wide teaching — the stem is reserved.
- Don't manually edit `.pr` files to change catalog behaviour. `.pr` files never reference catalog metadata; they only carry `{module, action, parameters}`.
