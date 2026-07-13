# Answer — TryConvert + the Json.cs sweep are one piece; the CLR door dies whole; dict→record lands on the `*` kind

Answers [`coder/stage2-tryconvert-entangled-with-json-sweep.md`](../coder/stage2-tryconvert-entangled-with-json-sweep.md). Settled with Ingi 2026-07-13.

**You own this.** Every code block below is a suggestion — bodies, factoring, and mechanics are yours. Members cited with `file:line` were verified against HEAD (7295b55ac) during the ruling; re-verify as you go.

## The four rulings

### 1. One coordinated piece — yes

Your entanglement finding is correct and slightly bigger than your table: `TryConvert` has THREE STJ-firing sites, not one — the string→record arm (`Conversion.cs:282`), the retry-as-`List<T>` inside it (`:293`), and the container→record arm (`:463-497`, dict/JsonElement/JsonNode/IList → serialize→deserialize round-trip, firing converters in BOTH directions). Plus `dict.Clr`'s record fallback (`dict/this.cs:364-366`). Order: kill the firing sites first, then strip the 13 converter files + their `[JsonConverter]` attribute lines — same reroute-before-strip sequencing the channel-write ruling pinned, baseline suites as the bar.

One correction to the Stage-0 removal list while you're in there: `item/serializer/json.cs` **STAYS** — ruling 8 (`wire-source-split/architect/ruling8-json-decode.md`, settled 2026-07-13) re-scoped it to the in-memory narrow job. `object/serializer/json.cs` is already deleted (wire-source-split merge). The strip is the 13 `type/item/<name>/Json.cs` + the type entity's own converter.

### 2. String→record: the arm DIES; nothing reroutes into the door

Ingi: *"string is just a string — if it needs to come in as json to be considered json, we don't do sniffing of `{`/`[`."*

A json-DECLARED value already has the complete pipeline, merged from wire-source-split ruling 8:

```
Kind[json].Parse(raw)            kind/json — the ONE sync decode
  └─ clr(json)
       └─ .Clr(target)           clr/this.cs → Kind.Clr
            └─ json.Clr          kind/json/this.cs:91-96 → json.Reader
                 └─ reflection.Read(target)   the host builds, zero STJ
```

So the reroute is not "TryConvert calls the reader instead of STJ" — it's that a bare undeclared string reaching a convert door with a record target is a **producer bug**. Your job: inventory what still feeds `:282` and `:463` live, and fix each birth site (declare the kind where the bytes are born), not the door. The known feeders are already dead: `.pr → Goal` went with the Stage-1 read path (`GoalReadOptions` is `[Obsolete]` for exactly this); `source`/`wire` materialization goes through their own kinds. If a feeder turns up that genuinely can't declare, surface it — don't sniff.

### 3. The CLR door dies — whole, not slimmed

Ingi's ruling (supersedes my keep-a-slim-door lean from the session): *"everything is an item in the plang runtime; they get type.Created from the basic CLR object, after that it's all item and everything should just accept item. Kill the clr door."* No `Convert(value, clrTarget)` survives when this branch closes — values lower THEMSELVES (`item.Clr`), types build THEMSELVES (`Create`).

