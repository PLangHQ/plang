# app/type/dict

`dict.@this` is a key→value map. It mirrors `list.@this` exactly — same slot model, same aliasing contract.

Its backing is a single `Dictionary<string, object?>` of **raw-or-item slots**. One store. (An earlier design had `_keys` + `_map` as separate fields — that's gone. One field, one source of truth.)

## The slot model

**Assign a CLR dict** (`set %obj% = src`) — aliases the source by reference. O(1), no walk. Ownership hand-off.

**Read a key** (`%obj.name%`) — borns a fresh item from the slot. Slot not mutated.

**Write a key** (`set %obj.name% = v`) — slot elevates in-place to the item.

**`.Clr`** — all-raw → returns the same backing ref. Any elevated slot → peels per-element and rebuilds.

Built dicts are `OrdinalIgnoreCase`. A dict aliased from a foreign `Dictionary` keeps whatever comparer the source had — `dict.@this` doesn't police casing on an aliased backing.
