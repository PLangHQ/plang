# Architect review — native-type usage & `ToRaw` decompose

**To:** coder · **From:** architect · Re: collections-are-data regression sweep (`57118474a`)

**You own the final shape.** File:line and suggested directions below are anchors, not prescriptions — change what reads wrong, keep the dispositions.

## Why

The branch's whole point is that `dict`/`list` hold `Data` end to end, so a value keeps its type-tag and signature instead of being decomposed to a raw CLR value. `ToRaw` is the one sanctioned way to flatten a native collection back to `Dictionary<string,object?>`/`List<object?>` — and it's only legitimate at a **leaf where true logic lives** (a typed conversion that genuinely needs a CLR container to hand to a domain record). This review checks every `ToRaw` call site and the raw-CLR holdings against that rule. The native types and the walkers are right; two places decompose where they shouldn't, and one `ToRaw` is silently not raw all the way down.

## Lens / scope

This is the **native-type & decompose** lens — complementary to codeanalyzer v1's compare-path lens, not a replacement. Overlap is called out: **C below = codeanalyzer F4**, do it once. **A's wire-shape detection is the cousin of codeanalyzer F5.** A, B, D are new.

## Context — the write-out path is already correct, don't touch it

Confirmed clean, so you don't go looking here:

- **`application/plang` never decomposes.** `Data.Normalize` (`app/data/this.Normalize.cs`) walks a native dict/list into a fresh native dict/list whose elements stay `Data`; `json.Writer` (`app/channel/serializer/json/writer.cs:152,168`) emits each element through the record arm, full `{name,type,value,properties,signature}` envelope, signatures survive. No `ToRaw` on this path.
- **Other formats serialize value-only** via the type's own `[JsonConverter]` (`type/dict/Json.cs:20`, `type/list/Json.cs:22`) — bare `{}`/`[]`, envelope stripped, recursing through each nested type's converter. That bare form is the correct "decomposed" output, and it's done by the value's own renderer, not `ToRaw`.

The decompose problems are all **inbound** (convert-in / bind), never on write.

## Leaf sites that are correct — leave them

These `ToRaw` calls are at genuine leaves; don't "fix" them:

- `type/catalog/Conversion.cs:148,161,163` — typed conversion (dict/list → domain record / `List<T>`).
- `module/identity/code/Default.cs:329` — dict → `Identity` reconstruction.
- `module/llm/code/OpenAi.cs:973` — cache materialization.

---

## A — Data wire-reconstruction decomposes a native dict to read two keys (correctness + hot path)

`app/data/this.cs`. `IsWireShape` (`:729`) calls `AsRawWireDict` (`:719`) which does `nd.ToRaw()` — a recursive deep decompose of the whole dict — only to test `ContainsKey("value") && ContainsKey("type")`. Then `FromWireShape` (`:735`) and `TypeFromWire` (`:747`) read `value`/`type`/name/kind/strict out of the pre-flattened raw `IDictionary`.

The native dict already answers all of this without decomposing: `nd.Has("value")`, `nd.Get("value")` (hands back a **`Data`** with inner type/signature intact), `nd.Get("type")`. The current code throws away the Data-keying this branch exists to preserve, then rebuilds it level-by-level. Two costs:

- **Hot-path waste** — every variable bind through `AsCanonical` (`:695`) deep-decomposes the dict even when it turns out *not* wire-shaped (the `ToRaw` result is computed and discarded).
- **Fragility** — correctness leans on "every nested wire level is always enveloped"; any nested value that isn't wire-shaped gets left as flattened raw with its type tag gone.

**Suggested direction:** `FromWireShape`/`TypeFromWire`/`IsWireShape` navigate the native dict (`Has`/`Get`) and never call `ToRaw`. `Get("value")` already returns the inner `Data` — reconstruct from it directly, no flatten-then-rebuild.

**Cousin of codeanalyzer F5:** `IsWireShape` keys on `"value"+"type"` presence, so an ordinary user dict like `{value: 9.99, type: "book"}` is mis-detected as a serialized Data on the bind path. Same fix neighborhood — prefer keying on `type` (and/or `signature`) shape, or whatever discriminator F5 lands on.

## B — `dict.ToRaw()` doesn't decompose a nested native list (real asymmetry bug)

`type/dict/this.cs:124-133`, `Unwrap`:

```csharp
@this nested => nested.ToRaw(),                    // nested dict ✓
string or byte[] => value,
System.Collections.IEnumerable seq => …,           // CLR list — native list is NOT IEnumerable
_ => value,                                         // ← native list.@this lands here, survives un-decomposed
```

`list.@this` implements only `IContext`/`IBooleanResolvable`, **not `IEnumerable`** — so a nested native list inside a dict falls to `_ => value` and stays a `list.@this` in the supposedly-raw `Dictionary<string,object?>`. `list.ToRaw()` (`type/list/this.cs:159-164`) handles the symmetric case correctly (both `@this nestedList` and `dict.@this nestedDict` arms), so `dict.ToRaw` is the broken one. The comment at `:127-129` ("Stage 1 lists are still `List<object?>`") is the tell — it's stale; lists went native in Stage 3.

This bites at the `Conversion.cs` leaf: a domain record with a `List<T>` property fed from a dict whose entry is a native list won't reconstruct.

**Suggested direction:** add a `list.@this nestedList => nestedList.ToRaw()` arm to `dict`'s `Unwrap`; delete the stale Stage-1 comment.

## C — legacy `List<object?>` arms in `sort`/`unique` (= codeanalyzer F4)

`module/list/sort.cs:27,32` (`Comparer<object>.Default`) and `module/list/unique.cs:28,32` (`list.Distinct()`) still carry a raw-`List<object?>` fallback below the native branch — divergent from the one-compare-path (nulls-last, mixed-type-throw, structural equality all lost). Either dead (parse seam + every op now produce native lists → delete) or reachable and silently divergent. Resolve, don't leave both. **Fold into codeanalyzer F4 — one fix.**

## D — `CommandLineParser` builds native then immediately decomposes (minor, perimeter)

`Utils/CommandLineParser.cs:138` — `((dict.@this)UnwrapJsonElement(element)).ToRaw()` builds a native dict only to flatten it back to raw for the CLI config bag. Line `:139` is also inconsistent — it returns a *native* list for arrays while objects come out raw. CLI config is infra (a flag property bag), so raw is the right target; the round-trip is the smell.

**Suggested direction:** parse straight to raw for the config bag, and make the array case consistent with the object case. Low priority.

## Order

1. **A** — drop the `ToRaw` decompose in wire-reconstruction; navigate the native dict. Correctness + hot path.
2. **B** — native-list arm in `dict.ToRaw`; kill the stale comment.
3. **C** — resolve the `List<object?>` legacy arms (one fix with F4).
4. **D** — perimeter cleanup, low priority.

A/B are new here; C/F4 overlap; A's detection couples to F5. Re-run both suites, hand back to codeanalyzer for re-review alongside F1–F3.
