# coder v6 — stage 6 plan: the reference

Branch `app-systems`. Plan section: architect `plan.md` "The reference (stage 6)" + the stage table row.
Trace first (read only); no code changed yet.

## The trace — how `%user.address[%i%].city%` runs today

```
.pr row {"type":{"name":"text","template":"plang"},"value":"…%x.y%…"}
  data/reader/this.cs:74          typeRef.Read → a lazy source; nothing parsed
  text/this.cs:103 Value(data)    TryFullVarMatch(_value) → Variable.Get(name)          // whole-value ref
                   :127 Rendered  RefRx "%[^%]+%" over _value, each → Variable.Get(name) // partial
  variable/list/this.cs:331 Get   CleanName, path.Parse(name).Root, remaining string → root.Get(remaining)
  data/this.Navigation.cs:17      path.Parse(remaining) again → Get(path)
                          :33     clr special case (whole tail to clr.Get); else switch on head:
                                    Infra  → GetInfrastructureValue (:240, the `!` order)
                                    Call   → InvokeMethod (:143, 7-name string switch + regex args)
                                    Index  → index.Key(store) (re-parses the inner as a path, :Segment.cs)
                                    Member → _item.Get(this, name)
                                  recurse on tail
write: variable/list/this.cs:106 Set(name) → path.Parse; bare root rebinds; deep → data.Set(path.Tail) (:104)
```

A reference is parsed at every read: `Parse` runs in the store (Get, Set, Peek, Contains, Ensure, Replace),
in `data.Get(string)`, in `clr.Get`, in `text.Unreachable`, and each index re-parses its inner.

Other definitions of a reference (all go): `text.RefRx` `%[^%]+%`, `data.TryFullVarMatch` `^%([^%]+)%$`,
`Formal.cs:418` `%[^%\s]+%` (no space, so `%x.replace("a b","c")%` can't be written), `Formal.cs:311`
bare-name regex, `step/this.Validate.cs:26` Marker, `step/this.Scope.cs:9` Named, `pick/list/this.cs:90,96,
132` (+ `:92` `as %x%`), `debug/this.cs:506`, `variable.@this.Convert`'s hand scan (Property,
IsMalformed, WasPercentWrapped), `CleanName` ×2.

Surfaces: `app.variable.@this` 39 prod / 94 test refs; 30 `Data<variable>` slots (list.* ×19,
variable.{set,get,remove,exists,compress}, loop.foreach Item/Key, channel.set Encryption/Signing);
116 marked rows in 16 tracked `.pr`. Method hops in goals today: 5 uses (`replace` ×4, `toupper` ×1).

## Shape (as settled)

```
app/type/item/variable/this.cs          variable : item — Text + Code; Value() → Start(ctx) → Code.Start(ctx); Set(value, ctx)
app/type/item/variable/parser/this.cs   the one definition: Parse(text) → variables; Read(text, at) → one variable (Formal)
app/type/item/variable/code/this.cs     the hop list (a list<hop>); Start walks it, each hop gets the previous Data
app/type/item/variable/code/{variable,property,index,method}.cs   each parses, writes, runs (and writes-at) its piece
app/type/item/variable/list/this.cs     the memory (moved from app/variable/list/)
app/type/item/this.cs                   Variable (read-only, shared empty), HasVariable, IsVariable
```

## Proposed order (each its own commit, suites at baseline, pushed)

- **6a — the move.** `app.variable.@this` → `app.type.item.variable.@this`; `app/variable/list` →
  `app/type/item/variable/list` (IName, Reserved, call/, serializer/ follow). Generator strings
  (`Discovery/this.cs:190`, `Emission/Property/Data/this.cs:192`) in the same commit, with a
  generator test that the missing-parameter guard still fires. No behaviour change.
- **6b — parser + code + hops.** Parser, the four hops, `variable = text + code`, `Start`, `Set`
  (last hop writes itself). The `!` hop keeps today's lookup order. C# tests per hop.
- **6c — text owns its methods.** grep, grepcount, maxlength, trim, tolower, toupper, replace on text;
  the method hop finds a method on the value; a miss is an error ("text has no method 'foo'").
- **6d — the .pr.** `item.Variable`/`HasVariable`; Data's Store view writes `"variable"`; the reader
  reads it; a marked row without one is PrFormatOutdated. text renders from its variables (no RefRx).
- **6e — throwaway pass** over the 16 marked `.pr` (parser + the Store writer), deleted before commit.
- **6f — consumers onto the variable**, old parsers deleted (the demolition list), `data.HasVariable`.
- **6g — twins + one eval run.**

## Questions for plang-40 (proceeding on 6a meanwhile)

1. **Inner slots.** A marked dict/list row (`{"a":"%x%"}`) births its inner texts when it materializes,
   through the same ReadContext. My proposal: the row's `"variable"` list rides on the ReadContext
   (beside `Template`), and an inner text born under it takes the variables whose `text` it holds — so
   loading never parses. OK?
2. **Read order.** The row writes `value` before `variable` (the plan's example). The reader has to hold
   the value's raw bytes until the object closes, then birth the item with its variables (born whole).
   Alternative: write `variable` before `value`. I'd keep the example's order and hold the raw.
3. **`setting` navigable** (`actor/this.cs:87`): its resolver takes the rest of the path as a string.
   Proposal: the root hop, on a navigable root, hands it the rest of the variable's text for now (one
   place, named), until settings is navigable by hop. Or should settings become a value the hops walk?
4. **Strings in C#.** `context.Variable.Get("user.name")` — C# callers passing a path string. Proposal:
   the store's `Get(string)`/`Set(string)` take a root name only; a path goes through a variable
   (`parser` once, then `variable.Value()`/`Set`). I'll audit the path-passing callers in 6f and
   list them.
5. **Index key at the container.** The hop's key is typed (`{"number":"%i%"}`), but containers'
   `Get(parent, string key)` / `Set(key, isIndex, …)` take a string. Proposal: the index hop resolves its
   typed value and hands the container its text form, as today; a typed container door is later work.
6. **Method lookup.** The value owns its methods: the method hop finds a public method on the item by
   name (ignoring case) with plang-typed parameters, the same way the `!` hop finds members today. Or an
   explicit per-type method table?
