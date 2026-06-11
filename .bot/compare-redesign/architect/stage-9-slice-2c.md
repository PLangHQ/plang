# Stage 9 — slice 2c: our methods accept plang types (mapped 2026-06-11, runs after 2b)

**Why:** the door migration left a residue 2b doesn't cover. Handlers now await `Value()` correctly — and then immediately lower the typed answer because **our own receiving method demands raw**: `target.Tag(entry.Name, (await entry.Value())?.ToString() ?? "")` exists because `call.Tag(string key, string value)` exists (`callstack/call/this.cs:153`). The call site is innocent; the signature is the violation. 62 production sites follow the `(await X.Value())?.ToString()` pattern today (grep below). This is Stage 7's conversion discipline applied to parameters codebase-wide: **when a caller lowers a value to feed a method we own, fix the method, not the caller.**

**The rule (per receiving method):** flip the parameter to the plang type (`string msg` → `text msg`; a genuinely polymorphic slot → `data`/`item`), trace the value inward hop by hop, and lower only where it crosses code we don't own — through `Clr` under the 2b conditions (adjacent .NET call, verified-missing typed op, logged to `missing-typed-ops.md`). `ToString()` stays what Stage 2 pinned it as: diagnostics only, never a value path.

**No half-flips (Ingi's ruling, 2026-06-11):** a flip counts as done only when the value stays typed INTO STORAGE. `Tag(text key, data value)` whose Tags bag stores `data` entries is done; `Tag(number n)` that assigns `int _n = n.Clr<int>()` inside is the same violation moved one hop inward. If the backing field/store can't go typed without pulling in a whole class, that class joins the Stage 10 inventory (below) — flip what closes, log what doesn't.

**Scope note:** this slice covers the 62 call-site families below — the visible decompose points. The full subject (every runtime class's value-carrying members: 291 raw-CLR public properties outside `app/type` as of today, plus fields/returns/params) is **[Stage 10](stage-10-typed-interior.md)** — 2c is its call-site-driven head start, not its substitute.

**Relationship to 2b (started):** while walking 2b sites, if typed flow is blocked by one of OUR signatures, do not `ToString()` past it — add the method to this map (it's append-able) and move on, or flip it on the spot if the trace is short. 2c is the dedicated pass over whatever remains.

## The receiving-API map (grouped from the 62 sites)

| Group | Receiving APIs | Sites | Flip |
|---|---|---|---|
| Error construction | `ServiceError(string msg, string key, …)` and kin; assert failure messages | assert ×10, error/throw, error/handle ×3 | errors carry `text` (or whole `data` — a message can be a value); status codes `number` |
| Callstack/debug tags | `call.Tag(string, string)` → `Tags.Set` | debug/tag, debug/this | `Tag(text key, data value)`; the Tags bag stores typed (our store, our rules) |
| LLM request building | model/format/cache-key plumbing in `OpenAi.cs` | ×7 + llm/query | typed through OUR builders; the true edge is the HTTP request serialization at the bottom |
| Goal/parse infra | `Goal.Parse(string, …)`, getTypes, GoalCall, goal/Methods | ×10 | parse takes `text` (or the file reference itself — it knows how to read) |
| List op internals | sort by-key, join's `List<string>` accumulator, group's key dict | ×3 | keys/accumulators are `text`-typed (ordinal-ignore-case dict keying is by design); join is arguably `text.Join(list)` — a typed op |
| Crypto/signing | `hash.FromBase64(string)`, Signature | ×3 | type constructors take `text`; the base64 decode inside is the leaf |
| Courier internals | `data/this.cs` ×2, Reconstruct, Navigation, variable/list ×2 | ×6 | judge individually — the courier ToStringing cargo is rule-#9 territory; most should dissolve with the 2b `Value<T>` rebuild |
| Builder/test/module infra | builder/code, test/discover, MarkdownTeaching, module/add, output/ask, http/HttpBuildHelpers, file/read, condition/Operator | ×10 | per-method flip, same rule |
| Type-folder sites | directory/this, directory/serializer | ×2 | inside the type — verify each is the type's own serializer leaf (legitimate) or fix |
| Third-party adapters | `ui/code/Fluid.cs` ×3 | ×3 | Fluid is code we don't own — a TRUE edge; lower at the adapter via `Clr`, log any missing typed op |

## Exit gate

The finder grep returns zero outside proven third-party edges:

```
grep -rn -e 'Value())?\.ToString()' -e 'Value())!\.ToString()' -e 'Peek())?\.ToString()' PLang/app --include=*.cs   # excluding tests
```

plus a sweep for the cousins (`Convert.To*` on awaited values, `(string)`/`(long)` casts feeding our own methods). When the grep is clean, propose the analyzer version (PLNG-style: `.ToString()` on an item subtype outside diagnostics = warning) so it stays clean — flag for architect review rather than building it unilaterally.

## You own this

The map is append-able, the groups are starting points, and per-method judgment (text vs data vs item for the flipped param) is yours. Two fixed rules: never `ToString()` past a blocked signature, and a flip traces the value inward until a genuine non-ours boundary — our code is never the leaf.
