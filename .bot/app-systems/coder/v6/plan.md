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

## Answers (plang-40)

1. Yes: the row's list rides on the ReadContext; an inner text takes the variables whose text it holds.
2. Yes: value before variable; hold the raw until the object closes, birth whole.
3. **Delete `%setting.X%`** in stage 6 (actor/this.cs:87, and `RegisterNavigable` if nothing else uses it).
4. Yes: the store's Get/Set(string) take root names; list the path-passing callers in 6f.
5. Yes, interim: the text form at the container; a typed container door is later work (note in write-up).
6. **Explicit opt-in:** a method is reachable from a variable only when its type marks it (`[LlmBuilder]`
   if it fits, else one small attribute). Unmarked or missing → "text has no method 'foo'".

7. **Index keys (approved):** `[0]` a number, `["k"]` a text, a bare path (`[idx]`) or `[%i%]` a
   variable key with its own code and no type. The silent literal fallback goes: an unset index
   variable is IndexNotSet. os/ and Tests/ have 9 bare indexes, all real variables; 0 relied on the
   fallback. Plang-visible: `%dict[key]%` meaning the literal "key" must now be `%dict["key"]%`.

## 6b decisions (as built)

- Hops are items (`code.@this` is a `list<Hop>`, like `action.list`); the base is `Hop`, not
  `hop.@this`, because the startup scan would register an abstract `@this` item as a type `hop`.
- A reference also ends at a space outside quotes, parentheses and brackets (`50% of %total%` holds
  one reference). The old `Formal.cs:418` regex had the same rule; `RefRx` didn't.
- Forms the old hand scan called malformed are now plain code: `%x.kind!cost%` (the child's binding),
  `%x!a!b%`, `%!x!cost%`. `%x!!cost%` and `%x!%` don't parse (Resolve throws InvalidVariable, the
  Convert door declines with the parser's reason). `RawValue`, `WasPercentWrapped`, `IsMalformed`,
  `Property` are gone; `Name` (the text without its `%`) stays until 6f moves the string callers.
- `variable.set` writes through `name.Set(Value, Context)`; a `!` write on an unset variable is
  VariableNotFound as before; a member/index write on an unset root makes it an empty dict as before.
- item gains `Get(parent, key, isIndex)` (the read twin of `Set(key, isIndex, …)`); clr answers it
  per step through its kind. Data gains `Set(key, isIndex, value)` (the leaf of `Set(path, …)`).
- Still running beside the new code until 6f: `data.Get(path)`'s walker (its `!` lookup and
  IndexNotSet are duplicated in the hops), the store's path handling, text's RefRx render.

## 6d/6e decisions (as built)

- `item.Variable` (read-only, each variable once, first written first; `[]` when none) and
  `item.HasVariable`. text holds its own; a stamped dict/list its entries'; source/wire the row's list;
  a variable holds itself. step's build-walk list is now `step.Typed` (ruled; properties.template too —
  the rendered prompt is unchanged, line 7 is inside a template comment).
- A stored row (Data's and an action property's) writes `"variable"` after `"value"` when its value
  holds any — a marked template, and a variable slot too (its one variable), so loading never parses a
  name either. Both readers hold the value's bytes until the row closes, then birth it with the list
  (`ReadContext.Variable`); a text inside a container takes the ones written in it.
- Refusal (PrFormatOutdated): an authored read (`ctx.Template` set, the .pr load) of a marked row
  without its list, and any action property row marked without one.
- Build side: `Formal.Born` hands the value its variables (the parser over each of the json's texts);
  `Formal.Arguments` embeds each argument row's list. Writing a list is synchronous (hops write leaves).
- The 16 tracked .pr with marked rows were rewritten by a throwaway pass (insert lists, then load and
  save through `serializer.Text`); `plang build` of Tests/Simple writes the same bytes. Relayed wire
  slices are now compact (the form the build writes); a bare variable name is written `%name%`.
- Twin: `tools/decider/variables.py` is the parser's twin; formal.py/formal_check.py mark by it and
  write each row's list; formal_golden.json regenerated; `VariableListTwinTests` holds the two equal.

## 6f decisions (as built)

- Ruled: `data.Get(string path)` stays as the relative-path door, holding no walk of its own — the
  parser reads the path (`parser.Path()`: hops, no root) and the code starts from this Data. Deleted:
  `app/type/item/variable/path/` (Parse, Segment, Index.Key's re-parse), the walker's switch and the
  clr/kind whole-path walk (a clr answers one step: `Get(parent, key, isIndex)`), `data.Set(path, …)`,
  Data's private `!` lookup (the property hop owns it), `CleanName` ×2.
- The variable store takes root names only (Get/Set/Peek/Contains/Ensure/Replace/Remove); the
  `%setting.X%` navigable mount is gone (settings go through `%!…%`, stage 7). Consumers moved onto the
  variable's doors: list.* (17), variable.get/exists/compress/decompress, loop.foreach (item/key as
  variables), output.ask (`%!ask.answer%` is a path through the `!ask` root). New variable doors:
  `Ensure(value, ctx)` / `Replace(expected, value, ctx)` — a bare name keeps the store's atomic ones.
- The other definitions of a reference now run the parser: Formal's bare-name check and `%…%` read,
  `step.Cover` (was `Marker`), `step.Scope` (was `Named`), the debug display (asks each value's
  `Variable`), pick's First/Target/Assigned/`write to` (the grammar around a reference stays a regex;
  the reference is the parser's: `variable.IsBare`, `variable.IsMembers`). The python twins
  (formal.py, prompt_c.py) mirror each through `variables.py`; every fixture regenerates unchanged.
- `data.HasVariableReference` → `data.HasVariable` (the item's answer), the generator's emitted guard too.
  A file marked a template holds no variables until it is read (FileHandlerTests says so).
- Tests: path strings handed to the store go through a variable; the `%setting.X%` tests and the
  path-tokenizer parity test are deleted with their feature; Data no longer strips `%`/spaces from names.

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
