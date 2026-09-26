# app-systems — every `app.X` is the X system

Branch `app-systems`, off `builder-formal`. Designed with Ingi, 2026-09-24 (the parked draft `.bot/goal-graph-singular/architect/app-systems-draft.md`) and 2026-09-26. **Under review with Ingi, round by round, until a round passes without comment. Coder does not start before that.**

> **Coder, you own the code.** The shapes below are sketches: names, members and file:line are the architect's reading on 2026-09-26. Trace before each stage and bring back what doesn't hold.

## Why

1. **The three paths disagree.** The type system is reached at `app.type` (plang `%!app.type%`, C# `App.Type`) but lives at `type/list/this.cs` as `type.list.@this`; "list" names both the system and the types in it. OBP's alignment test (`Documentation/v0.2/object_pattern_formal.md`, "The three paths agree") says that's a wrong shape. The same holds for goal, actor, module, test and variable.
2. **The type system has several ways in and several copies of its knowledge.** `Register` (`type/list/this.cs:390`), `RegisterRuntime` (`Registry.cs:100`) and the static `Loader.Register` (`Loader.cs:87`) all add types; five maps (`Registry.cs:24-36`) plus `_catalogByName` and `_full` (`type/list/this.cs:111`, `:216`) look them up; `Get(string)` and `Clr(string)` answer one question twice (`type/list/this.cs:90`, `:93`).
3. **Systems can't describe themselves, and the facts they'd show are thin or wrong.** `write out %!app.type%` throws `NoWireContract`, because the class declares no face. Only 2 of ~21 types declare a description, text's example is a filename (`readme.md`), and 356 of 594 `.goal` files put the goal's description under its name, where it becomes step 0's comment.
4. **A reference (`%x%`) has at least nine parsers and several definitions** (`data.TryFullVarMatch`, `text.HasVariable`, the coverage/types-walk/pick regexes, `Formal.cs:311,418` (`%[^%\s]+%`, which rejects a space), `debug/this.cs:506`, `pick/list/this.cs:94,132`, the old store render), plus two identical `CleanName`s and a hand-written name scan in `variable.@this.Convert`. `"save 50% now and 20% later"` parses differently depending on who reads it. Reading a variable re-parses its name and walks it through a switch (`data/this.Navigation.cs:51-89`).
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
- A list is navigated by index, so `list.all` never clashes with an element's name; one element by name stays on the system (`%!app.goal.start%`). plang paths are written in lowercase (`%!app.goal.show.name%`); navigation ignores case.

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

**Nodes lowercase, verbs PascalCase (Ingi: "the line where C# executes, and in C# starts uppercase").** Lowercase is what plang navigates (`app.type`, `list`, `current`, `on.before.create`, the facts). PascalCase is what C# calls: `Start()`, `Value()`, `Add()`, `Load()`. plang never calls a verb through a path (what runs, runs in a module; `%…%` only reads), so a verb is C#'s alone and sits with C# library methods (`ToString`, `DisposeAsync`). A read inside `%…%` (`%name.replace("-", " ")%`) works either way, since plang's navigation ignores case.

**What runs, runs in a module.** A step maps only to module actions. `%!app…%` and every `%…%` only read (a method call inside `%…%` must not change anything). An action's C# hands over to the owner in one line: `on.event`'s handler → `Item.on[When][Event].Add(Action, context)`.

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
| 1 | **`Run` → `Start`:** the C# entry verb of every executable object (`goal/this.cs:325`, `step/this.cs:111`, `step/list/this.cs:28`, `action/this.cs:165`, `action/list/this.cs:31`, the event bindings `binding/this.cs:44`, `binding/list/this.cs:22,28`), every handler's `Run()` (126 files) and the generator's emit. Starting one action is one verb end to end: today it's `action.Run` → `call.ExecuteAsync` (`callstack/call/this.cs:225`) → `handler.Execute()` (`ICodeGenerated.cs:37`) → the handler's `Run()`; `Execute`/`ExecuteAsync` go with `Run`. Virtual where an owner may override. No behaviour change. plang action names (`environment.run`, `test.run`, …) are plang vocabulary and not part of this. **Also** (fresh-eyes review): `list.range` already has a plang property `Start` (`list/range.cs:8`), which clashes with a `Start()` method, so it's renamed (builder-visible); `action.Return` finds the return type with `GetMethod("Run")` (`action/this.Schema.cs:53`), and the string changes with the rename; `App.Run<TAction>` (`app/this.cs:431,454`) and `RunGoalAsync` are in scope; `test.@this.Start()` already means "start the stopwatch" (`test/this.cs:80`) and gets another name; about 157 test files call `Run` | yes: range's property rename; twins |
| 2 | **Move type:** `type/list/this.cs` → `type/this.cs` (the system); `type/this.cs` → `type/type/this.cs` (one type). References by full name: `app.type.@this` 257. `App.Type` → `app.type` (lowercase). No behaviour change. **C# note:** a lowercase property hides a same-named namespace (a class owning `type` can no longer write `type.item.text.@this`, CS1061; checked by compiling), so references to those namespaces are written `global::app.type…` in every class that owns such a property. Also count the `app.type.list.@this` references (34 in 18 files) and the `AppTypes` alias | no |
| 3 | **One set of types:** the maps become one set, each type owning its name, aliases, C# class and facts. `Add` is the one way in; `Load` is the startup scan; `Get`/`Clr` become the one door. `Add` carries the static `Loader`'s checks that `code.load` relies on (`code/load.cs:40`): sealed names (a loaded DLL can't replace identity, signature, callback or channel, `Loader.cs:55-59`), reserved names (`type`, `error`, `success`, `@schema`), and renderer registration with its coverage check (`:144-175`). `_catalogByName` is built from `BuildTypeEntries(null)` (`type/list/this.cs:118-121`), the only source of Description, Example, Values, Property and Shape that prompt C reads, so the facts move onto each type here | yes: prompt twins byte-equal, or one eval run |
| 4 | **The type system is an item:** its stored context goes (`internal Context`, `list/this.cs:30`); `Output` writes its face; it answers its own navigation. **It is not a plang value type** (nothing holds a system as a value; you navigate to it): the item says so itself with a yes/no fact, and the registry's startup scan skips it. Without that, the system (`app.type`, namespace `app.type`) and one type (`app.type.type`) both claim the name `type` and startup throws (`Registry.cs:155-160`, named by folder at `:260-268`); the same for the variable system vs the reference in stage 7. Stage 5's internal items (`wire`, `source`, `clr`, `computed`) use the same fact | no |
| 5 | **Faces and honest facts** (details in "Faces" below): the type faces (system, type, choice `values`, kind); each type's real description and example; internal item classes stay out; prompt C's Types section renders from these facts (`properties.template:82` already reads type facts); `type/list/view` (already `[Obsolete]`) and `BuildTypeEntries` (`:425`) die. Also: the 356 `.goal` files get their description above the goal's name; `goal.Comment` is the one description member; the hash covers comments; `start.md` docs (`os/system/modules/ui/Builder/SetLayout.goal:3` reads `…/readme.md`, and `setlayout.pr` holds that path: both change) | yes: twins byte-equal, or one eval run |
| 6 | **The reference** (details below): `app.variable.@this` → `app.type.item.variable` (about 141 references: 45 production, 98 tests). A variable is `text` + `code`; `Value()` → `Start(context)` → `Code.Start(context)`. The parser (`app/type/item/variable/parser/`) is the one definition; each hop kind parses its own piece. Build validation writes each marked row's `"variable"` list into the .pr, and loading never parses again. `item.Variable` (a read-only list of variables, null when none); `HasVariable => Variable?.Count > 0` on the item, and `data.HasVariable => _item?.HasVariable ?? false`; `IsVariable` is one variable covering the whole value. **Every installed .pr with marked rows** (the builder's 6 marked files, `test.pr`, `show.pr`, and 8 of the 10 tracked `Tests/**/.build/*.pr`) is rewritten with its `"variable"` lists by a throwaway C# pass (the parser over each marked value, written through `plang.Text`; no LLM), since the loader refuses a marked row without its list. **Consumers to move** (fresh-eyes review): the store's Get/Set by name (`variable/list/this.cs:128,261,279,317,339,403`), `type/kind/this.cs:66-81`, `type/clr/this.cs:94-101`, `text/this.cs:153`, `data.Get(string)` (`data/this.Navigation.cs:17`), `data.Set(path, …)` (`:104`). **The source generator, its own step:** it emits `HasVariableReference` and `global::app.variable.@this` as strings (`Emission/Property/Data/this.cs:192`) and finds `IName` by the namespace string `"app.variable"` (`Discovery/this.cs:189-190`); if that moves unnoticed, the missing-parameter guard disappears silently | yes: `"variable"` in the .pr; twins + one eval run |
| 7 | **Every system in the same shape:** goal (93 references), actor (15), module (11), test (87), variable (the system at `app/variable/this.cs`, freed by stage 6). Each: `X/this.cs` system + `X/X/this.cs` element, `.list` (with `all`), `["name"]`, `.name`, `.current` where it means something, a face; its property on `app` goes lowercase (`App.Goal` → `app.goal`, …). The builder reads `%!app.module.list%` (`Decide.goal:14`) and its templates walk `m.Action` / `m.Modifier` (`module/this.cs:67-71`), so the module system's shape is checked against them. Test mode today is "`App.Test` is set" (`app/this.cs:198`); a test system that always exists needs test mode as its own fact | yes: the builder's own input; twins |
| 8 | **`on` and `current` on every object** (details below): events move from `event.on(Trigger=…)` to the object, as `on.before.<verb>` / `on.after.<verb>` for every public verb, plus outcomes (`on.error`, `on.hit`, `on.miss`). The `on` module's actions are one-line doors: `on.event(item, when, event, action)` for any event, and `on.error`, `on.cache`, `on.timeout`, which replace today's modifiers as events bound on the action before them (Ingi). Bindings are scoped to the actor that registered them unless `scope: app`. Every plang value is born through its type's `Create`, which fires `create`. `current` is each object's own answer. Payoff: value-level mocking (`- after file create, call LoadFixture`) | yes: the `on` actions; twins + one eval run |
| 9 | **Module pass, with file.read as the template (Ingi):** fix file.read first and make it the worked example of a correct action, written up as a doc ("how an action is written"). Then go over every module against it, one module per commit. The checklist: (1) plang values are born through their type (`app.type.file.Create(…)`; `new` only inside the type); (2) `Start()` hands over to the owner in one line; (3) no opened box, no broken seal (no `.Value()` on what it returns or forwards); (4) errors are results; (5) properties are typed (`Data<T>`), and a property that names where to write is a variable; (6) events fire from the owner, not the handler | per module: twins where a prompt changes |
| 10 | **`%!app` holds its systems:** built-ins register at startup, a plugin loaded with `code.load` registers its own (`%!app.stripe%`); `%!app.list%` lists them; one name, one system (a clash fails loudly); `%setting.X%` stays as a short form | no |
| 11 | **Tests through the app's own doors:** `new app.@this(test: true)`, `app.variable.Set("some", "var")`, `await app.module["file"]["read"].Start(new { Path = "…" })` (a start of that action with those property values, through `action.Start`). The static helpers `TestApp`/`TestAction` die: about 1,645 uses in 404 files, so this is a large stage; `TestApp` also installs the no-crypto signing mock (`TestApp.cs:37-47`), which test mode must keep. `app.module["file"]["read"]` is the shared catalog action (`module/this.cs:80`), so `Start(new {…})` works on a copy. `App.Run<TAction>` retires here (two doors otherwise). The builder warns about goals no public goal reaches (dead code); `app.test.coverage` shows what the tests reached | a build warning |
| 12 | **Exception pass, before the branch closes:** go over every `throw` in the code this branch touched. A problem the programmer caused is an error in the result; an exception only ever means plang itself is broken. Most should already be gone by then (the name lookups become `["name"]` doors answering NotFound; `Push` answers the overflow; the .pr readers return their error) | no |

