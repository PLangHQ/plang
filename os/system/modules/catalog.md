# The Action Catalog & Markdown Teaching

The catalog is the structured list of modules, actions, parameters, and per-action prose
that the builder sends the LLM. It's what turns a natural-language step into a
`{module, name, property}` mapping. This doc is for anyone adding or editing action
handlers.

How the catalog reaches the LLM is the builder's job — see
[`../builder/builder.code.md`](../builder/builder.code.md) for the Decide → Properties
pipeline and [`../builder/start.md`](../builder/start.md) for the index. In short:

- the **decider**'s State (`os/system/builder/llm/templates/decider.state.template`)
  lists every module with its description, notes, and its actions' example *step texts*,
  plus (stage 2) each candidate action's description;
- the **writer**'s user message (`…/templates/properties.template`) renders, for each
  action the decider **listed** for a step, that action's signature + first description
  line + notes; the cross-cutting system prompt is `…/llm/Properties.llm`.

So per-action teaching costs prompt tokens only for the actions actually in play on a
step. There is no "planner" and no per-step "Compile" call — those are retired.

## Where the catalog lives

1. **Shape (C#)** — the action handler class at `PLang/app/module/<module>/<action>.cs`.
   Class attributes declare structure (action name, modifier role, parameter defaults,
   types). The class is the single source of truth for what parameters exist.
2. **Prose (markdown)** — files under `os/system/modules/<module>/`. Description, Notes,
   and Examples, read fresh from disk **every build** — tuning teaching ships without a
   C# rebuild.
3. **Assembly** — `app.Module` (`%!app.module.list%`) is the action registry; its entries
   come from the source-generated handlers (class shape). The prose is **not** attached by a
   separate loader step — each catalog element exposes its teaching as **lazy file items**
   pointing into the module's folder: the action element
   (`PLang/app/goal/step/action/this.Schema.cs`) gives `Description` / `Note` / `Examples`
   (`<action>.{description,notes,examples}.md`), and the module element
   (`PLang/app/module/this.cs`) gives `Description` / `Notes`
   (`module.{description,notes}.md`). Content materializes only at the value door, and an
   absent file is falsy. Add a C# action and it appears automatically — you register nothing
   by hand.

## Markdown teaching layout

Per-action prose lives at `os/system/modules/<module>/`, three concerns, two scopes:

```
os/system/modules/<module>/
  module.description.md     # module-wide (the "module" stem)
  module.notes.md
  <action>.description.md   # per-action
  <action>.notes.md
  <action>.examples.md
```

Module-wide teaching is description + notes only — there is no `module.examples.md` slot;
examples are per-action.

Module folder names are lowercase and match `PLang/app/module/<module>/`.

### Module-wide vs per-action

Module-wide teaching (`module.description.md`, `module.notes.md`) and per-action teaching
render in **different places**, not concatenated: the module's description and notes appear
once in the decider's State (in that module's listing); each action's description, notes,
and examples appear under that action's signature in the writer's user message, and only for
the actions a step's `Pick` listed. So put a rule that applies to every action of a module
in `module.notes.md` once — don't repeat it per action.

Empty or missing files are fine — an absent file is simply falsy (existence truthiness), so
the slot contributes nothing and `{% if action.Examples %}` guards presence without reading.

### the `module` stem

`module.description.md` / `module.notes.md` are the module-wide layer, so an action named
`module` would collide with them. It's a convention to keep, not a checked rule (see next).

### No orphan scan — mind the filenames

Teaching files are resolved **by name** on demand (`<action>.description.md`, …), so a
misspelled or misplaced file (`reed.description.md`, or the right name in the wrong folder)
is **silently ignored** — the action just gets no prose for that slot. There is no load-time
orphan scan any more (the old `MarkdownTeaching.ScanOrphans` was removed with the teaching
loader). Double-check a new file's name matches the action and sits in
`os/system/modules/<module>/`, and confirm it landed by dumping the rendered prompt (below).

## Writing good Descriptions

`<action>.description.md` is one sentence: present-tense, verb-first, and it must add what
the signature doesn't. "Stores a value" on `variable.set` is noise; "Stores a value under
the named key in the current variable scope" teaches. `module.description.md` is the
one-line module sentence (rendered once per module).

## Writing good Notes

Notes is the file you'll touch most — the rulebook for **this specific action**: the
things not visible from the signature and not general enough for `Properties.llm`.

