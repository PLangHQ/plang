# The `.pr` File Format

The builder's output. One `.pr` file per `.goal` **file**; it holds the file's root
(public) goal plus its private sub-goals. The runtime loads it and executes it directly —
there is no parsing step at runtime.

```
.goal file (natural language) → builder → .pr file (JSON) → runtime executes
```

For how the builder *produces* this, see [`builder.code.md`](builder.code.md). This doc
is the shape of the result, as it is on disk today.

## .goal → .pr

A `.goal` file holds one **root goal** (public) and zero or more **sub-goals** (private):

```plang
Start
- call WriteOut

WriteOut
- write out "hello plang world"
```

- The **first** goal is the root, `visibility: "Public"`.
- Every goal after it is a sub-goal, `visibility: "Private"`, nested in the root's `child[]`.
- Goals are separated by blank lines; a goal is a name line followed by `- ` step lines.
- Comments are `/` lines (or `/* … */`). A comment above a goal or step attaches to it.
- If steps appear before any goal header, an implicit `Start` goal is created.

**PrPath:** `/folder/MyGoal.goal` → `/folder/.build/mygoal.pr` (lower-cased basename).

## The shape

```json
{
  "name": "Start",
  "step": [
    {
      "index": 0,
      "text": "read 'notes.txt', write to %content%",
      "line": { "number": 2 },
      "code": [
        {
          "module": "file",
          "name": "read",
          "property": [
            { "name": "Path", "type": { "name": "path" }, "value": "notes.txt" }
          ]
        },
        {
          "module": "variable",
          "name": "set",
          "property": [
            {
              "name": "Name",
              "type": { "name": "variable" },
              "value": "%content%",
              "variable": [ { "text": "%content%", "code": [ { "variable": "content" } ] } ]
            },
            { "name": "Value", "type": { "name": "item", "template": "plang" }, "value": "%!data%" }
          ]
        }
      ],
      "waitForExecution": true
    }
  ],
  "child": [
    { "name": "WriteOut", "visibility": "Private", "step": [ … ], "path": "/Test.goal", "prPath": "/.build/test.pr" }
  ],
  "visibility": "Public",
  "path": "/Test.goal",
  "prPath": "/.build/test.pr",
  "hash": "…",
  "isSetup": false, "isEvent": false, "isSystem": false, "isTest": false
}
```

### Goal

