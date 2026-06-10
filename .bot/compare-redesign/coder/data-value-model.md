# The Data / Value model

Written up from a design session with Ingi (2026-06-10), extended the same day
by the architect review session (immutability, collections, templates,
serialization — all settled and folded in below). Open points are listed at
the end; they are NOT settled.

## The one-sentence version

Data carries; the type instance IS the value and does all its own work; you
only ever ask for the value at the moment you actually use it.

And the second sentence: **values never change; Data is the only thing that
changes — it points at values.**

## Immutability and sharing (settled)

- **Every value instance is immutable.** plang cannot change an instance by
  design — `set` always makes a new value and rebinds. C# is held to the same
  rule by `WrapperImmutabilityTests` (readonly fields, no setters, sealed).
- **Stamps are ordinary typed properties on the instance** — `kind`, `strict`,
  `template` — set at creation, never after. There is no "stamps" object. Each
  type owns its own kind vocabulary (text: file extension; number: precision;
  rung-2 item: the CLR class name).
- **Sharing is therefore always safe.** `set %y% = %x%` → new Data, same value
  instance, and a NEW property bag (the bag is copied; the values inside it
  are shared by pointer). So `set %y!NewProp% = 1` lands on %y% only.
- **Collections are the deliberate exception: list and dict are mutable
  containers of immutable values** — reference semantics, exactly like
  `List<string>` in C#. A list is an identity (a place that changes over
  time); everything in it is a value.

  ```plang
  - set %x% = [1, 2]
  - set %y% = %x%        / same list instance — two names for one list
  - add 3 to %x%
  - write out %y%        / [1, 2, 3] — yes, by design
  ```

  `add %x% to %list%` mints a NEW Data for the entry, pointing at %x%'s
  current value — O(1), nothing copied. That is what keeps a later
  `set %x% = "b"` from touching the list: the entry has its own Data.
- **file is a value, not a container.** Changing content = a new file instance
  (same path, same properties, new content) + Data rebind. Never in place.
- **Consequences:** the copy-on-write todo and any "am I shared?" flag are
  dead — nothing to track. The F1 `CopyStructure` copy-on-add must be REMOVED
  (it copies the list on add — half value-semantics, fighting this rule). The
  rope/chunk list model becomes optional perf work, not a semantic need.

## The shape

```csharp
class Data
{
    item type;       // the typed instance — it IS the value. Data OWNS it.
    // name, signature, properties — Data's other concerns. Nothing else.

    async ValueTask<item> Value()
    {
        this.type = await type.Value();   // the type answers — maybe AS A DIFFERENT TYPE
        return type;
    }
}
```

- Data owns: **name, Type (the typed instance — the value), signature,
  properties**. It never looks inside its value, never asks what type it
  holds, has no special cases for any type.
- `Value()` is not forward-and-forget: the type's own `Value()` may answer
  with a **different type instance** — a file parses its bytes and answers
  with a dict. Data takes the answer as its new Type. That one assignment IS
  the narrow (file → dict); there is no narrowing mechanism in Data.
- There is no untyped value at runtime. When a CLR value enters from outside
  (input, .NET API return), it is put into the matching item immediately — we
  have the CLR→plang mapping for that. `5` becomes `number` (int is a *kind*
  of number, not a type). A json payload is just a serialization format —
  deserialized it becomes `dict`/`list`; there is no "json value".
- The type instance owns everything about the value: its bytes before parsing
  (`_raw` lives ON the type, not on Data), its parsed content, how to load
  itself, how to parse itself, how to navigate itself, its kind.

## The two read methods

Both live on the type (Data forwards). Both are async. The difference is
exactly one thing: **parse or no parse**.

```csharp
// content in the cheapest form that still has content. NEVER parses.
async Peek() { return value ?? _raw ?? await load(); }

// content parsed and ready to use/navigate. May answer as a different type.
async Value() { /* load if needed, parse if needed, return the typed instance */ }
```

- Each type knows how to load itself (a file reads its disk through its auth
  gate; a url fetches). Nobody above the type knows or cares how.
- text-vs-binary of loaded bytes is decided at the lowest level that reads
  them (the channel boundary stamps it from the mime). Nothing above ever
  asks that question again.
- Peek resolves nothing — it hands the instance over as-is. Rendering for
  output happens inside the type's own `Write` (see Serialization below).
- Sync framework methods (`ToString`, `Equals`, `GetHashCode`) answer from
  what is already in memory — they never load, never resolve.

## Templates and refs — live, resolved at use (settled)

