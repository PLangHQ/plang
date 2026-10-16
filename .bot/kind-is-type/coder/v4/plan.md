# coder v4 — kind-is-type stage 4 (trace + shape)

**Stage:** 4 — "a format owns its bytes" (decision 774, rulings 8–9).

**Trace-first design task.** The architect: *"Trace first, then send the shape."* DONE WHEN "stage 4's shape is with me." **No implementation this version** — deliverable is `v4/stage4-shape.md` → architect.

## Scope (architect + start.md stage 4)
- A json value (`{item, kind: json}`) holds its RAW bytes until first use, then parses itself into a plang **dict or list** — no `JsonElement` carrier.
- Passing it on writes the bytes unread (PassedOnUnread).
- csv the same, through table.
- A dict slot given an unread json reads through json (TypedSlotJsonReadTests, decision 758).
- The strict tests' magic bytes (`as image/gif, strict`).
- Goes: item's json navigator (`type/item/kind/json/this.cs` :64–84, :89–91 — Descend/Enumerate/Data/IsSequence walking a JsonElement, its count/first/last); text's json wrapper + `*format` coiner (`type/item/text/kind/{this,format}.cs`); **with them Held's last bare-name branch** (the coiner was its only remaining user, per stage 3 item 3).

Tests: PassedOnUnread, ReadIntoIsADict, ArrayIsAList, ReadIntoIsATable (guard), JsonIsItemsKind, TypedSlotJsonReadTests, the kind/strict tests.

## Approach
- Core json kind traced directly.
- "Who reads a json value's C# carrier (JsonElement)" — Explore fan-out (http bodies, settings, cached answers, llm), since each moves onto plang values.
- Synthesis → `v4/stage4-shape.md` → architect. REPLY Done/HASH/CHECK/OPEN.

## Core mechanism (traced)
- Today a structured json value is a `clr(JsonElement)` (`json.@this.ClrForm = JsonElement`); the json kind NAVIGATES it (Descend/Enumerate/Data/IsSequence/Set, :65–122) and writes it (Output :198). `Parse` (:130–144) eagerly builds the `clr(JsonElement)`.
- dict and list already have their own readers (`dict/serializer/Reader.cs`, `list/serializer/Reader.cs`) that read from a format reader (BeginObject/NextName, BeginArray). The json format reader is `kind/json/reader.cs`.
- **Shape:** json's `Parse` reads its raw bytes through the json reader into a plang **dict** (object) or **list** (array), or a scalar — not a `clr(JsonElement)`. Then the navigator verbs go (the dict/list navigate themselves). The source/wire already holds the raw and materializes lazily on first `.Value()` — so "raw until first use" is the existing defer; "passing it on unread" writes the raw bytes (source relay) before any `.Value()`. `Read` (the format→type bridge, :173) stays — a typed slot given unread json reads through json (758). `Clr`/`Output` move onto the parsed dict/list (and the host dict/list/reflection kinds, kept until 673).

## Status
Not blocked. Trace in progress; shape to follow.