- **Target one action's drift modes.** "Omit `Message` unless the step names a custom
  error message" is a fact about `assert.*`, not a kernel rule.
- **Name the failure mode.** "DO NOT emit a peer `channel.set` when the step names a
  routing channel" beats "use channels carefully" — the explicit bad-shape framing is what
  the model picks up.
- **Live at the right layer.** Family rule → `module.notes.md` (it renders once in the
  decider's module listing); one-action rule → `<action>.notes.md` (it renders under that
  action in the writer's message). Don't restate a family rule on every action.

## Writing good Examples

`<action>.examples.md` is one example per paragraph (blank-line separated). Each is a
**natural-language step line** and the **properties** it should produce for the main
action:

```
Step text: `read file.txt, write to %content%`
Properties: `{"Path": "file.txt"}` — the trailing `write to %content%` is its own action.

Step text: `read 'config/settings.json'`
Properties: `{"Path": "config/settings.json"}`
```

- `Step text:` is how a developer would actually write the step (any human language is
  valid elsewhere, but write examples in plain English).
- `Properties:` is a JSON object of the parameter values for **this** action — names match
  the C# properties (`Path`, `Data`, `Left`, …), values written as the step gives them.
- The implicit `variable.set` of a trailing `write to %x%`, a surrounding condition/loop,
  and clauses are **not** inlined into `Properties:` — note them in prose if they matter;
  they're their own actions, decided elsewhere.

How the two prompts consume examples: the decider State extracts just the `Step text:`
lines as `e.g. \`…\`` recall hints; the writer's user message shows the action's full
examples under its signature.

**Keep examples sparse.** One per action, only when the mapping teaches something the
signature doesn't (non-obvious param wiring, an enum selection, a structured value). Drop
examples that restate the signature.

**One discipline — don't enumerate a closed set's members in prose.** Enum values,
valid-value lists, MIME maps: the type system already injects those live into the writer's
Types block from the source of truth, so a hand-copied list goes stale the moment a member
is added. Name the *type* (`operator`, `trigger`) and teach shape/behavior; let the Types
block carry the members (at most one representative member as illustration).

## Attributes (class shape, not prose)

All attributes live in `app.Attributes`. There is **no `[Description]`, `[Example]`, or
`[ModuleDescription]`** on action handlers any more — all prose is markdown. C# `///`
xmldoc stays as developer documentation but isn't part of the catalog.

### On the action class

| Attribute | Purpose |
|---|---|
| `[Action]` / `[Action("name")]` | Marks the class a PLang action handler. Without it the generator and `Describe()` ignore it. `Cacheable = false` opts the action out of builder-side LLM caching. |
| `[Modifier(Order = N)]` | Marks a modifier — it wraps the preceding action instead of standing alone. Drives both the runtime fold order (lower N = outer) and catalog routing. A module may mix the two (`error.throw` standalone, `error.handle` a modifier). |

### On action properties (parameters)

| Attribute | Purpose |
|---|---|
| `[IsNotNull]` | Required; missing → build-time validation error. Renders without the trailing `?`. |
| `[Default(value)]` | Optional with a default; renders as `Name: type = default`. |
| `[Code]` | Hidden from the catalog — injected at runtime from `app.Code.Get<T>()` (the `[Code]`-tagged `IBuilder`/`ILlm`/`IDecider` slots). |
| `[IsInitiated]` | Source-generator hint: a `Data` property that must be non-null before `Run()` (runtime concern, not catalog). |

Property **kind** is gated at build time (PLNG001): a handler property must be `Data<T>`
(or plain `Data`) or `[Code] T`. A slot that *names* a variable (write targets, read-by-
name) is `Data<app.variable.Variable>` — `Variable` is `IRawNameResolvable`, so `%x%` and
bare `x` both resolve to `Variable { Name = "x" }`, and the handler reads `Foo.Value`.

### How a property's type renders in the signature

`Data<T>` unwraps to `T`'s PLang word. The writer sees the signature as
`## module.action(Name: type, Name?: type = default)` — a colon before the type, `?` for
optional, `= x` for a default. Type words: `text`, `number`, `bool`, `path`, `goal`,
`variable`, `list`, `dict`, `item`, `choice`, `bytes`, … (mapping in
`PLang/app/utils/TypeMapping.cs`). An enum / valid-values type renders as the **type name
only**; its members live in the Types block, not inlined per parameter.

## A fully-annotated action (end to end)

`variable.set` — compact, exercises most attributes.

```csharp
using app.Attributes;
using app.variable;

namespace app.module.variable;

[Action("set", Cacheable = false)]
public partial class Set : IContext
{
    // Data<Variable> — the slot NAMES a variable. Renders "Name: variable".
    // IRawNameResolvable: %x% and bare x both resolve to Variable { Name = "x" }.
    public partial data.@this<Variable> Name { get; init; }

    // plain Data → renders "Value: item" (any value).
    public partial data.@this Value { get; init; }

    // nullable → trailing "?": "Type?: text".
    public partial data.@this<string>? Type { get; init; }

    // [Default] → "Default: bool = false".
    [Default(false)]
    public partial data.@this<bool> Default { get; init; }
}
```

Prose for it:

```
os/system/modules/variable/
  module.description.md    # "Read, write, and inspect PLang runtime variables in the current scope"
  set.description.md       # "Assign a value to a named variable, optionally coercing to a type or setting only when unset"
  set.notes.md             # the Default (set-only-when-unset) rule
  set.examples.md          # a worked `set %x% = …` mapping
```

Rendered into the writer's user message when the decider lists `variable.set` for a step:

```
## variable.set(Name: variable, Value: item, Type?: text, Default?: bool = false)
Assign a value to a named variable, optionally coercing to a type or setting only when unset
```

…followed by the Notes paragraph and any example.

## Modifier classification

The only `[Modifier]` actions today:

| Action | Order | Role |
|---|---|---|
| `timeout.after` | 1 | Outermost — cancels the decorated action at the deadline. |
| `cache.wrap` | 2 | Caches the result; on a hit the inner delegate is skipped. |
| `error.handle` | 3 | Innermost — catches errors and decides retry / call-goal / ignore. |

Lower `Order` = outer, so all three run `timeout → cache → error → action`. In the `.pr`
these clauses are **peer `code` entries** (module `on`, name `timeout`/`cache`/`error`),
not a `modifiers` array — see [`../builder/pr-format.code.md`](../builder/pr-format.code.md).

## Adding a new action

1. Create `PLang/app/module/<module>/<action>.cs`; `public partial class YourAction : IContext`.
2. `[Action("<name>")]` (the string is optional when the name is the class name lowercased).
3. `[Modifier(Order = N)]` if it's a modifier.
4. Add properties (`Data<T>` / `[Code] T`) with the right attributes.
5. Write `<action>.description.md` (one sentence).
6. Write `<action>.notes.md` if it has drift-prone behavior the signature doesn't capture.
7. Write `<action>.examples.md` if a non-obvious mapping needs showing (sparse).
8. `dotnet build PlangConsole`.

Nothing else to register — the builder picks it up next build. Editing only the markdown
needs no rebuild (the loader re-reads per build).

## Adding a new module

Create `PLang/app/module/<module>/` with at least one `[Action]` class and
`os/system/modules/<module>/module.description.md`. Add `module.notes.md` for a rule that
applies to every action in the module.

## Viewing the rendered catalog

Build any goal with `cache:false` and dump the prompts:

```bash
plang build '--build={"cache":false}' '--debug={"llm":{"system":true,"user":true},"length":{"max":50000}}'
```

The decider State is the **system** side of the `Decide` call (module descriptions +
notes + example step texts); each listed action's signature/notes/examples are in the
**user** message of the `Properties` call. Scope with `{"goal":"Decide"}` or
`{"goal":"Properties"}`. The web trace viewer lives at `os/system/builder/web/`.

Whenever you change markdown or attributes, rebuild a sample goal and verify the catalog
renders as you expect before committing.

## Don't

- Don't rebuild `os/system/builder/BuildGoal/Start.goal` to test catalog changes — the
  catalog renders on *any* build; rebuilding the builder risks LLM drift on its own `.pr`
  (the most fragile build in the system — see
  [`../builder/bootstrap.code.md`](../builder/bootstrap.code.md)).
- Don't add a Description that restates the signature.
- Don't enumerate a closed set's members in prose (above) — name the type, let the Types
  block carry the members.
- Don't put a one-action rule into `Properties.llm` — the kernel stays cross-cutting;
  per-action rules go in `<action>.notes.md`.
- Don't repeat a family rule across every `<action>.notes.md` — put it once in
  `module.notes.md`.
- Don't add a `module.<foo>.md` stem for anything but module-wide teaching (reserved).
- Don't hand-edit `.pr` files to change catalog behavior — they carry only
  `{module, name, property}`, never catalog metadata.