Each stage is its own commits, green against the baseline before the next starts.

## The reference (stage 6), settled with Ingi 2026-09-26

- **The parser** takes any text holding `%…%` and returns its variables. It is the one definition of a reference: `%` + a path starting with a letter, `_` or `!`, closing at the first `%` outside quotes and parentheses. It finds each `%…%`, and each hop kind parses its own piece. Callers: build validation (writing the .pr's `"variable"` lists), a template born at run (`read …, resolve variables`), `item.Variable`, the build checks (coverage, the types walk, pick's write-to).
- **A variable is `text` + `code`,** like a step. `code` is the parsed execution path: a list of hops in order, each getting the previous value and doing its one step, the way actions pass `%!data%`. No `next`, no segment classes, no walker switch. A variable is a value, so its door stays `Value()`, and like everything with code it has `Start` (Ingi): `Value()` → `Start(context)` → `Code.Start(context)`.
- **The hop kinds**, each a small class that parses, writes and runs its own piece; the JSON key is the kind:
  - `variable`: the root, read from the memory (`user`, `!app`);
  - `property`: a member (`.address`). A name starting with `!` is born a `!` hop, and at run it looks in today's order (`data/this.Navigation.cs:240-287`): the Properties bag, then Data's own members (`!type`, `!error`, `!success`), then the value's members (`!path`, `!size`), then the value's methods that take a context (`!relative`). Whether `cost` is in Properties is only known at run, so that order is the hop's own lookup;
  - `index`: `[…]`. Its key is a typed value, keyed by its type (`{"number": "%i%"}`, `{"text": "k"}`), and a variable key carries its own code;
  - `method`: a call (`.replace("-", " ")`), whose values are its `parameter` list, parsed at build. **The value owns its methods (Ingi):** today's string switch on Data over 7 names with regex argument parsing (`data/this.Navigation.cs:143-233`: grep, grepcount, maxlength, trim, tolower, toupper, replace) becomes text's own methods (a datetime owns its own, and so on); a method the value doesn't have is an error ("text has no method 'foo'").
