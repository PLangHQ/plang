# app-systems — every `app.X` is the type X

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

**Every `app.X` is the type X (Ingi, round 5).** This reverses two earlier rulings: the separate system class (`X/this.cs` the system, `X/X/this.cs` one X, from the parked draft) and stage 4's "a system is not a plang value type". There are no system classes and no doubled files. The type named `goal` is what knows the goals, the same way the type named `text` knows text's kinds.
- **`app.X` is a `type<X>` (Ingi: "`%!app.goal%` is `app/goal/this.cs`, no type there").** One generic class, `type.@this<T>` (`type/this.Generic.cs`), serves every concept; there is no class per concept and no `X/type/` folder. `app.goal` is `new type.@this<goal.@this>("goal", new goal.list.@this(app))`: the type named `goal`, defined by `goal/this.cs`. `%!app.goal%` and `%!app.type.goal%` are the same object.
- **One X stays where it is:** `goal/this.cs`, `type/this.cs` (one type), `module/this.cs`.
- **The concept's own work lives in its list:** `X/list/this.cs` becomes a `list<X>`, as `step.list` (`goal/step/list/this.cs:14`) and `action.list` (`goal/step/action/list/this.cs:16`) already are. Goal's list keeps its store and its loading of `.pr` files; the registry, `type/list/this.cs`, becomes the `list<type>` at `app.type.list` and keeps the lookups by other keys (`Mime`, `Extension`, `[System.Type]`: `app.type.list.Mime(…)`). Today goal's, module's and actor's list classes don't inherit the plang list (`goal/list/this.cs:13`, `module/list/this.cs:14`, `actor/list/this.cs:8`).
- **No name clash in the registry:** a `list<T>` subclass is a kind of list and claims no name (`Registry.cs:246-254`), and the open generic `type.@this<T>` (reflection name "this\`1") isn't taken for an `@this` class (`:237-238`), so it claims nothing, like `list.@this<T>`.
- **A value type (text, number) is a plain `type.@this`** built from the catalog; it has no `list`.

