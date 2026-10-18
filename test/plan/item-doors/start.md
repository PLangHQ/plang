# item-doors

**Status: designed (decisions 867–872); waiting for Ingi's go before the line starts.** Nothing is built.

Traces to: the conversation with Ingi on 2026-10-17, logged in `.bot/item-doors/architect/summary.md`. The branch is cut from runtime2 (c47265322), with kind-is-type merged in. It was called "branch A" until now. llm-provider and llm-writer are parked waiting for it, because their clean answers kept landing here.

## Why

Three things in the core make every new feature build around them:

1. **A value made by name gets a bare type.** It has the kind's name, but none of the kind's facts (its mime, its format). So each type patches around it: the image keeps its own copy of the mime, and text checks its own type's name.
2. **A value writes through two doors and picks the writer's door itself.** It asks `Owns(writer)` and reads the writer's string `Format`, and the writer checks what the value is.
3. **A writer can't learn what a value is,** and schemas come from switches on type names.

## Scope: all five groups (Ingi, 2026-10-17: "yes all 5")

### 1. A type is born whole (agreed)

The type that makes a value hands itself, the registered one, to the value, which keeps it. `type`'s constructor no longer makes a kind from a name (`type/this.cs:146–149`); a type named in a step is looked up in the list. The `Type` overrides go (48 files, 29 of them in `type/item`), and so do the image's `_mime` (asked of its kind), text's name floor (`source.cs:115`), the render fix's `Name: "item"` check, and the "is this an action slot?" name checks.

```csharp
// item/this.cs: the value holds the type that made it
protected internal type.@this Type { get; }                 // given at birth, never rebuilt
protected @this(type.@this type) => Type = type;

// image: no copy of the mime, no kind made from a name
public @this(byte[] bytes, type.@this type) : base(type) => _bytes = bytes;
public string Mime => Type.kind.Mime.FirstOrDefault() ?? "application/octet-stream";
```

Also from kind-is-type: the four strict-kind tests (F-SC-1), which wait on this.