- **The same variable reads and writes (Ingi: it's a variable, not a "target").** `Value()` runs every hop. `variable.Set(value, context)` runs every hop but the last to reach the parent, and the last hop writes itself: a property hop sets that member, an index hop that key, a `!` hop the Properties bag, a bare root rebinds the variable in memory; a method hop can't be written (an error). This is today's `data.Set(path, …)` (`data/this.Navigation.cs:104-137`: read walk to the parent, one `Set` at the leaf) moved onto the variable. A property that names where to write (`variable.set`'s `Name`, `Data<Variable>` slots) holds the same variable class.
- **In the .pr:**

```json
{"name": "Data", "type": {"name": "text", "template": "plang"},
 "value": "hi %name.replace(\"-\", \" \")% from %user.address[%i%].city% (%order!cost%)",
 "variable": [
   {"text": "%name.replace(\"-\", \" \")%",
    "code": [{"variable": "name"}, {"method": "replace", "parameter": [{"type": {"name": "text"}, "value": "-"}, {"type": {"name": "text"}, "value": " "}]}]},
   {"text": "%user.address[%i%].city%",
    "code": [{"variable": "user"}, {"property": "address"},
             {"index": {"number": "%i%", "variable": [{"text": "%i%", "code": [{"variable": "i"}]}]}},
             {"property": "city"}]},
   {"text": "%order!cost%", "code": [{"variable": "order"}, {"property": "!cost"}]}]}
```

