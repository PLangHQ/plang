# Plan: `list`/`dict` raw-slot model — O(1) native-collection values

**Status:** designed + agreed with Ingi (this session). Not yet implemented.
**Owner:** coder, next context.
**Branch:** compare-redesign.

## The problem (what triggered this)

`set %list% = <a native CLR collection>` currently routes the value through
`Data` ctor → `Lift` → **`JsonSerializer.SerializeToElement(src)` → reparse** into a
brand-new `list.@this`. For an N-row list that's an O(N) serialize + O(N) reparse
(two full copies, a giant intermediate JSON blob). A million-row DB result is a
million-element JSON string round-trip. The reference is destroyed and `.Clr`
rebuilds yet again.

The bucket-2 tests (`Set_ListValue_StoresDistinctListInstance`,
`Set_DictValue_StoresDistinctDictInstance`) assert reference identity and fail
because of this. They are currently RED and were left at their original shape on
purpose — they belong to this plan (test-first).

## The agreed model

A `list.@this` **is an item**; its backing is `List<object?>` of **raw-or-item
slots** (never a `Data` per element):

```
list.@this  (: item.@this)
  _value: List<object?>

  assign CLR list        → alias it as _value, O(1), NO walk, NO json
  read  %list[i]%        → born a FRESH item from the slot; slot is NOT mutated
                           (enumeration-safe; keeps the backing pristine)
  write set %list[i]%=v  → slot ELEVATES to the item.@this (number.@this(9),
                           signature.@this{…}, …) — never a bare Data, never
                           "lowered to raw"
  .Clr                   → all slots raw  → return _value            (same ref, O(1))
                           any slot item  → peel each (item→raw), rebuild (new list)
```

Slot-state IS the history:
- **raw** = untouched, straight off the aliased source.
- **item** = touched by a write (or a structured value dropped in by add/set).
- **read** borns a fresh item but leaves the slot raw.

So: a million-row list you only *read* stays O(1) and same-ref forever; the
instant you *write* a row, that row elevates to an item and `.Clr` honestly
rebuilds (Ingi: "if we modify the list, it's not the same ref, so modifying is
fine").

`dict.@this` mirrors this exactly (key→raw-or-item slot map).

### Why "store the item on write", not raw CLR

Schema layers — `signature` / `encryption` / `compression` — are each their own
`item.@this` type (NOT `Data` subclasses) whose `.Value` is the inner Data; they
serialize as `{@schema:X, type, …, value:<inner>}` and `.Clr`-peel to the inner.
A signed element in a list is a `signature.@this` riding in the slot intact. So a
write must store the **item** (the only thing that preserves a signature/cipher
layer at rest). Scalars elevate to `number.@this`/`text.@this`; `.Clr` lowers
them back. Uniform: writes store items, reads born items, raw is only the
initial aliased state. Nothing is lost.

### Read must NOT write back (two reasons)

1. **Enumeration safety** — `Row()` doing `_value[i] = data` mid-`foreach` is a
   C# `InvalidOperationException` (collection modified during enumeration).
2. **Aliased foreign backing** — a mere read would mutate the caller's list and
   knock the list off the same-ref `.Clr` fast-path.

`set %list% = src` is an **ownership hand-off** (src becomes the list's backing);
that's the contract that licenses aliasing.

## Implementation (3 production spots + tests)

Test-first. Write/blacken the tests, then implement until green.

### Tests (write first)
- `SetTypeInferenceTests.Set_ListValue_StoresDistinctListInstance` — `set %list%
  = new List<object?>{…}` → `await Value()` is `list.@this`; `.Clr<List<object?>>()`
  ReferenceEquals the source (pure-read path, same ref); and assert NO json
  round-trip happened (e.g. a MaterializeCount/alloc probe, or simply that the
  backing IS the source via an internal accessor).
- `Set_DictValue_StoresDistinctDictInstance` — same for `Dictionary<string,object?>`.
- NEW: million-ish-row O(1) probe — wrap a large list, assert construction does
  not enumerate (a counting `IList` wrapper whose indexer/enumerator increments a
  counter; assert counter==0 after `set`, ==1 after a single `%list[k]%` read).
- NEW: write-elevates — `set %list[2]% = 9` then `.Clr` rebuilds (new ref) and
  slot 2 reads as `number.@this`.
- NEW: signed element rides intact — `add %sData% into %list%`; `%list[0]%` is a
  `signature.@this`; wire re-emits `{@schema:signature,…}`.

### Production
1. **`PLang/app/data/this.cs` — `Lift`** (the `IList`/`IDictionary` branch, ~line
   217): replace `json.Parse(SerializeToElement(v))` with a **direct alias** —
   construct `list.@this`/`dict.@this` wrapping the CLR container by reference
   (new internal ctor `list.@this(List<object?> backing, bool aliased)` /
   dict equivalent). No JSON, no walk.
2. **`PLang/app/type/list/this.cs` — `Row()`** (~line 40-52) and the dict read
   path: born fresh from the raw slot, **delete the `_items[i] = data`
   write-back**. The index-write path stores the `item.@this` (elevate), not raw.
3. **`PLang/app/type/list/this.cs` — `Clr()`** (~line 417) and dict `Clr`: add the
   same-ref fast path — if the backing is still all-raw (track an
   `_elevatedCount`/dirty flag flipped only on write), return the aliased backing
   directly; else the existing peel+rebuild.

### Watch-outs
- `dict.@this` parallels every step; do both together.
- The "all slots raw" check must be O(1) — track a dirty flag / elevated-count,
  don't scan N slots on every `.Clr`.
- Confirm `.Clr` egress on a list with a `signature` slot peels to the inner
  attested Data (current `Unwrap` already does `item.Clr<object>()`).
- Don't regress the existing JSON-ingest path (`json.Parse` of a real
  JsonElement off the wire still borns raw slots via `ArrayLeaf`/`AddRaw`).
- `AddRaw` stays the wire/literal-parse seam; the new alias ctor is the
  CLR-handoff seam.

## Done / not-done bookkeeping
- VarReference template-stamp test: DONE (committed — separate concern, the
  authored seam stamps `Template=plang`).
- bucket-2 tests: reverted to original (RED) — rewrite them as the first step of
  this plan.
