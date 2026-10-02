# Decide: the builder's decider step

## Purpose

`Decide` answers, for every step of one goal, **which actions the step may use**, before the LLM writes the step's code. It does this in two exchanges with the decider for the whole goal, not one LLM call per step. The answer lands on each step as its `Pick` (`step.Pick`), which the next stage (`Properties`) reads to know which actions to offer the LLM for that step.

The decider itself (`llm.decider`) is generic: it takes a state (text) and typed questions (a dict) and answers them. Everything goal-shaped is composed here: the templates write the words, and `build.pick` reads the numbers.

## Architecture

```
plang build
└─ os/system/builder/Build.goal → BuildGoal.goal → BuildGoal/Start.goal   (one .goal file)
   └─ Start.goal:32  Compile
      ├─ :34-36  cached or formal-only steps skip deciding
      ├─ :37     call Decide                    ← this goal
      ├─ :38     call Properties                (the LLM writes each step's actions in formal, offered what Pick allows)
      └─ :39     build.match                    (each step reads its answer line)
```

Decide's steps (`Decide.goal`):

| line | step | what it does |
|---|---|---|
| 14 | `set %modules% = %!app.module.list%` | every module the app has, for the templates |
| 15 | `read "/system/builder/llm/decider.json", write to %decider%` | the common and popular actions (see Data flow) |
| 17-21 | stage 1 | render the state and `decider1.template`'s questions; `llm.decider`; `build.pick` |
| 23-27 | stage 2 | the same with `decider2.template`, asking only what stage 1 left open |
| 29 | `call EmitBuildEvent kind="goal-decided"` | the build's progress channel |

- **Stage 1** asks each uncached, non-formal step one choice (which module does the main work, over every module's name) and one yes/no per **common action** (`decider.json`'s `common`).
- **Stage 2** asks only what stage 1 left open, as each step's `Pick` decides: the main module's action, a runner-up module, a module scored under certain, an if's branches, and, on a step stage 1 was unsure of, which **popular action** it uses.

C# the steps go through:
- `llm.decider` → `PLang/app/module/action/llm/decider.cs` (`State` text, `Question` dict, `Model`, provider `IDecider`).
- `build.pick` → `PLang/app/module/action/build/pick.cs` (`Goal`, `Answer` dict, `Popular` list); it writes each step's `Pick`.
- `render template` → `ui.render` → `PLang/app/module/action/ui/code/Fluid.cs`.
- `read` → `file.read` → `PLang/app/module/action/file/read.cs`.

## Data flow

