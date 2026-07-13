# Finding — the `[JsonConverter]` attributes barely fire; the perimeter already emits reflected garbage. "Byte-identical goldens" is the wrong acceptance.

Branch: `navigation-driven-record-builder`. Coder, mid-execution of `architect/stage2-json-serializer-fork-answer.md` step 2/3 (golden-pin + perimeter lower-then-STJ). Golden-pinning surfaced that the ruling's premise about current behavior is inverted.

## What I measured (STJ, `JsonSerializerOptions.Default`, live items)

| serialize form | `text "hello world"` | `dict {name:alice, age:30}` |
|---|---|---|
| **base-typed** — `Serialize(new { value = item })`, i.e. the EXACT perimeter shape (`p.Peek()` is `item.@this`-typed) | `{"value":{"Rank":100,"Cacheable":true,"Template":null,"RawText":"hello world","IsLeaf":true,…}}` | `{"value":{"Rank":700,"Cacheable":true,…}}` |
| object-typed — `Serialize((object)item)` | `"hello world"` (converter fires) | `{"name":{"IsVariable":false,"Raw":"alice","RawText":"alice","Rank":0,…},"age":30}` (shell clean, **nested reflects**) |
| **lowered** — `Serialize(item.Clr<object>())` | `"hello world"` | `{"name":"alice","age":30}` (fully clean) |

## Why the premise inverts
`Data.Peek()` returns **`app.type.item.@this`** (the base). STJ serializes a property/value by its **static** type, so `new { value = p.Peek() }` serializes `value` as `item.@this` → **reflects the base surface** (`Rank/Cacheable/Template/RawText/IsLeaf/…`). The concrete `text.Json`/`dict.Json` converter is **never consulted** — STJ only dispatches to a `[JsonConverter]` when the static type is the concrete type or `object`.

So, contra the ruling's "strip without touching these sites and the previews silently degrade to reflected C# surfaces":
1. **The perimeter previews ALREADY emit that reflected garbage today** — the converters do not fire there. Stripping the 13 converters changes nothing at these sites.
2. Even the object-typed path (where the converter *does* fire) is **half-broken**: the top-level item projects, but nested item values (dict entries) reflect garbage.
3. **Only lowering (`item.Clr<object>()`) yields clean output at every level.**

## Consequence for the plan
- **"Goldens byte-identical through step 4" is unachievable and wrong** — the current output is garbage; any real fix changes it. The correct acceptance is **clean lowered output** (a genuine improvement to the LLM/debug previews), pinned as the new golden.
- **The strip is even safer than thought**: at the perimeter the converters aren't firing, so stripping them is behavior-neutral there; the perimeter *fix* (lower-first) is a separate, strictly-better change.
- Object-typed production serialize sites need a regression sweep (do any rely on the converter's top-level projection?). Checked so far: `Error.FormatVerboseValue` (`Error.cs:435-443`) serializes only raw CLR `IDictionary`/`IList` (guarded) — safe. Remaining perimeter sites (`goal/Methods` 2, `build/code/Default` `FormatValue`+plan-dump, `ui/code/Fluid`, `type/spec/render`) all hand `Peek()`/param values and want lowering regardless.

## Proposed revised plan (coder — pending your ✓)
1. Perimeter sites: lower the value (`item.Clr<object>()`) before the STJ/anonymous-graph serialize (fixes garbage→clean at all nesting). Pin the **clean** output as the golden (not byte-identical to the garbage).
2. Strip the 13 `Json.cs` + `[JsonConverter]` lines (behavior-neutral at the perimeter; the object-typed sweep confirms no other regressions).
3. `writer.cs` nativize the type-entity `{name,kind?,strict?}` + the 14th converter per its dependency check.
4. Acceptance: perimeter previews emit clean lowered values; zero `Json.cs` under `type/item/` except `kind/json/`; baseline suites vs recorded reds.

Question for you: confirm the acceptance flips from **byte-identical** to **clean-lowered** (the previews improve), and that fixing the previews (a visible LLM-facing output change) in this same close-out is in scope — or should the perimeter fix be its own change and this close-out only strip the now-confirmed-dead converters?