- **The build validator stamps `template`** — detecting %refs% in an authored
  value is deterministic code, not the LLM. `template` is a string for now
  ("plang" is the only language).
- **A builder-authored value containing refs is LIVE** — template text
  (`"hello %name%"`) or a collection literal (`{ name: %name% }`). It holds
  the refs and resolves at every use, never at set. A container with refs
  inside gets the stamp too, so it knows something needs rendering without
  being walked.
- **The resolved result is never stored back.** The rule: `Value()` may keep
  its answer only when the answer depends on nothing but the value itself.
  Parse (file → dict) qualifies — keep it. Render depends on outside state
  (%name% can change between uses) — never keep it. Mechanically: cache iff
  `template == null`.
- **Render is single-pass.** It fills only the holes the builder recorded;
  the output is never re-scanned for refs. Input is never stamped, so a user
  who types "%secret%" gets it printed literally. A substituted value that is
  itself a stamped template renders through its OWN door first — door
  recursion is fine; string re-scanning is the injection hole and is banned.
- **There is one `app.variables`** — a traveling value resolves the same
  wherever it is used; a sub-goal sees the %name% set before the call.

## Serialization — Write is async, one walk (settled)

`Write(IWriter)` becomes `async ValueTask Write(IWriter w)`. The type
serializes itself and resolves its own refs while doing it: dict.Write awaits
each entry; the entry renders itself right there. An entry with
`template == null` completes synchronously — no async hop, no cost for plain
data. One walk; the `var _ = await item.GetValue()` pre-crawl dies.

The serializer stays dumb — it calls `Write` and never asks "is this a
template?". The type knows; it reaches app.variables through its context.

The one place that cannot go async is the STJ `[JsonConverter]` (Microsoft's
interface is sync). That is the bare-json interop view, not the channel path —
channel output goes through our own writers. The STJ path pre-resolves before
handing over; documented exception, nothing else inherits it.

**Wire mapping:** in memory, Data has one slot — the instance. The wire keeps
`{name, type, value, properties, signature}`: `Wire.Write` fills both `type`
(name/kind) and `value` from the one instance. No new envelope type, ever.
(`signature` rides on Data on this branch; it moves to its own schema layer on
the schema branch — see "Nested Data does not exist".)

## The two asks — untyped and typed

```csharp
var value = await data.Value();        // "give me what you are" — item back, whatever it evolved into
var dict  = await data.Value<dict>();  // "I need a dict" — the value parses/converts ITSELF to dict, or errors honestly
```

- The untyped ask is for flow and navigation — code that works with whatever
  the value is.
- The typed ask is for C# that has an expectation. The expectation can live
  in either of two places, same idea:
  - **on the slot**: a handler parameter `Data<file> file` — resolution made
    it a file before the method ran;
  - **at the call**: `data.Value<dict>()` — mid-code, "I need it as a dict
    now".

### What `Data<T>` means

`Data<T>` is a contract at the edges — handler parameters and typed returns:
"this slot expects T". It is NOT a promise about the value's whole life. A
variable in flight is just Data; its Type evolves freely (file → dict) and
`Value()` returns the current item. The generic never has to rebind because
typed expectations are stated where they exist (slot or call), not carried
forever.

## When to call what — the rule

- **Passing Data along** (variables, goal.call, callstack, channels, wire):
  call NEITHER. Couriers move Data whole. If code that just forwards Data
  calls Value(), that call is a bug.
- **`await Value()`** means: "I am going to use or manipulate this value, give
  it to me ready." It may appear anywhere in C# — handler, helper — as long
  as that code genuinely works on the value. Manipulating NEVER alters the
  instance (values are immutable): the work produces a new value and the Data
  rebinds — same rule as `set`:

  ```csharp
  async Task ReplaceSpaceWithDash(Data<file> file)
  {
      var f = await file.Value();                                // file loads itself if not loaded
      var changed = f.WithContent(f.Content.Replace(" ", "-"));  // Content is text; Replace returns a NEW
                                                                 // text; WithContent a NEW file (same path)
      file.Rebind(changed);                                      // Data points at the new value
  }
  ```

  (`WithContent`/`Rebind` shapes are the coder's call — the contract is
  new-instance + rebind, never in-place.)

- **`Peek()`** is for pass-through output: the channel writer writes content
  as-is, no parse, however big the value.

## Navigation — the type navigates itself

```csharp
// inside app.variable, resolving %config.name%
var value = await data.Value();        // file loads, parses, answers "I'm a dict"; Data.Type rebinds
var child = value.Navigate("name");    // the DICT navigates itself — it knows how
```