| Key | Type | Meaning |
|---|---|---|
| `name` | string | goal name from the `.goal` header |
| `comment` | string? | developer comment (the `/` lines above the goal header) |
| `step` | Step[] | the goal's steps (note: **`step`**, singular key, an array) |
| `child` | Goal[] | the file's private sub-goals |
| `visibility` | string | `"Public"` (root) or `"Private"` (sub-goals) |
| `path` | string | source `.goal` path, app-root relative (sub-goals share the parent's) |
| `prPath` | string | derived `.pr` path (sub-goals share the parent's) |
| `hash` | string | change-detection hash over name + step texts |
| `isSetup` / `isEvent` / `isSystem` / `isTest` | bool | classification flags |

> There is **no `description`** and **no `builderVersion`** on the goal — the current
> pipeline doesn't generate a goal summary, and the format isn't version-stamped per goal.

### Step

| Key | Type | Meaning |
|---|---|---|
| `index` | int | zero-based position within the goal |
| `text` | string | the step line as written (without `- `) |
| `line` | `{number}` | 1-based source line |
| `comment` | string? | comment from the `/` line above this step |
| `code` | Code[] | the step's actions (note: **`code`**, not `actions`) |
| `child` | Step[]? | the step's body steps — present only on a step whose code is an unclosed condition (see below) |
| `warning` | Warning[]? | build-time notes, e.g. a `Reopened` warning when the step was rebuilt because the catalog changed under it |
| `waitForExecution` | bool | whether to await completion (default true) |

### Code (an action)

| Key | Type | Meaning |
|---|---|---|
| `module` | string | the module (`file`, `variable`, `goal`, `condition`, `output`, `on`, …) |
| `name` | string | the action name within the module (`read`, `set`, `call`, `if`, `write`, `error`, …) |
| `property` | Property[] | the parameters the step gave a value |
| `default` | Property[]? | defaults the builder filled in (same shape as `property`) |
| `child` | Step[]? | a **condition's body** — present on a gate action (`condition.if` / `.elseif` / `.else`); the body steps nest here |

There is **no `modifiers` array**. A clause (`on error`, `cache for`, `timeout after`) is
its **own peer `code` entry** in the same step — module `on`, name `error` / `cache` /
`timeout` — carrying the recovery/clause configuration (see "Clauses" below).

### Property

| Key | Type | Meaning |
|---|---|---|
| `name` | string | the property name on the action (PascalCase: `Path`, `Name`, `Value`, `Left`, `Operator`) |
| `type` | `{name, kind?, template?}` | the value's PLang type. `name` is the type word (`text`, `number`, `bool`, `path`, `goal`, `variable`, `list`, `dict`, `item`, `choice`, …). `kind` narrows a container or choice (`"operator"` on a `choice`, `"action"` on a `list` of actions). `template: "plang"` marks a value that carries `%var%`/`%!data%` interpolation. |
| `value` | any | the value — string, number, bool, null, a list, a dict, or a nested structure (a `Parameter` list, a `Recovery` action list) |
| `variable` | Variable[]? | the `%var%` references parsed out of `value`, each `{text, code:[{variable}]}` — the resolution the runtime walks (so a `%goal.Name%` lands its member path here) |

## Conditions nest in `child`

A condition whose body is the indented steps below it carries that body on the **gate
action's `child`**. `build.fold` does this re-parenting after compile (it moves a step's
deeper-indented followers into the step's first `IsCondition` action's `child`).

`if %message% is not empty, write out %message%` →

```json
{
  "module": "condition", "name": "if",
  "property": [
    { "name": "Left", "type": { "name": "item", "template": "plang" }, "value": "%message%",
      "variable": [ { "text": "%message%", "code": [ { "variable": "message" } ] } ] },
    { "name": "Operator", "type": { "name": "choice", "kind": "operator" }, "value": "isnotempty" }
  ],
  "child": [
    { "index": 0, "text": "output.write(Data=%message%)", "line": { "number": 7 },
      "code": [ { "module": "output", "name": "write", "property": [ … ] } ],
      "waitForExecution": true }
  ]
}
```

The body-step `text` is the **formal** the writer produced for it, because the body was an
inline continuation of the condition's step. An `else` / `elseif` is the **next peer gate
action** in the same step's `code[]`, each with its own `child`.

## Clauses are peer code entries

`call Compile, on error call HandleBuildFailure` →

```json
"code": [
  { "module": "goal", "name": "call", "property": [ { "name": "Name", "type": { "name": "goal" }, "value": "Compile" } ],
    "default": [ { "name": "parallel", "type": { "name": "bool" }, "value": false } ] },
  { "module": "on", "name": "error",
    "property": [
      { "name": "Recovery", "type": { "name": "list", "kind": "action" },
        "value": [ { "module": "goal", "name": "call", "property": [ { "name": "Name", "type": { "name": "goal" }, "value": "HandleBuildFailure" } ] } ] }
    ],
    "default": [ { "name": "ignore", "type": { "name": "bool" }, "value": false } ] }
]
```

The `on.error` entry follows the action it guards. Its `Recovery` is a `list` of type
`kind: "action"` — each recovery is a full `code` entry. Several clauses on one action
stand as several peer `on.*` entries, in the order the step gives them.

## goal.call parameters

`call SendMail to=%email%, subject="Hi"` carries a `Parameter` property of type `list`,
whose `value` is a list of property-shaped entries (one per named argument):

```json
{ "module": "goal", "name": "call",
  "property": [
    { "name": "Name", "type": { "name": "goal" }, "value": "SendMail" },
    { "name": "Parameter", "type": { "name": "list" },
      "value": [
        { "name": "to", "type": { "name": "item", "template": "plang" }, "value": "%email%",
          "variable": [ { "text": "%email%", "code": [ { "variable": "email" } ] } ] },
        { "name": "subject", "type": { "name": "text" }, "value": "Hi" }
      ] } ] }
```

## Caching and reopening

A step whose source hasn't changed since its last build keeps its saved `code` — it is
**cached** and the decider/writer never see it (`step.IsCached`). When the catalog changes
under a built step (a parameter removed, a slot's kind changed, a gone option's frozen
default), the step **reopens**: it is rebuilt and a `Reopened` warning is attached to it in
the `.pr`, e.g. *"step 0's saved code no longer holds — rebuilt: … 'AsDefault' is not a
property of this action."*

## Runtime loading

The engine loads a `.pr` on demand: it resolves the goal's folder, reads
`{dir}/.build/{file}.pr`, deserializes, and registers the root goal plus every `child`
sub-goal. Path resolution: a name starting with `/` resolves from the app root; a relative
name resolves from the calling goal's folder first, then the app root; `system/` goals
resolve from the system directory.

## Key files

| File | Purpose |
|---|---|
| `os/system/builder/Build.goal` | build entry |
| `os/system/builder/BuildGoal/Start.goal` | per-goal pipeline; `build.goalsSave` writes the `.pr` |
| `PLang/app/goal/this.cs` | the Goal entity (`Parse`, `PrPath`) |
| `PLang/app/module/build/code/Default.cs` | the `IBuilder` actions (`goals`, `match`, `fold`, `goalsSave`, …) |
