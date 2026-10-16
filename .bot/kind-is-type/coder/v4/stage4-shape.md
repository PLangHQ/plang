# Stage 4 shape — a format owns its bytes

Branch `kind-is-type`, decision 774 rulings 8–9. Trace done; shape for approval **before** code. Stage 4 is **json-only in practice** — csv/table already holds a plang value (a guard), and http/settings/cache/llm already hold plang values (see §2).

## 1. The core change — a json value parses into a plang dict/list

Today (traced): a structured json value is born a **`clr.@this` carrying a `JsonElement`**. `json/this.cs:Parse` (:141–143) does `JsonDocument.Parse` → `new clr.@this(e.Clone(), …, this)`; the json kind then NAVIGATES that JsonElement (Descend/Enumerate/Data/IsSequence/Set, :65–122) and writes it (Output :198). `ClrForm = JsonElement` (:33).

Ruling 8+9: a json value holds its **raw bytes until first use**, then **parses itself into a plang `dict` or `list`** — no JsonElement carrier; the navigator goes.

**Shape:**
- **The defer already exists.** `source.cs:200–206` materializes lazily on first `.Value()`; `Peek()` returns the raw bytes unparsed. So "raw until first use" is the current source/wire behaviour — no change needed there.
- **`json.Parse` parses into a native dict/list.** `dict` and `list` already own readers that read from a format reader (`dict/serializer/Reader.cs`, `list/serializer/Reader.cs`), and the json format reader `json.Reader : IReader` (`kind/json/reader.cs`) is a token stream. So `Parse(raw)`: build a `json.Reader` over the bytes; peek — object → `dict.Reader.Read(ref jsonReader)` → `dict.@this`; array → `list.Reader.Read` → `list.@this`; scalar → the plang scalar. **No `JsonDocument`, no `JsonElement`, no `clr` wrapper.** (The DOM narrower `serializer/json.cs` — `Parse(JsonElement)→native` — becomes unused by this path; see §4.)
- **The navigator goes.** Descend/Enumerate/Data/IsSequence/Set (:65–122) and `Scalar`/`Write(JsonElement)` (:206–241) are deleted — a parsed json value IS a `dict`/`list` and navigates/writes itself. `ClrForm = JsonElement` goes with them.
- **Passing it on unread = raw passthrough.** `json.Owns(writer)` (:28, true for json/plang writers) already relays a captured wire slice byte-for-byte, so `write %cfg% to copy.json` before any read writes the bytes unread (PassedOnUnread). Once read into, the value is a dict/list and writes itself.
- **`Read` (the format→type bridge, :173) stays.** A typed slot given unread json reads through json into the declared type's reader (TypedSlotJsonReadTests, decision 758). This is json decoding its bytes into a typed slot — not the navigator — and remains json's job (it still owns reading its bytes). `Clr` (:157) and `Output` (:198) move onto the parsed dict/list (the host dict/list kinds, kept until 673, own their CLR lowering and write).
- **`Encode` (:42) stays** — json writes a plang value out as json through the json Writer (already type-switches on dict/list/item, no JsonElement).

## 2. Carrier-reader inventory (the architect's "who reads the C# carrier")

**Correction worth flagging:** the areas you named — http bodies, settings, cached answers, llm — **already hold plang values; no live `JsonElement` carrier-read remains there.**
- **settings/store:** none (round-trips through a lazy `wire` → native dict).
- **cached llm answers:** already migrated — `OpenAi.cs:454–480` stores the unwrapped native value; `RestoreFromCache` (:862–919) restores via `dict`/`clr` enumeration; the `JsonElement` mentions there are comments documenting the removal.
- **http bodies:** only **outbound** form-content construction reads a `JsonElement` (`http/code/Default.cs:784`, `:845`), pattern-matching a `clr{JsonElement}` to build multipart form. These break only if a json value stops being a `clr{JsonElement}` — so they move to reading the plang dict (`EnumerateItems`) instead. The only http site to touch.

