# item-doors — stage 0 trace

The plan's lists come from greps; this is the contract: every demolition member on the current tree
(branch item-doors, runtime2 c47265322 + kind-is-type), its real line, its callers, what replaces it,
which stage. Lines drift — the names and callers are the truth. Verdicts flagged where a verder looks
wrong against real callers.

## The one patch the whole branch removes: the Get-time re-resolve

`type.Get` (`type/this.cs:886`) throws away the instance it was called on and looks the type up again in
the live registry — `var full = parent.Context.App.type.list[this, parent.Context]` — then answers from
`full` (the REGISTERED type). So `%x!type.kind.X%` reaches the registered kind's facts regardless of the
value's own bare `_kind`. The same re-resolve is in the type's own wire write (`Out`, `type/this.cs:63`)
and in `Setting` (`type/this.cs:900`). This is the patch around the bare type (#1). It is WHY the three
stage-1 plang bullets come out green today (the facts reach navigation through the Get-time trip) and why
the structural red lives in C# (`ActionSlotIsByTypeTests`: a fresh `new type.@this("action")` has
`ClrType == null`, the registered one does not; the Name compare can't tell them apart). Stage 1 makes the
value hold the registered type, so `full == this` and all three re-resolves go.

Callers that navigate a type through `Get` (polymorphic): `variable/code/Variable.cs:33`,
`variable/code/Index.cs:43`, `type/current/this.cs:31`, `item/text/this.cs:321`, `item/this.cs:338`
(indexed overload), `type/this.Generic.cs:100,:121`, `type/kind/this.cs:61`, `type/root/this.cs:29`.

---

## Stage 1 — a type is born whole (867). Reds: ActionSlotIsByTypeTests, ImageKindKnowsItsMimeTests, TextKindIsTextByItselfTests (C#) + 3 kind/strict (plang). Guards: SetAsImageGifStrictMatching, BytesKnowItsMime, NamedTypeIsRegistered, TextKindIsText.

| # | member (actual line) | callers / readers | replaced by |
|---|---|---|---|
| 1 | type ctor minting a kind from a name — `type/this.cs:149` (`_kind = new kind(kind)`), in the `[JsonConstructor]` ctor `:146` | step-named-type path: `formal/reader.cs:354` (splits name/kind via TypeName/TypeKind `:458-459`), `:376`; `serializer/Reader.cs:25,63,65`; `list/this.cs:160,174,176,277`; `Registry.cs:98,99,163,168`; `kind/this.cs:196,265`, `empty/this.cs:117`; `pick/list:630,650`; `null/this.cs:40`; `reader/Record.cs:38`; `this.cs:253,256` | a step-named type is resolved through `app.type.list[{name,kind}, ctx]` (the `Full()` indexer, which already preserves Strict/Template); the ctor keeps only the name, no kind made |
| 2 | image minting its kind from a name — `item/image/this.cs:189` (ctor `@185`) | `image/this.Parse.cs:20` (FromBytes), `image/serializer/Reader.cs:25`, `Default.cs:21`, `screen/type/screen/view/this.cs:66`, `window/type/window/this.cs:112` | `image.@this(byte[], type.@this type) : base(type)` — the registered type is handed in |
| 3 | image `_mime` — `image/this.cs:59` (decl), read `:87` (Mime getter), written `:188,:202` | only `image/this.cs:87` reads it | `Mime => Type.kind.Mime.FirstOrDefault() ?? "application/octet-stream"` |
| 4 | image sniffing's hard-coded mimes — `image/this.Parse.cs:24` (`Sniff`), tables `:28-37` | `this.Parse.cs:20` (FromBytes, the only Sniff caller) | sniff answers a kind NAME, looked up in image's own kinds (the kind carries the mime) |
| 5 | the `Type` overrides — **48** (`grep override global::app.type.@this Type`), **29 under type/item** | each value's own navigation/write | `item` holds `protected internal type.@this Type { get; }` given at birth; all 48 overrides go. Full list in the agent map; non-item ones: snapshot, goal(+step/action/clause/keep/loop), type/table, type/clr, error, module/{list/query(+list), output/ask, code/registration, crypto/hash, code/defaultoverride, archive}, event, store |
| 6 | text's name floor — `source.cs:115`, `_type.kind.IsText \|\| _type.Name is "text"`, inside `Peek():110` | the text face (`Peek`) is read pervasively (100+ sites) | the registered text kind carries `IsText` — text's own format `[Format("", "text/plain", ".txt")]` lacks `Text = true` (pinned by `TextKindIsTextByItselfTests`); with it set / the registered kind held, the Name fallback goes |
| 7 | the render name check — `module/ui/code/Fluid.cs:165`, `format is { Name: "item" }` | one site (the render birth) | with the format type held whole, the render asks the type/kind whether it rests as a structure (json), not the Name string |
| 8 | the "is this an action slot?" name checks | `this.Validate.cs:54-55` (`Name=="action" \|\| (Name=="list" && kind.Name=="action")`); `formal/reader.cs:221` (`Name=="list" && kind.Name=="action"`), `:349` (`Name=="action" && …`); `serializer/Reader.cs:153-154` | ask by the slot's TYPE (its `ClrType` / `Takes` the action type), not by Name — the registered type has the ClrType a fresh one lacks |

**Also removed in stage 1:** the Get-time re-resolve (above: `type/this.cs:886`, `:63`, `:900`).

---

## Stage 2 — one door for writing (868, 844, 846)

| # | member (actual line) | callers | replaced by |
|---|---|---|---|
| 1 | sync `Write(IWriter)` — base `item/this.cs:873`; **47 `item.@this` overrides across 44 files** (plan said 46) | invoked at `data/this.Output.cs:109,133`; `type/this.cs:47`; `property/this.cs:167,178`; `source.cs:374`; `signature/this.cs:173-177,193,209-213,226`; `directory:129`; `image:114`; `clipboard:50`; `variable/code/this.cs:19`, `Method.cs:40,42,46`, `Index.cs:27,32`; `variable/serializer/Entry.cs:21`; `status:95,99,100`; `error:114,124`, `error/list:34`; `query/distinct:28`, `query/order/key:58`; `http/redirect:66`; `llm/limit:70-72`; `call:43`, `call/list:38`; **dispatch `v.Write(this)` at `text/writer.cs:113` and `json/writer.cs:185`** | `Output` is the only door; each type's `Output` writes its one token or its members |
| 2 | the `IsLeaf` branch in `Output` — `item/this.cs:910-911` (non-leaf → reflection Output; leaf → sync Write) | base `IsLeaf` `item/this.cs:814` (~40 overrides); readers `item/this.cs:735,885,910`, `type/this.cs:255,284,335,360`, `text/writer.cs:113`, `call/this.cs:440`, `Fluid.cs:93` | gone — each type's `Output` writes what it is, no leaf fork |
| 3 | `kind.Owns(IWriter)` — base `kind/this.cs:177`; overrides `item/kind/json/this.cs:26`, `wire/kind/plang/this.cs:126` (both `writer.Format is "plang" or "json"`) | `IWriter.cs:76`, `content/this.cs:138`, `source.cs:363`, `wire/this.cs:61,:74` | the pair registry (846) in `data.Output` + the record door; a value never asks the writer |
| 4 | `IWriter.Format` / `IReader.Format` strings — `IWriter.cs:26`, `IReader.cs:49` | writer Format read at `list/this.cs:300`, `json/this.cs:26`, `plang/this.cs:126`; **`IReader.Format` has NO production reader — already dead** | the writer carries its output kind (an object), not a string |
| 5 | list's `_formats` — `list/this.cs:293-294`; backing `list/format/formal.cs` | single reader `list/this.cs:300` | formal's pair-registry entry for list |
| 6 | `IWriter.Bytes(byte[])` / `Raw(byte[])` / `Raw(string)` — `IWriter.cs:51,:64,:60` | `source.cs:365,366,369`, `wire/this.cs:61,74`, `image:115`, `binary:211`, `content:138,140`, `text/writer.cs:69` | one byte door `Bytes(byte[] value, kind kind)` (844) |
| 7 | json writer `Value(object)` dict/list/item arms — `kind/json/writer.cs:130` (dict `:153-168`, list `:169-178`, item `:185` → `v.Write(this)`) | internal to the json writer | keep only C# primitives; the item arm (which calls sync Write) goes with #1 |

---

## Stage 3 — a type speaks for itself (869, 813)

| # | member (actual line) | callers | replaced by |
|---|---|---|---|
| 1 | `IWriter.EmitsSchema` — `IWriter.cs:36`; json writer bool `json/writer.cs:32` + its `"plang"` Format `:29`; Data's two ifs `data/this.Output.cs:83` (open envelope) & `:126` (close) | the two Data ifs are the only live readers (`kind/this.cs:346`, `text/writer.cs:16` are comments). `emitsSchema:true` writers: `goal/this.Item.cs:36`, `data/this.Output.cs:36`, `wire/kind/plang/this.cs:49` | Data always calls `BeginRecord`/`EndRecord`; each writer decides (bare json: value only; plang's own writer: `{type, value, signature}`, name in Store; llm table: the column's type, 815) |
| 2 | `MapPlangTypeToJsonSchema` — `llm/code/OpenAi.cs:782` (plan said 795-816, drifted) | `BuildParamSchema:765` ← `:183` (tool params) | each type's JSON Schema as a pair-registry `IOutput` entry; **goes with llm-provider**, which consumes this stage's schema entries |

---

## Stage 4 — reading a plang value as tokens (871)

| member (actual line) | callers | replaced by |
|---|---|---|
| `Step.Set`'s three branches + `FromJson` — `goal/step/this.Item.cs:35` (Set); branch 1 `:49` (already an action), 2 `:51` (reads through its own format), 3 `:54-55` (dict/list → `FromJson` json round-trip), throw `:56-57`; `FromJson` def `:65` (serialize-to-json then read back) | `FromJson` is private, single use (`:55`) | one path for every row: `new type.format.item.Reader(row, context)` (NEW structural IReader over a value in hand) → `reader.Read`; the three branches and the json round-trip go. Also `item.Set`'s C# lowering fallback (stage 5's `:241`) |

Note: stage 4 lands before stage 5 because #5's `Write` reads a dict into a record through this reader.

---

## Stage 5 — a type's members through its own list (870, 872)

| # | member (actual line) | callers | replaced by |
|---|---|---|---|
| 1 | `item.Get`'s clr-carrier reflection + `Member` — `item/this.cs:277-282` (`:280` clr carrier), `Member` `:292-311`, the `[LlmBuilder]` invoke-by-reflection in `item/kind/reflection/this.cs:23-32` | Get door: `type/this.cs:893`, `this.Generic.cs:120-121`, `kind/this.cs:60-61`, `current:31`, `computed:49`, `source:337`, `file:46`, `text:321`, base fallthroughs (`parent:27`, `root:29`, `goal/step/list:54`, `signature:90`, `note:99`, `variable:250`); `Member` at `dict/this.cs:293`, `list/this.cs:632,637` | `Type.Property?[key]?.Read(this, parent)` — one list, the builder's |
| 2 | `item.Set`'s reflection / Activator / SetValue / C# lowering / throw — `item/this.cs:209-245` (`:212` Reflect, `:217-218` OwnMember throw, `:223-224` "cannot take a child" throw, `:240` Activator.CreateInstance, `:241` `.Clr(...)` lowering, `:243` SetValue) | Set door: `data/this.Navigation.cs:51`, `goal/step/this.Item.cs:39`, `root:37`, `setting/module:30`, `clr/this.cs:136-141`, `variable/code/Index.cs:52`, `Property.cs:73` | `Type.Property?[key]?.Write(…)` else `Add(key, …)` — an unknown key is ADDED (872); the "cannot take a child" throw `:223-224` goes; only the `OwnMember` refusal `:217-218` stays |
| 3 | `property.IsDefault` by type names and text — `property/this.cs:118-138` (bool→word `:131`, variable→`%`-trim `:132`, number→parsed equality `:134-136`, else→text `:137`) | — | `Default != null && await value.Compare(Type.Make(Default, ctx), ctx) == 0` |

**Stays (verified):** `Kept` (`item/this.cs:286`; overrides on app `this.cs:26`, call `:21`, actor `:20`, module `:18`, goal `Item.cs:16`, step `Item.cs:11`); the reader registry + folder discovery; `ICreate` births; `BeginRecord`/`EndRecord`; `IOutput`; the unknown-kind-by-name answer (`type/list/this.cs:172`); `property` built from a C# class by reflection (`property/this.cs:21`); `type.Owns(System.Type)` (the CLR overload, `type/this.cs:727` + callers — NOT `kind.Owns(IWriter)`).

---

## Surface-golden lines that move (per locked change + decision)

Re-pin `PLang.Tests/Wire/App/Surface/surface_golden.txt` at each stage with its decision.

- **Stage 1 (867):** `app.type.item.this` — `protected virtual Type : app.type.this { get; }` and every `protected override Type : app.type.this { get; }` (48) move to the born-at-birth shape.
- **Stage 2 (868, 844, 846):** `IWriter` — `Bytes(System.Byte[] value)`, `Format : System.String`, `Raw(System.Byte[] value)`, `Raw(System.String value)` (→ `Bytes(byte[], kind)` + the output-kind object); `IReader` — `Format : System.String` (dead, removed); `app.type.item.this` — `virtual Write(app.type.format.IWriter)` + 47 overrides; `app.type.kind.this` — `virtual Owns(app.type.format.IWriter) : System.Boolean`. The `value.Reader` struct re-pins with `IReader`.
- **Stage 3 (869, 813):** `IWriter` — `virtual EmitsSchema : System.Boolean`. plang's own writer is NEW (its own output kind); `BeginRecord`/`EndRecord` stay.
- **Stage 4 (871):** a NEW `IReader` implementation (`type/format/item/reader.cs`) appears.
- **Stage 5 (870, 872):** `app.type.item.this` — the `Get`/`Set` signatures; `app.type.property.this` — `IsDefault(app.type.item.this value) : System.Boolean` changes, and `Read`/`Write` are **ADDITIONS** (property has neither today).

Counts to pin: `Type` overrides **48**; sync `Write` overrides **47** across **44** files.

---

## What the demolition list missed (flagged to the architect)

1. **More name-based type checks beyond the named ones** (same family as #1/#6/#8, stage 1):
   - `formal/reader.cs:317` — `declared.Type.Name == "text"` (text-face literal parsing).
   - `formal/reader.cs:415` — `if (v.Action != null) return "action";` (emits the literal "action" face — mirror of the stage-8 readers).
   - `serializer/Reader.cs:29` — `new @null.@this("action", kind)` (action slot by literal name on the null path).
   - `source.cs:240` — `_type.Name.ToLowerInvariant() is "dict" or "list"`; `source.cs:372` — `!_type.Is("text")`.
   - `separator/this.cs:28`, `prose/this.cs:31` — `new("text", …)` (text face minted by name).
   These are the kind-is-type F8.1/floor family surfacing again; if "by type not by name" is the rule, they belong with stage 1's name-check sweep. Verdict requested.
2. **Stage 2 `Write` framing conflates three signatures** (the plan's "sync Write / 46 files"): the real `item.@this.Write(IWriter)` is **47 overrides / 44 files**; separately there are the number-`Kind.Write(number.@this, IWriter)` (15 impls under `item/number/kind/**`, bridged at `number/this.cs:43`) and the `variable/serializer/Entry.Write(IWriter, …)` helper — both take an `IWriter` but are NOT `item.@this` overrides. Name the intent for those two.
3. **Module-side overrides outside `type/item/**`** (easy to miss under a `type/` scope): Write overrides `crypto/hash:51`, `browser/cursor:18`, `store:22`; several `Type` overrides are module-side (listed in #5). Included in the 47/48.
4. **`IReader.Format` is already dead** (no production reader) — removing it (stage 2) is free.
5. **The `Out` self-write (`type/this.cs:63`) and `Setting` (`:900`)** share the Get-time re-resolve pattern — they go with it in stage 1, not just `Get`.

---

## Stage 1's shape (for the architect)

**How a value gets its type at birth.** Today a value is constructed, then its type is produced two ways:
a per-class `override Type` (48 of them, e.g. `text/this.cs:92`, `image/this.cs:119`) that rebuilds a
`type.@this` each read, and the `[JsonConstructor]` type ctor (`type/this.cs:146`) that sets
`_kind = new kind(name)` — a kind holding the name but none of the registered kind's facts (mime, format,
IsText). The registered facts only reach a program because `type.Get` (`:886`) re-resolves through
`app.type.list` at read time. **Plan:** the registered type — the one that made the value — is handed to
the value's constructor and kept: `item.@this` gains `protected internal type.@this Type { get; }` set by
`protected @this(type.@this type) => Type = type`; every subtype's ctor takes and forwards the registered
type (`image.@this(byte[], type.@this) : base(type)`); the 48 `override Type` go; the image's `_mime`
becomes `Type.kind.Mime.FirstOrDefault()`; the Get/Out/Setting re-resolves go (the instance already holds
`full`). One place holds a value's type; nothing rebuilds it; no kind is made from a name.

**How a type named in a step is looked up.** Today a step's face string (`"image/gif"`, `"number"`) is
split (`formal/reader.cs:354`, `TypeName`/`TypeKind` at `:458-459`) and passed to
`app.type.list[new type.@this(name, kind), ctx]`; the indexer's `Full()` builds the entry, but the fresh
`type.@this(name, kind)` ctor on the way in mints `_kind = new kind(name)` — a bare kind. **Plan:** the
ctor keeps only the name; the registry lookup (`app.type.list[{name, kind}, ctx]` via `Full()`, which
already preserves `Strict` and `Template`) returns the registered type carrying the kind's facts. The one
changed line is the kind-from-name mint (`:149`); the lookup door already exists and is already how every
caller reaches a registered type — they just stop carrying a bare kind through it.

**The enabling fact for text** (pinned by `TextKindIsTextByItselfTests`): text's own format
`[Format("", "text/plain", ".txt")]` is declared without `Text = true`, so text's empty kind answers
`IsText = false` and `source.cs:115`'s Name floor compensates. Stage 1's "the registered kind carries the
fact" needs text's own-format to declare `Text = true` (or the registered kind to answer it) before the
floor can go. (This exact gap was found and left for branch A during kind-is-type.)

---

# Stage 0 — second pass (architect's verdicts, 2026-10-17)

## Verdicts accepted (recorded as the contract)
- The three re-resolves (`type.Get:886`, `Out:63`, `Setting:900`) all go in stage 1.
- Name checks: stage 1's sweep, but each fixed at **what it asks**, moved onto its owner — a string→class
  check is the same switch, so a "better comparison" is not the fix.
  - the bare-type makers (`separator/this.cs:28`, `prose/this.cs:31` `new("text", …)`,
    `serializer/Reader.cs:29` `@null("action", kind)`) ask the list for the registered type (its null, its
    kinded type).
  - the name comparisons (`formal/reader.cs:317`, `:415`, `source.cs:240`, `:372`): their questions below.
- number's `Kind.Write(number, IWriter)` (15) **stays** — the kind writing a value of its own kind, the
  kind being the behaviour, not a second item door. `variable/serializer/Entry.Write` is out of #2's scope
  — left, listed as a later look.
- module-side overrides move with the rest. `IReader.Format` removed with stage 2's `IWriter.Format`.
- Counts: **47 sync `Write` overrides in 44 files** (the plan's "46"); **48 `Type` overrides**.

## The four name comparisons — the question each really asks, and whose answer

| site | the question | whose answer | verdict (proposed) |
|---|---|---|---|
| `formal/reader.cs:317` `declared.Type.Name == "text"` | "does this declared property lex a bare unquoted word as its value?" — one arm of the formal reader's lexing chain (text here; `duration` `:323`; number etc. follow) | the declared property's type (what shape its bare literal takes) | a **parse-time lexing rule** keyed on the type. It is the same switch as the chain around it; moving one means a type-level "read a bare token" hook for all of them (text, duration, number, …). NOT a runtime value question. Bring the chain's reshape to the architect; may stay parser-local. |
| `formal/reader.cs:415` `LiteralType(v)` (`v.Action != null → "action"`, Items→list, Entries→dict, variable→item, else Kind) | "what type-name does a PARSED literal's shape denote?" | the parsed literal's own shape | **OBP violation (Ingi, 2026-10-17 — overrides the earlier "stays")**: a static helper switching on a literal's shape to return a type-NAME string is the fork / stray-helper smell (`Helper.X(thing)` that should be `thing.X()`). The `Literal` answers its own type, not a name-switch. It is production (the builder's formal reader), so OBP applies. Belongs with the **formal-lexer reshape** (its own item, with `:317`, the builder's parser) — NOT stage 1. |
| `source.cs:240` `_type.Name is "dict" or "list"` | "was this value declared a genuine container, so a parse to a non-container is round-trip loss — fail loud?" | the declared type (is it a container) | a plang-level **`is`** question — `_type.Is("dict") || _type.Is("list")` says it already (a type-level `IsContainer` would be cleaner). Proposed: rewrite as `_type.Is(...)`; fine as a plang `is`. |
| `source.cs:372` `_type.Template != null && IsVariable && !_type.Is("text")` | "is this a whole %ref% in a non-text slot, so write the variable itself, not the text?" | the value (`IsVariable`) + the slot's type (is-text) | already a plang `is` (`_type.Is("text")`), AND it lives inside the source's **sync `Write`** which is removed in **stage 2**. Dies there; the behaviour moves to the source's `Output`. No separate stage-1 fix. |

## Every `new type.@this(` — which become lookups

The leak is a caller MAKING a type object from a name (even as a lookup key). Stage 1 makes the by-name
ctor (`type/this.cs:146`) the **list's own** (internal); every caller that today builds a `new
type.@this(name[, kind])` as a key for `app.type.list[…]` calls a list door instead (taking the name and
kind; the `.pr` reader's `ReadContext` already carries the context).

**Become lookups** (a list door takes the name/kind; no bare type made by the caller):
| site | today | note |
|---|---|---|
| `formal/reader.cs:354` | `type.list[new @this(TypeName(face), TypeKind(face)), ctx]` | the step-named-type read |
| `formal/reader.cs:376` | `type.list[new @this(type.Name, kind.Name, strict, template), ctx]` | re-resolve a declared type |
| `type/serializer/Reader.cs:25` | `type.list[new @this(reader.String()), ctx]` | `.pr` type read (bare name) |
| `type/serializer/Reader.cs:65` | `type.list[new @this(name, typeKind, strict, marked), ctx]` | `.pr` kinded type read |
| `type/this.cs:253` | `type.list[new @this(described, kind), ctx]` | DescribeTo (dict shape) |
| `type/this.cs:256` | `type.list[new @this(named), ctx]` | DescribeTo (named) |
| `kind/empty/this.cs:117` | `type.list[new @this(_owner, k.Name), ctx]` | kind.list building its full types |
| `pick/list/this.cs:630` | `type.list[new @this(Shown(value)), ctx]` | pick resolve (bare name) |
| `pick/list/this.cs:650` | `type.list[new @this(picked.Name, kind), ctx]` | pick refine (kinded) |
| `type/list/this.cs:128` | `this[new @this("binary", ext), ctx]` | Extension (list-internal key) |
| `type/list/this.cs:133` | `this[new @this(kind.Owner, kind.Name), ctx]` | Of (list-internal key) |
| `serializer/Reader.cs:29` (null path) | `new @null.@this("action", kind)` | ask the list for the registered action null |
| `separator/this.cs:28`, `prose/this.cs:31` | `new("text", …)` | ask the list for the registered text |

**Stay — the by-name/entry ctor's only homes** (where a type is legitimately born, carrying `ClrType`, or the list building its own entries; these become the ctor's internal callers):
| site | why it stays |
|---|---|
| `Registry.cs:98,99,163,168` | startup CLR registration — carries `ClrType`, the registry is where types are born |
| `type/list/this.cs:160,174,277` | `Full()` / reface — the list building its full/kinded entries |
| `type/serializer/Reader.cs:63` | `new @this(entry, element, strict, marked)` — kinded-from-registered-entry (a generic's element), not bare |
| `type/clr/this.cs:68` | the clr carrier's `{item}` type — carries `typeof(item.@this)` |
| `type/this.cs:568` | `new @this(typeof(T))` — the CLR-type ctor (data.Refuses) |
| `kind/this.Generic.cs:22,27` | the kind's parent type `new @this(NameOf(typeof(T)))` — internal to kind construction |

So the `type.list` gains a door (name + optional kind/strict/template, taking a context) that is the only
way a program/reader names a type; the by-name ctor (`:146`) and the kinded-from-name are internal to the
list + registry. Nothing outside makes a bare type.

## The decode path and `Text = true` — how it changes (and the remaining gap)

On kind-is-type, `Text = true` alone (even with a narrow born-whole) still broke ~13 template/file/url
tests. Traced here, the cause is exact and it is a born-whole gap, not a reason to keep the floor.

The text face that templates read is `Peek()`:
- `content/this.cs:95` `read.Peek().HasVariable` — detects a template's `%vars%` on the decoded text.
- `source.cs:73` (in the source ctor, reached only when `_type.Template != null`) `var raw = Peek() as string;` — if `Peek()` returns `byte[]` not a string, `raw == null`, `_variable` is empty, and the template is never recognised. **This is the break.**
- `source.cs:295` (the `Template` property) `… && Peek() is string template`.

`Peek()` decodes bytes to the text face only when `_type.kind.IsText` (or, today, the `Name is "text"`
floor). `IsText` resolves: `FormatAttribute.Text` → `kind.this:82 _text` → `kind.this:128 IsText`; and an
empty kind delegates `empty.this:35 IsText => _format?.IsText ?? false`, where `_format` is the type's own
`[Format("")]`, loaded onto the registered entry's empty kind at registration (`empty.this:125`). Text's
own format `text/this.cs:30 [Format("", "text/plain", ".txt")]` has no `Text = true`, so today it is false.

Two sub-cases decide whether holding the registered type is enough:

- **A plain (non-template) file/url text** — `content`'s decode (`kind.this:265`) builds
  `new type.@this("text", null, null)`; `type.list[…]`→`Full` hits `list/this.cs:175`
  (`kind == null && !Strict && Template == null`) and returns the **registered** text entry, whose empty
  kind has `_format` loaded. So with `Text = true`, `_type.kind.IsText` is **true** — the floor closes for
  this path.
- **A template read** (exactly the `Read_TemplateTrue` / `AFile*`/`AUrl*` content-as-template tests that
  broke) — `content`'s decode passes `template`, so `Full` skips the registered entry (`Template != null`)
  and takes `list/this.cs:176` `new type.@this(entry, kind: null, strict, template)`. That
  kinded-from-family ctor (`type/this.cs:750-755`) at **`:754`** does
  `_kind = kind ?? new kind.empty.@this(Name)` — with `kind == null` it mints a **fresh** empty kind whose
  `_format == null`, **discarding** `family(entry).kind` which had the loaded format. So the
  template-declared value's `_type.kind.IsText` stays **false even with `Text = true`**, and only the Name
  floor keeps `Peek()` a string. This is why `Text = true` alone regressed on kind-is-type.

**How the path changes in stage 1 (the shape):** text's own format declares `Text = true`, AND the
born-whole closure reaches `type/this.cs:754` — the `kind == null` branch reuses `family.kind` (the
registered empty kind carrying `_format`) instead of minting a bare `new empty.@this(Name)`. Then a
template-declared text holds the registered kind, `_type.kind.IsText` is true on both paths, and the
`source.cs:115` Name floor goes with nothing left leaning on it. `:754` is part of "no bare kind" — it is
the kinded-from-family ctor making a bare kind, the same leak as the by-name ctor, so it is listed with
the `new type.@this(` closure above.

(Pins: `TextKindIsTextByItselfTests` for the plain path; the `Read_TemplateTrue` / `AFile*`/`AUrl*`
template tests — green today via the floor, must STAY green when the floor goes, proving `:754`'s fix.)
