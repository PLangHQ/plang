# The variable value: a reference, read and written

A variable is the value that names a place in memory. It is the `%…%` written
in a `.goal` or a `.pr`, held as a value so a step can carry it, store it or
write through it. This page is for the C# developer who needs to know what a
variable *is*, how one is made from text, and how it reads and writes. The
store that holds the names (`context.Variable`) is covered in
`../../../call/start.md` §4-5 and is not repeated here.

```
app.type.item.variable.@this    PLang/app/type/item/variable/this.cs       namespace app.type.item.variable
app.type.item.variable.parser   PLang/app/type/item/variable/parser/this.cs namespace app.type.item.variable.parser
app.type.item.variable.code     PLang/app/type/item/variable/code/this.cs   namespace app.type.item.variable.code
app.type.item.variable.list     PLang/app/type/item/variable/list/this.cs   namespace app.type.item.variable.list
                                                                            (global alias Variables)
```

> **Scope.** This is the value side: the `variable` item, its parser and its
> code. The memory it reads (frames, the store, the stack) is
> `../../../call/start.md`. The value apex it derives from is `../start.md`, and
> the type system around it is `../../start.md`.

> **Locked core.** A variable is a core value type, and the store it reads is
> variable memory. Both are on the locked-core list. This page describes their
> current surface. Additions go through the architect.

---

## 1. A variable is an item: Text and Code

`variable.@this` is a sealed `item`. It holds two things, set once at birth:

- **`Text`**: the reference as written, `%user.address[idx].city%`, with its `%`
  signs. It is `[Out]`, so it is the wire form.
- **`Code`**: a `code.@this`, the list of hops that reach what the text names.
  The root is the first hop. The rest are its members, bindings, indexes and
  method calls (§4).

```csharp
namespace app.type.item.variable;

[global::app.Attributes.PlangType("variable")]
public sealed class @this : global::app.type.item.@this, ICreate<@this>, IName, IMatch<@this>, ILoad<@this>,
    IList<@this, list.@this<@this>>
```

The `(text, code)` constructor is `internal`. A variable is made from text: the
parser (§3) is the one maker. The public `new variable("myList")` takes a name
and builds it through the parser. The type system's `Convert` hook reads a text as
a variable, and `Create` passes a variable through and declines anything else.

A variable is a leaf that names a slot. Its wire form is its text, and the
parser reads that text back into the same variable. Three overrides say what it
is:

| Override | Value | Meaning |
|---|---|---|
| `IsLeaf` | `true` | written as one bare token, its text |
| `IsVariable` | `true` | it is a reference; the instance binder aliases its target |
| `Cacheable` | `false` | a reference renders fresh every read, never memoised onto the holding `Data` |
| `Variable` | `[this]` | a variable holds itself |

`Write(IWriter)` writes `w.Variable(Name)`, where `Name` is the text between the
`%` signs. `ToString()` is the same `Name`. The implicit `string` operator also
gives `Name`, for the string boundaries that take a name.

---

## 2. Reading and writing: `Start` and `Set`

A variable reads through `Start(context)` and writes through
`Set(value, context)`. Both delegate to the code (§4), so the rule is the same
for every depth: the root is looked up in memory, and each later hop reaches
into what the one before it answered.

```csharp
public ValueTask<data.@this> Start(actor.context.@this context) => Code.Start(context);
public ValueTask<data.@this> Set(object? value, actor.context.@this context) => Code.Set(value, context);
```

- **`Start`** runs every hop and answers the `Data` the last one reached. A
  name that is not bound answers `NotFound`, which is `IsInitialized == false`.
- **`Set`** runs every hop but the last to reach the parent, then the last hop
  writes itself. A one-hop variable (a bare `%x%`) is a rebind in memory. A
  deeper one lands inside the value its root holds. A root that holds nothing is
  given an empty dict when a member or key is written into it, so
  `set %x.a% = 1` on a new `%x%` has somewhere to land.

Around these, the variable offers the forms the actions use:

| Member | Use |
|---|---|
| `Value(data)` | the door. Loud: a variable that holds nothing throws `VariableNotFoundException` |
| `Ensure(birth, context)` | the held value, or the value `birth` makes, written there in one step |
| `Use<TAs>(context, then)` | the held value as a `TAs`, read only |
| `Change<TAs>(context, then)` | the held value as a `TAs`, changed in place, then written back |
| `Replace(expected, value, context)` | writes only if the name still holds `expected` (compare-and-set) |

