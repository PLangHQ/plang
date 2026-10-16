# Stage 1 probe — the shape (number's kinds as `kind<number>`)

Trace-first, before writing (per the architect's brief). Decision 774: `kind<T> : kind : type`,
a kind is a type under its parent, answers what is its own, passes the rest to the parent.

## What I traced

- `type.@this : item.@this` (`type/this.cs:33`) — a type already **is** an item, so `kind : type`
  makes a kind a type-and-item.
- Today's base `kind` (`type/kind/this.cs`) carries **two roles welded together**:
  - **(a) subtype identity** — `Name`, `Mime`, `Extension`, `Alias`, `this[name]`/`[clr]`, `Values`,
    `Offers`, `Description`, `Example`, `type(ctx)`, `list(ctx)`, `Owner`, `Names(key)`.
  - **(b) host behavior** — `Set(host,…)`, `Output(obj,…)`, `Clr(host,…)`, `Read(obj,…)`, `Descend`,
    `Enumerate`, `IsSequence(host)`, `Parse`, `Load`, `Decode`, `Encode`, `Open`, `Convert`.
    Only the C# host kinds (json / dict / list / `*`) use (b); the plan keeps those **until 673**.
- A **number value asks its kind only two things**: `Kind.Write` (`number/this.cs:43`) and
  `Kind.Create` (`number/this.Unary.cs:33,40,47,63`). It never routes `Output`/`Clr`/`Set`/`Get`/
  `Descend`/`Convert` through the kind — number owns its own value ops. **So number never asks its
  kind back.** ✓ (no cycle to design around)
- Number's kind base (`number/kind/this.cs`) already adds only `Create(value)`, `Write(value,w)`,
  `Read<TReader>(ref r)`. `decimal` (`…/kind/decimal/this.cs`) overrides exactly those three.
- The clashes are all **overloads, not overrides** — `kind.Set(object host,…)` vs
  `item.Set(string key,…,ctx)` (`item/this.cs:176`); `kind.Clr(host,target,ctx)` vs
  `item.Clr(target)` (`item/this.cs:672`); `kind.Output(obj,…)` vs `item.Output(writer,…)`
  (`item/this.cs:870`). They'd coexist silently on `kind : item` — the smell is one class holding
  both "operate on a host of my kind" and "be a value myself."

## The shape I propose (3 layers)

```csharp
// app/type/kind/this.cs — a kind IS a type, under a parent. Keeps only role (a): subtype identity.
public abstract class @this : app.type.@this {
    protected @this(string parent, string name) : base(parent, kind: name) { }   // the type {parent, kind:self}
    // mime / extension / alias / Owner(parent) stay here; role (b) is GONE from this base.
}

// kind<T> : the one place that passes every unanswered type/value question up to the parent type.
// Mechanism: it IS a kinded type whose Family is the parent entity — type.@this already reads its
// facts (Property, Values, Shape, Description, Alias, Offers, Setting) through `Family`, so pointing
// Family at the parent forwards them for free; only Get/Output-as-a-value need an explicit forward.
public abstract class @this<T> : app.type.kind.@this where T : app.type.item.@this {
    // parent entity resolved by T's plang name; _family := parent; Get/Output defer to it.
}

// number's kind base — adds number's OWN three (parse + token); unchanged except its base.
public abstract class number.kind.@this : app.type.kind.@this<number.@this> {
    public abstract number.@this Create(item.@this value);
    public abstract void Write(number.@this v, IWriter w);
    public abstract item.@this Read<TReader>(ref TReader r) where TReader : IReader, allows ref struct;
}

// decimal — body unchanged; only its base changes (number.kind.@this is now a kind<number>).
public sealed class @decimal : number.kind.@this { Create / Write / Read }   // 3 lines, as today
```

`decimal` answers itself: **Create / Write / Read\<token\> (parse + precision) + its name "decimal".**
Everything else — add/round/abs, Output-as-a-value, Clr(target), navigation, Description, Example,
Offers, Values — is **number's**, reached through `kind<T>`'s Family forward. Number's value ops
never touched the kind to begin with, so nothing moves.

## Each clash, settled for number

| verb (host form on today's kind) | item/type self-form | settlement for number |
|---|---|---|
| `Set(host,key,…)` | `item.Set(key,…,ctx)` | **not number's** — a number has no children; host form stays on json/dict/list kinds (→673) |
| `Output(obj,…)` | `item.Output(writer,…)` | number value writes itself; the kind-as-value uses `type.Output` (its identity). Host form → host kinds (673) |
| `Clr(host,target,ctx)` | `item.Clr(target)` | number value self-lowers; host form → host kinds (673) |
| `Read(obj,ITypeReader,kind)` | `type.Read(slot)` + number's `Read<TReader>` | number's own is `Read<TReader>` (the token). The format-bridge `Read(obj,…)` declines for number (null today) → host kinds |
| `Convert(source)` | — | not number's; conversions are the outbound/host's |
| `IsSequence(host)` | — | false; host form → host kinds |
| `Names(key)` | `type.Names(key)` | **folds**: a kind is found *through its parent* (`number.kind["decimal"]`, ruling 2), so there's no app-wide `Names`. The type's own-name match covers it; the kind keeps mime/ext matching (empty for number) |

**The real cost (what stage 3 / 673 inherit):** role (b) must come *off* the `kind : type` base and
live on the C# host kinds. In stage 1 I don't move it — number's kind base simply stops inheriting it
(it changes base to `kind<number>`, which has only role (a)). Stage 3/4 need a home for (b) on the
host kinds (a `host.kind` base they keep, or on json/dict/list/`*` directly) until 673.

## OPEN (need your call before I write)

1. **`kind<T>` generic arg** — I read T as the value class (`number.@this`), with `kind<T>` resolving
   the parent **type entity** by T's plang name and setting `Family` to it. Agree? (This reuses the
   existing kinded-type `Family` indirection rather than inventing a forward.)
2. **The self-referential `kind` field** — a kind, as the type `{number, decimal}`, needs its own
   `kind` field to be *itself* (the decimal kind). `type.@this`'s ctor sets `_kind` from the kind
   *name*; for a `kind : type` it must set `_kind = this`. Confirm that's the intent (a kind's kind
   is itself; a kind's own kind-field is otherwise empty).
3. **Role (b) in stage 1** — OK to leave host behavior exactly where it is (json/dict/list/`*`) and
   just not inherit it into `kind<number>`? Nothing else moves this stage.
4. **`DecimalIsANumber`** — I can't find it by name in `PLang.Tests/`. Is it a plang test
   (`test/plan/kind-is-type/…`) or to-be-written? I'll confirm it green after you OK the shape and I
   implement.

No code written yet. Gate on implement: `./dev.sh test` + `cd test && …/plang --test` after `./dev.sh build`.