**No value is made without its app (880).** Ingi: "why would the be a type created without app, app always exists"; "maybe we need to remove Implicit conversions to plang type, fine to clr type. can't we give Create(raw) the app" → "yes". There were 57 context-free births: 34 implicit operators and 23 `Create(raw)` pure cores.
- The implicit conversions *into* plang types go (28 that make a plang value from a C# one). The ones out to C# stay.
- `Create(raw)` takes the type that makes the value, called by the type's `Make`, the one door (796):

```csharp
public static @this? Create(object? raw, type.@this type)           // today: Create(object? raw), no context
var value = await context.App.type.list["text"].Make("x", context);  // the type is looked up once, in the type
```

The coder's option of a cached stand-in stamped later by `Create` was ruled out: it's the late-stamp smell and it keeps the bare type.

### 1b. A list's rows are made when read (agreed, 890; built after stage 1's step 3)

Ingi spotted `_hasWrapped` in `list/this.cs:64` ("everytime I see some bool or name like wrapped, it feels wrong design"). Today a list can hold a C# list's rows raw. A row read is made into a value and written back into its slot (`Element`/`Keep`, `:178–192`), and the flag records that a slot has changed, so a never-touched list can go back to C# as the same object (`:653`). The list's own comment says rows are never cached back (`:31–35`), so the comment and the code disagree. Making every row up front is wrong for a billion rows (Ingi). Neither is two fields for the rows (Ingi: "_source and _own is double storage").

The shape:
- **One storage.** `_items` holds the rows: raw as given, plang values once written.
- **A read** makes the row through the list's element type and keeps nothing: `this[row, context] => Type.kind.Make(_items[row], context)`. In plang a row is always a plang value (`%rows[0]%`, `foreach`).
- **A write** puts the value in its slot.
- **Saved or sent,** a plang row writes itself and a raw row is written as it is, never made into a value. A list that's read and saved, never touched, is never changed. The one `is` in `Output` is the edge between a raw C# row and a plang one.
- **Back to C#,** a view lowers each row as C# reads it. No copy, no walk, no flag. It gives up today's "the same C# object back".

```csharp
private readonly List<object?> _items;
public Data this[int row, actor.context.@this context] => Type.kind.Make(_items[row], context);
public IEnumerable<Data> Items(actor.context.@this context) { for (var i = 0; i < _items.Count; i++) yield return this[i, context]; }
public void Set(int row, item.@this value) => _items[row] = value;
public override async ValueTask Output(IWriter writer, View mode, actor.context.@this context)
{
    writer.BeginArray(_items.Count);
    foreach (var slot in _items)
        if (slot is item.@this value) await value.Output(writer, mode, context);
        else writer.Value(slot);
    writer.EndArray();
}
internal override object? Clr(System.Type target) => new lowered(_items, target);
```

**Goes:**
- `_hasWrapped` and its 10 uses;
- `Keep`'s write-back on read;
- `Born` and `Row`, two doors for one question, become the element type's `Make`;
- the same-object exit (`:653`).

**To check:** a write through a row that was read (`set %rows[0].name% = "x"`) reaches the list through `Set`.

### 2. One door for writing (agreed)

`Output` is the only door. Each type's own `Output` writes what it is: its one token, or its members. The sync `Write` (overridden in 46 files, 36 of them in `type/item`) goes, and so does the `IsLeaf` branch in `Output` (`item/this.cs:906–913`; Ingi: "I like that IsLeaf check goes away").

- **The writer carries its output kind**, an object, not `string Format`.
- **`Owns` and its 5 checks go.**
- **The byte door** (844, Ingi's yes): `Bytes(byte[] value, kind kind)` replaces `Bytes(byte[])` and `Raw(byte[])`.
- **The pair registry** (846, Ingi's yes): what an input kind looks like in an output kind, when it isn't the default. json in json is copied as is; a list in formal is a list of arguments, which replaces list's `_formats`. It's looked up in one place, `data.Output`.
- **The json writer's `Value(object)`** keeps only C# primitives.

### 3. A type speaks for itself to a writer (agreed)

- **Data always tells the writer what it is, and each writer decides how much to show.** The record door already exists (`BeginRecord(data)`, `kind/json/writer.cs:69`, `text/writer.cs:84`), so the `EmitsSchema` bool (`IWriter.cs:36`) and Data's two `if`s on it (`data/this.Output.cs:83`, `:126`) go. That's 813.
  - Bare json shows only the value.
  - plang is its own output kind with its own writer: `{"type", "value", "signature"}` on the wire, plus `name` only in the Store view. The wire already leaves the name out (`data/this.Output.cs:94–99`; Ingi: "name should now be serialized on the data class" → "good").
  - The llm table learns a column's type from the cell's record (815).
- **When a type looks different in another output kind, that lives in the type's own folder** (`type/<name>/serializer/<kind>.cs`, discovered as readers are, `type/reader/this.cs:14–18`), as #2's pair-registry entry, through `IOutput` (`type/format/IOutput.cs`).
  - The first entries are each type's JSON Schema (todos.md 2026-09-23), used by llm-provider's tools: number → `"number"`, its int kind → `"integer"`, path → `"string"`.
  - plang's own word rides in JSON Schema's free `format` field (`{"type": "string", "format": "path"}`). OpenAI checks `type`, so plang's word can't go there (Ingi: "can we not use the plang types … if not that is ok").
  - `MapPlangTypeToJsonSchema` (`llm/code/OpenAi.cs:795–816`) goes.

```csharp
// data/this.Output.cs: will become
var value = Error ?? _item;                   // what this Data holds: its value, or its error (today `held`; Ingi: "name it what it is")
writer.BeginRecord(this);                     // the writer sees the Data: its type, its signature (its name in Store)
await value.Output(writer, mode, context);    // the value writes itself (#2)
writer.EndRecord(this);

// kind/json/writer.cs: bare json shows only the value
public void BeginRecord(data.@this record) { }
public void EndRecord(data.@this record) { }

// wire/kind/plang/writer.cs: NEW, plang's own writer, its own output kind
public void BeginRecord(data.@this record)
{
    _json.WriteStartObject();
    if (_view == View.Store) { _json.WritePropertyName("name"); _json.WriteStringValue(record.Name); }
    _json.WritePropertyName("type");  record.Type.Output(this, _view, record.Context);
    _json.WritePropertyName("value");
}
public void EndRecord(data.@this record)
{
    if (record.Signature is { } s) { _json.WritePropertyName("signature"); s.Output(this, _view, record.Context); }
    _json.WriteEndObject();
}

// module/llm/table.cs: a column's type from the cell itself (815)
public void BeginRecord(data.@this record) => _column ??= record.Type;

// type/item/number/serializer/schema.cs: NEW, a number's type as a JSON Schema
public sealed class schema : IOutput
{
    public ValueTask Output(item.@this type, IWriter w, View mode, actor.context.@this? context)
    {
        w.BeginObject();
        w.Name("type");   w.String("number");
        w.Name("format"); w.String(type.ToString());      // plang's own word: number<double>
        w.EndObject();
        return ValueTask.CompletedTask;
    }
}
// type/item/number/kind/int/serializer/schema.cs: "integer" · type/item/path/serializer/schema.cs: {"type": "string", "format": "path"}
```

> **For the coder: you own this code.** These are sketches of intent, agreed with Ingi piece by piece. Where the real code says another shape is better, bring it to the architect.

### 4. A type's members through its own list (agreed)

Today reading and writing a member uses C# reflection (`item/this.cs:277–311`: the clr carrier, `[LlmBuilder]` members invoked and their tasks awaited by reflection; `:209–245`: `Reflect`, `Activator.CreateInstance`, `SetValue`), not the type's own member list, `type.Property` (`type/this.cs:642`), which is what the builder offers. So the two lists can disagree. A member a value lacks throws (`:224`). `IsDefault` decides by type names and text (`type/property/this.cs:118–138`).

```csharp
// item: read and write a member through the type's own list
public virtual async ValueTask<data.@this> Get(data.@this parent, string key)
    => Type.Property?[key] is { } member ? await member.Read(this, parent) : await Setting(parent, key) ?? Kept?[key] ?? parent.Context.NotFound(key);

public virtual async ValueTask<data.@this> Set(string key, object? value, actor.context.@this context)
    => Type.Property?[key] is { } member
        ? await member.Write(this, value, context)          // one of its own members: Ok, or why it couldn't
        : await Add(key, value, context);                   // a member a program adds, kept beside its own (where: open)

// property: NEW. A member reads and writes itself on its owner
public async ValueTask<data.@this> Read(item.@this owner, data.@this parent) => …;
public async ValueTask<data.@this> Write(item.@this owner, object? value, actor.context.@this context) => …;

// property.IsDefault: the value asks itself, against the default made as this property's type
public async ValueTask<bool> IsDefault(item.@this? value, actor.context.@this context)
    => Default != null && value != null && await value.Compare(Type.Make(Default, context), context) == 0;
```

**A key the type doesn't have is added, not refused** (Ingi: "`- set %user.blabla% = 12`, will this throw error? because it should add the blabla property to the user object (however we will do that and where is a different question"). Today:
- a dict adds the key;
- a thing that lives on (app, call, actor, module, goal, step) keeps it in `Kept` (`item/this.cs:213–221`, `:286`);
- any other value throws (`:224`). That throw goes.

The only refusal left is writing over a member of its own that can't be written (today's `OwnMember`). Where an added member lives for a plain value, and how, is open.

Reflection stays only where `property` is built from a C# class (`property/this.cs:21`). It's locked: `item`'s `Get`/`Set`, and `property`'s `Read`, `Write` and `IsDefault`.

### 5. Reading a plang value as tokens (agreed)

A reader reads tokens, but the only reader over a value already in hand is scalar-only (`type/format/value/reader.cs:14–17`, `:60–67`). So a parsed dict is written out as json just to be read back. There are two cases:

- **The builder's answer.** It arrives as a dict, and `Step.Set` builds each action through three branches on what the row is, the last one being `FromJson`'s write-then-read (`goal/step/this.Item.cs:48–77`).
- **A dict going into a module's record slot,** for example llm.query's `limit: { token: 1000, tool: 5 }` (`llm/type/limit/this.cs`). It falls back to C# lowering (`item/this.cs:241`). (Ingi caught my made-up `as order`: plang has no such type, a dict is a dict.)

```csharp
// type/format/item/reader.cs: NEW. A plang value read as tokens: a dict as an object (its keys, then each value),
// a list as an array, any other value as its own token.
public struct Reader : IReader
{
    public Reader(item.@this value, actor.context.@this context) { … }
    public void BeginObject() => …;
    public bool NextName(out string name) => …;
    public void BeginArray() => …;
    public string String() => …;
    …
}

// goal/step/this.Item.cs: one way for every row, no branches on what it is, no json round trip
foreach (var row in Rows(incoming, context))
{
    var tokens = new type.format.item.Reader(row, context);
    node.Add(reader.Read(ref tokens, null, new ReadContext(context)) as action.@this
             ?? throw new AppException("cannot build an action from a …", "NotAnAction", 400));
}
```

What goes: `FromJson`, the `is dict or list` branch in `Step.Set`, and `item.Set`'s C# lowering fallback. It's locked: a new implementation of the serializer's `IReader`.

### 6. Settings store data and give it back (agreed, 892; 894)

Ingi asked for an OBP scan of settings ("we havent really done proper obp scan of the setting"). They work, but beside the type system, not through it. Ingi: "yes, lets do that". Then (894): "setting is just data, we dont really care what is in it, it´s the responsiblity of the user to 'convert' it (with something like Get<T>) to the right form, settings is just about storing and retrieving".

So the settings list keeps data by its address, in layers (this scope's, then the actor's saved row, the user's before the system's), and gives it back. It never looks inside: no check on a set, a save or a flag. The caller converts. `Get<T>()` reads the data at T's address (its type's name) and T's type makes a T from it, with T's own defaults for what the data leaves out. A bad row, or a member T can't take, is the answer's error.

```csharp
// actor/setting/this.cs, what stays (the bodies are the coder's)
public async Task<data.@this> Get(string address)               // the closest scope's data, else the saved row,
                                                                 // read from the store the first time it is asked; NotFound when none
public async Task<data.@this<T>> Get<T>()                        // NEW (854): Get(T's type's name), made into T by T's type
public data.@this Set(string address, data.@this data)          // this scope's, whole
public Task<data.@this> Save(string address, data.@this data)   // the actor's row, whole
public Task<data.@this> Remove(string address)
```

A member write, `set %!llm.setting.cache% = true`, goes through the variable path. It reads `llm.setting`'s data, sets `cache` on a copy (#4's `Set`, which adds a member the data lacks), and the settings list keeps the whole data in this scope. No path string is split. An action's fallback to its settings (`Get(action, option)`, `:102`) reads the action's address, then the module's, each data's member, else the action's default.