| variable | set by | its plang value | read by |
|---|---|---|---|
| `%goal%` | the caller (Start.goal's `%goal%`) | the goal being built | both templates (`goal.Step`, `goal.Name`), `build.pick` |
| `%modules%` | `:14` | the module list (`module.list`) | both templates (`modules | map: "Name"`, `where: "Name"`, `m.Action`, `m.Description`, `m.Notes`) |
| `%decider%` | `:15` `file.read` | **a lazy `file` reference** (`{file, json}`), not its content. See below | `decider1.template:28` and `decider.state.template:79` (`decider.common`), `decider.state.template:68` (`decider.popular`), and the goal steps `:21`/`:27` (`%decider.popular%`) |
| `%stage%` | `:17`, `:23` | number 1 or 2 | `decider.state.template:29,65` |
| `%state%` | `:18`, `:24` | text (the rendered state) | `llm.decider State=` |
| `%questions%` | `:19`, `:25` | text (rendered JSON) that the `Question` slot takes as a dict | `llm.decider Question=` |
| `%answer%` | `:20`, `:26` | the decider's answers, a dict keyed by question id (`s<index>_@module`, `s<index>_<module.action>`, …) | `build.pick Answer=` |

`decider.json` (`os/system/builder/llm/decider.json`) has two keys:
- `common`: an **object** keyed by action name (`"variable.set"`, `"goal.call"`, `"output.write"`, …), each `{ "true": "<what yes means>", "false": "<what no means>" }`. The templates iterate it as pairs: `c[0]` is the name, `c[1].true` / `c[1].false` the criteria.
- `popular`: a **list** of action names, offered to an unsure step in stage 2.

**How `%decider%` reaches the templates:**
1. `file.read` answers a `file` reference: nothing is read at the step (`file/read.cs:61-68`).
2. `ui.render` binds every variable as `FluidValue.Create(await data.Value(), …)` (`Fluid.cs`, the variable-binding loop).
3. The file's value door reads the bytes and decodes them by MIME (`file/this.cs:77-110`). json is item's `json` kind, and structured json stays a **`clr(JsonElement)`**, navigated lazily by that kind (`type/item/kind/json/this.cs:103-119`). It is not a `dict`/`list`.
4. Fluid receives any plang container (an item that is not a leaf) through one value, `Fluid.Item`, which reads through the item's own doors: members and indexes through its `Get`, children through its `EnumerateItems`. A container that says it holds its children by position (`item.IsSequence`: a list, a table, a json array — a `clr` host asks its kind) reaches Fluid as an array (`join`, `sort`, `map`, `where` act on it); any other is a hash whose `{% for %}` yields `[key, value]` pairs. So `decider.common` iterates its pairs and `decider.popular | join` joins its names, whatever container carries them.

**From the goal side,** `%decider.popular%` (`:21`, `:27`) navigates in plang: the file reference narrows on first touch and the json kind descends (`kind/json/this.cs:47-67`), so `build.pick` gets its list.

## Contracts

- Stage 1's questions include **one yes/no per common action per asked step**. If `decider.common` renders nothing, stage 1 silently asks only the module choice, and every common action (on.error, variable.set, goal.call, …) is never asked.
- The question keys and wording are pinned as C# goldens by `PickListTests` (`PLang.Tests/Wire/App/Decider/PickListTests.cs`, `pick_golden.json`) — re-pinned from C# through its `AcceptTheFixture`/re-pin path after any intended change.
- `build.pick` needs `Answer` as a dict and `Popular` as a list.

## How it's tested, and what the tests don't see

- `RenderTests.Render_AReadJsonFile_IteratesItsDictAndList` (`PLang.Tests/Modules/App/Modules/ui/RenderTests.cs`) pins the real path: `file.read` of a json file, `variable.set` from `%!data%`, then `ui.render` iterating the object and joining the list.
- `PickListTests` (`PLang.Tests/Wire/App/Decider/PickListTests.cs:62-78`) renders both templates and compares them with the pinned C# golden (`pick_golden.json`). **It sets `%decider%` to a native dict it built from `JsonDocument` (`:66-68`, `Answer(…)`), not through `file.read`**, so on its own it can't see how the real `read … write to %decider%` value reaches Fluid; the pin above covers that.
- The golden pins the prompts the builder *should* render; it is a pure C# fixture (the former Python eval under `tools/decider/` is retired).
- The builder check (rebuild with the target `.pr` moved aside, then compare) is **not** masked by the LLM cache: `OpenAi.ComputeCacheKey` hashes every message's role and content plus model, temperature, schema and format, so any prompt change misses the cache. `llm.decider` has no cache. What it can't see is a bug present both before and after ("byte-identical" compares with the previous build), or a rendering change that yields the same `.pr`.

## Known issues

- **Fixed 2026-09-28 (app-systems, decisions 152/153/154): `decider.common` rendered nothing in the real builder.**
  - `%decider%` is a file reference whose content is `clr(JsonElement)`; Fluid's old converter knew only `dict`/`list`/`JsonNode`, and its door accessor handed the `clr` itself, so `{% for c in decider.common %}` iterated nothing and `decider.popular` rendered as its raw json text. Stage 1 never asked the common questions.
  - **It never worked.** A bisect with the pin failed at every commit back to `bec5f56df`, the commit that introduced `decider1.template` (decider v5). `PickListTests` always injected a parsed dict.
  - Fixed in `Fluid.cs` by one value over any container (Data flow, step 4), replacing the `dict`/`list`/`JsonNode` switch. Nothing changed in the goal.
- **Open:** with the common questions asked, `system/error/Show.goal` step 1 (`… on error 404 call Fallback then retry once …`) is refused: the LLM writes `RetryCount=1` for "retry once", and the invented-number check refuses a number the step's text doesn't hold. `BuildGoal/Start.goal` is refused too: for `if %goal.IsCached%, return %goal.Cache%` the LLM puts `goal.return` beside the if, and for `if %goal.Step.IsCached%, return` the decider doesn't list `goal.return`. Decider/teaching quality — the builder's.