## Events on each object (stage 8), settled with Ingi 2026-09-26

Events are on everything, and they cost nothing unless bound: an object's `on` is null until something binds, and the call is `on?.before.create.Start(this, context)`, returning at once.

**A value is born through its type, so `create` fires every time.** Today plang values are born with `new` in at least 78 production places (`file/read.cs:72`: `new global::app.type.item.file.@this(path, Context, template)`), none passing the type, so `after file create` would miss them; a constructor can't fire it (it can't await, and an item stores no context). Stage 8 makes the type's `Create` door (`type/this.cs:206`, with overloads at `:289`, `:330`) fire `on.before/after.create`; stage 9 moves every birth onto it (`new` of a plang value only inside its type). In C# that's `app.type["file"].Create(raw, context)`, which answers an untyped item, so each moved site casts.

**Modifiers become events (Ingi).** This reverses the earlier ruling today (the `[Modifier(Order)]` 0/50/100 nesting, `modifier.list` staying, `on.error` staying a modifier). Error handling, caching and timeouts are bindings on the action's own `on`, written as `on` actions right after the action they bind on (the postfix attachment rule stays):

```
- read file.txt, before calling it call PreRead, cache for 10 min, timeout 30 sec, on error 404 call Fix then retry once, write to %content%

file.read(Path="file.txt");
on.event(when: "before", event: "start", goal.call: PreRead);
on.cache(Duration="10 min");
on.timeout(After="30 sec");
on.error(StatusCode=404, RetryCount=1, Order="GoalFirst", goal.call: Fix);
variable.set(Name=%content%, Value=%!data%)
```