What goes:

| what | where | why |
|---|---|---|
| the setting's own member system, all reflection: `Options` (plural, a bare `IEnumerable<PropertyInfo>`), `Option`, `Switch`, `Apply`, `IsComposite`, `Construct`, `Copy`, its `Get`/`Set`/`Write`/`Next` overrides, `Create`'s option walk | `type/item/setting/this.cs:53–283` | a settings class is a plain type: its members are `Type.Property` (#4), and its type makes it (#1) |
| `Rows`, the teaching rows | `setting/this.cs:290–306` | the type's `Property` list |
| the address cut out of the C# namespace (`StartsWith("app.module.")`) | `setting/this.cs:23–32` | the address is the type's name |
| `Enabled` and `Disabled`, two names for one fact | `setting/this.cs:41–47` | one name |
| the option checks in the action and module nodes (`NotSupportedException`) | `setting/action/this.cs:39–52`, `setting/module/this.cs:27–37` | the store doesn't look inside. Whether the two classes stay as the address is traced in stage 0 and comes back |
| this run's values as flat `"path.option"` strings in a plural `_values` dict; `Flatten`; `Under`'s split on `.`; `Set(path, IDictionary)` and its `Apply` check | `actor/setting/this.cs:19`, `:117–158`, `:403–431` | this scope's data by address, whole |
| `_built`, `_version`, `Root`, `Move`, `Build`, `this[path]` (a built-setting cache) | `actor/setting/this.cs:27–47`, `:275–306` | nothing is built; `Get<T>` converts when asked |
| six doors (`Of<T>()`, `Of(owner)`, `Of(type)`, `Of(Type)`, `Of(module)`, `Of(action)`), `Named`, `Instance`, `Fresh`, `Make`, `Class`, the `_classes` index made by making one of every settings class, the static `Path<T>` | `actor/setting/this.cs:308–398`, `:347–368` | one `await Get<T>()` |
| every row read when the app starts: `Load`, `_rows`, `_held`, `_unread`, `_reading` | `actor/setting/this.cs:21–26`, `:79–94`, `:235–268`, `app/this.cs:406` | read on use |
| `Option(row, name)`'s switch on what a row is; `Save`'s `NotASettingClass` refusal | `actor/setting/this.cs:160–180`, `:223–231` | the store doesn't look inside |
| `Own` and `OwnAttribute`: no class carries it | `actor/setting/this.cs:219–220`, `setting/Own.cs` | dead |
| the throws (`Owner`, `Build` twice, `Apply`'s four caught exception types) | `actor/setting/this.cs:203`, `:292`, `:295`, `:304`; `setting/this.cs:221–227` | the error is the answer |
| the row key `"{actor}!{path}"` built and parsed in three places | `actor/setting/this.cs:174`, `:189`, `:238` | owned once |

The settings door and read-on-use were agreed for llm-provider (its 854). They're core, so they're built here, and llm-provider uses them. A setting asking for itself (`[Ask]`) stays llm-provider's: it runs in `Get<T>`, where the conversion is.

Each settings class is born with its type through a one-line constructor (891), part of stage 1.

### 7. Collections are singular, with their own list (893, 894)

Ingi: "Errors, Values og Options dont follow the correct pattern. should be sigular with .list", then "and any other plural you see". A collection is a singular property naming the concept, of its own `X.list` type (`Type.Property`). Never a plural name or a bare `List`/`Dictionary`/`IEnumerable`. Each is fixed in the stage that already touches its file. Plang-visible names and locked surfaces are marked; they wait for Ingi's yes.

| what | where | the fix | stage |
|---|---|---|---|
| `property.Arguments`, a bare `IReadOnlyList` | `type/property/this.cs:69` (4 uses) | `Argument`, a `property.list` | 5 |
| `type.Values`, `OwnValues`, `_values`: a choice's words as strings, beside `Offers`, which gives the same options as items. Plang-visible (`%x!type.values%`), locked | `type/this.cs:668`, `:676–677`; overrides in `choice/set/this.cs:35`, `choice/set/member/this.cs:10`, `choice/set/family/this.cs:23` | one door: `Option`, a list of plang values on the kind; `Offers` reads it for a closed kind | 5 |
| the static `Alias` on text, list, dict, bool and binary, read by its name through reflection in two places; the kinds declare theirs through `OwnAlias` | `text/this.cs:68`, `list/this.cs:29`, `dict/this.cs:27`, `bool/this.cs:17`, `binary/this.cs:153`; read at `type/this.cs:805`, `type/list/Registry.cs:149` | the type is born with its alias (#1), declared once on `[PlangType]` (`[PlangType("text", "string")]`). Locked (an attribute) | 1 |
| `item._identities`, a static cache | `type/item/this.cs:563` | goes: the registry asks once while it builds the type, and the type keeps the answer | 1 |
| `number`'s static `Kinds` dict | `number/this.cs:72` | goes with step 2 (kinds in each app's list) | 1 |
| `item.Fields` and dict's override | `type/item/this.cs:646`, `dict/this.cs:426` (6 uses) | goes: the type's `Property` list names the members (#4) | 5 |
| `dict.KeyNames` (plural and glued) | `dict/this.cs:154` (3 uses) | goes: the dict's walk below | 5 |
| four names for one walk: `list.Items(context)`, `dict.Entries(context)`, `item.Rows(context)`, `EnumerateItems` (also verb+noun) | `item/this.cs:347`, `:395`; `list/this.cs:277`, `:313`, `:325`; `dict/this.cs:165` (about 180 uses) | one walk on `item`, overridden by each container. Its name is Ingi's | 1b |
| `Registry.Assemblies` and `reader.Assemblies`: public bare `List<Assembly>`, the same list twice | `type/list/Registry.cs:27`, `type/reader/this.cs:56` | one, singular, of its own list type, owned by the app's type list | 2 |
| `table.Headers`, names only. Plang-visible (`%t.headers%`) | `type/table/this.cs:36` (31 uses) | the table's columns as a `property.list`, each a name and its type (#3 labels a column by its type) | 3 |
| `data.Properties` and its class `app.data.Properties`; `property.Properties`. Locked (data) | `data/this.cs:205`, `type/property/this.cs:145` (241 uses) | a singular name. The name is Ingi's | 2 |

Not in this branch's files (a later sweep, its own list): `call.Errors`, `Diffs`, `Tags`, `Arguments` (`call/this.cs:120–258`); `call.list.Variables` (`call/list/this.cs:81`); `error.Params`, `CallFrames`, `Variables`, `HelpfulLinks` (`error/this.cs:61–168`); `app.Services` (`app/this.cs:247`); `service.Channels`, `channel.Channels`, `channel.list.ChannelNames` and the static `Defaults` (`channel/list/this.cs:47`, `:153`); `module.ActionNames` (`module/this.cs:220`); `goal.setup.Goals`; `pick.question.Values`; `signature.Contracts`; the static `permission.AllVerbs`; `build.Files`; `snapshot.Entries`; the static `Choices` (`data/Operator.cs:80`, `module/code/kind/this.cs:16`, `test/format/this.cs:20`); `RequiresCapability.Capabilities`; `note.IAbout.Parts`; the screen and terminal modules' internals (`Codecs`, `Globals`, `Types`, `Fds`, `Tools`, `Rules`). llm's message `Images` and `ToolCalls` go with llm-provider, which rewrites the message.

## What the developer gets

The same plang, the same output: every `.pr`, wire and json write stays byte-equal. Three things change for a developer:
- **`set %user.blabla% = 12` adds the member** to a plain value, where today it throws (#4).
- **A strict kind can be checked,** so `as image/gif strict` works (F-SC-1's four tests go green, #1).
- **The llm table labels a column by its real type** (path, duration), not "text" (#3). That shows once llm-writer resumes.

## Stages

Each stage ends with its tests built and run, and each test must go red without its change. Each stage's shape comes to the architect before it's built.

0. **Trace.** Every member the demolition list names, with its callers, on the current tree. Lines drift; the names below are the truth.
1. **A type is born whole** (#1). Everything else stands on it.
2. **One door for writing** (#2): `Output` only, the byte door, the writer's output kind, the pair registry.
3. **A type speaks for itself to a writer** (#3): the record door always, plang's own writer, the JSON Schema entries. It needs #2's registry.
4. **Reading a plang value as tokens** (#5). It comes before #4 because #4's `Write` reads a dict into a record through this reader.
5. **A type's members through its own list** (#4).
6. **Settings store data and give it back** (#6). It needs #4: a member write and `Get<T>`'s conversion go through the type's members. Then the list's rows made when read (#1b).

The plural names (#7) are fixed in the stage named beside each.

## Demolition

The verdicts are the contract; the order inside a stage is the coder's. If a verdict looks wrong against real callers, flag it back; never keep a member quietly.

**Stage 1:**
- [ ] `type`'s constructor making a kind from a name (`type/this.cs:146–149`, `new type.kind.@this(kind)`); a type named in a step is looked up in `app.type.list`
- [ ] the image making its kind from a name (`image/this.cs:189`)
- [ ] the image's `_mime` (`:59`, `:87`); its mime is asked of its kind
- [ ] the hard-coded mimes in the image's sniffing (`image/this.Parse.cs:17–27`); sniffing answers a kind name, looked up in the image's own kinds
- [ ] the `Type` overrides, each value holding the type that made it (grep `override global::app.type.@this Type`: 48 files)
- [ ] text's name floor (`type/item/source.cs:115`, `_type.kind.IsText || _type.Name is "text"`)
- [ ] the render fix's name check (`module/ui/code/Fluid.cs:165`, `format is { Name: "item" }`; codeanalyzer F3.v3)
- [ ] `type.Get` re-resolving through `app.type.list` on every navigation (`type/this.cs:884–886`), another patch around the bare type: it's why plang never sees the bare kind (found by the test-designer)
- [ ] the "is this an action slot?" name checks (`goal/step/action/this.Validate.cs:54–55`, `goal/step/action/formal/reader.cs:221`, `:349`, `goal/step/action/serializer/Reader.cs:153–154`; F8.1)

**Stage 2:**
- [ ] the sync `Write(IWriter)` and its overrides (46 files)
- [ ] the `IsLeaf` branch in `Output` (`type/item/this.cs:906–913`)
- [ ] `kind.Owns(IWriter)` (`type/kind/this.cs:177`), its overrides (`type/item/kind/json/this.cs:26`, `type/item/wire/kind/plang/this.cs:126`), and its calls (`type/format/IWriter.cs:76`, `type/item/content/this.cs:138`, `type/item/source.cs:363`, `type/item/wire/this.cs:61`, `:74`)
- [ ] `IWriter.Format` and `IReader.Format` as strings (`type/format/IWriter.cs:26`, `IReader.cs:49`); the writer carries its output kind
- [ ] list's `_formats` (`type/item/list/this.cs:293–300`); its formal form (`list/format/formal.cs`) becomes formal's pair-registry entry for list
- [ ] `IWriter.Bytes(byte[])` and `IWriter.Raw(byte[])` (`IWriter.cs:51`, `:64`), replaced by `Bytes(byte[] value, kind kind)` (844)
- [ ] the dict, list and item arms of the json writer's `Value(object)`

**Stage 3:**
- [ ] `IWriter.EmitsSchema` (`IWriter.cs:36`), the json writer's bool and its "plang" format, Data's two `if`s on it (`data/this.Output.cs:83`, `:126`)

`MapPlangTypeToJsonSchema` (`llm/code/OpenAi.cs:795–816`) goes with llm-provider, which uses this stage's schema entries.

**Stage 4:**
- [ ] `Step.Set`'s three branches and `FromJson` (`goal/step/this.Item.cs:48–77`)

**Stage 5:**
- [ ] `item.Get`'s reflection through the clr carrier, and `Member`'s (`type/item/this.cs:277–320`)
- [ ] `item.Set`'s reflection, `Activator.CreateInstance`, `SetValue`, its C# lowering fallback and its throw (`:209–245`)
- [ ] `property.IsDefault`'s decision by type names and text (`type/property/this.cs:118–138`)

**Not this branch, its own item:** the formal reader's lexing, for the builder's parser, with its own trace:
- the chain switching on type names (`goal/step/action/formal/reader.cs:317`: text, duration, number arms). The direction: each type answers how a bare word reads as it (877).
- `LiteralType` (`:415`), a static helper switching on a literal's shape to return a type name. I ruled it "stays"; Ingi flagged it to the coder ("LiteralType obpv if it's on code base not a test"). The literal answers its own type (878).

**Stays:**
- the reader registry and its folder discovery (`type/reader/this.cs`);
- `kind.Every`'s discovery of kind classes;
- `ICreate` births;
- `BeginRecord`/`EndRecord`;
- `IOutput`, as the pair-registry entry;
- `Kept` for things that live on;
- an unknown kind name answered by its name (`type/list/this.cs:172`), on purpose;
- `property` built from a C# class by reflection (`type/property/this.cs:21`), the one place reflection stays.

## What tests can't show (checks the coder runs and reports)

- **Byte-equal outputs:** every `.pr` read and written again; the plang wire (`application/plang`); json writes; the decider pins and the builder's prompts.
- **A real build after stage 4,** because the builder's answer path changes.
- **The surface golden re-pinned for each locked change**, with its decision:
  - `item`'s `Type` (867);
  - `IWriter`/`IReader`, kind, the pair registry (868, 844, 846);
  - the record door's writers (869);
  - `IReader`'s new implementation (871);
  - `item`'s `Get`/`Set` and `property`'s members (870, 872).
- **Every stage:** PLNG001/002 at 0; no new red against runtime2's baseline.

## OBP validation

| new surface | check | result |
|---|---|---|
| `item.Type`, given at birth | one place holds a value's type; no per-class rebuild; never a kind from a name | to verify at stage 1 |
| `Output`, the one door | each type writes what it is; no `IsLeaf` fork, no second door | to verify at stage 2 |
| the writer's output kind | an object, not a string; no value reads it | to verify at stage 2 |
| `Bytes(byte[], kind)` | one byte door; the value says what it has | to verify at stage 2 |
| the pair registry | looked up in one place (`data.Output`); entries live in their type's folder; no `is`/`switch` on a value | to verify at stages 2–3 |
| the record door, always | each writer decides what to show; no bool, no `if` in Data | to verify at stage 3 |
| the token reader over a plang value | one reader for a value in hand; no json round trip | to verify at stage 4 |
| `property.Read`/`Write` | what a program reads is what the builder offers; one list | to verify at stage 5 |
| `Set` adding an unknown key | added beside the value's own members; no throw | to verify at stage 5 |
| the three paths | plang `%x.member%` = C# `property.Read` = the member's place on its type | to verify at stage 5 |

> **For the coder: you own this code.** These are sketches of intent, agreed with Ingi group by group. Where the real code says another shape is better, bring it to the architect.