**The live carrier-reads to move are all in the value model:**
- `type/item/kind/json/this.cs` — the whole navigator (delete, per §1).
- `type/item/serializer/json.cs` — the DOM narrower `Parse(JsonElement)→native dict/list` (`JsonDocument.Parse` at :59/98/108/149). Unused once `json.Parse` streams into dict/list; **candidate to delete** (confirm — Q1).
- `type/item/kind/dict/this.cs:54–65` + `type/item/kind/list/this.cs:92–103` — the `Convert` seams extract a `JsonElement` from a `clr` and run `serializer.json.Parse`. With json no longer a `clr{JsonElement}`, the `clr{Value:JsonElement}` branch no longer matches → drop those branches.
- `type/item/dict/this.cs:79–91` + `type/item/list/this.cs:114–115` — `Create`'s `clr{Value:JsonElement}` / `JsonDocument.Parse` branches → drop (a json value is already a dict/list; `as dict`/`as list` is then identity or a dict/list read).
- `http/code/Default.cs:784,845` — the one outbound http site above → read the plang dict.
- `type/clr/this.cs`, `type/kind/this.cs:145,148`, `type/list/this.cs:74` — comments/logic naming `JsonElement→json`; prune the `ClrForm=JsonElement` resolution when the navigator goes.

**Leave (not born-json carriers):** CDP/devtools/IPC payloads (`parent/this.cs`, `module/browser/type/cdp/**`, `window/**`, `screen/**`, `terminal/type/process`), the JSON diff (`data/this.Diff.cs`), CLI arg parse (`Utils/CommandLineParser.cs`), `.pr` navigation (`formal/reader.cs:393–394`), and the `JsonSerializer.Deserialize<string>` unquoters (`type/this.cs:475`, `wire/this.cs:40`, `formal/reader.cs:334,526`, `property/this.cs:124`) — these are not json VALUE carriers.

## 3. text's json wrapper + the `*format` coiner go → Held's last bare-name branch goes

`text/kind/format/this.cs` (the wrapper: a `{text, json}` holding raw characters, `Open` → delegates parse to the json kind) and `text/kind/this.cs` (the `*format` coiner, `Coin` via `_types.Kind(n)` — the last caller of the bare-name `Kind(string)`/`Held` path). Once json itself holds its raw bytes and parses on first use, the text wrapper is redundant — both go. With the coiner gone, **`Held`'s bare-name branch and the one-arg `Kind(string)` have no caller left → removed** (the stage-3 deferral closes here). Registry.cs:331 (`Hold(new text.kind.@this(this))`) goes with it.

## 4. csv/table — a guard
`table.@this` is already a plang value (Rows/headers; `csv.Parse(text)→records→table`), navigated as itself, no C# carrier. `ReadIntoIsATable` is a guard that must stay green — no change.

## 5. Tests
PassedOnUnread (raw passthrough), ReadIntoIsADict (object→dict), ArrayIsAList (array→list), JsonIsItemsKind ({item,kind:json}, is-text, kind.name — test-designer unstaled its header for stage 4), TypedSlotJsonReadTests (758, a dict slot reads unread json through json), the kind/strict tests (magic-byte verification — §6), ReadIntoIsATable (guard).

## 6. Open questions
- **Q1 — delete `serializer/json.cs`?** Once `json.Parse` streams raw→dict/list via the readers, the DOM narrower is unused. Delete it, or keep it as a shared helper the dict/list `Convert` seams still call (until those seams go at 673)? I lean delete — the streaming parse replaces it and the `Convert` seams' `clr{JsonElement}` branches go anyway.
- **Q2 — the kind/strict magic-byte tests.** `as image/gif, strict` must refuse non-gif bytes. This is a format verifying its bytes (ruling 8). Is that already implemented (the tests were red at baseline for another reason), or is strict magic-byte verification new stage-4 work on the image kind? I'll confirm by tracing the strict path once you greenlight the shape — flagging now in case it's scope you want split.
- **Q3 — a scalar-root json** (`42`, `"x"`): today `json.Parse` returns the native scalar (number/text), not a dict/list. Keep that (a bare scalar is its value, not a one-element container)? I assume yes (matches today).