> **The door is loud on purpose.** `Value(data)` throws when the variable is
> unset, because a reference that names nothing is a bug at the reference.
> Boolean questions (conditions) tolerate absence through their own path and do
> not call this door. A chain of references deeper than the memory allows is a
> cycle, and the door throws `VarResolveCycle` past 50 levels.

---

## 3. One definition: the parser

The parser is the only way text becomes a variable. A reference has one
definition, and it lives in `parser/this.cs`. Nothing else scans for `%…%`. Ask
the parser, or ask the value. Do not write a regex or a hand scan.

```csharp
namespace app.type.item.variable.parser;

public sealed class @this
{
    public @this(string text)
    public IReadOnlyList<variable.@this> Variable { get; }   // every reference the text holds
    public variable.@this? Read(int at)                       // the one reference opening at `at`
    public variable.@this? Whole { get; }                     // the text as exactly one variable
    public code.@this? Path()                                 // the text as hops with no root
    public IReadOnlyList<global::app.error.Error> Error { get; }  // why a reference does not parse
}
```

| You have | You call | You get |
|---|---|---|
| the whole text of one reference | `new parser.@this(text).Whole` | the variable, or null (the reason is in `Error`) |
| a text with several references | `new parser.@this(text).Variable` | every reference, in order; a malformed one is left out |
| a position in a larger text | `new parser.@this(text).Read(at)` | the reference that opens at `at`, and the reader continues after it |
| a path read from a value (`choices[0].message`) | `new parser.@this(text).Path()` | the hops with no root, for a value to walk |
| a variable's own reach | `item.Variable` / `item.HasVariable` | the references a value holds; a variable holds itself |

The definition, as the parser's own doc comment states it: `%`, then a name that
starts with a letter, `_` or `!`, closing at the first `%` outside quotes,
parentheses and brackets. A space outside those ends the scan. So
`%user.address[%i%].city%` and `%name.replace("%", "")%` are each one reference,
and `50% of %total%` holds one.

