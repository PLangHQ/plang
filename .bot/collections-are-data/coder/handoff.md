# Coder handoff — Stage 3 (arrays-as-Data / F1): two design forks for architect

**From:** coder · **To:** architect · **Status:** mid-flight pause, blocked on two decisions

## Progress so far

Stages 1–2 landed and pushed to `origin/collections-are-data`, both suites green:

- **Stage 1 — dict native type** (`b3472639a`): `app/type/dict/` holds `Dictionary<string,data>`; parse seam, navigator, Normalize, writer (`case dict`→`{}`, property-bag arm deleted), primitive map, all consumer sweeps. `[JsonConverter]` on dict for the raw-STJ value view (wire still rides Normalize). Two builder regressions found+fixed (reflected message roles via the converter; `SetValueOnObject` reflecting a dict into a `Context→App→Culture` snapshot-clone cycle). 15 Stage-1 C# tests green.
- **Stage 2 — set rebinds** (`516c6ccdf`): both raw `Variables.Set` branches rebind (mint + carry subscribers) instead of mutating in place; dot-path `SnapshotClone` deleted (independence comes from `WalkContainerVars` copying on read). 4 Stage-2 C# tests green.

Full C# suite: **4050 pass, 0 regressions** (remaining 25 = Stage 3/4/6 skeletons). Existing plang runtime suite: **273/273**. Builder verified building simple goals.

> Environment note: the sandbox build LLM (`gpt-5.4-nano`) mis-compiles (e.g. `assert … equals` → `condition.if` with an invalid `equals` operator), so new PLang test `.pr` can't be reliably built here — `.test.goal` sources are committed as the contract (like the test-designer skeletons), `.pr` to be built in a good-LLM env. C# tests are the authoritative verification.

## The two forks

Starting Stage 3, your plan and the test-designer's C# tests don't fully agree on the list representation, and the F1 element-wire shape needs a ruling.

**Verified facts:**
- An array *can* just be `List<data>` — the existing `IEnumerable` writer arm emits `[]` and the `IList` navigator works.
- **But** under raw STJ (the `application/json` serializer — which is what `save %x% to file.json` uses: `path.Save` → serializer-by-extension → `Json.cs` raw-STJ on `data.Value`), a bare `List<data>` reflects each element's `Data` C# surface into junk — the exact failure that made `dict` need a wrapper + `[JsonConverter]` in Stage 1.
- F1's load-bearing test (`Tests/LazyDeserialize/SignedListSurvivesJsonRoundTrip`) saves a signed element to **`.json`** and reads it back to `verify %list[0]%`. So the `.json` egress must carry the element's signature and the parse must reconstruct it as a Data.

### Fork 1 — list in-memory shape

- **(a) `list.@this` wrapper holding `List<data>` + `[JsonConverter]`** — fully symmetric to `dict`. `Stage3_ArraysAsDataTests.ListValueType_HoldsListOfData` + `PrimitiveMap_ListRegistered_RawListEntryRetired` expect this. Cleanest raw-STJ handling, one owner for `[]`-rendering; cost is a dict-sized consumer sweep. (Folder-name snag: `app/type/list/` is the type *registry* — the value type needs a sibling folder; your call.)
- **(b) Raw `List<data>` directly** — your plan's "List<data> IS List<app.data.@this> … writer disambiguates List<data>→[]". Less new code, but raw-STJ (`application/json`) still needs Data-element handling *somewhere*, and there's no single owner — the test-designer's `ListValueType` test would need reshaping.

**Q: which? If (b), where does the raw-STJ Data-element handling live?**

### Fork 2 — element wire shape (the F1 crux)

A signed element must carry its `{name,type,value,signature}` envelope so `verify %list[0]%` survives a round-trip — but `[1,"two"]` must not become `[{…},{…}]`.

- **(a) Bare-unless-signed** — an element emits its bare value normally, and the full envelope only when it carries a `Signature` (or meaningful `Properties`). `[1,"two"]` stays `[1,"two"]`; signed elements round-trip. Matches today's mixed behavior (raw elements bare, the signed `add`-ed Data already envelopes).
- **(b) Always-envelope** — every element self-describes; `[1,"two"]` → `[{…},{…}]`. Uniform, but churns array json output broadly and will move many tests.

**Q: which?** Coder leans **(a)+(a)** — symmetric `list.@this`, bare-unless-signed elements.

## What unblocks me

A ruling on both forks. With (a)+(a) I proceed: stand up the list value type (mirror dict), repoint `UnwrapJsonArray`/`Materialize`, collapse the List navigator (`Element` returns the element Data, delete `WrapItem`), unwrap in `Conversion`, drop `add.cs`'s `ShallowClone` to a reference, reconcile `Data.Load()` row-Q passthrough, and sweep residual `is List<object?>` value sites — landing the F1 round-trip.
