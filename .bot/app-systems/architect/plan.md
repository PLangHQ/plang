# app-systems — every `app.X` is the X system

Branch `app-systems`, off `builder-formal`. Designed with Ingi, 2026-09-24 (the parked draft `.bot/goal-graph-singular/architect/app-systems-draft.md`) and 2026-09-26. **Under review with Ingi, round by round, until a round passes without comment. Coder does not start before that.**

> **Coder, you own the code.** The shapes below are sketches: names, members and file:line are the architect's reading on 2026-09-26. Trace before each stage and bring back what doesn't hold.

## Why

1. **The three paths disagree.** The type system is reached at `app.type` (plang `%!app.type%`, C# `App.Type`) but lives at `type/list/this.cs` as `type.list.@this`; "list" names both the system and the types in it. OBP's alignment test (`Documentation/v0.2/object_pattern_formal.md`, "The three paths agree") says that's a wrong shape. The same holds for goal, actor, module, test and variable.
2. **The type system has several ways in and several copies of its knowledge.** `Register` (`type/list/this.cs:390`), `RegisterRuntime` (`Registry.cs:100`) and the static `Loader.Register` (`Loader.cs:87`) all add types; five maps (`Registry.cs:24-36`) plus `_catalogByName` (`:111`) and `_full` (`:216`) look them up; `Get(string)` and `Clr(string)` answer one question twice (`:90`, `:93`).
3. **Systems can't describe themselves.** `write out %!app.type%` throws `NoWireContract`, because the class declares no face.
4. **A reference (`%x%`) has six parsers and four definitions** (`data.TryFullVarMatch`, `text.HasVariable`, the coverage/types-walk/pick regexes, the old store render), plus two identical `CleanName`s and a hand-written name scan in `variable.@this.Convert`. `"save 50% now and 20% later"` parses differently depending on who reads it.
5. **Events live beside the objects, not on them.** `event.on(Trigger=…)` is one module over an enum (`app/event/Trigger.cs`). The thing an event is about doesn't own it.
6. **C# tests don't use the app's own doors.** They go through static helpers (`PLang.Tests/Shared/TestApp.cs`, `TestAction.cs`), not the way plang runs an action.

## The shape

**A system.** `X/this.cs` is the X system, reached at `app.X`; `X/X/this.cs` is one X.
- Members: `.list` (all of them), `.current` (the one in play, each system's own answer, read from the context: `app.actor.current => context.Actor`), `["name"]` (the door to one, the same door as C#'s indexer), `.name` (shorthand for `["name"]`; the system's own members win, so a type named `list` needs `["list"]`).
- **The system is an item.** It writes its face through `Output` (facts, any writer), a formatter (a template) presents them, and it answers its own navigation (`Get(parent, key)`: a member first, else the element with that name, else NotFound). Like every item, it stores no context: the caller passes it.

**One element.** It owns its facts, its `on` (events about it) and its `current` (the instance in play).

**What runs, runs in a module.** A step maps only to module actions. `%!app…%` and every `%…%` only read (a method call inside `%…%` must not change anything). An action's C# hands over to the owner in one line: `on/create.cs` → `app.type.text.on.create(LoadText)`.

| plang | C# | file |
|---|---|---|
| `%!app.type%` | `app.type` | `app/type/this.cs`, the type system |
| `%!app.type.list%` | `app.type.list` | a member |
| `%!app.type["text"]%`, `%!app.type.text%` | `app.type["text"]` | `app/type/type/this.cs`, one type |
| `%!app.goal.current%` | `app.goal.current` | the running goal |
| `%!app.variable.some%` | `app.variable["some"]` | `app/variable/this.cs`, the variable system; memory at `app/variable/list/this.cs` |

## Stages

| # | Stage | Changes what the builder sees |
|---|---|---|
| 0 | **Base:** re-record builder-formal's `Compile` (TypeSafe is back) so builder-formal closes green; take a baseline of the six suites | — |
| 1 | **Move type:** `type/list/this.cs` → `type/this.cs` (the system); `type/this.cs` → `type/type/this.cs` (one type). References by full name: `app.type.@this` 257. No behaviour change | no |
| 2 | **One set of types:** the maps become one set, each type owning its name, aliases, C# class and facts. `Add` is the one way in; `Load` is the startup scan; `Get`/`Clr` become the one door | no |
| 3 | **The type system is an item:** its stored context goes (`internal Context`, `list/this.cs:30`); `Output` writes its face; it answers its own navigation | no |
| 4 | **Faces:** system (`list` names, `kind`, `scheme`, `choice`); a type (`name`, `description`, `example`, `kind`); a choice type adds `values`; a kind (`name`, `extension`, `mime`). Prompt C's Types section renders from these facts (`properties.template:82` already reads type facts). `type/list/view` (already `[Obsolete]`) and `BuildTypeEntries` (`:425`) die | yes: twins byte-equal, or one eval run |
| 5 | **The reference** (details below): `app.variable.@this` → `app.type.item.variable` (53 references). A variable is its `text` plus its `code`; `Value()` is `Code.Run(context)`. The parser (`app/type/item/variable/parser/`) is the one definition; each hop kind parses its own piece. Build validation writes each marked row's `"variable"` list into the .pr, and loading never parses again. `item.Variable` (a read-only list of variables, null when none); `HasVariable => Variable?.Count > 0` on the item, and `data.HasVariable => _item?.HasVariable ?? false`; `IsVariable` is one variable covering the whole value. The six parsers, `variable.path` with its `Segment` classes, and the walker's switch go | yes: `"variable"` in the .pr; twins + one eval run |
| 6 | **Every system in the same shape:** goal (93 references), actor (15), module (11), test (87), variable (the system at `app/variable/this.cs`, freed by stage 5). Each: `X/this.cs` system + `X/X/this.cs` element, `.list`, `["name"]`, `.name`, `.current` where it means something, a face | no |
| 7 | **`on` and `current` per object:** events move from `event.on(Trigger=…)` into `X.on.<moment>` (`app.type.text.on.create`, `goal.on.error`, `%user%.on.change`). The `on` module's actions are one-line doors (`on.create`, `on.step`, `on.goal`, …; `on.error`, the modifier, stays). `current` is each object's own answer. Payoff: value-level mocking (`- on file create, call LoadFixture`) | yes: the `on` actions; twins + one eval run |
| 8 | **`%!app` holds its systems:** built-ins register at startup, a plugin loaded with `code.load` registers its own (`%!app.stripe%`); `%!app.list%` lists them; one name, one system (a clash fails loudly); `%setting.X%` stays as a short form | no |
| 9 | **Tests through the app's own doors:** `new app.@this(test: true)`, `app.variable.set("some", "var")`, `await app.module["file"]["read"].run(new { Path = "…" })` (a run of that action with those property values, through `action.Run`). The static helpers `TestApp`/`TestAction` die. The builder warns about goals no public goal reaches (dead code); `app.Test.Coverage` shows what the tests reached | a build warning |

| 10 | **Exception pass, before the branch closes:** go over every `throw` in the code this branch touched. A problem the programmer caused is an error in the result; an exception only ever means plang itself is broken. Most should already be gone by then (the name lookups become `["name"]` doors answering NotFound; `Push` answers the overflow; the .pr readers return their error) | no |

Each stage is its own commits, green against the baseline before the next starts.

## The reference (stage 5), settled with Ingi 2026-09-26

- **The parser** takes any text holding `%…%` and returns its variables. It is the one definition of a reference: `%` + a path starting with a letter, `_` or `!`, closing at the first `%` outside quotes and parentheses. It finds each `%…%`, and each hop kind parses its own piece. Callers: build validation (writing the .pr's `"variable"` lists), a template born at run (`read …, resolve variables`), `item.Variable`, the build checks (coverage, the types walk, pick's write-to).
- **A variable is `text` + `code`,** like a step. `code` is the parsed execution path: a list of hops in order, each getting the previous value and doing its one step, the way actions pass `%!data%`. No `next`, no segment classes, no walker switch: `Value()` is `Code.Run(context)`.
- **The hop kinds**, each a small class that parses, writes and runs its own piece; the JSON key is the kind:
  - `variable`: the root, read from the memory (`user`, `!app`);
  - `property`: a member (`.address`). A name starting with `!` is on `.Properties` (`!cost`); the hop is born knowing which, and doesn't re-check at run;
  - `index`: `[…]`. Its key is a typed value, keyed by its type (`{"number": "%i%"}`, `{"text": "k"}`), and a variable key carries its own code;
  - `method`: a call (`.tostring("dd.")`), whose values are its `parameter` list.
- **In the .pr:**

```json
{"name": "Data", "type": {"name": "text", "template": "plang"},
 "value": "today is %now.tostring(\"dd.\")% for %user.address[%i%].city% (%order!cost%)",
 "variable": [
   {"text": "%now.tostring(\"dd.\")%",
    "code": [{"variable": "now"}, {"method": "tostring", "parameter": [{"type": {"name": "text"}, "value": "dd."}]}]},
   {"text": "%user.address[%i%].city%",
    "code": [{"variable": "user"}, {"property": "address"},
             {"index": {"number": "%i%", "variable": [{"text": "%i%", "code": [{"variable": "i"}]}]}},
             {"property": "city"}]},
   {"text": "%order!cost%", "code": [{"variable": "order"}, {"property": "!cost"}]}]}
```

## Cross-cutting decisions

- **The .pr:** a marked row without its `"variable"` list is an old format (PrFormatOutdated, rebuild), not something to parse on load.
- **Errors, not exceptions (Ingi):** a problem the programmer caused is an error in the result, never an exception. An exception only ever means plang itself is broken. New code in every stage follows it; stage 10 checks the rest once.
- **Builder-visible stages (4, 5, 7):** the prompt twins stay byte-equal, or the change gets one eval run (C + nano, the 5 goals + the builder's 12). No nano chasing past that.
- **Renames of hundreds of references** go through the compiler's positions and the Edit tool (the hook blocks sed), in the stage that moves the class.
- **Rulings from builder-formal that carry over:** only a marked value renders `%var%` (the row's `template: plang`); file.read's item is born marked; `variable.list.Resolve` is gone (text renders itself).

## Demolition

| Dies | Stage |
|---|---|
| `type.list.@this` as the system's class name; `type/list/` as its folder | 1 |
| `Registry.cs`'s five maps, `_catalogByName`, `_full`; `Register`, `RegisterRuntime`; the static `Loader` (`Register`, `SealedNames`, `ReservedCore`, `ReservedShadow`); `Get(string)`/`Clr(string)` as two doors | 2 |
| the type system's stored `Context` | 3 |
| `type/list/view/` (whole folder) and `BuildTypeEntries` | 4 |
| `data.TryFullVarMatch`; static `text.HasVariable(string)`; `CleanName` ×2 (`data/this.cs:647`, `variable/list/this.cs:617`); `variable.@this.Convert`'s hand scan; the regexes in `step/this.Validate.cs:26`, `step/this.Scope.cs:9`, `pick/list/this.cs:90,96`; `Segment.Index.Key`'s string re-parse (`Segment.cs:67`); `Segment.Call.Args`; `data.HasVariableReference` | 5 |
| the per-concept `X.list.@this` as the class reached at `app.X` (goal, actor, module, test) | 6 |
| `event.on`, `Trigger` as a list of moments beside the objects | 7 |
| reflection over the C# `App` as `%!app`'s answer | 8 |
| `PLang.Tests/Shared/TestApp.cs`, `TestAction.cs` (statics) | 9 |

**Stays:** `variable.path` (the one parser, moving in stage 5); `modifier.list`; the `on.error` modifier; `mock.intercept` (it mocks an action; `on.create` mocks a value — different layers); the builder pipeline as built on builder-formal.

## OBP validation

| New or moved surface | plang path | C# | file | Check |
|---|---|---|---|---|
| type system | `%!app.type%` | `app.type` | `app/type/this.cs` | an item; no stored context; one way in (`Add`) |
| one type | `%!app.type["text"]%` / `.text` | `app.type["text"]` | `app/type/type/this.cs` | owns its name, aliases, facts, `on`, `current` |
| a reference | `%user.name%` | `app.type.item.variable.@this` | `app/type/item/variable/this.cs` | holds its parsed path; the only definition |
| an item's references | — (a value's own) | `item.Variable` | `app/type/item/this.cs` | read-only, born whole, null when none |
| variable system | `%!app.variable%` | `app.variable` | `app/variable/this.cs` | the memory of the actor in play |
| an object's events | `%!app.type.text.on%` (read) | `x.On` | each element's folder | registered by a step through the `on` module |
| running an action from C# | — | `app.module["file"]["read"].run(…)` | `module/module/…` | through `action.Run`; nothing test-only |
| names | — | — | — | no verb+noun; `Add`, `Load`, `Variable`, `On`, `current`, `list`: one word each |

## Open for the next round

1. The base: branching from builder-formal is assumed (Ingi's first suggestion); merging up first is the alternative.
2. Which moments each system's `on` offers (type: `create`; goal: `start`, `end`, `error`; step; variable: `change`…), and which `event.on` triggers map where.
3. Face facts beyond the start set, per system (goal, actor, module, test, variable).
