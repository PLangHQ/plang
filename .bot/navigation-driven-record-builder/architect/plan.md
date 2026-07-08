# One construction door — `Type.Create(source, kind?)`, reached three ways

**Branch:** `navigation-driven-record-builder` (off the merged `clr-navigators`/`variable-as-value` line). The name is now a subset — record-building is Stage 1 of a larger collapse: unifying the whole conversion system onto one primitive.
**Companion:** coder's `catalog-removal-and-create-unification.md` (converged independently on this same one-`Create`-door conclusion, and goes further — see note below), coder's seed `from-source-spec.md`, coder's `plan-review.md`, and the conversion-door census (this session).

**This plan is the construction-primitive spine (the coder's Decision 2).** The coder's companion doc adds two moves in the same effort that this plan does not detail: **Decision 1** — delete the `catalog` god-object (`type.catalog.@this`); the registry of all types *is* `list<type>` (`app.type.list`), the 8 sub-registries rehome to `app.type.*`. **Decision 3** — module discovery becomes `app.module.list : list<module>` (navigable view over reflection), the compile prompt a Fluid render. Those are compatible with and extend this spine; the coder left six open items for the architect on them (answered separately, not folded in here without Ingi).

**You (coder/test-designer) own the final code and test shape.** Every body and signature below pins the mechanism, not the exact lines. If a cleaner shape falls out while implementing, take it — flag it back if it moves a seam.

---

## Why

The builder dies writing a clr(json) onto a plang-typed slot (`set %goal.Steps[i].Actions% = %compileResult.actions%`): the clr(json) tries to *lower* to `actions.@this`, hits the terminal LOWER door, throws, and the built goal has no actions. That is the symptom. The disease, found by censusing every conversion path: **there are six different mechanisms that turn a source into a typed value, and none of them is the one true door.** `ICreate.Create`, the reflective `convert.OfStatic`, the 15-stage `TryConvert` pipeline, `type.Convert`, `dict.Clr`'s STJ round-trip, `type.Build`'s lift — all reach the same operation through reflection, STJ serialize-loops, and hand-dispatch instead of through the type. The clr(json) gap exists because not one of them navigates.

Settled with Ingi (2026-07-08): collapse them. **One construction primitive, owned by each type, reached three ways.** Each type parses its own input and builds itself — no reflective hub, no STJ round-trip. This closes the write gap, retires the goal-as-type STJ bridge, and makes every value round-trip the same plang-native way.

---

## The model (settled with Ingi)

**One primitive:** `Type.Create(source, kind?)` — a type builds itself from a source, optionally refined by a kind. Each type owns it; `Create` parses/coerces its own input and declines (returns null) when it can't. No `convert.OfStatic`, no reflective `Discover`/`OwnerOf`, no STJ serialize→deserialize.

**Reached three ways — all landing on the same `Create`:**

| you ask by | door | lands on | example |
|---|---|---|---|
| static type (compile-time) | `Value<T>()` | `T.Create(self)` | a handler declares `Data<number>` |
| kind (runtime) | `data.Convert(k)` | `k.Type.Create(self, k)` | `text.convert("mp3")`, `text.convert("html")` |
| birth (from a `.pr`) | the read path | `T.Create(source)` over `{name, type, value}` | building `goal` from clr(json) |

**`Convert(kind)` is a thin front over `Create`, not a peer.** A kind knows its type — `kind.Type` resolves `json→item`, `csv→table`, `mp3→audio` (reader/format registry). So `text.convert("mp3")` = resolve `mp3`→type `audio` → `audio.Create(theText, kind: mp3)`, and `audio` owns whether that text can become mp3 (a path to load, TTS, or decline). Convert crosses type *because the kind carries its type*. This is why "Create = become type, Convert = become kind" is true at the surface but underneath Convert delegates to Create.

**The other two directions stay distinct** (they are not construction):
- **lower** — plang value → CLR — is `item.Clr`. The exit door. Stays.
- **write a child** — set `%x.y% = v` — the value owns (`kind.Set` shape). `SetValueOnObject`'s reflection dies.

---

## `Create`'s contract

```csharp
static ValueTask<TSelf?> Create(item value, data data, kind? kind = null)
```

- **Async.** Navigation can be I/O (`%var%` resolution, bracket-index) — same rule as `IBooleanResolvable` making the condition pipeline async because one leaf can be I/O. `Value<T>()` is already async; the dispatch becomes `await T.Create(await Value(), this)`.
- **The type parses its own input** (Ingi, part 1): `number.Create("42")` parses the string, picks the precision kind, coerces — the body relocated verbatim from today's `number.Convert`. The private ctor takes the clean CLR value; `Create` holds "is this even a number?" because `Create` can decline (null) and a ctor can only throw.
- **Declines with null, reason on `data.Fail`** — the existing ICreate contract, unchanged.
- **Records navigate-pull each declared property**: `module`/`action` as `text`, `Parameters` as `List<Data>` through the Data reader (the seam, below), nested records recurse `Create`, `list<T>` properties route through `list<T>`'s own `Create`. Scalars coerce their raw. Same door, different bodies.

---

## The incumbent (leaf trace) — the six paths that collapse

Each reaches `Type.Create(source, kind)` today through a redundant route. Grounded at file:line; disposition stated.

1. **`ICreate.Create` default** — `item/ICreate.cs:30`. Today: pass-through → facet → `convert.OfStatic` (reflective) → `type.Create(raw)` → `dict/list.Clr(typeof(TSelf))` (STJ). It funnels into the very doors we're removing. **→ becomes the one primitive.** Keep pass-through + facet (free); the tail (OfStatic, the dict/list STJ) is replaced by "record → navigate-pull; scalar → the type's own `Create` override."

2. **`convert.OfStatic` / `Of` / `Invoke` / `Discover` / `OwnerOf` / `_ownership`** — `convert/this.cs`. Reflectively discovers a `static Convert(object,kind,ctx)` per type and invokes it, routed by a reflection-built CLR→family table. **→ dies.** The per-type `Convert` bodies are legitimate; they relocate onto each type as its `Create`. The reflective dispatch is the OBP violation and goes.

3. **`TryConvert`** — `catalog/Conversion.cs:128-522`, 15 stages. The god-function: null → assignable → Data-wrap → item.Clr-lower → nullable → **family hook (reflective)** → JsonNode → FromWire → **STJ string→record** → list/element → ctor(string) → enum → primitive ChangeType → **STJ dict→record** → mismatch. Only **4 external callers** (`type.Convert` type/this.cs:225, type/this.cs:602, `list<T>` this.Generic.cs:64, `setting/this.cs:102`). **→ collapses.** The construction stages fold into `Create`; the genuine primitive/ChangeType lowering survives inside `item.Clr`. Verify the two non-obvious callers (type/this.cs:602, setting/this.cs:102) route through `Create` before deleting.

4. **`type.@this.Convert(value, ctx)`** — `type/this.cs:187`. Value→type coercion: unwrap leaf, dispatch to the reflective hub, fall back to `item.Clr`, then `TryConvert`. Duplicates `Create` + the hub. **→ dies into `Create`.** Callers (`variable/set.cs:302`, `type.Build:306`) route to `Create`. NB `type.Convert(string raw)` (type/this.cs:576, the wire-string/`FromWire` reader) is a *different* method — verify its snapshot/crypto callers before touching; likely stays or folds into the read path, not this cut.

5. **`dict.Clr(Type)`** — `dict/this.cs:323`. STJ serialize→deserialize to build a record. Same STJ round-trip also appears as TryConvert stages 9 and 14 — three homes for one smell. **→ the record-build use dies into `Create` (navigate-pull);** the genuine untyped dict→`Dictionary<string,T>`/CLR-map lowering STAYS (verified non-record callers: `variable/list/this.cs:440,456,477,553`, typed-map slots).

6. **`type.@this.Build` / `type.@this.Create(raw)`** — `type/this.cs:249` / `:439`. `Create(raw)` is the polymorphic lift (raw CLR → plang value) — **folds into the one `Create`.** `Build` also owns the **deferred-source born rule** (a string/bytes value becomes a lazy source; a full-match `%var%` borns a `variable`) — that laziness is read-time, a separate concern from construction, and **stays**.

**The write site** (`variable/list/this.cs:364` `SetValueOnObject`) already does CONVERT-first on its CLR-property arm (`OfStatic` → fall back to `Clr`); its index-arms (`:440,:456`) still blind-LOWER. **→ all arms route to the value's own `Create`/child-write;** the seven reflection arms die (one arm, the clr(json) `kind.Set` delegation at `:389`, is already the correct shape).

**The Data-leaf seam (must stay byte-identical).** `action.Parameters` is `List<app.data.@this>` — param values `{name,type,value}` are **Data leaves**, not record fields. They carry a full-match `%var%`-born-as-variable, deferred source, template flag, signing — owned by `app/data/reader/this.cs`. When `Create` reaches a `Data`-typed property it hands the child **to that reader**, never converts it to a value. This half does not change.

**The async spine** is already async everywhere except the two sync islands (`ICreate.Create`, `list<T>.Convert`). `Data.Value<T>()`/`Value()`/`GetChild`/`Navigate`, `clr.Navigate`, `kind.Navigate`/`Convert`, `data.Convert(kind)` (`data/this.cs:135`, already `=> to.Convert(this, _context)`) — all `ValueTask`.

---

## Decisions (settled with Ingi, 2026-07-08)

1. **`Create` and the convert front go async. No interface split.** Same seam-goes-async rule as `IBooleanResolvable`. ~40 `ICreate` implementors change signature; a sync leaf returns `new(result)` (no `async`, no state machine). **Lands as its own prep branch first** (Stage 0) — pure signature sweep, isolated from the design change.
2. **Each type owns its `Create`; no reflective hub.** Scalars override `Create` with the coercion relocated from their `Convert` hook (`number.Create` parses "42", picks precision). Records get a generic navigate-pull default (their declared properties *are* the spec). `convert.OfStatic` dies.
3. **`Convert(kind)` is a front over `Create`** — resolve `kind.Type`, call `Type.Create(self, kind)`. Not a separate mechanism. Fills the currently-stub `kind.behavior.Convert` (only `dict` has a real converter today).
4. **`dict.Clr`'s STJ round-trip stays for non-record targets.** Verified: typed `Dictionary<string,T>` slots and CLR-map lowering reach it. Only its record-build use retires into `Create`.
5. **Reflective navigation-pull first; codegen deferred.** The generic default navigates any record with zero per-type code (better than `dict.Clr` — reflects the property list once, no round-trip). A source-generator `Create` per record is a later perf pass, emitting the record's own body — no new type, no "FromSource" name.

---

## Sequencing

**Stage 0 — prep branch (async sweep), landed first.** `ICreate.Create` + `list<T>.Convert` → `ValueTask`, sweep across ~40 implementors, no behavior change. Dispatch at `data/this.cs:512` → `await T.Create(…)`. **Also covers the second async spine** (coder review I1): `list<T>.Convert` is reached through `convert.OfStatic`→`Invoke` (`hook.Invoke(...) as Data`), so `OfStatic`/`Invoke`, `SetValueOnObject`, and `Conversion.cs:216` go async with it. `Invoke` awaits a hook that returns a `ValueTask`, else uses the value directly.

**Stage 1 — unblock the builder (write path).** On top of merged Stage 0.
- `list<T>` accepts a **navigable** source: when `value` is a clr/dict/list item, enumerate via its own `EnumerateItems`/`Enumerate` (the carrier delegates to its kind) instead of demanding raw `IEnumerable`.
- **Hand-write `action.Create`** (Decision 5 — hand-write first, then generalize): pull `module`/`action` as `text`, `Parameters` as `List<Data>` through `app/data/reader`, recurse `Modifiers`. `actions.Create` accepts a clr(json)/navigable array (defer to `list<action>`).
- Write index-arms (`variable/list/this.cs:440,456`) → CONVERT-first, matching the property arm.
- **Blocker-1** (`data/reader/this.cs:79-80`, clr-navigators demolition #5): route an `object`/`dict`/`list`/json-kind-declared wire value → json → clr(json). Rule (coder review I4): **String token → unchanged (text/plain, incl `%var%`); non-String or json-kind-declared → clr(json).** The String branch is provably out of scope — a full-match `%var%` borns a `variable` in `type.Build` (`type/this.cs:265`), a different branch. Sequence first.
- **Blocker-2** (`goal.getTypes` List-lower): same LOWER-instead-of-CONVERT bug at a native-`List`→`list<dict>` return. `list<dict>.Convert` already takes a native `List`; confirm it routes through the async convert door.
- **I3 seam (coder review):** navigation hands a clr(json) child, not bytes. Give `app/data/reader` a `JsonElement`-input door that reuses the byte path's `FromRaw` deferral tail (same format pick, same `%var%`/template/signing) — **not** a re-serialize round-trip. Named Stage-1 deliverable; it makes `%var%` + signing byte-identical on both write and read paths. DoD: a round-trip test that a navigation-built param signs identically to a byte-read one (coder review I7 — sign-if-missing fires in `Wire.Write`).

**Stage 2 — the collapse (one `Create`).** The big one.
- Relocate the 14 per-type static `Convert` hooks → `Create` overrides on each type (`number`, `text`, `datetime`, `date`, `time`, `duration`, `bool`, `binary`, `guid`, `image`, `path`, `dict`, `list`, `GoalCall`). Same bodies, moved from reflectively-discovered statics to the type's own `Create`.
- Delete `convert.OfStatic`/`Of`/`Invoke`/`Discover`/`OwnerOf`/`_ownership`/`BuildOwnership`.
- Collapse `TryConvert`: construction stages fold into `Create`; primitive `ChangeType` lowering lives in `item.Clr`. Route its 4 callers to `Create` (verify the two non-obvious ones).
- Delete `type.Convert(value)`; callers route to `Create`.
- `data.Convert(kind)` becomes the front: resolve `kind.Type` → `Type.Create(self, kind)`. Fills `kind.behavior.Convert`.
- The generic record navigate-pull default; `step`/`goal`/every record inherits it. The Stage-1 hand-written `action.Create` collapses in unless it carries a real quirk.
- `SetValueOnObject`'s reflection arms → the value owns its child-write.

**Stage 3 — retire the goal-as-type STJ read bridge.**
- `.pr` load routes through navigate-pull: a `.pr` is a clr(json) that `Create`s itself into `goal`.
- Delete `goal/serializer/Reader.cs` + `Default.cs` (`Deserialize<goal>`), `GoalReadOptions` (`catalog/Conversion.cs:55`, its only callers are those two), the goal-specific dispatch (`Conversion.cs:282`).
- Retire `dict.Clr`'s record-build use (`ICreate.cs:61-62`) — the STJ method stays for maps.
- Collapse `build/code/Default.cs` dual-path step readers (`GetString` `:855-862`, `SetValue` `:868-877`) — the `step is JsonElement` fork dies when steps navigate as Data (clr-navigators demolition #10).

---

## Code to remove (demolition worklist)

Tagged **[dead]** delete / **[replace]** rewrite in place / **[relocate]** move onto the owning type / **[candidate]** collapses — verify / **[stays]** do not touch. Cross-checked against the clr-navigators audit.

### Stage 0 (async prep)
- **[replace]** `ICreate.Create` sig — `item/ICreate.cs:30`, `TSelf? → ValueTask<TSelf?>` (~40 implementors follow).
- **[replace]** `list<T>.Convert` sig — `list/this.Generic.cs:52`, → `ValueTask<Data>`.
- **[replace]** `convert.Invoke`/`Of`/`OfStatic` — `convert/this.cs:28,43,47`, await a `ValueTask`-returning hook. `SetValueOnObject` (`variable/list/this.cs:364`) and `Conversion.cs:216` go async.
- **[replace]** dispatch — `data/this.cs:512`, `await T.Create(...)`.

### Stage 1 (unblock)
- **[replace]** `list<T>.Convert` IEnumerable-only guard — `list/this.Generic.cs:54` → "navigable source."
- **[replace]** blind LOWER in write index-arms — `variable/list/this.cs:440,456`.
- **[replace]** deferred-read format guess — `data/reader/this.cs:79-80` (String→text/plain, else clr(json)).
- **[new]** `app/data/reader` `JsonElement`-input door reusing the `FromRaw` tail (I3).

### Stage 2 (collapse)
- **[relocate]** 14 per-type static `Convert` hooks → each type's `Create`: `type/*/this.Convert.cs` (number, text, datetime, date, time, duration, bool, binary, guid, image, path, dict, list) + `goal/GoalCall.cs:60`.
- **[dead]** `convert.OfStatic`, `Of`, `Invoke`, `Discover` (the Convert-hook finder) — `convert/this.cs`. The CLR-keyed reflective *dispatch to per-type `Convert`* is the violation and goes.
- **[relocate/stays]** `OwnerOf`, `_ownership`, `BuildOwnership`, `OwnedClrTypes` — the raw-CLR→family map. NOT dead: `type.Create(raw)` (`type/this.cs:439`) needs "a `long` → number." It survives as a **private perf index behind `type.Create(raw)`**, not a public door anyone reaches (coder's correction — I earlier mis-tagged it dead).
- **[candidate]** `TryConvert` + `ConvertElementsInto` — `catalog/Conversion.cs:128,94`. Construction stages fold into `Create`; verify callers `type/this.cs:602`, `setting/this.cs:102` route through `Create`; keep primitive lowering in `item.Clr`.
- **[dead]** `type.Convert(value, ctx)` — `type/this.cs:187`. (NB `type.Convert(string)` `:576` is separate — verify FromWire callers.)
- **[replace]** `data.Convert(kind)` — `data/this.cs:135` → resolve `kind.Type`, call `Create`; `kind.behavior.Convert` gains real converters.
- **[dead]** `SetValueOnObject` reflection arms — `variable/list/this.cs` (bracket-index, IList<T>, CLR-property, ConvertToDictionary); value owns child-write.

### Stage 3 (retire read bridge)
- **[dead]** `goal/serializer/Reader.cs` + `Default.cs` (`Deserialize<goal>`).
- **[dead]** `GoalReadOptions` — `catalog/Conversion.cs:55-59` (only callers are the two readers).
- **[dead]** goal-specific dispatch — `catalog/Conversion.cs:282`.
- **[dead]** `dict.Clr` record-build use — `item/ICreate.cs:61-62` (the STJ method at `dict/this.cs:323` STAYS for maps).
- **[candidate]** `build/code/Default.cs` dual-path step readers — `GetString` `:855-862`, `SetValue` `:868-877`.

### [stays] — do NOT remove
- `app/data/reader/this.cs` — the Data-leaf reader. Stage 1 only *adds* a JsonElement door + corrects the format-routing line; `%var%`/template/signing stay byte-identical.
- `item.Clr` / `ClrConvert` — the plang→CLR lower exit. A different direction; stays.
- `dict.Clr`'s STJ method for untyped dict→map/CLR lowering — stays; only the record use dies.
- `list.Clr` element-wise lowering — stays.
- `type.Build`'s deferred-source born rule (`%var%`→variable, string/bytes→lazy source) — read-time laziness, stays.
- `type.Convert(string)` / `FromWire` — verify snapshot/crypto callers; likely stays (wire reconstruction, not construction).
- `kind.Navigate`/`Enumerate`/`Set`/`Load`/`Output` — the value-plane behaviors; untouched.

---

## app-model plang-types audit

- `action.Module`/`ActionName` land as `string` (the `.pr`-on-disk perimeter shape); the **pull** is `Value<text>()` (plang `text`). No CLR leaf in the model.
- `Parameters` lands as `List<Data>` — Data leaves through the Data reader. Correct: a param value is a Data.
- No new record fields introduced. If Stage 2/3 surfaces a record field typed raw-CLR where a plang type belongs (`DateTime`→`datetime`, `int`→`number`), flag it then.

---

## OBP validation

| Surface | Shape check | Verdict |
|---|---|---|
| `Type.Create(source, kind?)` | Single verb `Create`. One door per type, owned. No compound. | Clean |
| per-type `Create` (relocated from `Convert`) | Behavior moves ONTO the type from a reflective static — removes an outside registry doing a type-switch. | Clean — fixes the smell |
| `data.Convert(kind)` front | Single verb. Resolves kind→type, delegates to `Create`. No parallel mechanism. | Clean |
| navigate-pull body | Uses `src.GetChild(k).Value<T>()`. No new API, no new name. | Clean |
| Data-leaf read | Reuses `app/data/reader`; hands the child *to the reader*, does not decompose the Data into scalars (Rule #7/#8). | Clean |
| write via `Create`/child-write | The value owns its own set (`kind.Set` shape); the seven reflection arms of `SetValueOnObject` go. Removes lower-here/convert-there divergence (Smell #4). | Clean — one write discipline |
| `item.Clr` kept as lower | A distinct direction (plang→CLR), not construction — correctly NOT folded into `Create`. | Clean |
| dropped "FromSource" name | Two-word preposition+noun for what is just `Create`. Not introduced. | Avoided |

No `GetX`/`IsX`/verb+noun surfaces added. The only new *behavior* (a type parsing itself, a source navigating) lands on the value that owns it, never in a caller switch.

---

## Open at implementation time (not blocking Stage 0/1)
- The two non-obvious `TryConvert` callers (`type/this.cs:602`, `setting/this.cs:102`) — confirm they route through `Create` before deleting the pipeline (Stage 2).
- `type.Convert(string)` / `FromWire` — confirm snapshot/crypto reconstruction stays outside the cut (Stage 2).
- Stage 2 is large; it can split further if the diff gets unreviewable — coder's call once Stage 1 is green.
