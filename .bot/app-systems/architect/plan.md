# app-systems — every `app.X` is the X system

Branch `app-systems`, off `builder-formal`. Designed with Ingi, 2026-09-24 (the parked draft `.bot/goal-graph-singular/architect/app-systems-draft.md`) and 2026-09-26. **Under review with Ingi, round by round, until a round passes without comment. Coder does not start before that.**

> **Coder, you own the code.** The shapes below are sketches: names, members and file:line are the architect's reading on 2026-09-26. Trace before each stage and bring back what doesn't hold.

## Why

1. **The three paths disagree.** The type system is reached at `app.type` (plang `%!app.type%`, C# `App.Type`) but lives at `type/list/this.cs` as `type.list.@this`; "list" names both the system and the types in it. OBP's alignment test (`Documentation/v0.2/object_pattern_formal.md`, "The three paths agree") says that's a wrong shape. The same holds for goal, actor, module, test and variable.
2. **The type system has several ways in and several copies of its knowledge.** `Register` (`type/list/this.cs:390`), `RegisterRuntime` (`Registry.cs:100`) and the static `Loader.Register` (`Loader.cs:87`) all add types; five maps (`Registry.cs:24-36`) plus `_catalogByName` (`:111`) and `_full` (`:216`) look them up; `Get(string)` and `Clr(string)` answer one question twice (`:90`, `:93`).
3. **Systems can't describe themselves, and the facts they'd show are thin or wrong.** `write out %!app.type%` throws `NoWireContract`, because the class declares no face. Only 2 of ~21 types declare a description, text's example is a filename (`readme.md`), and 356 of 594 `.goal` files put the goal's description under its name, where it becomes step 0's comment.
4. **A reference (`%x%`) has six parsers and four definitions** (`data.TryFullVarMatch`, `text.HasVariable`, the coverage/types-walk/pick regexes, the old store render), plus two identical `CleanName`s and a hand-written name scan in `variable.@this.Convert`. `"save 50% now and 20% later"` parses differently depending on who reads it. Reading a variable re-parses its name and walks it through a switch (`data/this.Navigation.cs:51-89`).
5. **Events live beside the objects, not on them.** `event.on(Trigger=…)` is one module over an enum (`app/event/Trigger.cs`). The thing an event is about doesn't own it.
6. **The entry verb differs from plang's.** plang's entry point is `Start` (`Start.goal`, and `App.Start()` at `app/this.cs:480`), but everything else is entered through `Run` (goal, step, action, the lists, 126 action handlers).
7. **C# tests don't use the app's own doors.** They go through static helpers (`PLang.Tests/Shared/TestApp.cs`, `TestAction.cs`), not the way plang starts an action.

## The shape

**A system.** `X/this.cs` is the X system, reached at `app.X`; `X/X/this.cs` is one X.
- Members: `.list` (the ones loaded so far; see below), `.current` (the one in play, each system's own answer, read from the context: `app.actor.current => context.Actor`), `["name"]` (the door to one, the same door as C#'s indexer), `.name` (shorthand for `["name"]`; the system's own members win, so a type named `list` needs `["list"]`).
- **The system is an item.** It writes its face through `Output` (facts, any writer), a formatter (a template) presents them, and it answers its own navigation (`Get(parent, key)`: a member first, else the element with that name, else NotFound). Like every item, it stores no context: the caller passes it.

**`list` is a real object (Ingi).** Each system's `list` is its `X.list` type (`app/goal/list/this.cs`), which inherits from the plang list: it prints, enumerates, counts and indexes like any list, and carries its own members.
- `%!app.goal.list%` is the goals loaded so far (goals load when they're called, `goal/list/this.cs:128-176`).
- **`%!app.goal.list.all%` is every goal in the app,** built from a listing of `.build/` (the `.pr` files, not read); each goal loads when it's first touched. The dead-goal warning (stage 11) uses it.
- **Every list has `all`, for consistency.** Where everything is already present (the types are registered at startup), `all` answers the list itself, so nobody needs to know which systems load lazily.
- **`all` takes settings (Ingi):** a method with named, optional parameters; `.all` without parentheses runs it with the defaults (one door). For goals: `show` = `public` (default) | `private` | `all`, and `os` = whether os/system goals are included (default false: the app's own goals). `%!app.goal.list.all(show: "all", os: true)%`. The default lists each `.pr`'s main goal without reading the files; `private`/`all` read them for their sub-goals.
- A list is navigated by index, so `list.all` never clashes with an element's name; one element by name stays on the system (`%!app.goal.Start%`).

```csharp
// sketch: app/goal/list/this.cs
public sealed class @this : app.type.item.list.@this<goal.goal.@this>
{
    public list.@this<goal.goal.@this> all { get; }     // every goal in the app, each loads when touched
}
```

**One element.** It owns its facts, its `on` (events about it) and its `current` (the instance in play).

**`Start` is the entry point of everything that runs (Ingi).** As `Start.goal` is plang's entry: `app.Start()`, `goal.Start(context)`, `step.Start`, `action.Start`, each handler's `Start()`, a list's `Start`, a code's `Start`. It's virtual, so an owner can change what starting it means. **A value keeps `Value()`** (Ingi); anything that has code also has `Start`, for consistency. A variable is both: `variable.Value()` → `variable.Start(context)` → `Code.Start(context)`.

**plang vocabulary is lowercase in C# too (Ingi).** The structure plang navigates is lowercase: `app.type`, `app.goal`, `app.variable`, `app.module`, `app.actor`, `app.test`, their `list`, `current`, `all`, `on`, `before`, `after` and the event classes. Facts keep their C# names (`Name`, `Path`, `Comment`): plang writes its paths in lowercase (`%!app.goal.show.name%`) and its navigation ignores case, and the face writes facts lowercase already. C# plumbing plang never navigates stays PascalCase, including an item's `Variable` list and a variable's `Code` (`%order.variable%` must reach the order's own key, not the item's metadata); a C# keyword keeps its `@` (`app.@event`); an item's own `Type` (its type entity) stays. So the three paths match letter for letter: `%!app.type["text"]%` ↔ `app.type["text"]` ↔ `app/type/type/this.cs`. Each system's property on `app` is renamed in the stage that moves it (type: stage 2; the others: stage 7). This replaces CLAUDE.md's "Property names on `app.@this` stay PascalCase" (proposal filed).

**Nodes lowercase, verbs PascalCase (Ingi: "the line where C# executes, and in C# starts uppercase").** Lowercase is what plang navigates (`app.type`, `list`, `current`, `on.before.create`, the facts). PascalCase is what C# calls: `Start()`, `Value()`, `Add()`, `Load()`. plang never calls a verb through a path (what runs, runs in a module; `%…%` only reads), so a verb is C#'s alone and sits with C# library methods (`ToString`, `DisposeAsync`). A read inside `%…%` (`%now.tostring("dd.")%`) works either way, since plang's navigation ignores case.

**What runs, runs in a module.** A step maps only to module actions. `%!app…%` and every `%…%` only read (a method call inside `%…%` must not change anything). An action's C# hands over to the owner in one line: `on/after.cs` → `app.type.text.on.after.create(LoadText)`.

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
| 1 | **`Run` → `Start`:** the C# entry verb of every executable object (`goal/this.cs:325`, `step/this.cs:111`, `step/list/this.cs:28`, `action/this.cs:165`, `action/list/this.cs:31`, the event bindings `binding/this.cs:44`, `binding/list/this.cs:22,28`), every handler's `Run()` (126 files) and the generator's emit. Starting one action is one verb end to end: today it's `action.Run` → `call.ExecuteAsync` (`callstack/call/this.cs:225`) → `handler.Execute()` (`ICodeGenerated.cs:37`) → the handler's `Run()`; `Execute`/`ExecuteAsync` go with `Run`. Virtual where an owner may override. No behaviour change. plang action names (`environment.run`, `test.run`, …) are plang vocabulary and not part of this | no |
| 2 | **Move type:** `type/list/this.cs` → `type/this.cs` (the system); `type/this.cs` → `type/type/this.cs` (one type). References by full name: `app.type.@this` 257. `App.Type` → `app.type` (lowercase). No behaviour change | no |
| 3 | **One set of types:** the maps become one set, each type owning its name, aliases, C# class and facts. `Add` is the one way in; `Load` is the startup scan; `Get`/`Clr` become the one door | no |
| 4 | **The type system is an item:** its stored context goes (`internal Context`, `list/this.cs:30`); `Output` writes its face; it answers its own navigation | no |
| 5 | **Faces and honest facts** (details in "Faces" below): the type faces (system, type, choice `values`, kind); each type's real description and example; internal item classes stay out; prompt C's Types section renders from these facts (`properties.template:82` already reads type facts); `type/list/view` (already `[Obsolete]`) and `BuildTypeEntries` (`:425`) die. Also: the 356 `.goal` files get their description above the goal's name; `goal.Comment` is the one description member; the hash covers comments; `start.md` docs | yes: twins byte-equal, or one eval run |
| 6 | **The reference** (details below): `app.variable.@this` → `app.type.item.variable` (53 references). A variable is `text` + `code`; `Value()` → `Start(context)` → `Code.Start(context)`. The parser (`app/type/item/variable/parser/`) is the one definition; each hop kind parses its own piece. Build validation writes each marked row's `"variable"` list into the .pr, and loading never parses again. `item.Variable` (a read-only list of variables, null when none); `HasVariable => Variable?.Count > 0` on the item, and `data.HasVariable => _item?.HasVariable ?? false`; `IsVariable` is one variable covering the whole value. **Every installed .pr with marked rows** (the builder's 7, `test.pr`, `show.pr`) is rewritten with its `"variable"` lists by a throwaway C# pass (the parser over each marked value, written through `plang.Text`; no LLM), since the loader refuses a marked row without its list | yes: `"variable"` in the .pr; twins + one eval run |
| 7 | **Every system in the same shape:** goal (93 references), actor (15), module (11), test (87), variable (the system at `app/variable/this.cs`, freed by stage 6). Each: `X/this.cs` system + `X/X/this.cs` element, `.list` (with `all`), `["name"]`, `.name`, `.current` where it means something, a face; its property on `app` goes lowercase (`App.Goal` → `app.goal`, …) | no |
| 8 | **`on` and `current` on every object** (details below): events move from `event.on(Trigger=…)` to the object, as `on.before.<verb>` / `on.after.<verb>` for every public verb, plus outcomes (`on.error`, `on.hit`, `on.miss`). The `on` module's actions are one-line doors (`on.before`, `on.after`, the outcome actions; `on.error`, the modifier, stays). `current` is each object's own answer. Payoff: value-level mocking (`- after file create, call LoadFixture`) | yes: the `on` actions; twins + one eval run |
| 9 | **Module pass, with file.read as the template (Ingi):** fix file.read first and make it the worked example of a correct action, written up as a doc ("how an action is written"). Then go over every module against it, one module per commit. The checklist: (1) plang values are born through their type (`app.type.file.Create(…)`; `new` only inside the type); (2) `Start()` hands over to the owner in one line; (3) no opened box, no broken seal (no `.Value()` on what it returns or forwards); (4) errors are results; (5) properties are typed (`Data<T>`), write targets are variables; (6) events fire from the owner, not the handler | per module: twins where a prompt changes |
| 10 | **`%!app` holds its systems:** built-ins register at startup, a plugin loaded with `code.load` registers its own (`%!app.stripe%`); `%!app.list%` lists them; one name, one system (a clash fails loudly); `%setting.X%` stays as a short form | no |
| 11 | **Tests through the app's own doors:** `new app.@this(test: true)`, `app.variable.Set("some", "var")`, `await app.module["file"]["read"].Start(new { Path = "…" })` (a start of that action with those property values, through `action.Start`). The static helpers `TestApp`/`TestAction` die. The builder warns about goals no public goal reaches (dead code); `app.Test.Coverage` shows what the tests reached | a build warning |
| 12 | **Exception pass, before the branch closes:** go over every `throw` in the code this branch touched. A problem the programmer caused is an error in the result; an exception only ever means plang itself is broken. Most should already be gone by then (the name lookups become `["name"]` doors answering NotFound; `Push` answers the overflow; the .pr readers return their error) | no |

Each stage is its own commits, green against the baseline before the next starts.

## The reference (stage 6), settled with Ingi 2026-09-26

- **The parser** takes any text holding `%…%` and returns its variables. It is the one definition of a reference: `%` + a path starting with a letter, `_` or `!`, closing at the first `%` outside quotes and parentheses. It finds each `%…%`, and each hop kind parses its own piece. Callers: build validation (writing the .pr's `"variable"` lists), a template born at run (`read …, resolve variables`), `item.Variable`, the build checks (coverage, the types walk, pick's write-to).
- **A variable is `text` + `code`,** like a step. `code` is the parsed execution path: a list of hops in order, each getting the previous value and doing its one step, the way actions pass `%!data%`. No `next`, no segment classes, no walker switch. A variable is a value, so its door stays `Value()`, and like everything with code it has `Start` (Ingi): `Value()` → `Start(context)` → `Code.Start(context)`.
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

## Events on each object (stage 8), settled with Ingi 2026-09-26

Events are on everything, and they cost nothing unless bound: an object's `on` is null until something binds, and the call is `on?.before.create.Start(this, context)`, returning at once.

**A value is born through its type, so `create` fires every time.** Today plang values are born with `new` in at least 78 production places (`file/read.cs:72`: `new global::app.type.item.file.@this(path, Context, template)`), none passing the type, so `after file create` would miss them; a constructor can't fire it (it can't await, and an item stores no context). Stage 8 makes the type's `Create` door (`type/this.cs:340`) fire `on.before/after.create`; stage 9 moves every birth onto it (`new` of a plang value only inside its type).

**The rule, one line: every public verb has `on.before.<verb>` and `on.after.<verb>`; an outcome that isn't a method is named for what happened (`on.error`, cache `on.hit` / `on.miss`).** The event name is the method's own name, so the three paths agree (`goal.Start()` → `on.before.start`), and a new verb gets its events for free.

**Each event is a class** (Ingi): `app/event/before/create/this.cs`, `app/event/after/start/this.cs`, … The object's `on` navigates to them, lowercase like every node: `%!app.type.text.on.before.create%` ↔ `app.type.text.on.before.create` ↔ `app/event/before/create/this.cs`.

```csharp
// sketch: where a value is created
on?.before.create.Start(this, context);      // on is null, or no bindings → nothing happens
…
on?.after.create.Start(this, context);
```

| object | verbs → events | today's `Trigger` |
|---|---|---|
| app | `Start` | BeforeAppStart, AfterAppStart |
| goal, step | `Start`, `Load`; goal `on.error` | BeforeGoal/AfterGoal, BeforeStep/AfterStep, OnBefore/After…Load, OnError |
| action | `Start` | BeforeAction, AfterAction |
| type (any value) | `Create` (`app.type.text.on.before.create`) | new |
| variable | `Set`, `Remove` | OnVariableChange |
| channel | `Write`, `Read`, `Ask` | BeforeWrite/AfterWrite, BeforeRead/AfterRead, OnAsk |
| cache | outcomes `on.hit`, `on.miss` | OnCacheHit, OnCacheMiss |
| actor, module, setting, test, … | their verbs, by the same rule | new |

- **`current`** during a handler is the item passed in (`this`): `%!app.type.text.current%` is the text being created.
- **An object's own `on` vs its type's:** `%user%`'s bindings are that one variable's; `app.type.text.on…` binds every text.

```
- before goal start, call LogStart        → on.before(… goal.Start …, goal.call(LogStart))
- after text create, call LoadText        → app.type.text.on.after.create(LoadText)
- after %user% set, call UserChanged      → the variable's on.after.set
```

## Faces (stages 5 and 7), settled with Ingi 2026-09-26

A system's face is a summary (names only); detail comes by navigating to one element. `current` shows where something is in play. The facts are the start set; a system may add a fact that's worth showing.

**Docs entry is `start.md` (Ingi):** inside plang apps (`os/**` and every app a programmer writes), a folder's docs are `start.md`, as `Start.goal` is its entry. The four `readme.md` under `os/system/ui/templates/{uikit,default}/` and `os/system/modules/ui/Builder/templates/{uikit,default}/` are renamed in stage 5. The C# repo keeps `README.md` where GitHub or tools expect it, with a `start.md` beside it (Ingi): in the repo root, `PLang/`, `PlangConsole/`, `Skill/` and `tools/decider/`, `start.md` is the one edited and `README.md` is its copy, and a small test fails when the two differ. `.semgrep/`, `Documentation/v0.2/audit/`, `PlangTests/` and `.bot/**` keep `README.md` only.

**A comment says what the next code line does (Ingi).** A goal's description goes above its name, and a step's comment above the step. The parser already follows this (`goal/this.cs:584-599`: lines above the name are the goal's `Comment`, lines above a step are that step's). The `.goal` files don't: 356 of 594 in `os/` and `Tests/` describe the goal under its name, so the text lands on step 0. In stage 5:
1. Those files are fixed: the lines move above the goal's name (Edit tool; split across helpers as needed).
2. One member: `goal.Comment` is the description; `goal.Description` (never set by the parser, only read from a `.pr` key nothing writes, and "Goal not found" at `:402`) goes. The goal face shows `comment`.
3. The goal's hash covers the source as written, comments included, so a comment-only change re-saves the `.pr` (every step still cached, no decider or LLM).
4. The rule goes into the plang docs, and a CLAUDE.md proposal covers bots writing `.goal` files.

**Honest facts come with the faces (stage 5).** Today only text and base64 declare a description; text's example is a filename (`readme.md`, `text/this.cs:33`); archive, binary and signature have placeholder examples (`(archive)`, `(bytes)`, `(signature)`). Each type gets a real description and a real example. Internal item classes under `type/item/` (`wire`, `source`, `clr`, `computed`) declare that they're not plang types and stay out of the face. `channel` and `serializers` carry `[PlangType]` but are collections, not choices; they're placed properly.

| face | shows |
|---|---|
| `%!app.type%` | `list` (type names), `kind`, `scheme`, `choice` |
| `%!app.type.text%` | `name`, `description`, `example`, `kind`; a choice type adds `values` |
| `%!app.type.text.kind.md%` | `name`, `extension`, `mime` |
| `%!app.goal%` | `list` (names of the goals loaded so far), `current` |
| `%!app.goal.Start%` | `name`, `path`, `comment`, its steps (index and text), `child` (its sub-goals), `on` |
| `%!app.actor%` | `list` (system, user), `current` |
| `%!app.actor.user%` | `name` |
| `%!app.module%` | `list` (module names) |
| `%!app.module.file%` | `name`, `description`, its action names |
| `%!app.test%` | `list` (test names), `current` |
| `%!app.test.X%` | `name`, `status` |
| `%!app.variable%` | `list` (variable names) |
| `%!app.variable.some%` | `name`, `type` |

## Cross-cutting decisions

- **The .pr:** a marked row without its `"variable"` list is an old format (PrFormatOutdated, rebuild), not something to parse on load.
- **Errors, not exceptions (Ingi):** a problem the programmer caused is an error in the result, never an exception. An exception only ever means plang itself is broken. New code in every stage follows it; stage 12 checks the rest once.
- **Builder-visible stages (5, 6, 8, and 9 where a module's prompt teaching changes):** the prompt twins stay byte-equal, or the change gets one eval run (C + nano, the 5 goals + the builder's 12). No nano chasing past that.
- **Renames of hundreds of references** go through the compiler's positions and the Edit tool (the hook blocks sed), in the stage that moves the class.
- **Rulings from builder-formal that carry over:** only a marked value renders `%var%` (the row's `template: plang`); file.read's item is born marked; `variable.list.Resolve` is gone (text renders itself).

## Demolition

| Dies | Stage |
|---|---|
| `Run` as the entry verb (runtime objects, handlers, the generator's emit) | 1 |
| `type.list.@this` as the system's class name; `type/list/` as its folder | 2 |
| `Registry.cs`'s five maps, `_catalogByName`, `_full`; `Register`, `RegisterRuntime`; the static `Loader` (`Register`, `SealedNames`, `ReservedCore`, `ReservedShadow`); `Get(string)`/`Clr(string)` as two doors | 3 |
| the type system's stored `Context` | 4 |
| `type/list/view/` (whole folder) and `BuildTypeEntries`; `goal.Description`; the goal hash that ignores comments; the four `os/` `readme.md` (→ `start.md`) | 5 |
| `app/variable/path/` (`Parse`, `Segment` and its kinds, `Segment.Index.Key`'s string re-parse, `Segment.Call.Args`); the walker's switch and clr special case (`data/this.Navigation.cs:33-94`); `data.TryFullVarMatch`; static `text.HasVariable(string)`; `CleanName` ×2 (`data/this.cs:647`, `variable/list/this.cs:617`); `variable.@this.Convert`'s hand scan; the regexes in `step/this.Validate.cs:26`, `step/this.Scope.cs:9`, `pick/list/this.cs:90,96`; `data.HasVariableReference` | 6 |
| the per-concept `X.list.@this` as the class reached at `app.X` (goal, actor, module, test) | 7 |
| `event.on`, `Trigger` as a list of moments beside the objects | 8 |
| every direct `new` of a plang value outside its type (≥ 78 production sites); whatever else the checklist catches, per module | 9 |
| reflection over the C# `App` as `%!app`'s answer | 10 |
| `PLang.Tests/Shared/TestApp.cs`, `TestAction.cs` (statics) | 11 |

**Stays:** `modifier.list`; the `on.error` modifier; `mock.intercept` (it mocks an action; `on.after.create` mocks a value, a different layer); the builder pipeline as built on builder-formal.

## OBP validation

| New or moved surface | plang path | C# | file | Check |
|---|---|---|---|---|
| type system | `%!app.type%` | `app.type` | `app/type/this.cs` | an item; no stored context; one way in (`Add`) |
| one type | `%!app.type["text"]%` / `.text` | `app.type["text"]` | `app/type/type/this.cs` | owns its name, aliases, facts, `on`, `current` |
| a variable | `%user.name%` | `app.type.item.variable.@this` | `app/type/item/variable/this.cs` | `text` + `code`; `Value()` → `Start()` → `Code.Start()` |
| the parser | — | `app.type.item.variable.parser.@this` | `app/type/item/variable/parser/this.cs` | the only definition of a reference |
| an item's variables | — (a value's own) | `item.Variable` | `app/type/item/this.cs` | read-only, born whole, null when none |
| variable system | `%!app.variable%` | `app.variable` | `app/variable/this.cs` | the memory of the actor in play |
| an object's events | `%!app.type.text.on%` (read) | `x.on` | `app/event/before/<verb>/this.cs`, `app/event/after/<verb>/this.cs` | registered by a step through the `on` module; null until bound |
| a value's birth | — | `app.type.file.Create(…)` | `app/type/type/this.cs` (`Create`) | the one door; fires `on.before/after.create`; `new` only inside the type |
| how an action is written | — | — | a doc, file.read as its worked example | the module pass's template |
| starting an action from C# | — | `app.module["file"]["read"].Start(…)` | `module/module/…` | through `action.Start`; nothing test-only |
| every goal | `%!app.goal.list.all%` | `app.goal.list.all(show, os)` | `app/goal/list/this.cs` | a method with optional parameters; `.all` = the defaults |
| a goal's description | `%!app.goal.Show.comment%` | `goal.Comment` | `app/goal/goal/this.cs` | the lines above the goal's name; the only description member |
| names | — | — | — | no verb+noun; `Start`, `Add`, `Load`, `Variable`, `Code`, `On`, `current`, `list`, `all`: one word each; plang vocabulary lowercase |

## Open for the next round

Rounds 1–3 closed. Round 3 added: lowercase covers the structure (facts keep their C# names), values born through their type (`create` fires reliably), and the module pass with file.read as the template (stage 9). Next: round 4, a full pass.
