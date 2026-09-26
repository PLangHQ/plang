# app-systems — every `app.X` is the X system

Branch `app-systems`, off `builder-formal`. Designed with Ingi, 2026-09-24 (the parked draft `.bot/goal-graph-singular/architect/app-systems-draft.md`) and 2026-09-26. **Under review with Ingi, round by round, until a round passes without comment. Coder does not start before that.**

> **Coder, you own the code.** The shapes below are sketches: names, members and file:line are the architect's reading on 2026-09-26. Trace before each stage and bring back what doesn't hold.

## Why

1. **The three paths disagree.** The type system is reached at `app.type` (plang `%!app.type%`, C# `App.Type`) but lives at `type/list/this.cs` as `type.list.@this`; "list" names both the system and the types in it. OBP's alignment test (`Documentation/v0.2/object_pattern_formal.md`, "The three paths agree") says that's a wrong shape. The same holds for goal, actor, module, test and variable.
2. **The type system has several ways in and several copies of its knowledge.** `Register` (`type/list/this.cs:390`), `RegisterRuntime` (`Registry.cs:100`) and the static `Loader.Register` (`Loader.cs:87`) all add types; five maps (`Registry.cs:24-36`) plus `_catalogByName` (`:111`) and `_full` (`:216`) look them up; `Get(string)` and `Clr(string)` answer one question twice (`:90`, `:93`).
3. **Systems can't describe themselves.** `write out %!app.type%` throws `NoWireContract`, because the class declares no face.
4. **A reference (`%x%`) has six parsers and four definitions** (`data.TryFullVarMatch`, `text.HasVariable`, the coverage/types-walk/pick regexes, the old store render), plus two identical `CleanName`s and a hand-written name scan in `variable.@this.Convert`. `"save 50% now and 20% later"` parses differently depending on who reads it. Reading a variable re-parses its name and walks it through a switch (`data/this.Navigation.cs:51-89`).
5. **Events live beside the objects, not on them.** `event.on(Trigger=…)` is one module over an enum (`app/event/Trigger.cs`). The thing an event is about doesn't own it.
6. **The entry verb differs from plang's.** plang's entry point is `Start` (`Start.goal`, and `App.Start()` at `app/this.cs:480`), but everything else is entered through `Run` (goal, step, action, the lists, 126 action handlers).
7. **C# tests don't use the app's own doors.** They go through static helpers (`PLang.Tests/Shared/TestApp.cs`, `TestAction.cs`), not the way plang starts an action.

## The shape

**A system.** `X/this.cs` is the X system, reached at `app.X`; `X/X/this.cs` is one X.
- Members: `.list` (all of them), `.current` (the one in play, each system's own answer, read from the context: `app.actor.current => context.Actor`), `["name"]` (the door to one, the same door as C#'s indexer), `.name` (shorthand for `["name"]`; the system's own members win, so a type named `list` needs `["list"]`).
- **The system is an item.** It writes its face through `Output` (facts, any writer), a formatter (a template) presents them, and it answers its own navigation (`Get(parent, key)`: a member first, else the element with that name, else NotFound). Like every item, it stores no context: the caller passes it.

**One element.** It owns its facts, its `on` (events about it) and its `current` (the instance in play).

**`Start` is the entry point of everything that runs (Ingi).** As `Start.goal` is plang's entry: `app.Start()`, `goal.Start(context)`, `step.Start`, `action.Start`, each handler's `Start()`, a list's `Start`, a code's `Start`. It's virtual, so an owner can change what starting it means. **A value keeps `Value()`** (Ingi): a variable is a value, so `variable.Value()` → `Code.Start(context)`.

**What runs, runs in a module.** A step maps only to module actions. `%!app…%` and every `%…%` only read (a method call inside `%…%` must not change anything). An action's C# hands over to the owner in one line: `on/create.cs` → `app.type.text.on.create(LoadText)`.

| plang | C# | file |
|---|---|---|
| `%!app.type%` | `app.type` | `app/type/this.cs`, the type system |
| `%!app.type.list%` | `app.type.list` | a member |
| `%!app.type["text"]%`, `%!app.type.text%` | `app.type["text"]` | `app/type/type/this.cs`, one type |
| `%!app.goal.current%` | `app.goal.current` | the running goal |
| `%!app.variable.some%` | `app.variable["some"]` | `app/variable/this.cs`, the variable system; memory at `app/variable/list/this.cs` |
| `Start.goal` | `app.Start()`, `goal.Start(context)` | the entry point, one word everywhere |

## Stages

| # | Stage | Changes what the builder sees |
|---|---|---|
| 0 | **Base:** re-record builder-formal's `Compile` (TypeSafe is back) so its BootstrapTests pass; take a baseline of the six suites | — |
| 1 | **`Run` → `Start`:** the C# entry verb of every executable object (`goal/this.cs:325`, `step/this.cs:111`, `step/list/this.cs:28`, `action/this.cs:165`, `action/list/this.cs:31`, the event bindings `binding/this.cs:44`, `binding/list/this.cs:22,28`), every handler's `Run()` (126 files) and the generator's emit. Virtual where an owner may override. No behaviour change. plang action names (`environment.run`, `test.run`, …) are plang vocabulary and not part of this | no |
| 2 | **Move type:** `type/list/this.cs` → `type/this.cs` (the system); `type/this.cs` → `type/type/this.cs` (one type). References by full name: `app.type.@this` 257. No behaviour change | no |
| 3 | **One set of types:** the maps become one set, each type owning its name, aliases, C# class and facts. `Add` is the one way in; `Load` is the startup scan; `Get`/`Clr` become the one door | no |
| 4 | **The type system is an item:** its stored context goes (`internal Context`, `list/this.cs:30`); `Output` writes its face; it answers its own navigation | no |
| 5 | **Faces:** system (`list` names, `kind`, `scheme`, `choice`); a type (`name`, `description`, `example`, `kind`); a choice type adds `values`; a kind (`name`, `extension`, `mime`). Prompt C's Types section renders from these facts (`properties.template:82` already reads type facts). `type/list/view` (already `[Obsolete]`) and `BuildTypeEntries` (`:425`) die | yes: twins byte-equal, or one eval run |
| 6 | **The reference** (details below): `app.variable.@this` → `app.type.item.variable` (53 references). A variable is `text` + `code`; `Value()` → `Code.Start(context)`. The parser (`app/type/item/variable/parser/`) is the one definition; each hop kind parses its own piece. Build validation writes each marked row's `"variable"` list into the .pr, and loading never parses again. `item.Variable` (a read-only list of variables, null when none); `HasVariable => Variable?.Count > 0` on the item, and `data.HasVariable => _item?.HasVariable ?? false`; `IsVariable` is one variable covering the whole value | yes: `"variable"` in the .pr; twins + one eval run |
| 7 | **Every system in the same shape:** goal (93 references), actor (15), module (11), test (87), variable (the system at `app/variable/this.cs`, freed by stage 6). Each: `X/this.cs` system + `X/X/this.cs` element, `.list`, `["name"]`, `.name`, `.current` where it means something, a face | no |
| 8 | **`on` and `current` per object:** events move from `event.on(Trigger=…)` into `X.on.<moment>` (`app.type.text.on.create`, `goal.on.error`, `%user%.on.change`). The `on` module's actions are one-line doors (`on.create`, `on.step`, `on.goal`, …; `on.error`, the modifier, stays). `current` is each object's own answer. Payoff: value-level mocking (`- on file create, call LoadFixture`) | yes: the `on` actions; twins + one eval run |
| 9 | **`%!app` holds its systems:** built-ins register at startup, a plugin loaded with `code.load` registers its own (`%!app.stripe%`); `%!app.list%` lists them; one name, one system (a clash fails loudly); `%setting.X%` stays as a short form | no |
| 10 | **Tests through the app's own doors:** `new app.@this(test: true)`, `app.variable.set("some", "var")`, `await app.module["file"]["read"].Start(new { Path = "…" })` (a start of that action with those property values, through `action.Start`). The static helpers `TestApp`/`TestAction` die. The builder warns about goals no public goal reaches (dead code); `app.Test.Coverage` shows what the tests reached | a build warning |
| 11 | **Exception pass, before the branch closes:** go over every `throw` in the code this branch touched. A problem the programmer caused is an error in the result; an exception only ever means plang itself is broken. Most should already be gone by then (the name lookups become `["name"]` doors answering NotFound; `Push` answers the overflow; the .pr readers return their error) | no |

Each stage is its own commits, green against the baseline before the next starts.

## The reference (stage 6), settled with Ingi 2026-09-26

- **The parser** takes any text holding `%…%` and returns its variables. It is the one definition of a reference: `%` + a path starting with a letter, `_` or `!`, closing at the first `%` outside quotes and parentheses. It finds each `%…%`, and each hop kind parses its own piece. Callers: build validation (writing the .pr's `"variable"` lists), a template born at run (`read …, resolve variables`), `item.Variable`, the build checks (coverage, the types walk, pick's write-to).
- **A variable is `text` + `code`,** like a step. `code` is the parsed execution path: a list of hops in order, each getting the previous value and doing its one step, the way actions pass `%!data%`. No `next`, no segment classes, no walker switch. A variable is a value, so its door stays `Value()` (Ingi), and `Value()` → `Code.Start(context)`.
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
- **Errors, not exceptions (Ingi):** a problem the programmer caused is an error in the result, never an exception. An exception only ever means plang itself is broken. New code in every stage follows it; stage 11 checks the rest once.
- **Builder-visible stages (5, 6, 8):** the prompt twins stay byte-equal, or the change gets one eval run (C + nano, the 5 goals + the builder's 12). No nano chasing past that.
- **Renames of hundreds of references** go through the compiler's positions and the Edit tool (the hook blocks sed), in the stage that moves the class.
- **Rulings from builder-formal that carry over:** only a marked value renders `%var%` (the row's `template: plang`); file.read's item is born marked; `variable.list.Resolve` is gone (text renders itself).

## Demolition

| Dies | Stage |
|---|---|
| `Run` as the entry verb (runtime objects, handlers, the generator's emit) | 1 |
| `type.list.@this` as the system's class name; `type/list/` as its folder | 2 |
| `Registry.cs`'s five maps, `_catalogByName`, `_full`; `Register`, `RegisterRuntime`; the static `Loader` (`Register`, `SealedNames`, `ReservedCore`, `ReservedShadow`); `Get(string)`/`Clr(string)` as two doors | 3 |
| the type system's stored `Context` | 4 |
| `type/list/view/` (whole folder) and `BuildTypeEntries` | 5 |
| `app/variable/path/` (`Parse`, `Segment` and its kinds, `Segment.Index.Key`'s string re-parse, `Segment.Call.Args`); the walker's switch and clr special case (`data/this.Navigation.cs:33-94`); `data.TryFullVarMatch`; static `text.HasVariable(string)`; `CleanName` ×2 (`data/this.cs:647`, `variable/list/this.cs:617`); `variable.@this.Convert`'s hand scan; the regexes in `step/this.Validate.cs:26`, `step/this.Scope.cs:9`, `pick/list/this.cs:90,96`; `data.HasVariableReference` | 6 |
| the per-concept `X.list.@this` as the class reached at `app.X` (goal, actor, module, test) | 7 |
| `event.on`, `Trigger` as a list of moments beside the objects | 8 |
| reflection over the C# `App` as `%!app`'s answer | 9 |
| `PLang.Tests/Shared/TestApp.cs`, `TestAction.cs` (statics) | 10 |

**Stays:** `modifier.list`; the `on.error` modifier; `mock.intercept` (it mocks an action; `on.create` mocks a value, a different layer); the builder pipeline as built on builder-formal.

## OBP validation

| New or moved surface | plang path | C# | file | Check |
|---|---|---|---|---|
| type system | `%!app.type%` | `app.type` | `app/type/this.cs` | an item; no stored context; one way in (`Add`) |
| one type | `%!app.type["text"]%` / `.text` | `app.type["text"]` | `app/type/type/this.cs` | owns its name, aliases, facts, `on`, `current` |
| a variable | `%user.name%` | `app.type.item.variable.@this` | `app/type/item/variable/this.cs` | `text` + `code`; `Value()` starts its code |
| the parser | — | `app.type.item.variable.parser.@this` | `app/type/item/variable/parser/this.cs` | the only definition of a reference |
| an item's variables | — (a value's own) | `item.Variable` | `app/type/item/this.cs` | read-only, born whole, null when none |
| variable system | `%!app.variable%` | `app.variable` | `app/variable/this.cs` | the memory of the actor in play |
| an object's events | `%!app.type.text.on%` (read) | `x.On` | each element's folder | registered by a step through the `on` module |
| starting an action from C# | — | `app.module["file"]["read"].Start(…)` | `module/module/…` | through `action.Start`; nothing test-only |
| names | — | — | — | no verb+noun; `Start`, `Add`, `Load`, `Variable`, `Code`, `On`, `current`, `list`: one word each |

## Open for the next round

1. Which moments each system's `on` offers (type: `create`; goal: `start`, `end`, `error`; step: `start`, `end`; variable: `create`, `change`, `remove`; channel: `write`, `read`, `ask`), and which `event.on` triggers map where.
2. Face facts beyond the start set, per system (goal: `name`, `path`, `description`, its steps by index and text; actor: `name`; module: `name`, `description`, its action names; test: `name`, `status`; variable: `name`, `type`).