A bare name is also a variable. `Whole` reads `user` as `%user%` when the text
does not start with `%`. That is the form a name slot may carry
(`variable.set`'s name, a `Data<variable>` parameter).

`Resolve(raw, context, given)` is the throw boundary for C# callers: it returns
a row's already-parsed variable if one is given, otherwise the parser's `Whole`,
or throws `InvalidVariable` with the parser's reason.

> **Ask the value, not the text.** A value that may hold references answers
> `Variable` and `HasVariable`. Code that needs "which references does this
> hold" calls those, not the parser on the value's text again.

---

## 4. The code: hops

`code.@this` is a `list<Hop>`. Each hop is one step. It gets what the step
before it answered and does its one step. The `Hop` base is abstract and
carries its own `.pr` form, one object under its kind:

```csharp
public abstract class Hop : global::app.type.item.@this, ICreate<Hop>
{
    public string Text { get; }                    // the piece as written: .address, [idx], !cost
    public abstract string Kind { get; }           // "variable" | "property" | "index" | "method"
    public abstract ValueTask<data.@this> Start(data.@this? previous, actor.context.@this context);
    public virtual ValueTask<data.@this> Set(data.@this? parent, object? value, actor.context.@this context);
}
```

The four hops:

| Kind | Class | Text | Start | Set |
|---|---|---|---|---|
| `variable` | `code/Variable.cs` | `user`, `!app` | reads `context.Variable`; a `!` name the memory does not bind falls to a shortcut, then the app's member, then a module | rebinds the name in memory |
| `property` | `code/Property.cs` | `.name`, `."a.b"`, `!cost` | the member by name; a `!` name reads the binding (`Properties`, then `Data`'s own members) | a member is written through `parent.Set(name)`; a `!` name lands in the binding's `Properties` |
| `index` | `code/Index.cs` | `[0]`, `["k"]`, `[idx]`, `[%i%]` | the key is resolved, then the parent is peeked and read at it | the key is resolved, then the parent is written at it |
| `method` | `code/Method.cs` | `.replace("-", " ")` | calls a method on the value that carries `[LlmBuilder]` | not writable (the default `Set` answers `VariableNotWritable`) |

Two rules from the hops:

- **A variable only reads methods the value marks.** The method hop reaches only
  methods with `[LlmBuilder]`. Its values are literals or variables, resolved at
  run.
- **An index key is a literal or a variable.** `[0]` and `["k"]` are literals.
  `[idx]` is a variable with its own code, resolved at run. An unset key fails
  with `IndexNotSet`. It is never read as the literal text of its name.

The root (`code/Variable.cs`) is the only hop whose `Set` goes to memory. The
`Start` of a root also knows the `!` names: a binding (`!app`, `!data`) answers
first, then a shortcut, then the app's member, then the module of that name.

The `.pr` form is an array of these objects, one per hop:

```
%user.items[0].name%  ->  [ {"variable":"user"}, {"property":"items"}, {"index":{"number":0}}, {"property":"name"} ]
```

The exact `.pr` keys come from `Hop.Write`, `Index.Piece` and `Method.Piece`.
Read them there before relying on the shape.

---

## 5. How `%user.name%` parses and resolves

The walk-through, from the text to the value:

1. **Parse.** The parser's `Body` reads `user` as the root (a `code.Variable`),
   then `.name` as a `code.Property`. The two hops form the code. The variable's
   `Text` is `%user.name%`.
2. **Start.** `variable.Start(context)` calls `Code.Start(context)`. The code
   walks its hops in order.
3. **Root.** The root asks `context.Variable.Get("user")`. The store walks the
   frames inner first, then this memory (`../../../call/start.md` §4). The answer
   is the `Data` `user` holds, or `NotFound`.
4. **Member.** The property hop asks the held value for `name` through its own
   `Get(parent, "name")`. The value answers the member `Data`, or `NotFound`.
5. **Answer.** The code returns the `Data` of the last hop. A reference's value
   is whatever the value's door (`Value(data)`) opens, through §2.

`Set` runs the same walk, but stops one hop short, reaches the parent, and then
the last hop writes itself. A write of `%user.name%` is `Property.Set(parent,
value)`, and the parent is what `user` held.

> **Own values.** A hop is own when it names no `!` binding and every value it is
> handed is own (`IsOwn` on each hop). A variable is the program's own when every
> hop is. Content read from outside holds only own variables. A change to an
> own variable's deeper value happens in place; the store copies a value read from
> beyond the frame that keeps it before the change (`list.Own`, see
> `../../../call/start.md` §4).

---

## 6. Reserved names

`variable/Reserved.cs` is a static class of `const string` names. The `!`-prefixed
ones are PLang infrastructure variables: the runtime sets them and the code
reads them. The rest are conventional identity names.

| Group | Examples | Meaning |
|---|---|---|
| run and step | `!Goal`, `!Step`, `!StepIndex`, `!Event`, `!Variables` | where the run is |
| errors | `!Error`, `!DetailedError`, `!VerboseError` | the error in play |
| debug and build | `!Debug`, `!CSharpDebug`, `!StrictBuild`, `!Test` | run switches |
| injection | `!Inject_Logger`, `!Inject_LLMService`, … | services a binding is given |
| identity | `Identity`, `MyIdentity` | the non-`!` identity names |

`Reserved.Keywords` reflects over the constants, and `Reserved.IsReserved(key)`
checks a name against them, ignoring case and `%`. No caller of either was found
in `PLang/` when this page was written.

> **Reserved is the name list, not the rule.** The `!` prefix is what marks a
> system name in code: `Variable.IsBare`, `Variable.Paths`, the root hop's
> `Start`, and the store's enumerations all test `StartsWith('!')`. A new system
> name needs the prefix to behave as one.

---

## Find ...

| You want | Plang | C# | Source |
|---|---|---|---|
| a reference from text | `%…%` | `parser.@this.Whole`, `.Variable`, `.Read(at)` | `variable/parser/this.cs` |
| a path from a value | `user.name` | `parser.@this.Path()` | `variable/parser/this.cs` |
| the value a reference holds | `%user%` | `variable.Start(context)` | `variable/this.cs` |
| the write to a reference | `set %user.name% = …` | `variable.Set(value, context)` | `variable/this.cs` |
| the hops | — | `code.@this` (a `list<Hop>`) | `variable/code/this.cs` |
| one hop, by kind | `.name`, `[0]`, `!x`, `.m()` | `code.Property`, `code.Index`, `code.Method`, `code.Variable` | `variable/code/*.cs` |
| the variable's own name | — | `Text`, `Name` | `variable/this.cs` |
| a system name | `%!app%`, `%!error%` | `Reserved.*` | `variable/Reserved.cs` |
| the store that holds names | `%!app.variable%` | `context.Variable` (`Variables`) | `variable/list/this.cs` |

---

## The tree from here

| Page | Covers |
|---|---|
| `../start.md` | the value apex: `Create`, `Value`, `Rank`, `Output` |
| `../../start.md` | the type system: the type list, kinds, formats |
| `../../../start.md` | the app root and the collection-node pattern |
| `../../../call/start.md` | frames, the stack, and variable memory: what a read sees (§4) and what a write does (§4) |