| action | bound on the action | today |
|---|---|---|
| `on.error` | the error outcome. Filters (StatusCode, Key, Message), `RetryCount`/`RetryOverMs` (re-starts the action, as today), `Order`, `IgnoreError` are the binding's settings; the recovery goal is the handler. Several clauses are the error event's bindings in written order, and the first that matches handles it (today's grouped try/catch) | the `on.error` modifier (`module/action/on/error.cs`) |
| `on.cache` | `before start`: a hit cancels with the cached value; `after start`: stores the result. The C# hands over to the cache system (`app.cache`, `module/action/cache/ICache.cs`, `Memory.cs`) | `cache.wrap` |
| `on.timeout` | `before start`: sets a deadline on the run; `after start`: clears it. A timeout is a failure, so `on.error` catches it; "error outermost" falls out with no order numbers. Reacting to a timeout is `on.error(Key="Timeout", …)` | `timeout.after` |
| `on.event` | any other event, including a plain before/after hook on the action (`before calling it call PreRead`) | `event.on` |

- An `on` action with no `item` binds on the action written right before it; the binding is part of the program and fires every time that action starts (unlike a runtime `on.event(item: …)`, which is scoped to its actor).
- Dies: `modifier.list`, `Wrap` and the catch grouping, `[Modifier(Order)]`, `IModifier`, `ModifierAttribute`, `cache.wrap`, `timeout.after` (the `timeout` module goes; `cache` keeps its provider as the cache system).
- Prompt C's modifier teaching (rule 8, the examples) changes: one eval run.

**Mocking is an event (Ingi).** The `mock` module stays so the builder can see it, and its action points to `app.test.mock` in one line. `mock.intercept`'s wiring (`Trigger.BeforeAction`, `Context.Events.Register`, `context.EventOverride`, `mock/intercept.cs:59-77`) goes.

```
- if %admin% call SetupMocks

SetupMocks
- mock http url=http://example.com, call MockExample
    → the mock action → app.test.mock: binds on app.module["http"].on.before.start,
      with a filter (url equals http://example.com), scoped to this actor, born cancelling

MockExample
- return file.read(example.json)
```

When any http action starts, the binding checks its filter; on a match MockExample runs, its return becomes the action's result, and the real `Start()` is cancelled. The mechanism exists today (`action/this.cs:193-201`: a handled before-result replaces the dispatch). `action: "request"` binds on that one action instead of the module.

**Cancel (Ingi).** Any before-handler can cancel what it's before with a step, `- cancel %!event%`, and its return value becomes the result. A mock binding is born cancelling, so a mock goal only returns.

**`test.run`'s coverage and output capture** (`test/run.cs:126,135`, `Trigger.BeforeWrite` today) become ordinary bindings: after-start on actions, before-write on the output channel.

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

Six of the 21 `Trigger` values never fire today (BeforeAppStart, AfterAppStart, OnError, OnVariableChange, OnCacheHit, OnCacheMiss): those are new features, not a port.

- **`current`** during a handler is the item passed in (`this`): `%!app.type.text.current%` is the text being created.
- **An object's own `on` vs its type's:** `%!app.variable.user%`'s bindings are that one variable's; `app.type.text.on…` binds every text.

```
- before goal show start, call LogStart   → on.event(item: %!app.goal.show%, when: before, event: start, goal.call: LogStart)
- after text create, call LoadText        → on.event(item: %!app.type.text%, when: after,  event: create, goal.call: LoadText)
- after %user% set, call UserChanged      → on.event(item: %!app.variable.user%, when: after, event: set, goal.call: UserChanged)
```

The item is always the object itself, reached through its system: `%user%` would read the variable's VALUE (and put the event on it), so one variable is `%!app.variable.user%`.

**One action, `on.event` (Ingi).** Its properties: `item` (the item the event is put on, read through a path; `%…%` only reads, the binding happens in the module), `when` (`before` | `after`, or none for an outcome), `event` (the verb or outcome: `create`, `start`, `set`, `error`, `hit`, …), and the action to run (a `goal.call`, or any action). Its C# is one line, the same for every item: `Item.on[When][Event].Add(Action, context)`. The event class's verbs are `Add` (bind) and `Start` (fire). Names are the architect's sketch; coder owns the final ones.

**Scope.** A binding fires only inside the actor that registered it, as `event.on` behaves today (a test's fixture only affects the test); `scope: app` makes it global (a system logger).

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
| `%!app.goal.start%` | `name`, `path`, `comment`, its steps (index and text), `child` (its sub-goals), `on` |
| `%!app.actor%` | `list` (system, user), `current` |
| `%!app.actor.user%` | `name` |
| `%!app.module%` | `list` (module names) |
| `%!app.module.file%` | `name`, `description`, its action names |
| `%!app.test%` | `list` (test names), `current` |
| `%!app.test.x%` | `name`, `status` |
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
| `type/list/view/` (whole folder) and `BuildTypeEntries`; `goal.Description` (`goal/this.cs:41-42`, its write in `build/code/Default.cs:206-214`, its read in `goal/serializer/Reader.cs:54`); the goal hash that ignores comments; the four `os/` `readme.md` (→ `start.md`) | 5 |
| `app/variable/path/` (`Parse`, `Segment` and its kinds, `Segment.Index.Key`'s string re-parse, `Segment.Call.Args`); the walker's switch and clr special case (`data/this.Navigation.cs:33-94`); `data.TryFullVarMatch`; static `text.HasVariable(string)`; `CleanName` ×2 (`data/this.cs:647`, `variable/list/this.cs:552`); the other parsers (`Formal.cs:311,418`, `debug/this.cs:506`, `pick/list/this.cs:94,132`); `InvokeMethod`'s string switch (`data/this.Navigation.cs:143-233`); `variable.@this.Convert`'s hand scan; the regexes in `step/this.Validate.cs:26`, `step/this.Scope.cs:9`, `pick/list/this.cs:90,96`; `data.HasVariableReference` | 6 |
| the per-concept `X.list.@this` as the class reached at `app.X` (goal, actor, module, test) | 7 |
| `event.on`, `Trigger` as a list of moments beside the objects; the per-context binding lists (`context.LifecycleFor(…)`, called at `goal/this.cs:333`, defined at `actor/context/this.cs:425,448,472`; `app/event/lifecycle/`): bindings live on the item, each carrying its scope; `mock.intercept`'s wiring (replaced by `mock` → `app.test.mock`); the modifier concept: `modifier.list`, `Wrap`, the catch grouping, `[Modifier(Order)]`, `IModifier`, `ModifierAttribute`, `cache.wrap`, `timeout.after` and the `timeout` module (replaced by `on.error`, `on.cache`, `on.timeout` as events) | 8 |
| every direct `new` of a plang value outside its type (≥ 78 production sites); whatever else the checklist catches, per module | 9 |
| reflection over the C# `App` as `%!app`'s answer | 10 |
| `PLang.Tests/Shared/TestApp.cs`, `TestAction.cs` (statics) | 11 |

**Stays:** the `mock` module (its action now points to `app.test.mock`); `on.error` as the name of the error event's action; the postfix attachment rule (an `on` action binds on the action before it); the builder pipeline as built on builder-formal.

## OBP validation

| New or moved surface | plang path | C# | file | Check |
|---|---|---|---|---|
| type system | `%!app.type%` | `app.type` | `app/type/this.cs` | an item; no stored context; one way in (`Add`) |
| one type | `%!app.type["text"]%` / `.text` | `app.type["text"]` | `app/type/type/this.cs` | owns its name, aliases, facts, `on`, `current` |
| a variable | `%user.name%` | `app.type.item.variable.@this` | `app/type/item/variable/this.cs` | `text` + `code`; `Value()` → `Start()` → `Code.Start()` |
| the parser | — | `app.type.item.variable.parser.@this` | `app/type/item/variable/parser/this.cs` | the only definition of a reference |
| an item's variables | — (a value's own) | `item.Variable` | `app/type/item/this.cs` | read-only, born whole, null when none |
| variable system | `%!app.variable%` | `app.variable` | `app/variable/this.cs` | the memory of the actor in play |
| registering an event | `- after text create, call LoadText` | `on.event(item, when, event, action)` → `Item.on[When][Event].Add(…)` | `app/module/action/on/event.cs` | one action for every item; scoped to the registering actor |
| an object's events | `%!app.type.text.on%` (read) | `x.on` | `app/event/before/<verb>/this.cs`, `app/event/after/<verb>/this.cs` | registered by a step through the `on` module; null until bound |
| a value's birth | — | `app.type["file"].Create(raw, context)` | `app/type/type/this.cs` (`Create`) |
| a mock | `- mock http url=…, call MockExample` | the `mock` action → `app.test.mock` | `app/module/action/mock/…` → `app/test/…` | a before-start binding with a filter, born cancelling, scoped to the actor | the one door; fires `on.before/after.create`; `new` only inside the type |
| how an action is written | — | — | a doc, file.read as its worked example | the module pass's template |
| starting an action from C# | — | `app.module["file"]["read"].Start(…)` | `module/module/…` | through `action.Start`; nothing test-only |
| every goal | `%!app.goal.list.all%` | `app.goal.list.all(show, os)` | `app/goal/list/this.cs` | a method with optional parameters; `.all` = the defaults |
| a goal's description | `%!app.goal.show.comment%` | `goal.Comment` | `app/goal/goal/this.cs` | the lines above the goal's name; the only description member |
| names | — | — | — | no verb+noun; verbs `Start`, `Add`, `Load`, `Create`, plumbing `Variable`, `Code`, nodes `on`, `current`, `list`, `all`: one word each; nodes lowercase, verbs PascalCase |

## Open for the next round

Rounds 1–4 closed. Round 4 added: one `on.event(item, when, event, action)` for every event, bindings scoped to the registering actor, and binding/firing as verbs (`Add`, `Start`). Next: round 5, a full pass.