**Caller dispositions** — 4 production sites total (grep-verified; the door's doc comment listing "Data.As&lt;T&gt;, wire reconstruct, Sqlite…" is stale):

| site | holds | disposition |
|---|---|---|
| `build/code/Default.cs:985` | `p.Type.Name` — a PLANG type name | never needed a CLR target: the entity's own door, `App.Type[p.Type.Name]` → `Create` |
| `build/code/Default.cs:1178` (`ToGoalCall`) | `typeof(GoalCall)` — a plang value (keeps `ICreate`, Stage-1 ruling) | entity door, same as above |
| `setting/this.cs:102` | `prop.PropertyType` — the one genuinely CLR-facing edge (CLI `--flag` walk) | lift-then-self-lower: `App.Type.Create(kvp.Value, ctx)` → `item.Clr(prop.PropertyType)`; composites already descend (`:93-99`) |
| `type/this.cs:304` | the no-family CLR-mate fallback inside entity `Create` | dies with the door: a family decline already errors via the carrier (`:301`); no family hook + no kind = loud error, not a hub fallback |

**Residue dispositions** — where each `TryConvert` arm's live behavior goes:

| arm | where | disposition |
|---|---|---|
| primitive `ChangeType` | `:438-460` | dies — raw scalars lift to their family (born-native), lower via the value's own `Clr`; no raw→raw converter |
| enum leaf | `:415-432` | moves into the value's own `Clr` (text lowers itself via `Enum.Parse`, number via `Enum.ToObject`) — `reflection.ReadValue:108-111` already does this at the token level; same knowledge, value-side |
| CLR `List<T>` construction + scalar wrap | `:325-373` + `ConvertElementsInto` | the list kind's job (it claims `IList`). CAUTION: the comment at `:314-324` records that routing this through STJ broke 9 tests (scalar wrap `5 → [5]`, generic-only `IList<T>` sources) — those behaviors MOVE to the list kind, they don't die |
| `FromWire` dispatch | `:254-262` + `type/this.cs:458` `WireReader` | dispatch leaves the hub; feeders call the type's own static directly (`snapshot.FromWire` — `module/snapshot/resume.cs` already names it; `crypto/type/hash/this.cs:80`; `signature/this.Wire.cs`). If your feeder inventory shows the only generic dispatch was TryConvert via now-dead callers, `WireReader` dies too |
| string-ctor arm | `:379-410` | inventory; likely dead once builders route through entity doors. A live CLR-mate needing it → surface, don't keep silently |
| `data.@this` target | `:153-159` | inventory the feeder; a Data target is courier work, not conversion |
| string → `JsonNode` | `:240-245` | the json kind's `Parse` is the one decode (`variable/set.cs` `[Type]=json` path routes there) |
| bind-failure messages | `BindFailureMessage`/`FormatTypeMismatch`/`TypeMismatchHint`/`FormatValuePreview`/`WithSlot` | the message AUTHORING is real value (slot naming, plang type labels, the unresolved-`%var%` hint) — it moves with whoever now reports the failure (`item.Clr` failures / the binding layer), your placement. Don't lose the `%var%` hint |
| `GoalReadOptions`/`ContextualReadOptions`/`_caseInsensitiveRead`/`CaseInsensitiveRead` | `:34-63` | die with the STJ arms; the test facade re-points or dies with its tests |

### 4. dict→record: IN SCOPE — the `*` kind grows the record-from-slots door; NO helper class

There is no record-builder branch to defer to — the plan dropped the navigate-pull record builder ("records are hosts now"). Host construction IS the reflection kind, and Ingi ruled explicitly: **no helper class** — the `*` kind is the owner (it already carries `Read`, `Set`, `Output`, the whole host-knowledge surface).

The door: a second `Read` source on `reflection.@this`, beside the `IReader` one — same policy (the `Tagged.PropertiesFor` wire-name selector `reflection/this.cs:81-82`, settable gate, recurse via the value's own `Clr`), pulled from live slots instead of tokens:

```csharp
// NEW — on reflection.@this, beside Read<TReader>. Same verb, second source.
public object? Read(global::app.type.item.dict.@this slots, global::System.Type target,
    global::app.actor.context.@this ctx)
{
    var host = global::System.Activator.CreateInstance(target)!;
    foreach (var entry in global::app.channel.serializer.filter.Tagged.PropertiesFor(target, global::app.View.Store))
    {
        if (!entry.Property.CanWrite || !slots.Has(entry.WireName)) continue;
        var v = slots.Slot(entry.WireName).Peek();
        entry.Property.SetValue(host, v is global::app.type.item.@this iv ? iv.Clr(entry.Property.PropertyType) : v);
    }
    return host;
}
```

`dict.Clr`'s record fallback (`dict/this.cs:361-366`) becomes one line handing itself over (json.Clr's `new reflection.@this()` at `kind/json/this.cs:96` is the reach precedent; route through the kind door if you prefer — your call):

```csharp
// replaces dict/this.cs:361-366 — dict/Json.cs dies with this
return new global::app.type.item.kind.reflection.@this().Read(this, target, Context);
```

Guards, both from Ingi's session:

- **Two pull arms, ONE policy.** Do NOT unify by flattening the dict into a token stream to reuse the `IReader` `Read` — the dict's values are already live items; tokenizing them to re-read them is the internal round-trip one level down.
- **The ctor-bound gate.** `Activator` + `SetValue` only handles settable-prop classes; a ctor-bound record (get-only, primary constructor) got ctor binding from STJ for free. Inventory the actual consumers (the comment names SettingsStore/Identity) FIRST: all settable → build the arm, `dict/Json.cs` dies in the sweep; a ctor-bound record in the set → new surface, come back to architect before building.
- Verify key-match semantics: the `IReader` `Read` matches names `OrdinalIgnoreCase` (`:79-80`); check `dict.Has`/`Slot` case behavior matches, since a hand-built plang dict's keys aren't guaranteed camelCase.

This door will be needed again (list → `List<SomeClass>` recurses through it per element; `setting.Set`'s hand-rolled composite descend can collapse onto it later; `code.load` user classes) — that future reuse is WHY it lives on the kind and not inline in dict.

## The `data.Convert` architecture (settled this session — orientation, so conversion knowledge lands on the right side)

- **The TARGET owns convert-from.** `data.Convert(to) => to.Convert(this, ctx)` (`data/this.cs:135`, `kind/this.cs:135` — already the shape). html knows md→html; md knows nothing about html. Same law as `Create`: a thing constructs itself, nothing converts outward.
- **The resolution ladder lives in the kind selection door.** `Kind[name]`: kind by that name → its `Convert`; no such kind → look up `type[name]` → its **default kind** converts (`data.convert('audio')` → audio's default, e.g. mp3). "Default kind" is a new declared fact on the type/family — NOT built yet, noted for the plan; don't build it here unless a Stage-2 site needs it.

## Demolition list

**Dies with the firing-site reroute (step 1):** the string→json arm whole (`Conversion.cs:269-309`, incl. `:282` + `:293`); the container→record arm (`:463-497`); `dict.Clr`'s STJ fallback (`dict/this.cs:361-366` → the new reflection door); `GoalReadOptions` (`:56-60`) + `ContextualReadOptions` (`:43-44`).

**Dies with the door (step 2):** `TryConvert` whole (`:131-518`) — every arm per the disposition table; the public `Convert` door (`:74-79`); `ConvertElementsInto` (`:96-123`); `_caseInsensitiveRead`/`CaseInsensitiveRead` (`:34-63`); the private message helpers (move with the failure-reporting or die — audit every remaining member of the `Conversion.cs` partial, nothing survives unaccounted); `WireReader` (`type/this.cs:458-463`) conditional on the feeder inventory.

**Dies with the strip (step 3):** the 13 `type/item/<name>/Json.cs` + their `[JsonConverter]` attribute lines; the type entity's own converter (the 14th wearer — plan line, verify current location). EXCEPT `dict/Json.cs` only after step 1's dict arm lands, and NOT `item/serializer/json.cs` (stays per ruling 8).

**Stays:** `item.Clr`/the value's own lowering (grows the enum arm); the list kind (gains CLR collection construction incl. the 9-test behaviors); `reflection.Read` + the NEW slots door; the `kind.Parse`/`Load`/`Clr` pipeline (ruling 8); `item.serializer.json` (in-memory narrow); the `FromWire` statics on their owners; `dict.Clr`'s O(1) backing arm + the `IDictionary` structural arm (`:344-358`); the `setting.Set` walk itself.

## Order of work

1. Builder sites → entity doors; `setting.Set` → lift-then-lower (contained, safe).
2. Firing-site kills: string→record arm dies (with feeder birth-site fixes); dict→record via the new reflection door.
3. Door + residue relocation: enum → value's `Clr`, list construction → list kind, `FromWire` → owners' own calls; delete `TryConvert` + the door.
4. Converter strip + baseline verification (union baselines from wire-source-split are the bar; goal: zero `Json.cs` under `type/item/` except `kind/json/`).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| record-from-slots as `Read` overload on `reflection.@this` | one verb (caller's intent), second source, no new name invented; host policy has ONE owner | ✓ |
| no helper class | Ingi explicit; a `RecordHelper` would be the convert hub reborn (stray helper) | ✓ |
| door kill | hub removed; values self-lower (`item.Clr`), types self-build (`Create`) — construction/lowering always on the thing itself | ✓ |
| enum arm on the value's `Clr` | the value owns its CLR projection; no central leaf | ✓ |
| CLR collection construction on the list kind | the kind claims `IList`; construction is its job | ✓ |
| no `{`/`[` sniffing anywhere | format is declared, never guessed — consistent with the wire-source-split model | ✓ |
| default-kind fact | new declared fact, owner named (type/family), deferred until a site needs it — no speculative build | ✓ |
