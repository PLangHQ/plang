# app/type/list

`list.@this` is an indexed sequence. Its backing is `List<object?>` of **raw-or-item slots**.

## The slot model

A slot holds either a raw CLR value or an item. That's it — never a `Data` wrapping a `Data`.

**Assign a CLR list** (`set %list% = src`) — the list aliases the source backing by reference. O(1), no walk, no JSON round-trip. This is an ownership hand-off: the CLR list becomes the list's backing.

**Read a slot** (`%list[i]%`) — borns a fresh item from the raw slot. The slot is not mutated. This keeps the backing pristine and makes it safe to read inside a `foreach`.

**Write a slot** (`set %list[i]% = v`) — the slot elevates in-place to the item (`number.@this(9)`, `signature.@this{…}`, …). Never a bare Data, never lowered back to raw.

**`.Clr`** — if the backing is all raw (nothing has been written), returns the original backing reference. O(1), same ref. If any slot was elevated, peels each element and rebuilds. This keeps signed or encrypted elements intact at rest — a `signature.@this` in a slot is not wrongly unwrapped on the fast path.

Slot state is the history:
- **raw** — untouched, straight off the aliased source or the wire.
- **item** — written at least once.

A million-row list you only read stays O(1) and same-ref the whole time. The instant you write a row, that row elevates and `.Clr` honestly rebuilds.

## Why reads don't write back

Two reasons:

1. **Enumeration safety** — writing back to `_value[i]` mid-`foreach` throws `InvalidOperationException` (collection modified during enumeration).
2. **Aliased backing** — a mere read would mutate the caller's original list, knocking the all-raw `.Clr` fast-path off the same-ref guarantee.
