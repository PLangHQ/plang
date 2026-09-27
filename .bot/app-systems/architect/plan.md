# app-systems — every `app.X` is the type X

Branch `app-systems`, off `builder-formal`. Designed with Ingi, 2026-09-24 (the parked draft `.bot/goal-graph-singular/architect/app-systems-draft.md`), 2026-09-26 and 2026-09-27. **Under review with Ingi, round by round, until a round passes without comment. Coder does not start before that.**

> **Coder, you own the code.** The shapes below are sketches: names, members and file:line are the architect's reading on 2026-09-26. Trace before each stage and bring back what doesn't hold.

## Why

1. **The three paths disagree.** The type system is reached at `app.type` (plang `%!app.type%`, C# `App.Type`) but lives at `type/list/this.cs` as `type.list.@this`; "list" names both the system and the types in it. OBP's alignment test (`Documentation/v0.2/object_pattern_formal.md`, "The three paths agree") says that's a wrong shape. The same holds for goal, actor, module, test and variable.
2. **The type system has several ways in and several copies of its knowledge.** `Register` (`type/list/this.cs:390`), `RegisterRuntime` (`Registry.cs:100`) and the static `Loader.Register` (`Loader.cs:87`) all add types; five maps (`Registry.cs:24-36`) plus `_catalogByName` and `_full` (`type/list/this.cs:111`, `:216`) look them up; `Get(string)` and `Clr(string)` answer one question twice (`type/list/this.cs:90`, `:93`).
3. **Systems can't describe themselves, and the facts they'd show are thin or wrong.** `write out %!app.type%` throws `NoWireContract`, because the class declares no face. Only 2 of ~21 types declare a description, text's example is a filename (`readme.md`), and 356 of 594 `.goal` files put the goal's description under its name, where it becomes step 0's comment.
4. **A reference (`%x%`) has at least nine parsers and several definitions** (`data.TryFullVarMatch`, `text.HasVariable`, the coverage/types-walk/pick regexes, `Formal.cs:418` (`%[^%\s]+%`, which rejects a space) and its bare-name regex at `:311`, `module/action/debug/this.cs:506`, `pick/list/this.cs:94,132`, the old store render), plus two identical `CleanName`s and a hand-written name scan in `variable.@this.Convert`. `"save 50% now and 20% later"` parses differently depending on who reads it. Reading a variable re-parses its name and walks it through a switch (`data/this.Navigation.cs:51-89`).
5. **Events live beside the objects, not on them.** `event.on(Trigger=…)` is one module over an enum (`app/event/Trigger.cs`). The thing an event is about doesn't own it.
6. **The entry verb differs from plang's.** plang's entry point is `Start` (`Start.goal`, and `App.Start()` at `app/this.cs:480`), but everything else is entered through `Run` (goal, step, action, the lists, 125 action handlers).
7. **C# tests don't use the app's own doors.** They go through static helpers (`PLang.Tests/Shared/TestApp.cs`, `TestAction.cs`), not the way plang starts an action.

## The shape

**Every `app.X` is the type X (Ingi, round 5).** This reverses two earlier rulings: the separate system class (`X/this.cs` the system, `X/X/this.cs` one X, from the parked draft) and stage 4's "a system is not a plang value type". There are no system classes and no doubled files. The type named `goal` is what knows the goals, the same way the type named `text` knows text's kinds.
- **`app.X` is a `type<X>` (Ingi: "`%!app.goal%` is `app/goal/this.cs`, no type there").** One generic class, `type.@this<T>` (`type/this.Generic.cs`), serves every concept; there is no class per concept and no `X/type/` folder. App writes `goal = new(this);` (C#'s target-typed `new`, from the property type `type<goal>`): the type named `goal`, defined by `goal/this.cs`. The name comes from goal's class (`item.NameOf`, `type/item/this.cs:333-341`, the namespace tail), and the list from goal itself (`List(app)`, beside `Current`). `%!app.goal%` and `%!app.type.goal%` reach the same object (Ingi).
- **One X stays where it is:** `goal/this.cs`, `type/this.cs` (one type), `module/this.cs`.
- **The concept's own work lives in its list:** `X/list/this.cs` becomes a `list<X>`, as `step.list` (`goal/step/list/this.cs:14`) and `action.list` (`goal/step/action/list/this.cs:16`) already are. Goal's list keeps its store and its loading of `.pr` files; the registry, `type/list/this.cs`, becomes the `list<type>` at `app.type.list` and keeps the lookups by other keys (`Mime`, `Extension`, `[System.Type]`: `app.type.list.Mime(…)`). Today goal's, module's, actor's and test's list classes don't inherit the plang list (`goal/list/this.cs:13`, `module/list/this.cs:14`, `actor/list/this.cs:8`, `test/list/this.cs:13`). **One store: the list (Ingi, round 6: "if it's a list and we need pr path, it is just list.where(prpath == …)").** `list<T>` keeps its elements in its own slots (`type/item/list/this.Generic.cs:26-29`), and every lookup walks the list or uses `where`: by address, each element's `Match(key)`; by `.pr` path, `p.PrPath == location`. The dictionaries beside it go: goal's `_goals`, `_byPath`, `_byName` (`goal/list/this.cs:18-23`), the registry's maps (stage 3), module's and actor's. Otherwise the list's own storage is empty (`count` 0, printing nothing), or two stores drift, since the list's `Add`/`Remove` are public and not virtual (`type/item/list/this.cs:287,301,337,394`). The frequent type lookups (`type.Create` asking `App.Type[Name]?.ClrType` on every value's birth, `type/this.cs:132,218,247,259,318`) exist only because the type in hand may be a copy that doesn't know its C# class; stage 3 makes each type own it, so they go. An index comes back only if a measurement asks for one, inside the list, with `Add`/`Remove` made virtual.
- **No name clash in the registry:** a `list<T>` subclass is a kind of list and claims no name (`Registry.cs:246-254`), and the open generic `type.@this<T>` (reflection name "this\`1") isn't taken for an `@this` class (`:237-238`), so it claims nothing, like `list.@this<T>`.
- **A value type (text, number) is a plain `type.@this`** built from the catalog; it has no `list`.

**Members of a collected type (`type.@this<X>`):**
- `list`: a `list<X>`, the X's loaded so far; `list.all` is every one (below).
- `current(context)`: the one in play, an X (`%!app.goal.current%` is a `goal/this.cs` instance, Ingi). The element's class answers it through a `static virtual` member, the same pattern as ICreate's `Create` (`type/item/ICreate.cs`): goal `context.Goal` (`actor/context/this.cs:100`), actor `context.Actor` (`:89`); a concept nothing is inside answers none (404). Today navigation reaches a method taking the context only on the `!` hop (`data/this.Navigation.cs:276-283`; a plain hop reads properties only, `type/item/kind/reflection/this.cs:17-26`), so the type's own navigation (`Get`, below) answers `current` with the asker's context, and plang writes `%!app.goal.current%`.
- `Get(key)`: walks `list.all()` (every default) and answers the first element whose `Match(key)` answers, as `data<X>`. No match is a 404 NotFound result, not an exception (today both lookups throw: `goal/list/this.cs:244`, `type/list/this.cs:171`). **One async door, no C# indexer (Ingi, round 6):** a C# indexer can't await, and finding a goal may load its `.pr`, so C# writes `await app.goal.Get("/show")`, `await app.type.Get("text")`. plang's `["key"]` and `.key` reach the same `Get`: navigation is already async (every item's `ValueTask<data> Get(data parent, string key)`, `type/item/this.cs:235`, `type/this.cs:496`), and the type's navigation calls `Get(key)` for a key that isn't one of its own members. Two `Get`s on one object answer the same question ("what does `key` name"). Walking `all()` is async (goal's lazy list reads the disk as it goes); `all()` itself answers at once.
- **The element answers who a key names (`IMatch<TSelf>.Match(key)`, round 6):** itself, one of its own, or none (async: touching a goal's children loads its `.pr`). A goal answers for its `Address` (`goal/this.cs:214`: its .goal path without the extension, `/system/builder/EmitBuildEvent`, the name that reaches it from anywhere), and **a sub-goal's address is its file's address + `#` + its name** (`/start#show`; Ingi: "get loads start and asks for show"): `/` already means a folder (`call BuildGoal/Start` is Start in the folder BuildGoal, `goal/list/this.cs:107-117`), so `/start/show` would collide with `start/show.goal`, and in a URL `#` names a part inside one document. **The parser sets a sub-goal's parent when it adds the child (Ingi),** as the `.pr` reader already births it with its parent (`goal/serializer/Reader.cs:37-44,66`); today the parse only adds to `Child` (`goal/this.cs:616-621`), so right after a build a sub-goal has no `Parent`. **Visibility is derived from it:** `Parent == null` is public, else private, replacing the `goals.Count == 0 ?` at parse (`:600`) and the `.pr`'s `visibility` key (`Reader.cs:69-72`). A type answers for its name or one of its aliases (`"string"` → text; each type owns its aliases, stage 3); a module or an actor its name. A precision (`int`) is a kind of number, not an alias: `["number"].kind["int"]`. The spelled forms `"text/md"` and `"list<path>"` become `["text"].kind["md"]` and `["list"].kind["path"]`. (`type.Is(string)`, `type/this.cs:438`, isn't reused: it answers true for `item` on every type.)
- `.key`: shorthand for `["key"]` where the key is a plain word (`%!app.type.text%`, `%!app.module.file%`). The type's own members win: `%!app.type.list%` is the list, and the type named `list` is `["list"]`. A goal's key is a path, so a goal is always `%!app.goal["/show"]%`.
- the facts, `on` and `kind`, as on every type.
- **A bare goal name is `call`'s lookup, not the indexer's (Ingi; reverses round 3's "a bare name resolves the way call does").** `call Start` searches from the caller: the caller, its children, each ancestor and theirs, then the caller's folder and up (`goal/list/this.cs:131-169`). `goal.Get("x")` takes an address only, and searches `list.all()`, so a goal that isn't loaded yet still answers.

```csharp
// sketch: type/this.Generic.cs — NEW; the same pattern as item/list/this.Generic.cs
namespace app.type;
public sealed class @this<T> : @this
    where T : item.@this, item.ICreate<T>, item.IMatch<T>, item.ICurrent<T>, item.IList<T>
{
    public @this(app.@this app) : base(item.@this.NameOf(typeof(T))) => list = T.List(app);   // base ctor: type/this.cs:105
    public item.list.@this<T> list { get; }

    // C#'s door: one element by key. No context of its own (type/this.cs:192-193), so the result has none;
    // a C# caller takes the value and already has its own context.
    public async ValueTask<data.@this<T>> Get(string key)
    {
        await foreach (var p in list.all())                                       // every default; a lazy list loads as it goes
            if (await p.Match(key) is { } found) return data.@this<T>.Ok(found);  // data/this.cs:713
        return data.@this<T>.FromError(new Error($"no {Name} '{key}'", "NotFound", 404));   // :714
    }

    // plang's door: one navigation step; the parent brings the asker's context (round 6, Ingi: (a))
    public override async ValueTask<data.@this> Get(data.@this parent, string key)
    {
        if (await base.Get(parent, key) is { Success: true } member) return member;   // the type's own members first (type/this.cs:496-497)
        var found = await Get(key);
        if (!found.Success) return found;                                              // the 404
        return new data.@this(key, (await found.Value())!, parent: parent);           // data/this.cs:223-233: context from the parent
    }

    // current has the asker's context in hand, so its Data is born with it
    public data.@this<T> current(actor.context.@this context)
        => T.Current(context) is { } one ? new data.@this<T>("current", one, context: context)   // data/this.cs:709
           : data.@this<T>.FromError(new Error($"no {Name} is current", "NotFound", 404));
}

// sketch: app/this.cs — the properties' types change; today goal.list.@this (:146), type.list.@this (:210), module.list.@this (:135)
public type.@this<goal.@this>   goal   { get; }   // goal = new(this);   its list: goal.list.@this
public type.@this<type.@this>   type   { get; }   // type = new(this);   its list: the registry, type/list/this.cs
public type.@this<module.@this> module { get; }   // module = new(this); its list: module.list.@this

// sketch: type/item/IMatch.cs — NEW: the element that answers to key (itself or one of its own), or none
public interface IMatch<TSelf> { ValueTask<TSelf?> Match(string key); }

// sketch: type/item/ICurrent.cs — NEW: which one is in play (one item); the ICreate pattern, one question per interface
public interface ICurrent<TSelf> where TSelf : @this, ICurrent<TSelf>
{
    static virtual TSelf? Current(actor.context.@this context) => null;          // nothing is ever inside it
}

// sketch: type/item/IList.cs — NEW: which list holds them (shares its short name with .NET's IList<T>;
// outside app.type.item it's written item.IList<…>)
public interface IList<TSelf> where TSelf : @this, ICreate<TSelf>, IList<TSelf>   // list<T> is strict
{
    static virtual list.@this<TSelf> List(app.@this app) => new();                // a plain list<T>
}
// goal/this.cs — NEW
public static goal.@this? Current(actor.context.@this context) => context.Goal;
public static item.list.@this<goal.@this> List(app.@this app) => new goal.list.@this(app);   // it loads .pr files
// actor/this.cs — NEW
public static actor.@this? Current(actor.context.@this context) => context.Actor;

// sketch: goal/this.cs — NEW: /start answers itself; /start#show asks its children
public async ValueTask<@this?> Match(string key)
{
    if (string.Equals(Address, key, System.StringComparison.OrdinalIgnoreCase)) return this;
    if (!key.StartsWith(Address + "#", System.StringComparison.OrdinalIgnoreCase)) return null;
    foreach (var child in Child) if (await child.Match(key) is { } found) return found;
    return null;
}
// goal/this.cs:214 — Address, CHANGED: a sub-goal adds #name to its file's address
public string? Address => Parent is { } parent ? $"{parent.Address}#{Name}" : File;   // File = today's body (path without .goal)
// goal/this.cs:620 — parse, CHANGED: the parent is set where the child is added
goals[i].Parent = goals[0]; goals[0].Child.Add(goals[i]);

// type/this.cs — NEW (Alias: stage 3)
public ValueTask<@this?> Match(string key)
    => new(string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase) || Alias.Contains(key) ? this : null);
// module/this.cs, actor/this.cs — NEW
public ValueTask<@this?> Match(string key) => new(string.Equals(Name, key, System.StringComparison.OrdinalIgnoreCase) ? this : null);
```

**`list<T>` is strict (Ingi: "stricter is always better").** `list<T>` takes `T : item, ICreate<T>`, as `data<T>` does (`data/this.cs:698`); today it takes `T : item` (`item/list/this.Generic.cs:15`). Gaining ICreate: test (`test/this.cs:16`), test timing (`test/timing/this.cs:12`), `LlmMessage` (`module/action/llm/LlmMessage.cs:12`), type (`type/this.cs:32`, also unsealed), and module, which isn't an item yet (`module/this.cs:12`). Goal, step, action, actor, path, text, tag and identity already are. Also `PLang.Tests/Shared/CollectionTestExtensions.cs:16-17` (`ToListData<T> where T : item`). **Type gaining ICreate:** type's own instance `Create(object?, context)` and `Create(object?, data)` (`type/this.cs:206,330`) have the same signatures as ICreate's static `Create`, so type implements ICreate explicitly (a C# detail, coder's).

**C# callers of today's indexers (round 6).** `Get(key)` answers `data<X>`, because a miss must say why (404) and only a Data carries an error. The ~50 production callers of today's indexers (`App.Type[…]`, `App.Module[…]`, `App.Actor[…]`) sort into three groups:
1. **Not asking by name** (~20): by C# class or by a type's identity (`App.Type[raw.GetType()]`, `type/item/this.cs:90`; `App.Type[new type.@this("file", kind, template)]`, `file/read.cs:73`). These are the list's lookups by other keys, typed, over the list's own elements (`p.ClrType == t`): `app.type.list[…]`. A type asking for its own full self (`type/this.cs:132,218,247,259,318`, on every value's birth) stops asking: stage 3 makes each type own its C# class.
2. **Stamping a type the value already knows** (~7): `Context.Ok(kept, Context.App.Type["list"])` (`where.cs:46`; also `:55`, `any.cs:34`, `file/read.cs:55`, `action/serializer/Reader.cs:152`, `action/this.Scope.cs:23`, `variable/set.cs:106`). The lookup goes (`Ok`'s type is optional, `actor/context/this.cs:193`); stage 9.
3. **A name the programmer or the `.pr` wrote** (~20): these want the result, one ask instead of today's `Contains` then indexer (`variable/set.cs:218-222`): `var found = await app.type.Get(name); if (!found.Success) return found; var type = await found.Value();` (`data/this.cs:707`).

**No sync callers (Ingi: "they shouldn't be sync then").** A caller that can't await becomes async: `data.Is(string)` (`data/this.cs:139-143`) is only called from the condition operator (`condition/Operator.cs:163,170`), already async; the formal reader (`Formal.cs:163,180,323`, called from `step/list/this.cs:121`, `pick/list/this.cs:331`). **The `.pr` reader resolves nothing (Ingi: "the solution is higher up the stack"):** it reads through a `ref` JSON reader that can't cross an await, so it keeps the module's name (`action/serializer/Reader.cs:53` looks it up today so a missing module fails at load, `:50-52`), and the async goal load above it does the lookup, where a missing module is a load error, not a throw. No `GetAwaiter()`, no `Peek()` casts.

**The LLM learns the element type once (architect's choice; Ingi left it open).** `PlangName` (`type/list/this.cs:356`) reads `list.@this<T>` as `{list, kind: T}` (`:288`), so `list: list<goal>` comes from the C# type with no extra work. Today the catalog lists only `[LlmBuilder]` properties, from inside `BuildTypeEntries` (`:537`, which dies in stage 5); `type<T>.list` is marked for the builder, and stage 5's faces carry its kind. The prompt teaches the rule once: `%!app.X["key"]%` is one element of `%!app.X.list%`. The element type is stated in one place, the list's kind.

- **The type is an item.** It writes its face through `Output` (facts, any writer), a formatter (a template) presents them, and it answers its own navigation (`Get(parent, key)`: a member first, else `Get(key)`, else NotFound). Like every item, it stores no context: the caller passes it.
- **Where `current` gets its context (Ingi):** `%!app%` is a variable in the actor's own memory, registered with that context (`actor/context/this.cs:165`), and navigating from it carries that context down every hop. So `%!app.goal.current%` is answered by the goal type's navigation from the asker's context. `current` stays on every type where it means something (goal, actor, test), for symmetry; C# code that holds the context gets the same answer as `context.Goal`.

**`list` is a real object (Ingi).** Each collected type's `list` is its concept's `X/list/this.cs`, a `list<X>`: it prints, enumerates, counts and indexes like any list, and carries its own members (goal's store and loading; the registry's lookups).

**What `list` is, each type decides (Ingi).** Today the base item takes the name for every item: `item.list` is the item's **history**, the values it was made from (`type/item/this.cs:262-269`; a dict parsed from a file holds the file, so `%config% is file` stays true, `:282-286`; an image made from a path holds the path, `type/item/image/this.cs:165`). It moves to its own object: `item.history` (`app/type/item/history/this.cs`, today `type/item/type/list/this.cs`), whose `list` is those values in order (wire → source → dict), with `Add(prior)` and `Has(type)` as today (`type/item/type/list/this.cs:25-36`). That frees `list` on every item, so `type<T>.list` hides nothing, and `type/list` means only the registry. The call sites: `type/item/this.cs:269,286`, `type/item/source.cs:146,161`, `type/item/image/this.cs:165`, `type/item/file/this.cs:49,106`, `type/item/url/this.cs:35,91`, and `PLang.Tests/Shared/MaterializeProbeExtensions.cs:24`. (`ICreate.cs:66`'s `errVal.list` is an error's own causes, `error/Error.cs:63`, not the history.)
- `%!app.goal.list%` is the goals loaded so far (goals load when they're called).
- **`%!app.goal.list.all%` is every goal an address reaches,** the app's and `/system/`'s, built from a listing of `.build/` (the `.pr` files, not read); each goal loads when it's first touched. `goal.Get(address)` walks it; the dead-goal warning (stage 11) asks it for the app's own (`os: false`).
- **Every list has `all`, and `all` takes a `setting` (round 6, Ingi; replaces the `private`/`os` parameters settled in round 5).** One signature on every list, so an override never changes it. The base list gets `all` (it has none today) and answers itself; a list that loads lazily overrides it. **`all` answers its list at once; walking it is async (Ingi: "this should just be `foreach (var p in list.all())`"):** goal's answer is a lazy list that lists `.build/` and loads each `.pr` as it is walked, and every list is walkable from C# as `IAsyncEnumerable<T>`, so a caller writes `await foreach (var p in list.all())`: no `await` on `all`, no `.Items()`, no `empty` passed:

```csharp
// type/item/list/this.Generic.cs — NEW: every list takes the call's setting as it came
public virtual list.@this<T> all(data.@this setting) => this;           // the same list
public list.@this<T> all() => all(empty);                                // no setting: every default (empty = the shared empty setting)
// list<T> : IAsyncEnumerable<T> — C# walks any list with await foreach; a lazy list loads as it goes

// goal/list/setting/this.cs — NEW: what goal's list can be told; the defaults live here
namespace app.goal.list.setting;
public sealed class @this : item.@this, item.ICreate<@this>          // makes itself from the call's dict
{
    public @bool os { get; init; } = true;    // include the system goals (Ingi), so all() is every goal an address reaches
    public list.@this<choice.@this<goal.Visibility>> visibility { get; init; } = [goal.Visibility.Public];   // both: [public, private]
}

// type/item/ISetting.cs — NEW: an item names its setting class; the builder shows that class's properties and defaults
public interface ISetting<TSetting> where TSetting : item.@this, item.ICreate<TSetting> { }

// goal/list/this.cs
public sealed class @this : item.list.@this<goal.@this>, item.ISetting<setting.@this>
{
    // a lazy list: when walked, it reads the setting (the typed ask, `await setting.Value<setting.@this>()`,
    // data/this.cs:707), then lists .build/ by s.os and s.visibility and loads each .pr as it's reached
    public override list.@this<goal.@this> all(data.@this setting) => new all.@this(this, setting);
}
```

  - plang: `%!app.goal.list.all%` (every default: the public goals of the app and of `/system/`, one per `.pr`, from the listing alone) and `%!app.goal.list.all(setting: {os: false, visibility: [public, private]})%` (the app's own, sub-goals too; the dead-goal warning's ask).
  - **The settings are a C# class (Ingi: "define the settings in c#, not get, because we can then build on top of that"):** `setting/this.cs` under its owner. A missing key is the property's own default (`= true`, `= [Public]`; Ingi: never an empty result). `visibility` writes its default because its enum's zero is `Private` (`goal/this.cs:16`).
  - **The builder reads the setting class** (named by `ISetting<TSetting>`) the way it reads a type's facts: properties, types, defaults (`os: bool = true, visibility: list<visibility> = [public]`); a plang call with an unknown key fails at build. No generator step.
  - One signature on every list, `all(setting)`: each list turns the call's dict into its own setting class; a list with none ignores it. `all()` is every default; the type's `Get(key)` walks it.
- **`all` is one of the list's own words (round 6, Ingi).** Navigating a list knows only `count`/`length`, `first`, `last`, `random` and a number; an empty list answers NotFound first, and any other word goes to the first element (`type/item/list/this.cs:556-576`: `%list.street%` → `%list[0].street%`). So `all` joins them, before the empty check:

```csharp
// type/item/list/this.cs — Get(parent, key), NEW, right after count/length
if (string.Equals(key, "all", System.StringComparison.OrdinalIgnoreCase))
    return new Data(key, all(), parent: parent);    // goal's override runs; an empty list still answers
```

  `.all` runs `all` with the shared empty setting (every layer's defaults); `all(setting: {…})` is a method step on the list (stage 6's `method` hop). `all` lives on the base list class, where `Get` is; the typed `list<T>` view over it is coder's. plang paths are written in lowercase (`%!app.goal["/show"].name%`); navigation ignores case.

**One element.** It owns its facts and its `on` (events about it); `current` (the one in play) is its type's.

**`Start` is the entry point of everything that runs (Ingi).** As `Start.goal` is plang's entry: `app.Start()`, `goal.Start(context)`, `step.Start`, `action.Start`, each handler's `Start()`, a list's `Start`, a code's `Start`. It's virtual, so an owner can change what starting it means. **A value keeps `Value()`** (Ingi); anything that has code also has `Start`, for consistency. A variable is both: `variable.Value()` → `variable.Start(context)` → `Code.Start(context)`.

**plang vocabulary is lowercase in C# too (Ingi).** The structure plang navigates is lowercase: `app.type`, `app.goal`, `app.variable`, `app.module`, `app.actor`, `app.test`, their `list`, `current`, `all`, `on`, `before`, `after` and the event classes. Facts keep their C# names (`Name`, `Path`, `Comment`): plang writes its paths in lowercase (`%!app.goal["/show"].name%`) and its navigation ignores case, and the face writes facts lowercase already. C# plumbing plang never navigates stays PascalCase, including an item's `Variable` list and a variable's `Code` (`%order.variable%` must reach the order's own key, not the item's metadata); a C# keyword keeps its `@` (`app.@event`); an item's own `Type` (its type entity) stays. So the three paths match letter for letter: `%!app.type["text"]%` ↔ `app.type.Get("text")` ↔ `app/type/this.cs`. Each type's property on `app` is renamed in the stage that moves it (type: stage 4; the others: stage 7). This replaces CLAUDE.md's "Property names on `app.@this` stay PascalCase" (proposal filed).

**Nodes lowercase, verbs PascalCase (Ingi: "the line where C# executes, and in C# starts uppercase").** Lowercase is what plang navigates (`app.type`, `list`, `current`, `on.before.create`, the facts). PascalCase is what C# calls: `Start()`, `Value()`, `Add()`, `Load()`. plang never calls a verb through a path (what runs, runs in a module; `%…%` only reads), so a verb is C#'s alone and sits with C# library methods (`ToString`, `DisposeAsync`). A read inside `%…%` (`%name.replace("-", " ")%`) works either way, since plang's navigation ignores case.

**What runs, runs in a module.** A step maps only to module actions. `%!app…%` and every `%…%` only read (a method call inside `%…%` must not change anything). An action's C# hands over to the owner in one line: `on.event`'s handler → `Item.on[When][Event].Add(Action, context)`.

| plang | C# | file |
|---|---|---|
| `%!app.type%` | `app.type` | `app/type/this.cs`, the type named `type` (a `type<type>`) |
| `%!app.type.list%` | `app.type.list` | `app/type/list/this.cs`, the registry as a `list<type>` |
| `%!app.type["text"]%`, `%!app.type.text%` | `await app.type.Get("text")` | `app/type/this.cs`, one type |
| `%!app.goal%`, `%!app.type.goal%` | `app.goal` | `app/goal/this.cs`, the type named `goal` (a `type<goal>`) |
| `%!app.goal.list%` | `app.goal.list` | `app/goal/list/this.cs`, a `list<goal>` |
| `%!app.goal["/show"]%` | `await app.goal.Get("/show")` | `app/goal/this.cs`, one goal |
| `%!app.goal.current%` | `app.goal.current(context)` | `app/goal/this.cs`, the running goal |
| `%!app.variable.some%` | `context.Variable` (C# uses its own context) | the type named `variable` (a `type<variable>`); its list is the asker's memory, reached through navigation's context |
| `Start.goal` | `app.Start()`, `goal.Start(context)` | the entry point, one word everywhere |

## Stages

| # | Stage | Changes what the builder sees |
|---|---|---|
| 0 | **Base:** re-record builder-formal's `Compile` (TypeSafe is back) so its BootstrapTests pass; take a baseline of the six suites. **Delete the deprecated v0.1 files** under `os/`: every `NN. stepname.pr` and every `00. Goal.pr` (Ingi: "all XX. stepname.pr files are deprecated and can be deleted", "also all 00. Goal.pr files"; the reviewer counted 552 of the 584 tracked `.pr` files there with the `NN. name.pr` form), leaving the v0.2 goal `.pr` files (one file per goal, all its steps) and the `.build/` folders themselves | — |
| 1 | **`Run` → `Start`:** the C# entry verb of every executable object (`goal/this.cs:325`, `goal/step/this.cs:111`, `goal/step/list/this.cs:28`, `goal/step/action/this.cs:165`, `goal/step/action/list/this.cs:31`), every handler's `Run()` (125 files) and the generator's emit. **Code a later stage deletes isn't renamed:** the event bindings (`event/lifecycle/binding/`), `cache.wrap`, `timeout.after`, `event.on`, `mock.intercept`, `on/error` (stage 8), `App.Run<TAction>` (stage 11). Starting one action is one verb end to end: today it's `action.Run` → `call.ExecuteAsync` (`callstack/call/this.cs:225`) → `handler.Execute()` (`ICodeGenerated.cs:37`) → the handler's `Run()`; `Execute`/`ExecuteAsync` go with `Run`. Virtual where an owner may override. No behaviour change. **The two plang actions named `run` are renamed too (round 6, Ingi: "it should be start, because call, step, action will all be renamed Start"; reverses this stage's earlier "plang action names are not part of this"):** `environment.run` → `environment.start` (`module/action/environment/run.cs:9`: starts a goal call, a step or an action, optionally on another actor; the module name `environment` stays temporary, its naming pass still deferred) and `test.run` → `test.start` (`module/action/test/run.cs:21`), with their teaching files (`os/system/modules/environment/run.*.md`, `os/system/modules/test/run.*.md` → `start.*.md`). Builder-visible. **Also** (fresh-eyes review): `list.range` already has a plang property `Start` (`module/action/list/range.cs:8`), which clashes with a `Start()` method, so it's renamed (builder-visible); `action.Return` finds the return type with `GetMethod("Run")` (`goal/step/action/this.Schema.cs:53`), and the string changes with the rename; `RunGoalAsync` is in scope; `test.@this.Start()` already means "start the stopwatch" (`test/this.cs:80`) and gets another name; about 132 test files call `.Run(` (179 with `RunAsync`/`RunGoalAsync`) | yes: range's property rename; twins |
| 2 | **Folded into stage 4** (round 6: lowercasing `App.Type`'s 80 references in 41 files here, then re-pointing the same references to `app.type.list[…]` in stage 4, edits them twice). What stage 4 does with it: no file moves; `type/this.cs` stays one type and `type/list/this.cs` stays the registry; `App.Type` → `app.type`. **C# note:** a lowercase property hides a same-named namespace (a class owning `type` can no longer write `type.item.text.@this`, CS1061; checked by compiling), so references to those namespaces are written `global::app.type…` in every class that owns such a property | no |
| 3 | **One set of types:** the maps become one set, each type owning its name, aliases (`Alias`), C# class and facts, and answering `Match(key)` (`IMatch` arrives here). `Add` is the one way in; `Load` is the startup scan; `Get`/`Clr` become the one door. The name door's spellings go: an alias is the type's own (`Primitive.Aliases`, `type/list/this.cs:79,241`), a precision is number's kind (`Precision`, `:189-195`), and `"text/md"` / `"list<path>"` (`:161-169`) are written `["text"].kind["md"]`. **The registry's `Choice`, `Kind` and `Scheme` stores (`:40,48,56`) move onto the types they belong to (Ingi).** **One kind, one member (round 6):** a kind is already one concept, the subtype token and its behaviour (`type/kind/this.cs:3-13`: json, md, int, `*`; one with no class is a base instance carrying its name). A type's `kind` is today's `Kind` (`type/this.cs:60`, lowercased like every node): never null (a bare `text` has text's empty kind, which knows its type); `kind["md"]` is one kind; **`kind.list` answers full types, each with its kind, so every entry carries its type (Ingi):** `%!app.type.text.kind.list%` → `[{text, md}, {text, csv}, …]`, a `list<type>` at `type/kind/list/this.cs` (today that file is the global store, `app.type.Kind`). Entries come from the type: number's precisions (`number/this.cs:75`), item's kind classes (`type/item/kind/json`, `list`, `dict`, `reflection`: today's global store; the lookup by C# class is item's list searched by `ClrForm`, `type/kind/this.cs:36`), text's from the formats that map to text (text's kind is open, `text/this.cs:44`). **`Kinds` dies (Ingi: "it shouldn't exist"):** `type/this.cs:475`, fed only by `hash` (`module/action/crypto/type/hash/this.cs:34`) and read only by the obsolete view (`type/list/view/this.cs:103`) and `Full` (`type/list/this.cs:251`). Choice works the same way (`choice/this.cs`, its `.list` the sets); path's schemes are its kinds. `Add` carries the static `Loader`'s checks that `code.load` relies on (`code/load.cs:40`): sealed names (a loaded DLL can't replace identity, signature, signedoperation, callback or channel, `Loader.cs:55-59`), reserved names (`type`, `error`, `success`, `@schema`), and renderer registration with its coverage check (`:144-175`). `_catalogByName` is built from `BuildTypeEntries(null)` (`type/list/this.cs:118-121`), the only source of Description, Example, Values, Property and Shape that prompt C reads, so the facts move onto each type here | yes: prompt twins byte-equal, or one eval run |
| 4 | **The collected type:** `type.@this<T>` (`type/this.Generic.cs`: `list`, `Get(key)`, `current(context)`), `IMatch<T>`, `ICurrent<T>`, `IList<T>` (one question each), and the strict `list<T>` (`ICreate<T>`; test, test timing, `LlmMessage` and type gain ICreate; type is unsealed). First, the item's history moves from `item.list` to `item.history` (`history.list`), so `list` is each type's own. `app.type` becomes a `type<type>` over the registry, and the registry (`type/list/this.cs`) becomes a `list<type>`: its stored context goes (`internal Context`, `:30`); it keeps the lookups by other keys: `Mime`, `Extension`, `this[System.Type]`, the identity door `this[app.type.@this]` (a value's type → its full type, `:224`, 7 production uses), `Reader` (`:203,211,261,224,75`). Coder traces the rest of the registry's members: `Renderer` (`:66`, vestigial), `Contains(string)` (`:97`; the result door replaces the Contains-then-index pattern), `IsClrTypeName` (`Registry.cs:49`), `Primitive` (`:79`; its aliases move onto the types in stage 3). The type writes its face through `Output` and answers navigation (a member first, else `Get(key)`). A lookup miss is a 404 result. The type's instance is added to the list under its name (`type`, `goal`, …), one entry with the catalog's facts for that name (stage 3's `Add`). Stage 5's internal items (`wire`, `source`, `clr`, `computed`) say by their own fact that they're not plang types. The prompt teaches `%!app.X["key"]%` once | yes: one rule in the prompt; twins |
| 5 | **Faces and honest facts** (details in "Faces" below): the type faces (the type named `type`, one type, a choice's kinds with their `values`, a kind); each type's real description and example; internal item classes stay out; prompt C's Types section renders from these facts (`properties.template:82` already reads type facts); `type/list/view` (already `[Obsolete]`) and `BuildTypeEntries` (`:425`) die; module's `Schema` is a `type.list.view.@this` (`module/list/this.cs:30,35`), and about 11 test files call `app.Module.Schema.Build()`. Also: the 356 `.goal` files get their description above the goal's name; `goal.Comment` is the one description member; the hash covers comments; `start.md` docs (`os/system/modules/ui/Builder/SetLayout.goal:3` reads `…/readme.md`, and `setlayout.pr` holds that path: both change) | yes: twins byte-equal, or one eval run |
| 6 | **The reference** (details below): `app.variable.@this` → `app.type.item.variable` (about 136 references: 42 production, 94 tests); the memory moves beside it, `app/variable/list/this.cs` → `app/type/item/variable/list/this.cs` (Ingi). A variable is `text` + `code`; `Value()` → `Start(context)` → `Code.Start(context)`. The parser (`app/type/item/variable/parser/`) is the one definition; each hop kind parses its own piece. Build validation writes each marked row's `"variable"` list into the .pr, and loading never parses again. `item.Variable` (a read-only list of variables; one shared empty list when none, never null: the OBP rule "No null checks"); `HasVariable => Variable.Count > 0` on the item, and `data.HasVariable => _item.HasVariable` (`_item` is never null: `data/this.cs:34` starts it as the null item); `IsVariable` is one variable covering the whole value. **Every installed .pr with marked rows** (the builder's 6 marked files, `test.pr`, `show.pr`, and 8 of the 10 tracked `Tests/**/.build/*.pr`) is rewritten with its `"variable"` lists by a throwaway C# pass (the parser over each marked value, written through `plang.Text`; no LLM), since the loader refuses a marked row without its list. **Consumers to move** (fresh-eyes review): the store's Get/Set by name (`variable/list/this.cs:128,261,279,317,339,403`), `type/kind/this.cs:66-81`, `type/clr/this.cs:94-101`, `text/this.cs:153`, `data.Get(string)` (`data/this.Navigation.cs:17`), `data.Set(path, …)` (`:104`). **The source generator, its own step:** it emits `HasVariableReference` and `global::app.variable.@this` as strings (`Emission/Property/Data/this.cs:192`) and finds `IName` by the namespace string `"app.variable"` (`Discovery/this.cs:189-190`); if that moves unnoticed, the missing-parameter guard disappears silently | yes: `"variable"` in the .pr; twins + one eval run |
| 7 | **Every concept is its type:** goal, actor, module, test, variable. Each: `app.X` becomes a `type<X>` over its list; today's class at `app.X` (`X/list/this.cs`) becomes a `list<X>` and keeps the concept's own work (goal: the store, the loading, `all` listing `.build/`); one X stays at `X/this.cs` and answers `Match` and `Current`. Module becomes an ICreate item (`module/this.cs:12`). **Settings** (section "Settings"): setting classes under their owners, one row per actor per class, loaded before the action runs; identity and permission move in; setup and `LlmCache` keep their own rows in `app.store`. Goal's name lookups go: `_byName` and `Get(string)`'s form scans (`goal/list/this.cs:23,52-61,68-120`); their other callers: `callstack/this.Snapshot.cs:212-213` (`Get(goalName)`, then `Get(goalPrPath)`: the PrPath scan at `goal/list/this.cs:96-105` exists for it), `goal/setup/this.cs:26,62` (`AllIncludingSetup`, `Add`). The bare-name lookup `call` uses (`GetAsync`, `:131-169`) is call's, and coder traces where it lives; `module/action/ui/code/Fluid.cs:391` also calls it, with no caller goal. **Members that collide with the base list** (count, first, add, remove, contains): goal's `list` (`:262`), `this[path]` (`:249-257`), `int Count` (`:316`), `Names`, `Public`, `Events`, and the stale "no app-level current" comment (`:264-267`); module's `All` (`:142`, differs from `all` only by case, and navigation ignores case), `int Count` (`:136`, counts actions), `Schema` (`:30`); actor's `this[Name]` (`actor/list/this.cs:23`); test's `Count` (`:115`), `Tests` (`:122`), `Current` (`:33`). Its property on `app` goes lowercase (`App.Goal` → `app.goal`, …). The builder reads `%!app.module.list%` (`Decide.goal:14`) and its templates walk `m.Action` / `m.Modifier` (`module/this.cs:67-71`), so the module type's shape is checked against them. **test (round 6, settled with Ingi):** today `test/list/this.cs` holds three things and `App.Test` is null when not testing (`app/this.cs:185`); they split four ways: **test's settings** `test/setting/this.cs` (`TimeoutSeconds` `:38`, `Parallel` `:41`, `Verbose` `:44`, `Format` `:47`, `Include`/`Exclude` `:51,54`; `--test={…}` applies onto them, `Executor.cs:79`); **test's list** `test/list/this.cs` as a `list<test>` (`Add` `:119`, `Tests` `:122`, `Count` `:115`, `Create` `:61`, `Exclusion` `:93`); **the test session, a session channel on the user actor by default; the actor is one of test's settings, system or user (Ingi; `actor: choice<actor.Name> = user`, the existing choice `actor/Name.cs:8-13`, which holds exactly those two)**: built on `channel/type/session/this.cs` (the kept-open, stateful channel base, `channel/this.cs:18-21`), so anyone can use one; open while tests run, and everything the tests write goes into it (replacing `test.run`'s before-write capture, `test/run.cs:126,135`); **test's report** `test/report/this.cs` (Ingi: "is that not part of report"): `StartedAt` `:27`, `Coverage` `:30`, `Summary()` `:125`, `Verdict()` `:139`, and it reads the session; the `test.report` action (`module/action/test/report.cs`) writes it out. `Current` (`:33`) becomes `ICurrent`, the test the asker's context is running. Starting tests is the verb: C# `app.test.Start()`, plang `test.start` (stage 1). `app.test` is a `type<test>` that always exists; "are we testing" is whether the test session is open, and app mode derives from it (`app/this.cs:196-199` already derives the mode from presence). Stage 8's `app.test.mock` belongs to the running tests (bindings scoped to the test's actor); stage 11's `app.test.coverage` is the report's | yes: the builder's own input; twins |
| 8 | **`on` on every object** (details below; `current` is the type's, stage 4): events move from `event.on(Trigger=…)` to the object, as `on.before.<verb>` / `on.after.<verb>` for every public verb, plus outcomes (`on.error`, `on.hit`, `on.miss`). The `on` module's actions are one-line doors: `on.event(item, when, event, action)` for any event, and `on.error`, `on.cache`, `on.timeout`, which replace today's modifiers as events bound on the action before them (Ingi). Bindings are scoped to the actor that registered them unless `scope: app`. Every plang value is born through its type's `Create`, which fires `create`. Payoff: value-level mocking (`- after file create, call LoadFixture`). **Other consumers of today's event wiring:** `module/action/debug/this.cs:183-214` (5 bindings), `channel/this.cs:120-170` and `channel/event/this.cs` (typed on `lifecycle.binding`), `module/Events.cs` and `goal.Events` (`goal/this.cs:28-34`), `mock/reset.cs:16,22` and `mock/this.cs:16` (`EventBindingId`), the `event.remove` and `event.skipAction` actions, `GlobalUsings.cs:8-9` (`Lifecycle`, `Bindings`). **Of the modifiers:** the builder templates read `m.Modifier` (`decider.state.template:47,72,83`, `properties.template:50,61,75,95`, `Properties.llm:45`) | yes: the `on` actions; twins + one eval run |
| 9 | **Module pass, with file.read as the template (Ingi):** fix file.read first and make it the worked example of a correct action, written up as a doc ("how an action is written"). Then go over every module against it, one module per commit. The checklist: (1) plang values are born through their type (`app.type.file.Create(…)`; `new` only inside the type); (2) `Start()` hands over to the owner in one line; (3) no opened box, no broken seal (no `.Value()` on what it returns or forwards); (4) errors are results; (5) properties are typed (`Data<T>`), and a property that names where to write is a variable; (6) events fire from the owner, not the handler | per module: twins where a prompt changes |
| 10 | **`%!app` holds its types:** built-ins register at startup, a plugin loaded with `code.load` registers its own (`%!app.stripe%`); `%!app.list%` lists them; one name, one type (a clash fails loudly) | no |
| 11 | **Tests through the app's own doors:** `new app.@this(test: true)`; `var file = await (await app.module.Get("file")).Value();` then `await file!["read"].Start(new { Path = "…" })` (a start of that action with those property values, through `action.Start`; `Get` answers `data<module>`, so one `await` more); a variable is set the way plang sets one, through the `variable.set` action (a variable lives in an actor's memory, `actor/context/this.cs:43`, so `app.variable` has nothing to set it in). The static helpers `TestApp`/`TestAction` die: about 1,710 uses in 404 files, so this is a large stage; `TestApp` also installs the no-crypto signing mock (`TestApp.cs:37-47`), which test mode must keep. The module's `["read"]` is the shared catalog action (`module/this.cs:80`), so `Start(new {…})` works on a copy. `App.Run<TAction>` retires here (two doors otherwise). The builder warns about goals no public goal reaches (dead code); test's report shows what the tests reached (its coverage) | a build warning |
| 12 | **Exception pass, before the branch closes:** go over every `throw` in the code this branch touched. A problem the programmer caused is an error in the result; an exception only ever means plang itself is broken. Most should already be gone by then (the name lookups become `Get(key)` doors answering NotFound; `Push` answers the overflow; the .pr readers return their error) | no |

Each stage is its own commits, green against the baseline before the next starts.

## Settings (stage 7), settled with Ingi 2026-09-27

- **A setting is a C# class under its owner** (Ingi: "define the settings in c#, because we can then build on top of that"): `goal/list/setting/this.cs`, `module/action/identity/setting/this.cs`, `actor/permission/setting/this.cs`, a module's own, and so on. Its properties are the options; their initializers are the defaults. The owner names it with `ISetting<TSetting>`, and the builder reads its properties and defaults for the LLM.
- **One settings table. A row is one actor's instance of one setting class (Ingi):** key `<actor>!<class path>` (`user!goal.list.setting`), value `Data<the class>`, whole, never split into fields. The actor is in the key because settings are per actor; today's permission grant carries its actor inside and is filtered by hand after reading (`actor/permission/this.cs:62-70`).
- **Loaded before the action runs (Ingi), in layers (Ingi chose (b)):** the actor's saved row, or the class's defaults when there is none → this run's own settings (`set %!x%` in the running goal, which shadows its caller's, as today: `actor/context/this.cs:128-135`) → the step's own values, for that call only. This is today's action-parameter order (step value → setting → default, `PLang.Generators/Emission/Property/Data/this.cs:143-161`) with the setting as one typed instance.
- **Read, write for this run, save:** `%!goal.list.setting.os%` reads the instance and steps into `os`. `- set %!goal.list.setting.os% = true` sets it for this run only, in the running context, as today (`module/action/variable/set.cs:137-145`). **Saving is its own step:** `- save %!goal.list.setting%` stores the instance whole in the actor's row (a `setting.save` action whose C# hands over in one line; `setting.remove` deletes the row, back to the defaults). A temporary tweak never becomes permanent by accident.
- **The user falls back to the system (Ingi: "setting first checks user, then gives system"):** a lookup goes the step's values → this run's `set %!x%` → the user's row → the system's row → the class's defaults; the system actor skips the user's row. **Exception: identity and permission never fall back** (a user with no identity would sign as system; a user with no grant would get the system's grants): their setting classes say so with a marker on the class (name to settle). Later, not now: whether the system can lock a setting against a user's override.
- **The machinery belongs to the actor (Ingi: "should be System.Setting, not app.setting"):** today's `app/setting/this.cs` (the chain, get/set by key, the this-run/saved split) is how every actor's settings work, so it moves to `actor/setting/this.cs`: one actor's settings, its saved rows plus the fallback to the system actor, with each context's this-run layer on top. The root is `System.Setting` (C#: `app.System.Setting`, `app.User.Setting`); `actor/context/this.cs:135` (`Parent?.Setting ?? App.Setting`) falls back to its actor's instead. `Executor.cs:130` (`llm.cache` off for this run) lands on `System.Setting`.
- **`app/setting/this.cs` is the app's own setting class (Ingi: "app.setting are different, they are the app.id and such"):** `id`, `name`, `create`, `environment`, like any owner's. The CLI's `--app={…}` lands there already (`Executor.cs:88`; `Create`, `app/this.cs:201-204`). The other CLI flags follow the same pattern: `--debug`, `--test`, `--build` and the callstack flags (`Executor.cs:64-116`) each apply onto their owner's setting class (`debug/setting/`, `test/setting/`, …).
- **Owned data is not a setting (Ingi):** setup keeps one row per executed step, written by setup, which knows its shape (`goal/setup/this.cs:130-143`); `LlmCache` is a data store (`llm/code/OpenAi.cs:39`). They keep today's store (`IStore`: tables owned by their owners, `Get`/`Set`/`Remove` by table and key, `module/action/setting/IStore.cs`), without its settings table, named **`store`** (Ingi): `app/store/this.cs`, reached as `app.store`. Follow-up after this branch (Ingi; `Documentation/Runtime2/todos.md`): plang steps save and load values through it (`- set %user.name% = "ingi", store by identity, encrypt idp`, `- load %user% by identity idp`), with one database per identity (the isolated data pattern).
- **Moving into settings:** identity (`identity/code/Default.cs:20,207-281`, one row per identity today → the identity setting's instance holds them), permission (`actor/permission/this.cs:23,62,99,129`), `app.setting`'s persistent side (`app/setting/this.cs:17,52,74`).
- **Dies:** `App.Setting` as the chain root (`app/this.cs:173`, `actor/context/this.cs:135`) and `app/setting/` as the machinery's home (→ `actor/setting/`); `App.SettingsStore` as the name (`app/this.cs:164`); the `settings`, `identity` and `permission` tables as separate key-value tables; `%setting.X%` (`actor/this.cs:87`; no `.goal` in `os/` or `Tests/` uses it); `Storage.InMemory` / `Storage.Persistent` as two lifetimes behind one door (`app/setting/this.cs:5-6`); permission's hand filter by actor; the `setting` module's get and set as they are today (`module/action/setting/get.cs`, `set.cs`: the key-value door), replaced by `%!…%` reads and `set %!x%` (coder checks `.goal` uses); the module keeps `save` and `remove` for the actor's row.

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

**A value is born through its type, so `create` fires every time.** Today plang values are born with `new` in at least 78 production places (`file/read.cs:72`: `new global::app.type.item.file.@this(path, Context, template)`), none passing the type, so `after file create` would miss them; a constructor can't fire it (it can't await, and an item stores no context). Stage 8 makes the type's `Create` door (`type/this.cs:206`, with overloads at `:289`, `:330`) fire `on.before/after.create`; stage 9 moves every birth onto it (`new` of a plang value only inside its type). In C# that's the file type (`await app.type.Get("file")`, a `data<type>` since stage 4) and its `Create(raw, context)`, which answers an untyped item, so each moved site unwraps and casts.

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

**`test.start`'s coverage and output capture** (`test/run.cs:126,135`, `Trigger.BeforeWrite` today): the tests' output goes into the test session (a session channel, stage 7), so no capture hook is needed; coverage is an ordinary after-start binding on actions, and it lands in test's report.

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

- **`current`** during a handler is the item passed in (`this`): `%!app.type.text.current%` is the text being created. (Round 6, open: `current` is defined on the collected `type<T>`; a value type like text is a plain `type.@this`, so this needs `current` on every type, answered from the running handler.)
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

A collected type's face is a summary (names only); detail comes by navigating to one element. `current` shows where something is in play. The facts are the start set; a type may add a fact that's worth showing.

**Docs entry is `start.md` (Ingi):** inside plang apps (`os/**` and every app a programmer writes), a folder's docs are `start.md`, as `Start.goal` is its entry. The four `readme.md` under `os/system/ui/templates/{uikit,default}/` and `os/system/modules/ui/Builder/templates/{uikit,default}/` are renamed in stage 5. The C# repo keeps `README.md` where GitHub or tools expect it, with a `start.md` beside it (Ingi): in the repo root, `PLang/`, `PlangConsole/`, `Skill/` and `tools/decider/`, `start.md` is the one edited and `README.md` is its copy, and a small test fails when the two differ. `.semgrep/`, `Documentation/v0.2/audit/`, `PlangTests/` and `.bot/**` keep `README.md` only.

**A comment says what the next code line does (Ingi).** A goal's description goes above its name, and a step's comment above the step. The parser already follows this (`goal/this.cs:584-599`: lines above the name are the goal's `Comment`, lines above a step are that step's). The `.goal` files don't: 356 of 594 in `os/` and `Tests/` describe the goal under its name, so the text lands on step 0. In stage 5:
1. Those files are fixed: the lines move above the goal's name (Edit tool; split across helpers as needed).
2. One member: `goal.Comment` is the description; `goal.Description` (never set by the parser; the build sets it, `build/code/Default.cs:213`, the `.pr` writer writes it, `goal/this.Item.cs:43`, and the reader reads it back; "Goal not found" at `:402`) goes. The goal face shows `comment`.
3. The goal's hash covers the source as written, comments included, so a comment-only change re-saves the `.pr` (every step still cached, no decider or LLM).
4. The rule goes into the plang docs, and a CLAUDE.md proposal covers bots writing `.goal` files.

**Honest facts come with the faces (stage 5).** Today only text and base64 declare a description; text's example is a filename (`readme.md`, `text/this.cs:33`); archive, binary and signature have placeholder examples (`(archive)`, `(bytes)`, `(signature)`). Each type gets a real description and a real example. Internal item classes under `type/item/` (`wire`, `source`, `clr`, `computed`) declare that they're not plang types and stay out of the face. `channel` and `serializers` carry `[PlangType]` but are collections, not choices; they're placed properly.

| face | shows |
|---|---|
| `%!app.type%` | `list` (type names) |
| `%!app.type.text%` | `name`, `description`, `example`, `alias`, `kind` (its kind names; `kind.list` answers the full types) |
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
| the deprecated v0.1 files under `os/`: `NN. stepname.pr` and `00. Goal.pr` (Ingi) | 0 |
| `Run` as the entry verb (runtime objects, handlers, the generator's emit) | 1 |
| `Registry.cs`'s five maps, `_catalogByName`, `_full`; `Register`, `RegisterRuntime`; the static `Loader` (`Register`, `SealedNames`, `ReservedCore`, `ReservedShadow`); `Get(string)`/`Clr(string)` as two doors; `Primitive.Aliases` as the registry's (onto each type), `Precision` (`type/list/this.cs:189-195`), the spelled-form parsing in the name door (`:161-169`); the registry's `Choice`, `Kind` and `Scheme` stores (`:40,48,56`, onto the types) | 3 |
| `item.list` as the name of an item's history (`type/item/this.cs:268-269`) and its folder `type/item/type/list/` (→ `item.history`, `type/item/history/`); `App.Type` in PascalCase (stage 2, folded in); the registry's stored `Context`; the throw on a type lookup miss (`type/list/this.cs:171`); `list<T>`'s loose constraint; `type.@this` being sealed; the registry as the class reached at `app.type` (it becomes `app.type.list`) | 4 |
| `type/list/view/` (whole folder) and `BuildTypeEntries`; `goal.Description` (`goal/this.cs:41-42`, its write in `build/code/Default.cs:206-214` and `goal/this.Item.cs:43`, its read in `goal/serializer/Reader.cs:54`); module's `Schema` (`module/list/this.cs:30,35`); the goal hash that ignores comments; the four `os/` `readme.md` (→ `start.md`) | 5 |
| `app/variable/path/` (`Parse`, `Segment` and its kinds, `Segment.Index.Key`'s string re-parse, `Segment.Call.Args`); the walker's switch and clr special case (`data/this.Navigation.cs:33-94`); `data.TryFullVarMatch`; static `text.HasVariable(string)`; `CleanName` ×2 (`data/this.cs:647`, `variable/list/this.cs:552`); the other parsers (`Formal.cs:311,418`, `debug/this.cs:506`, `pick/list/this.cs:94,132`); `InvokeMethod`'s string switch (`data/this.Navigation.cs:143-233`); `variable.@this.Convert`'s hand scan; the regexes in `step/this.Validate.cs:26`, `step/this.Scope.cs:9`, `pick/list/this.cs:90,96`; `data.HasVariableReference` | 6 |
| the per-concept `X.list.@this` as the class reached at `app.X` (goal, actor, module, test: each stays as the concept's `list<X>`, reached at `app.X.list`); their own name lookups and their throws (`goal/list/this.cs:243-244`, `:249-257`, `module/list/this.cs:118-121`, `actor/list/this.cs:23`: the type's `Get(key)` replaces them); the members that collide with the base list (stage 7's list); module's `list` member (`module/list/this.cs:125`: the class is the list); goal's `_byName`, `Get(string)`'s form scans and `All` (a second name for `list`, `goal/list/this.cs:23,52-61,68-120,311`) | 7 |
| `event.on`, `Trigger` as a list of moments beside the objects; the per-context binding lists (`context.LifecycleFor(…)`, called at `goal/this.cs:333`, defined at `actor/context/this.cs:425,448,472`; `app/event/lifecycle/`): bindings live on the item, each carrying its scope; `mock.intercept`'s wiring (replaced by `mock` → `app.test.mock`); the modifier concept: `modifier.list`, `Wrap`, the catch grouping, `[Modifier(Order)]`, `IModifier`, `ModifierAttribute`, `cache.wrap`, `timeout.after` and the `timeout` module (replaced by `on.error`, `on.cache`, `on.timeout` as events); `module.@this.Modifier` (`module/this.cs:71`) and the templates' `m.Modifier` walks | 8 |
| every direct `new` of a plang value outside its type (≥ 78 production sites); whatever else the checklist catches, per module | 9 |
| reflection over the C# `App` as `%!app`'s answer | 10 |
| `PLang.Tests/Shared/TestApp.cs`, `TestAction.cs` (statics) | 11 |

**Stays:** the `mock` module (its action now points to `app.test.mock`); `on.error` as the name of the error event's action; the postfix attachment rule (an `on` action binds on the action before it); the builder pipeline as built on builder-formal.

## OBP validation

| New or moved surface | plang path | C# | file | Check |
|---|---|---|---|---|
| the type named `type` | `%!app.type%` | `app.type` | `app/type/this.cs` (a `type<type>`) | over the registry; one way in (`Add`) |
| the list of types | `%!app.type.list%` | `app.type.list` | `app/type/list/this.cs` | a `list<type>`; no stored context; keeps the lookups by other keys (`Mime`, `Extension`, `[System.Type]`) |
| one type | `%!app.type["text"]%` / `.text` | `await app.type.Get("text")` | `app/type/this.cs` | owns its name, aliases, facts, `kind`, `on`; answers `Match` |
| a collected type | `%!app.goal%` (= `%!app.type.goal%`) | `app.goal` | `app/goal/this.cs` (a `type<goal>`) | one generic class for every concept; no class per concept |
| a concept's list | `%!app.goal.list%` | `app.goal.list` | `app/goal/list/this.cs` | a `list<goal>`, like `step.list`; holds the concept's own work (loading) |
| the one in play | `%!app.goal.current%` | `app.goal.current(context)` → `data<goal>` | `app/type/item/ICurrent.cs`; `goal.Current` | one item, the element's `static virtual`, the ICreate pattern; 404 when none |
| which list holds them | `%!app.goal.list%` | `goal.List(app)` | `app/type/item/IList.cs` | its own interface, one question; default a plain `list<T>`; the name is shared with .NET's `IList<T>` |
| picking one | `%!app.goal["/show"]%` | `await app.goal.Get("/show")` → `data<goal>` | `app/type/this.Generic.cs` | `await foreach (var p in list.all())`, the first `Match` that answers; async (may load a `.pr`); no C# indexer; a miss is a 404 result |
| an item's history | `%config!history.list%` (the `!` hop reaches a value's members) | `item.history` | `app/type/item/history/this.cs` | its `list` is the values the item was made from (wire → source → dict); `list` itself is each type's own |
| who a key names | — | `await x.Match(key)` → itself, one of its own, or none | `app/type/item/IMatch.cs` | the element's knowledge (goal: its address, a sub-goal `/start#show` through its parent; type: name or alias); no raw `bool` on an item's public member (PLNG003) |
| a sub-goal's address | `%!app.goal["/start#show"]%` | `goal.Address` | `app/goal/this.cs` | the file's address + `#` + its name; the parent is set where the child is added; visibility derives from the parent |
| a variable | `%user.name%` | `app.type.item.variable.@this` | `app/type/item/variable/this.cs` | `text` + `code`; `Value()` → `Start()` → `Code.Start()` |
| the parser | — | `app.type.item.variable.parser.@this` | `app/type/item/variable/parser/this.cs` | the only definition of a reference |
| an item's variables | — (a value's own) | `item.Variable` | `app/type/item/this.cs` | read-only, born whole; the shared empty list when none |
| the type named `variable` | `%!app.variable%` | `app.variable` | a `type<variable>` | its list is the memory of the actor in play, reached through navigation's context (as `current` is) |
| registering an event | `- after text create, call LoadText` | `on.event(item, when, event, action)` → `Item.on[When][Event].Add(…)` | `app/module/action/on/event.cs` | one action for every item; scoped to the registering actor |
| an object's events | `%!app.type.text.on%` (read) | `x.on` | `app/event/before/<verb>/this.cs`, `app/event/after/<verb>/this.cs` | registered by a step through the `on` module; the shared empty `on` until bound |
| a value's birth | — | the type's `Create(raw, context)` | `app/type/this.cs` (`Create`) | the one door; fires `on.before/after.create`; `new` only inside the type |
| a mock | `- mock http url=…, call MockExample` | the `mock` action → `app.test.mock` | `app/module/action/mock/…` → `app/test/…` | a before-start binding with a filter, born cancelling, scoped to the actor |
| how an action is written | — | — | a doc, file.read as its worked example | the module pass's template |
| starting an action from C# | — | `(await (await app.module.Get("file")).Value())!["read"].Start(…)` | `module/this.cs` | through `action.Start`; nothing test-only |
| every goal | `%!app.goal.list.all%`, `…all(setting: {os: false})%` | `app.goal.list.all(setting)` | `app/goal/list/this.cs` | one signature on every list; `.all` = every default |
| an actor's settings | — (read through `%!…%`) | `app.System.Setting`, `app.User.Setting` | `app/actor/setting/this.cs` | the chain: this run's layer → the actor's rows → the system's (for the user) → defaults; identity and permission never fall back |
| the app's settings | — | `app.setting` | `app/setting/this.cs` | the app's own setting class (`id`, `name`, `create`, `environment`); `--app={…}` lands here |
| a stored setting | `%!goal.list.setting.os%`; `- set %!goal.list.setting.os% = true` (this run); `- save %!goal.list.setting%` | the settings table, key `user!goal.list.setting` → `Data<goal.list.setting.@this>` | `app/goal/list/setting/this.cs` | one row per actor per class, the instance whole; loaded before the action runs: saved row or defaults → this run's → the step's values |
| a list's settings | `all(setting: {os: false, visibility: [public, private]})` | `await setting.Value<goal.list.setting.@this>()` | `app/goal/list/setting/this.cs`; `app/type/item/ISetting.cs` | a C# class under its owner; a missing key is the property's default; the owner names it with `ISetting<TSetting>`; the builder reads its properties and defaults |
| a goal's description | `%!app.goal["/show"].comment%` | `goal.Comment` | `app/goal/this.cs` | the lines above the goal's name; the only description member |
| names | — | — | — | no verb+noun; verbs `Start`, `Add`, `Load`, `Create`, `Match`, `Get`, `Save`; the element's `Current` and `List` named for what they answer; plumbing `Variable`, `Code`, `Alias`, nodes `on`, `current`, `list`, `all`, `kind`, `history`: one word each; nodes lowercase, verbs PascalCase |

## Open for the next round

Rounds 1–4 closed. Round 4 added: one `on.event(item, when, event, action)` for every event, bindings scoped to the registering actor, and binding/firing as verbs (`Add`, `Start`).

Round 5 (2026-09-26) settled every `app.X` as the type X: one generic `type<X>` over the concept's `list<X>` (no class per concept, no `X/type/` folder), `IMatch`, `ICurrent`, `IList` (one question per interface), `list<T>.First` (dropped in round 6: `Get` walks the list), the strict `list<T>`, goal keyed by its address, bare names left to `call`, and the choice/kind/scheme stores moved onto the types. Parked for after this branch: `goal.PrPath` → a `pr` object (`pr.path`, later `pr.encryption`; `Documentation/Runtime2/todos.md`). Also settled (Ingi): `%!app.goal%` and `%!app.type.goal%` reach the same object; `app.X = new(this)`, the name from X's class and the list from X (`List(app)`); variable's list, the asker's memory (`actor/context/this.cs:43`), is reached through navigation's context, as `current` is (a `list` getter has no context; coder traces the C# shape); `all(private, os)`, two yes/no settings (replaced in round 6 by `all(setting)`). Nothing open from round 5.

Round 7 (2026-09-27, the architect's read-over): wording left from round 6 fixed (leftover `First`, `["key"]` and indexer calls, `%setting.X%` in stage 10 contradicting Settings, the variable row's C#, paths, the `os` examples). **Open, for Ingi:** (A) `current` on a value type (text) during an event handler, now that `current` sits on `type<T>`; (B) where the `mock` action hands over, now that `app.test` is the generic `type<test>` with no `mock` member.

Round 6 (fresh eyes; closed 2026-09-27, every point below settled with Ingi). Settled: the item's history is `item.history` with its `list`, so `list` is each type's own; `["key"]` answers `data<X>`, its C# callers sort into three groups, and a caller that can't await becomes async (the `.pr` reader keeps the name; the async load above it looks it up). The review's factual fixes are applied (citations, paths, missed callers, stage order: stage 1 skips code later stages delete, stage 2 folds into 4, `IMatch` arrives in stage 3). **Open, one at a time with Ingi** (all checked in the code):
1. ~~**Stored twice**~~: settled, the list is the one store; lookups walk it or use `where` (see "The concept's own work lives in its list").
2. ~~**The indexer can't load**~~: settled, `Get(key)` is the one async door and the C# indexer goes (see "Members of a collected type").
3. ~~**`all` isn't on the base list**~~: settled, the base list's `all(setting)` answers itself and goal overrides it; the settings are a C# class under their owner (`goal/list/setting/this.cs`), named by `ISetting<TSetting>` (see "`list` is a real object"). Both visibilities (Ingi): `visibility` is a list of goal's own choice, default `[public]`; `all(setting: {visibility: [public, private]})` gives both.
4. ~~**List navigation sends an unknown name to the first element**~~: settled, `all` is one of the list's own words, checked before the empty check (see "`list` is a real object").
5. ~~**Two kinds**~~: settled, one concept and one member: a type's `kind` (today's `Kind`), whose `list` answers full types with their kind (`type/kind/list/this.cs`); `Kinds` dies; the global store becomes item's kind list (see stage 3).
6. ~~**Sub-goals share their file's address**~~: settled, a sub-goal's address is `/start#show`; `Match` answers the element (itself or one of its own); the parser sets the parent where it adds the child; visibility derives from the parent (see "Members of a collected type").
7. ~~**test is a session**~~: settled (Ingi): test's settings (`test/setting/`), its list (`list<test>`), the test session (a session channel on the user actor; the tests' output goes into it) and test's report (`test/report/`: start time, coverage, summary, verdict; reads the session); `test.start` and `environment.start` (see stages 1 and 7).
8. ~~**variable** needs the asker's context~~: settled (Ingi: "app.variable has the context, just use that"): `%!app%` sits in the actor's memory with its context (`actor/this.cs:90`) and every navigation step passes its parent Data with it (`type/item/this.cs:235`), so the variable type's navigation reads `parent.Context.Variable` (`actor/context/this.cs:43`); C# code uses its own `context.Variable`; `Get(key)` takes no context. The memory moves beside the element (Ingi): `app/variable/list/this.cs` → `app/type/item/variable/list/this.cs` (stage 6), so `%!app.variable.list%` and its folder agree.
9. ~~**Registering `type<X>`**~~: settled (Ingi). The App constructor, which already builds everything in order (`app/this.cs:274-284`), builds `type = new(this)` first (the registry loads the plain types by its scan), then each concept type (`goal = new(this); type.list.Add(goal);`, and module, actor, test, variable the same way), each taking its name's scanned entry's place: one entry per name (stage 3's `Add`). A concept type's facts come from its class `T`, as a scanned type's do (stage 3). Equality by name/kind/strict (`type/this.cs:397-401`) makes `type<goal>` equal a plain `type("goal")`, which is right: a copy navigates to the registered one (`type/this.cs:496`). The scan's claim of `goal` for the value class `goal.@this` (`Registry.cs:214-223`) stays; the open generic claims nothing (`:237-238`).
10. **Smaller:** ~~`First` would mean two things~~ (gone: `Get` walks the list, no new `First`); ~~`data<T>.Ok` is born without context~~ (settled, Ingi: the type's navigation step builds its Data from the parent, `new data.@this(key, item, parent: parent)`, which takes the parent's context, `data/this.cs:223-233`; C#'s `Get(key)` stays context-less; `current(context)` births its Data with the context it was given); ~~`Match` returns a raw `bool`~~ (gone: it answers the element); ~~the indexer as a middleman over `list`~~ (gone: `Get` walks the list and the element answers). **os goals in `all` (settled):** `/system/…` isn't linked into the app: path resolution falls back to `<os>/system/…` one path at a time, the app's own copy winning (`path/file/this.Validate.cs:50-58, 72-81`; `goal/list/this.cs:172-177`), and a listing of `.build/` doesn't. **Settled (Ingi): `all` returns one list, every goal available, including the system goals when `os` is true, each entry already resolved: an address points either at the app's goal or at `/system/…`'s, the app's copy winning, as resolution does.** The old v0.1 step files `NN. name.pr` are deleted in stage 0 (Ingi), so the listing never meets them. **`os` defaults to true (Ingi),** so `all()` is every goal an address reaches and `Get("/system/…")` finds what `call /system/…` finds; the dead-goal warning asks `os: false`.