**Members of a collected type (`type.@this<X>`):**
- `list`: a `list<X>`, the X's loaded so far; `list.all` is every one (below).
- `current(context)`: the one in play, an X (`%!app.goal.current%` is a `goal/this.cs` instance, Ingi). The element's class answers it through a `static virtual` member, the same pattern as ICreate's `Create` (`type/item/ICreate.cs`): goal `context.Goal` (`actor/context/this.cs:100`), actor `context.Actor` (`:89`); a concept nothing is inside answers none (404). A method taking only the context is navigated like a property (`type/list/this.cs:527-528`), so plang writes `%!app.goal.current%`.
- `["key"]`: `list.all.First(p => p.Match(key))`, answering `data<X>`. No match is a 404 NotFound result, not an exception (today both lookups throw: `goal/list/this.cs:244`, `type/list/this.cs:171`).
- **The element says which key is its own (`IMatch.Match(key)`, one line each):** a goal its `Address` (`goal/this.cs:214`: its .goal path without the extension, `/system/builder/EmitBuildEvent`, the name that reaches it from anywhere); a type its name or one of its aliases (`"string"` → text; each type owns its aliases, stage 3); a module or an actor its name. A precision (`int`) is a kind of number, not an alias: `["number"].kind["int"]`. The spelled forms `"text/md"` and `"list<path>"` become `["text"].kind["md"]` and `["list"].kind["path"]`. (`type.Is(string)`, `type/this.cs:438`, isn't reused: it answers true for `item` on every type.)
- `.key`: shorthand for `["key"]` where the key is a plain word (`%!app.type.text%`, `%!app.module.file%`). The type's own members win: `%!app.type.list%` is the list, and the type named `list` is `["list"]`. A goal's key is a path, so a goal is always `%!app.goal["/show"]%`.
- the facts, `on` and `kind`, as on every type.
- **A bare goal name is `call`'s lookup, not the indexer's (Ingi; reverses round 3's "a bare name resolves the way call does").** `call Start` searches from the caller: the caller, its children, each ancestor and theirs, then the caller's folder and up (`goal/list/this.cs:131-169`). `goal["x"]` takes an address only, and searches `list.all`, so a goal that isn't loaded yet still answers.

```csharp
// sketch: type/this.Generic.cs — NEW; the same pattern as item/list/this.Generic.cs
namespace app.type;
public sealed class @this<T> : @this
    where T : item.@this, item.ICreate<T>, item.IMatch, item.ICurrent<T>
{
    public @this(string name, item.list.@this<T> list) : base(name) => this.list = list;   // base ctor: type/this.cs:105
    public item.list.@this<T> list { get; }
    public data.@this<T> this[string key] => list.all.First(p => p.Match(key));  // p is a T: plain C#
    public data.@this<T> current(actor.context.@this context)
        => T.Current(context) is { } one ? data.@this<T>.Ok(one)
           : data.@this<T>.FromError(new Error($"no {Name} is current", "NotFound", 404));
}

// sketch: app/this.cs — the properties' types change; today goal.list.@this (:146), type.list.@this (:210), module.list.@this (:135)
public type.@this<goal.@this>   goal   { get; }   // new("goal", new goal.list.@this(this))
public type.@this<type.@this>   type   { get; }   // new("type", the registry: type/list/this.cs)
public type.@this<module.@this> module { get; }   // new("module", new module.list.@this(this))

// sketch: type/item/IMatch.cs — NEW: what a collected element answers to
public interface IMatch { bool Match(string key); }

// sketch: type/item/ICurrent.cs — NEW: which one is in play; the ICreate pattern
public interface ICurrent<TSelf> where TSelf : @this, ICurrent<TSelf>
{
    static virtual TSelf? Current(actor.context.@this context) => null;          // nothing is ever inside it
}
// goal/this.cs — NEW
public static goal.@this? Current(actor.context.@this context) => context.Goal;
// actor/this.cs — NEW
public static actor.@this? Current(actor.context.@this context) => context.Actor;

// sketch: goal/this.cs — NEW
public bool Match(string key) => string.Equals(Address, key, System.StringComparison.OrdinalIgnoreCase);
// type/this.cs — NEW (Alias: stage 3)
public bool Match(string key) => string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase) || Alias.Contains(key);
// module/this.cs, actor/this.cs — NEW
public bool Match(string key) => string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase);

// sketch: item/list/this.Generic.cs — NEW member: the first element that matches; 404 when none
public data.@this<T> First(System.Func<T, bool> match)
{
    foreach (var item in Items())                                                // :26, typed
        if (match(item)) return data.@this<T>.Ok(item);                          // data/this.cs:713
    return data.@this<T>.FromError(new Error($"no {typeof(T).Name} matches", "NotFound", 404));   // :714
}
```

**`list<T>` is strict (Ingi: "stricter is always better").** `list<T>` takes `T : item, ICreate<T>`, as `data<T>` does (`data/this.cs:698`); today it takes `T : item` (`item/list/this.Generic.cs:15`). Gaining ICreate: test (`test/this.cs:16`), test timing (`test/timing/this.cs:12`), `LlmMessage` (`llm/LlmMessage.cs:12`), type (`type/this.cs:32`, also unsealed), and module, which isn't an item yet (`module/this.cs:12`). Goal, step, action, actor, path, text, tag and identity already are.

**The LLM learns the element type once (architect's choice; Ingi left it open).** A type's facts list its `[LlmBuilder]` properties through `PlangName` (`type/list/this.cs:537`), which reads `list.@this<T>` as `{list, kind: T}` (`:288`), so `list: list<goal>` shows up with no extra work. The prompt teaches the rule once: `%!app.X["key"]%` is one element of `%!app.X.list%`. The element type is stated in one place, the list's kind.

- **The type is an item.** It writes its face through `Output` (facts, any writer), a formatter (a template) presents them, and it answers its own navigation (`Get(parent, key)`: a member first, else `this[key]`, else NotFound). Like every item, it stores no context: the caller passes it.
- **Where `current` gets its context (Ingi):** `%!app%` is a variable in the actor's own memory, registered with that context (`actor/context/this.cs:165`), and navigating from it carries that context down every hop. So `%!app.goal.current%` is answered by the goal type's navigation from the asker's context. `current` stays on every type where it means something (goal, actor, test), for symmetry; C# code that holds the context gets the same answer as `context.Goal`.

**`list` is a real object (Ingi).** Each collected type's `list` is its concept's `X/list/this.cs`, a `list<X>`: it prints, enumerates, counts and indexes like any list, and carries its own members (goal's store and loading; the registry's lookups).
- `%!app.goal.list%` is the goals loaded so far (goals load when they're called).
- **`%!app.goal.list.all%` is every goal in the app,** built from a listing of `.build/` (the `.pr` files, not read); each goal loads when it's first touched. The dead-goal warning (stage 11) and `goal["address"]` use it.
- **Every list has `all`, for consistency.** Where everything is already present (the types are registered at startup), `all` answers the list itself, so nobody needs to know which types load lazily.
- **`all` takes settings (Ingi):** a method with named, optional parameters; `.all` without parentheses runs it with the defaults (one door). For goals: `show` = `public` (default) | `private` | `all`, and `os` = whether os/system goals are included (default false: the app's own goals). `%!app.goal.list.all(show: "all", os: true)%`. The default lists each `.pr`'s main goal without reading the files; `private`/`all` read them for their sub-goals.
- A list is navigated by index, so `list.all` never clashes with an element's key. plang paths are written in lowercase (`%!app.goal["/show"].name%`); navigation ignores case.

```csharp
// sketch: app/goal/list/this.cs
public sealed class @this : app.type.item.list.@this<goal.@this>
{
    public list.@this<goal.@this> all { get; }     // every goal in the app, each loads when touched
}
```

**One element.** It owns its facts, its `on` (events about it) and its `current` (the instance in play).

**`Start` is the entry point of everything that runs (Ingi).** As `Start.goal` is plang's entry: `app.Start()`, `goal.Start(context)`, `step.Start`, `action.Start`, each handler's `Start()`, a list's `Start`, a code's `Start`. It's virtual, so an owner can change what starting it means. **A value keeps `Value()`** (Ingi); anything that has code also has `Start`, for consistency. A variable is both: `variable.Value()` → `variable.Start(context)` → `Code.Start(context)`.

**plang vocabulary is lowercase in C# too (Ingi).** The structure plang navigates is lowercase: `app.type`, `app.goal`, `app.variable`, `app.module`, `app.actor`, `app.test`, their `list`, `current`, `all`, `on`, `before`, `after` and the event classes. Facts keep their C# names (`Name`, `Path`, `Comment`): plang writes its paths in lowercase (`%!app.goal["/show"].name%`) and its navigation ignores case, and the face writes facts lowercase already. C# plumbing plang never navigates stays PascalCase, including an item's `Variable` list and a variable's `Code` (`%order.variable%` must reach the order's own key, not the item's metadata); a C# keyword keeps its `@` (`app.@event`); an item's own `Type` (its type entity) stays. So the three paths match letter for letter: `%!app.type["text"]%` ↔ `app.type["text"]` ↔ `app/type/this.cs`. Each type's property on `app` is renamed in the stage that moves it (type: stage 2; the others: stage 7). This replaces CLAUDE.md's "Property names on `app.@this` stay PascalCase" (proposal filed).

**Nodes lowercase, verbs PascalCase (Ingi: "the line where C# executes, and in C# starts uppercase").** Lowercase is what plang navigates (`app.type`, `list`, `current`, `on.before.create`, the facts). PascalCase is what C# calls: `Start()`, `Value()`, `Add()`, `Load()`. plang never calls a verb through a path (what runs, runs in a module; `%…%` only reads), so a verb is C#'s alone and sits with C# library methods (`ToString`, `DisposeAsync`). A read inside `%…%` (`%name.replace("-", " ")%`) works either way, since plang's navigation ignores case.

**What runs, runs in a module.** A step maps only to module actions. `%!app…%` and every `%…%` only read (a method call inside `%…%` must not change anything). An action's C# hands over to the owner in one line: `on.event`'s handler → `Item.on[When][Event].Add(Action, context)`.

| plang | C# | file |
|---|---|---|
| `%!app.type%` | `app.type` | `app/type/this.cs`, the type named `type` (a `type<type>`) |
| `%!app.type.list%` | `app.type.list` | `app/type/list/this.cs`, the registry as a `list<type>` |
| `%!app.type["text"]%`, `%!app.type.text%` | `app.type["text"]` | `app/type/this.cs`, one type |
| `%!app.goal%`, `%!app.type.goal%` | `app.goal` | `app/goal/this.cs`, the type named `goal` (a `type<goal>`) |
| `%!app.goal.list%` | `app.goal.list` | `app/goal/list/this.cs`, a `list<goal>` |
| `%!app.goal["/show"]%` | `app.goal["/show"]` | `app/goal/this.cs`, one goal |
| `%!app.goal.current%` | `app.goal.current(context)` | `app/goal/this.cs`, the running goal |
| `%!app.variable.some%` | `app.variable["some"]` | the type named `variable`; its list is the asker's memory (`app/variable/list/this.cs`); see Open |
| `Start.goal` | `app.Start()`, `goal.Start(context)` | the entry point, one word everywhere |

## Stages

| # | Stage | Changes what the builder sees |
|---|---|---|
| 0 | **Base:** re-record builder-formal's `Compile` (TypeSafe is back) so its BootstrapTests pass; take a baseline of the six suites | — |
| 1 | **`Run` → `Start`:** the C# entry verb of every executable object (`goal/this.cs:325`, `step/this.cs:111`, `step/list/this.cs:28`, `action/this.cs:165`, `action/list/this.cs:31`, the event bindings `binding/this.cs:44`, `binding/list/this.cs:22,28`), every handler's `Run()` (126 files) and the generator's emit. Starting one action is one verb end to end: today it's `action.Run` → `call.ExecuteAsync` (`callstack/call/this.cs:225`) → `handler.Execute()` (`ICodeGenerated.cs:37`) → the handler's `Run()`; `Execute`/`ExecuteAsync` go with `Run`. Virtual where an owner may override. No behaviour change. plang action names (`environment.run`, `test.run`, …) are plang vocabulary and not part of this. **Also** (fresh-eyes review): `list.range` already has a plang property `Start` (`list/range.cs:8`), which clashes with a `Start()` method, so it's renamed (builder-visible); `action.Return` finds the return type with `GetMethod("Run")` (`action/this.Schema.cs:53`), and the string changes with the rename; `App.Run<TAction>` (`app/this.cs:431,454`) and `RunGoalAsync` are in scope; `test.@this.Start()` already means "start the stopwatch" (`test/this.cs:80`) and gets another name; about 157 test files call `Run` | yes: range's property rename; twins |
| 2 | **`app.type` lowercase:** no file moves. `type/this.cs` stays one type and `type/list/this.cs` stays the registry (the list of types from stage 4). `App.Type` → `app.type` (lowercase). No behaviour change. **C# note:** a lowercase property hides a same-named namespace (a class owning `type` can no longer write `type.item.text.@this`, CS1061; checked by compiling), so references to those namespaces are written `global::app.type…` in every class that owns such a property | no |
| 3 | **One set of types:** the maps become one set, each type owning its name, aliases (`Alias`), C# class and facts, and answering `Match(key)`. `Add` is the one way in; `Load` is the startup scan; `Get`/`Clr` become the one door. The name door's spellings go: an alias is the type's own (`Primitive.Aliases`, `type/list/this.cs:79,241`), a precision is number's kind (`Precision`, `:189-195`), and `"text/md"` / `"list<path>"` (`:161-169`) are written `["text"].kind["md"]`. **The registry's `Choice`, `Kind` and `Scheme` stores (`:40,48,56`) move onto the types they belong to (Ingi):** a type's `kind` is an object that knows its type (`%!app.type.text.kind%`), `kind.list` gives md, csv, …, and `kind["md"]` is one kind; choice works the same way (`choice/this.cs`, its `.list` the sets); path's schemes are its kinds. `Add` carries the static `Loader`'s checks that `code.load` relies on (`code/load.cs:40`): sealed names (a loaded DLL can't replace identity, signature, callback or channel, `Loader.cs:55-59`), reserved names (`type`, `error`, `success`, `@schema`), and renderer registration with its coverage check (`:144-175`). `_catalogByName` is built from `BuildTypeEntries(null)` (`type/list/this.cs:118-121`), the only source of Description, Example, Values, Property and Shape that prompt C reads, so the facts move onto each type here | yes: prompt twins byte-equal, or one eval run |
| 4 | **The collected type:** `type.@this<T>` (`type/this.Generic.cs`: `list`, `["key"]`, `current(context)`), `IMatch`, `ICurrent<T>`, `list<T>.First(match)`, and the strict `list<T>` (`ICreate<T>`; test, test timing, `LlmMessage` and type gain ICreate; type is unsealed). `app.type` becomes a `type<type>` over the registry, and the registry (`type/list/this.cs`) becomes a `list<type>`: its stored context goes (`internal Context`, `:30`); it keeps the lookups by other keys: `Mime`, `Extension`, `this[System.Type]`, `Reader` (`:203,211,261,75`). The type writes its face through `Output` and answers navigation (a member first, else `this[key]`). A lookup miss is a 404 result. The type's instance is added to the list under its name (`type`, `goal`, …), one entry with the catalog's facts for that name (stage 3's `Add`). Stage 5's internal items (`wire`, `source`, `clr`, `computed`) say by their own fact that they're not plang types. The prompt teaches `%!app.X["key"]%` once | yes: one rule in the prompt; twins |
| 5 | **Faces and honest facts** (details in "Faces" below): the type faces (system, type, choice `values`, kind); each type's real description and example; internal item classes stay out; prompt C's Types section renders from these facts (`properties.template:82` already reads type facts); `type/list/view` (already `[Obsolete]`) and `BuildTypeEntries` (`:425`) die. Also: the 356 `.goal` files get their description above the goal's name; `goal.Comment` is the one description member; the hash covers comments; `start.md` docs (`os/system/modules/ui/Builder/SetLayout.goal:3` reads `…/readme.md`, and `setlayout.pr` holds that path: both change) | yes: twins byte-equal, or one eval run |
| 6 | **The reference** (details below): `app.variable.@this` → `app.type.item.variable` (about 141 references: 45 production, 98 tests). A variable is `text` + `code`; `Value()` → `Start(context)` → `Code.Start(context)`. The parser (`app/type/item/variable/parser/`) is the one definition; each hop kind parses its own piece. Build validation writes each marked row's `"variable"` list into the .pr, and loading never parses again. `item.Variable` (a read-only list of variables; one shared empty list when none, never null: the OBP rule "No null checks"); `HasVariable => Variable.Count > 0` on the item, and `data.HasVariable => _item?.HasVariable ?? false`; `IsVariable` is one variable covering the whole value. **Every installed .pr with marked rows** (the builder's 6 marked files, `test.pr`, `show.pr`, and 8 of the 10 tracked `Tests/**/.build/*.pr`) is rewritten with its `"variable"` lists by a throwaway C# pass (the parser over each marked value, written through `plang.Text`; no LLM), since the loader refuses a marked row without its list. **Consumers to move** (fresh-eyes review): the store's Get/Set by name (`variable/list/this.cs:128,261,279,317,339,403`), `type/kind/this.cs:66-81`, `type/clr/this.cs:94-101`, `text/this.cs:153`, `data.Get(string)` (`data/this.Navigation.cs:17`), `data.Set(path, …)` (`:104`). **The source generator, its own step:** it emits `HasVariableReference` and `global::app.variable.@this` as strings (`Emission/Property/Data/this.cs:192`) and finds `IName` by the namespace string `"app.variable"` (`Discovery/this.cs:189-190`); if that moves unnoticed, the missing-parameter guard disappears silently | yes: `"variable"` in the .pr; twins + one eval run |
| 7 | **Every concept is its type:** goal, actor, module, test, variable. Each: `app.X` becomes a `type<X>` over its list; today's class at `app.X` (`X/list/this.cs`) becomes a `list<X>` and keeps the concept's own work (goal: the store, the loading, `all` listing `.build/`); one X stays at `X/this.cs` and answers `Match` and `Current`. Module becomes an ICreate item (`module/this.cs:12`). Goal's name lookups go: `_byName` and `Get(string)`'s form scans (`goal/list/this.cs:23,52-61,68-120`); the bare-name lookup `call` uses (`GetAsync`, `:131-169`) is call's, and coder traces where it lives. Its property on `app` goes lowercase (`App.Goal` → `app.goal`, …). The builder reads `%!app.module.list%` (`Decide.goal:14`) and its templates walk `m.Action` / `m.Modifier` (`module/this.cs:67-71`), so the module system's shape is checked against them. Test mode today is "`App.Test` is set" (`app/this.cs:198`); a test system that always exists needs test mode as its own fact | yes: the builder's own input; twins |
| 8 | **`on` and `current` on every object** (details below): events move from `event.on(Trigger=…)` to the object, as `on.before.<verb>` / `on.after.<verb>` for every public verb, plus outcomes (`on.error`, `on.hit`, `on.miss`). The `on` module's actions are one-line doors: `on.event(item, when, event, action)` for any event, and `on.error`, `on.cache`, `on.timeout`, which replace today's modifiers as events bound on the action before them (Ingi). Bindings are scoped to the actor that registered them unless `scope: app`. Every plang value is born through its type's `Create`, which fires `create`. `current` is each object's own answer. Payoff: value-level mocking (`- after file create, call LoadFixture`) | yes: the `on` actions; twins + one eval run |
| 9 | **Module pass, with file.read as the template (Ingi):** fix file.read first and make it the worked example of a correct action, written up as a doc ("how an action is written"). Then go over every module against it, one module per commit. The checklist: (1) plang values are born through their type (`app.type.file.Create(…)`; `new` only inside the type); (2) `Start()` hands over to the owner in one line; (3) no opened box, no broken seal (no `.Value()` on what it returns or forwards); (4) errors are results; (5) properties are typed (`Data<T>`), and a property that names where to write is a variable; (6) events fire from the owner, not the handler | per module: twins where a prompt changes |
| 10 | **`%!app` holds its types:** built-ins register at startup, a plugin loaded with `code.load` registers its own (`%!app.stripe%`); `%!app.list%` lists them; one name, one type (a clash fails loudly); `%setting.X%` stays as a short form | no |
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

Events are on everything, and they cost nothing unless bound: every item starts with the same shared empty `on` (one instance for the whole app, never null: the OBP rule "No null checks"), where `on.before.create.Start(this, context)` does nothing; the first binding gives the item its own `on`.

**A value is born through its type, so `create` fires every time.** Today plang values are born with `new` in at least 78 production places (`file/read.cs:72`: `new global::app.type.item.file.@this(path, Context, template)`), none passing the type, so `after file create` would miss them; a constructor can't fire it (it can't await, and an item stores no context). Stage 8 makes the type's `Create` door (`type/this.cs:206`, with overloads at `:289`, `:330`) fire `on.before/after.create`; stage 9 moves every birth onto it (`new` of a plang value only inside its type). In C# that's the file type (`app.type["file"]`, a `data<type>` since stage 4) and its `Create(raw, context)`, which answers an untyped item, so each moved site unwraps and casts.

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
on.before.create.Start(this, context);       // the shared empty on, or no bindings → nothing happens
…
on.after.create.Start(this, context);
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
- before goal show start, call LogStart   → on.event(item: %!app.goal["/show"]%, when: before, event: start, goal.call: LogStart)
- after text create, call LoadText        → on.event(item: %!app.type.text%, when: after,  event: create, goal.call: LoadText)
- after %user% set, call UserChanged      → on.event(item: %!app.variable.user%, when: after, event: set, goal.call: UserChanged)
```

The item is always the object itself, reached through its type: `%user%` would read the variable's VALUE (and put the event on it), so one variable is `%!app.variable.user%`.

**One action, `on.event` (Ingi).** Its properties: `item` (the item the event is put on, read through a path; `%…%` only reads, the binding happens in the module), `when` (`before` | `after`, or none for an outcome), `event` (the verb or outcome: `create`, `start`, `set`, `error`, `hit`, …), and the action to run (a `goal.call`, or any action). Its C# is one line, the same for every item: `Item.on[When][Event].Add(Action, context)`. The event class's verbs are `Add` (bind) and `Start` (fire). Names are the architect's sketch; coder owns the final ones.

**Scope.** A binding fires only inside the actor that registered it, as `event.on` behaves today (a test's fixture only affects the test); `scope: app` makes it global (a system logger).

## Faces (stages 5 and 7), settled with Ingi 2026-09-26

A collected type's face is a summary (names only); detail comes by navigating to one element. `current` shows where something is in play. The facts are the start set; a system may add a fact that's worth showing.

**Docs entry is `start.md` (Ingi):** inside plang apps (`os/**` and every app a programmer writes), a folder's docs are `start.md`, as `Start.goal` is its entry. The four `readme.md` under `os/system/ui/templates/{uikit,default}/` and `os/system/modules/ui/Builder/templates/{uikit,default}/` are renamed in stage 5. The C# repo keeps `README.md` where GitHub or tools expect it, with a `start.md` beside it (Ingi): in the repo root, `PLang/`, `PlangConsole/`, `Skill/` and `tools/decider/`, `start.md` is the one edited and `README.md` is its copy, and a small test fails when the two differ. `.semgrep/`, `Documentation/v0.2/audit/`, `PlangTests/` and `.bot/**` keep `README.md` only.

**A comment says what the next code line does (Ingi).** A goal's description goes above its name, and a step's comment above the step. The parser already follows this (`goal/this.cs:584-599`: lines above the name are the goal's `Comment`, lines above a step are that step's). The `.goal` files don't: 356 of 594 in `os/` and `Tests/` describe the goal under its name, so the text lands on step 0. In stage 5:
1. Those files are fixed: the lines move above the goal's name (Edit tool; split across helpers as needed).
2. One member: `goal.Comment` is the description; `goal.Description` (never set by the parser, only read from a `.pr` key nothing writes, and "Goal not found" at `:402`) goes. The goal face shows `comment`.
3. The goal's hash covers the source as written, comments included, so a comment-only change re-saves the `.pr` (every step still cached, no decider or LLM).
4. The rule goes into the plang docs, and a CLAUDE.md proposal covers bots writing `.goal` files.

**Honest facts come with the faces (stage 5).** Today only text and base64 declare a description; text's example is a filename (`readme.md`, `text/this.cs:33`); archive, binary and signature have placeholder examples (`(archive)`, `(bytes)`, `(signature)`). Each type gets a real description and a real example. Internal item classes under `type/item/` (`wire`, `source`, `clr`, `computed`) declare that they're not plang types and stay out of the face. `channel` and `serializers` carry `[PlangType]` but are collections, not choices; they're placed properly.

| face | shows |
|---|---|
| `%!app.type%` | `list` (type names) |
| `%!app.type.text%` | `name`, `description`, `example`, `alias`, `kind` (its kind names) |
| `%!app.type.text.kind.md%` | `name`, `extension`, `mime` |
| `%!app.type.choice.kind.operator%` | `name`, `values` |
| `%!app.goal%` | `list` (addresses of the goals loaded so far), `current` |
| `%!app.goal["/start"]%` | `name`, `path`, `comment`, its steps (index and text), `child` (its sub-goals), `on` |
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
| `App.Type` in PascalCase | 2 |
| `Registry.cs`'s five maps, `_catalogByName`, `_full`; `Register`, `RegisterRuntime`; the static `Loader` (`Register`, `SealedNames`, `ReservedCore`, `ReservedShadow`); `Get(string)`/`Clr(string)` as two doors; `Primitive.Aliases` as the registry's (onto each type), `Precision` (`type/list/this.cs:189-195`), the spelled-form parsing in the name door (`:161-169`); the registry's `Choice`, `Kind` and `Scheme` stores (`:40,48,56`, onto the types) | 3 |
| the registry's stored `Context`; the throw on a lookup miss (`type/list/this.cs:171`, `goal/list/this.cs:244`, `module/list/this.cs:121`); `list<T>`'s loose constraint; `type.@this` being sealed; the registry as the class reached at `app.type` (it becomes `app.type.list`) | 4 |
| `type/list/view/` (whole folder) and `BuildTypeEntries`; `goal.Description` (`goal/this.cs:41-42`, its write in `build/code/Default.cs:206-214`, its read in `goal/serializer/Reader.cs:54`); the goal hash that ignores comments; the four `os/` `readme.md` (→ `start.md`) | 5 |
| `app/variable/path/` (`Parse`, `Segment` and its kinds, `Segment.Index.Key`'s string re-parse, `Segment.Call.Args`); the walker's switch and clr special case (`data/this.Navigation.cs:33-94`); `data.TryFullVarMatch`; static `text.HasVariable(string)`; `CleanName` ×2 (`data/this.cs:647`, `variable/list/this.cs:552`); the other parsers (`Formal.cs:311,418`, `debug/this.cs:506`, `pick/list/this.cs:94,132`); `InvokeMethod`'s string switch (`data/this.Navigation.cs:143-233`); `variable.@this.Convert`'s hand scan; the regexes in `step/this.Validate.cs:26`, `step/this.Scope.cs:9`, `pick/list/this.cs:90,96`; `data.HasVariableReference` | 6 |
| the per-concept `X.list.@this` as the class reached at `app.X` (goal, actor, module, test: each stays as the concept's `list<X>`, reached at `app.X.list`); their own name lookups (`goal/list/this.cs:243`, `module/list/this.cs:118`: the type's `["key"]` replaces them); module's `list` member (`module/list/this.cs:125`: the class is the list); goal's `_byName`, `Get(string)`'s form scans and `All` (a second name for `list`, `goal/list/this.cs:23,52-61,68-120,311`) | 7 |
| `event.on`, `Trigger` as a list of moments beside the objects; the per-context binding lists (`context.LifecycleFor(…)`, called at `goal/this.cs:333`, defined at `actor/context/this.cs:425,448,472`; `app/event/lifecycle/`): bindings live on the item, each carrying its scope; `mock.intercept`'s wiring (replaced by `mock` → `app.test.mock`); the modifier concept: `modifier.list`, `Wrap`, the catch grouping, `[Modifier(Order)]`, `IModifier`, `ModifierAttribute`, `cache.wrap`, `timeout.after` and the `timeout` module (replaced by `on.error`, `on.cache`, `on.timeout` as events) | 8 |
| every direct `new` of a plang value outside its type (≥ 78 production sites); whatever else the checklist catches, per module | 9 |
| reflection over the C# `App` as `%!app`'s answer | 10 |
| `PLang.Tests/Shared/TestApp.cs`, `TestAction.cs` (statics) | 11 |

**Stays:** the `mock` module (its action now points to `app.test.mock`); `on.error` as the name of the error event's action; the postfix attachment rule (an `on` action binds on the action before it); the builder pipeline as built on builder-formal.

## OBP validation

| New or moved surface | plang path | C# | file | Check |
|---|---|---|---|---|
| the type named `type` | `%!app.type%` | `app.type` | `app/type/this.cs` (a `type<type>`) | over the registry; one way in (`Add`) |
| the list of types | `%!app.type.list%` | `app.type.list` | `app/type/list/this.cs` | a `list<type>`; no stored context; keeps the lookups by other keys (`Mime`, `Extension`, `[System.Type]`) |
| one type | `%!app.type["text"]%` / `.text` | `app.type["text"]` | `app/type/this.cs` | owns its name, aliases, facts, `kind`, `on`; answers `Match` |
| a collected type | `%!app.goal%` (= `%!app.type.goal%`) | `app.goal` | `app/goal/this.cs` (a `type<goal>`) | one generic class for every concept; no class per concept |
| a concept's list | `%!app.goal.list%` | `app.goal.list` | `app/goal/list/this.cs` | a `list<goal>`, like `step.list`; holds the concept's own work (loading) |
| the one in play | `%!app.goal.current%` | `app.goal.current(context)` → `data<goal>` | `app/type/item/ICurrent.cs`; `goal.Current` | the element's `static virtual`, the ICreate pattern; 404 when none |
| picking one | `%!app.goal["/show"]%` | `app.goal["/show"]` → `data<goal>` | `app/type/this.Generic.cs` | one line: `list.all.First(p => p.Match(key))`; a miss is a 404 result |
| what an element answers to | — | `x.Match(key)` | `app/type/item/IMatch.cs` | the element's knowledge (goal: address; type: name or alias); one line each |
| the first match | — | `list.First(match)` → `data<T>` | `app/type/item/list/this.Generic.cs` | typed predicate; no context needed; 404 when none |
| a variable | `%user.name%` | `app.type.item.variable.@this` | `app/type/item/variable/this.cs` | `text` + `code`; `Value()` → `Start()` → `Code.Start()` |
| the parser | — | `app.type.item.variable.parser.@this` | `app/type/item/variable/parser/this.cs` | the only definition of a reference |
| an item's variables | — (a value's own) | `item.Variable` | `app/type/item/this.cs` | read-only, born whole; the shared empty list when none |
| the type named `variable` | `%!app.variable%` | `app.variable` | open (see Open) | its list is the memory of the actor in play |
| registering an event | `- after text create, call LoadText` | `on.event(item, when, event, action)` → `Item.on[When][Event].Add(…)` | `app/module/action/on/event.cs` | one action for every item; scoped to the registering actor |
| an object's events | `%!app.type.text.on%` (read) | `x.on` | `app/event/before/<verb>/this.cs`, `app/event/after/<verb>/this.cs` | registered by a step through the `on` module; the shared empty `on` until bound |
| a value's birth | — | the type's `Create(raw, context)` | `app/type/this.cs` (`Create`) | the one door; fires `on.before/after.create`; `new` only inside the type |
| a mock | `- mock http url=…, call MockExample` | the `mock` action → `app.test.mock` | `app/module/action/mock/…` → `app/test/…` | a before-start binding with a filter, born cancelling, scoped to the actor |
| how an action is written | — | — | a doc, file.read as its worked example | the module pass's template |
| starting an action from C# | — | `app.module["file"]["read"].Start(…)` | `module/this.cs` | through `action.Start`; nothing test-only |
| every goal | `%!app.goal.list.all%` | `app.goal.list.all(show, os)` | `app/goal/list/this.cs` | a method with optional parameters; `.all` = the defaults |
| a goal's description | `%!app.goal["/show"].comment%` | `goal.Comment` | `app/goal/this.cs` | the lines above the goal's name; the only description member |
| names | — | — | — | no verb+noun; verbs `Start`, `Add`, `Load`, `Create`, `Match`, `First`, `Current`, plumbing `Variable`, `Code`, `Alias`, nodes `on`, `current`, `list`, `all`, `kind`: one word each; nodes lowercase, verbs PascalCase |

## Open for the next round

Rounds 1–4 closed. Round 4 added: one `on.event(item, when, event, action)` for every event, bindings scoped to the registering actor, and binding/firing as verbs (`Add`, `Start`).

Round 5 (2026-09-26) settled every `app.X` as the type X: one generic `type<X>` over the concept's `list<X>` (no class per concept, no `X/type/` folder), `IMatch`, `ICurrent`, `list<T>.First`, the strict `list<T>`, goal keyed by its address, bare names left to `call`, and the choice/kind/scheme stores moved onto the types. Parked for after this branch: `goal.PrPath` → a `pr` object (`pr.path`, later `pr.encryption`; `Documentation/Runtime2/todos.md`). Open:
1. `%!app.goal%` and `%!app.type.goal%` are one object reached by two names.
2. **Variable:** its list is the asker's memory (`actor/context/this.cs:43`), which a `list` getter can't reach: it has no context. Navigation carries the context (as it does for `current`). Where the variable type's class lives follows from that, since one variable moves to `app/type/item/variable/` in stage 6.
3. **Round 3's 5(c):** `show` in `all(show, os)` as a choice.