The variable does NOT check "is it dict or list" — that would be asking type
questions again. Every item answers navigation itself: dict by key, list by
index, and plain text answers with the honest teaching error ("cannot
navigate text — add `as object/json`"). The variable just asks.

## Failure

A load or parse failing inside `Value()` (missing file, malformed bytes)
surfaces as `Data.Error` — the existing typed-error convention, already
handled in the codebase. No new failure shape.

## The type chain — evolved values still answer for what they were

```plang
- read config.json, write to %config%      / file, nothing read
- set %config.name% = "ingi"               / parsed → dict, then mutated
- call DoStuff %config%                    / DoStuff(Data<file> file) — works
```

Narrowing keeps identity: after file → dict the chain is `[dict, file, path,
item]`. `is file` is still true, `%config!file!path%` still answers, and a
`Data<file>` slot is satisfied — the chain answers with the file facet. This
is plang's type chain, NOT C# class inheritance; the source gen just asks for
`file` like always and the value answers from its chain.

**What the file facet's content means after mutation (settled):** the CURRENT
value, serialized back in the file's format — never a re-read of the disk.
One variable has one value; the file facet contributes location and format;
the disk changes only on an explicit save. There is no hidden second truth on
disk.

## The lifecycle, traced

```plang
- read config.json, write to %config%   / Data, Type = file. The file holds only its path. Nothing read.
- write out %config%                    / channel writer calls Peek(): no value, no _raw → file loads
                                        / its bytes (auth gate), bytes written out. NO parse.
- write out %config.name%               / variable navigation calls await Value(): bytes parse → dict.
                                        / Same Data. Data.Type changed: file → dict (file kept in the
                                        / type chain). _raw emptied — single storage, always.
```

Storage never doubles: nothing → `_raw` (loaded) → parsed value (raw emptied).
The parse *moves* the value, it doesn't copy it.

## Engine plumbing is not a plang value (settled)

The "everything is Data, every Data is an item" rule is about plang values —
things plang code can touch (`%x%`). A CLR runtime artifact a plang developer
can never hold is NOT a plang value and never rides in Data.

Case settled: `Assembly` (module/code loading). Today `path.LoadAssemblyAsync`
returns a bare Data with the Assembly smuggled in the value slot ("rides in a
BARE Data" — a deliberate dodge of `T : item`). Wrong. The shape:

```csharp
// in the module loader — the one place that knows .NET reflection exists
await dllPath.Authorize(Execute);                 // path owns the gate, as always
var asm = Assembly.LoadFrom(dllPath.Absolute);    // take-over API, documented exception (like sqlite)
```

- path keeps location + gate; it stops knowing what an Assembly is.
- The Assembly lives entirely in C#, used on the spot. Data in the flow
  carries only success/error — `Ok()`, nothing smuggled.
- No `binary/assembly` plang type — not needed (would only ever exist if a
  plang dev held the dll FILE as bytes, which nothing needs today).

## Strongly-typed C# objects in plang — the three rungs (settled)

```plang
- read start.pr, write to %goal%     / lazy file, as always
- write out %goal.Steps[0].Text%     / access: runtime sees .pr, deserializes<Goal> — the Goal
                                     / INSTANCE is the value. Data.Type = the goal itself.
```

`goal.@this` (and step, action, actor, variable, snapshot, …) already inherit
item — the strongly-typed instance IS the value, no extra mechanism.

For any C# type, three rungs:

1. **Own item type** — inherit item when you own the type and want it to
   answer the standard questions ITS OWN way (file's truthiness is "do I
   exist", number's compare is the numeric tower, image's strict check sniffs
   magic bytes). The preferred way for everything in our codebase.
2. **`item | kind`** — a strongly-typed C# object plang can hold but that has
   no custom behavior (third-party classes, deserialized POCOs — you can't or
   needn't make them inherit). `item` is plang's `object`; `kind` names the
   class. Everything generic works on day one: navigation by property
   reflection, json rendering, structural compare, non-null truthiness. The
   instance underneath stays strongly typed — nothing is lost.
3. **Never in Data** — engine plumbing plang can't hold (Assembly).

Inheritance is NOT required to be visible in plang — that would be busywork
and impossible for types you don't own (C# is single-inheritance). A type
"graduates" from rung 2 to rung 1 only on the day a generic answer stops
being the true answer for it; most passing-through types never need to.

## Nested Data does not exist — the schema layers (settled direction)

The cases that nest a Data inside a Data today (signing, compress, wire
reads) are solved by the schema model (lands on a future branch, not this
one): a few schemas exist — `data | signature | encryption | compress` —
with two rules:

- `data` cannot wrap another data. Absolute, no exceptions.
- `signature | encryption | compress` ALWAYS wrap a data. They are their own
  objects with their own properties, sealing the data as their value:

```
{ @schema: "signature", nonce: ..., signature: ..., value: { @schema: "data", ... } }
```

Signature moves OUT of Data into its own layer. Layers compose (compress
around signature around data). A list's rows being Data is unaffected — the
list owns its rows; that is collection structure, not a value slot.

**For this branch:** the `SetValueDirect` courier sites (wire read
reconstruction, compress wrap, test couriers) are transitional debt that the
schema model deletes. Do not design on them, do not extend them; they are
countable and marked.

## `type` is an item (settled)

The type entity (`{name: "image", kind: "gif", strict}`) is a plang value —
it is authored in the language (`as image/gif, strict`), rides in the .pr,
and a developer can hold it:

```plang
- set %type% = %x!type%    / %type% now holds the type entity — so it must be an item
```

So `type.@this` becomes an item. The contrast with Assembly is the test for
every future case: can plang code hold it in `%x%`? Yes → item. No → engine
plumbing, never in Data.

## CLR in and out

- **In**: only at the entry boundary. CLR value → matching item, immediately,
  once. Raw CLR never travels between the boundaries.
- **Out**: only when talking to .NET / an external API, and the item lowers
  ITSELF: `number.ToInt64()`, `duration.Clr<TimeSpan>()`. Checked, loud on
  loss. plang→plang calls keep plang types end to end (no decomposing a
  `number` into `int` to pass it to another plang method).

## What this kills in the current code

The current `compare-redesign` codebase predates this model in places. The
following are wrong and must go:

- **Bare non-generic `data.@this` with an `object? _value` slot** — the value
  slot disappears; the typed instance is the value, held as Data's Type.
- **`_raw` on Data** — moves onto the type instance.
- **`_type` as a separate descriptor entity next to the value** — the instance
  knows its own name/kind; Data carries one thing.
- **`Materialize()`** — DELETE, or at minimum make private inside the type's
  own parse path. The bytes→format parse step it performs still has to exist —
  but it lives inside the type's `Value()` (the type parses itself), not as a
  public sync method anyone can call. Every public/internal call site of
  `Materialize()` outside the type's own parse is a bug to remove. Sync
  parse-on-call defeats the lazy model; everything is async.
- **`ScalarContent` / any file-or-url branch in Data** — Data deciding how a
  reference yields content was Data doing the file's job. Already understood
  as a violation; the behavior belongs to the type's own load.
- **`is string` / `is byte[]` / mime words / type-switches anywhere above the
  type itself** — every one of these is code asking a question some item
  should answer. The compiler sweep that retypes `Value()` will surface them
  all; each gets rewritten to ask the item or to lower via `Clr<T>` at a real
  .NET edge.
- **Navigator selection above the type** — navigation is the item's own
  member; nothing picks a navigator by inspecting the value's type.
- **`CopyStructure` copy-on-add on list** — copies the list when adding,
  which is half value-semantics. Collections have reference semantics now
  (see Immutability and sharing); the copy is wrong, not cautious.
- **The `await GetValue()` pre-crawl before sync serialization** — Write is
  async; the type resolves while serializing, one walk (see Serialization).

## Not settled — do NOT build on these yet

Open questions for the next session:

1. Reconciling Type-evolution with the existing type-chain/narrow code. The
   model is agreed (same Data, type rebinds on Value()'s answer, identity
   accumulates); the code reconciliation is not designed. Two sub-questions
   now belong here:
   - **Where does the chain live?** Likely on the answering instance the
     parse mints (it is fresh, never shared) — but that is a choice, not
     decided.
   - **Narrow visibility across aliases changed.** Old code mutated the Data
     in place, so every alias saw the narrow; now narrow is a per-Data
     rebind, so two Datas sharing one unparsed file each parse independently
     (idempotent, but the cross-alias visibility is gone). This is a
     deliberate reversal of the old stage-2 note — confirm nothing depended
     on it.
2. `Value<T>()` vs the existing `As<T>` — same concern, which name/shape
   survives is not decided.

## State of the working tree (handover note)

At the time of writing, `PLang/app/data/this.cs` has an in-flight, incomplete
retype of `Value()` (returns `item` instead of `object?`; ~74 consumer compile
errors are the worklist). That change was made BEFORE this model was fully
understood — it patches the old shape rather than building this one. Treat
this document as the target; the in-flight diff is disposable.
