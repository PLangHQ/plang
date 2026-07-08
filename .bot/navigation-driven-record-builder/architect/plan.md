# Navigation-driven record conversion — a target builds itself from any navigable source

**Branch:** `navigation-driven-record-builder` (off the merged `clr-navigators`/`variable-as-value` line).
**Companion:** coder's seed `.bot/navigation-driven-record-builder/coder/from-source-spec.md`. This plan settles the two decisions the coder handed up (async fork, generated-vs-reflective) and separates the builder-unblock from the read-path retirement.

**You (coder/test-designer) own the final code and test shape.** Every body and signature below is a suggestion that pins the mechanism, not a spec of the exact lines. If a cleaner shape falls out while implementing, take it — flag it back if it moves a seam.

---

## Why

`clr-navigators` made LLM results ride as `clr(json)` — a lazy JsonElement navigated by its kind, never materialized. Correct. But it opened one gap: **writing a clr(json) onto a plang-typed slot has no conversion door.** The builder dies on it — `set %goal.Steps[i].Actions% = %compileResult.actions%` tries to *lower* the clr(json) to `actions.@this`, hits the terminal LOWER door, and throws. The build's steps end up with no actions.

The fix the coder and Ingi settled (2026-07-08): don't patch `actions`. Build the **general** mechanism — a target record builds itself by *pulling* each declared property from a navigable source (clr(json) / dict / clr(POCO)), one path for every source. That closes the write gap now and, later, retires the goal-as-type STJ bridge (`Deserialize<goal>` + `dict.Clr` round-trip) so every record round-trips the same plang-native way.

The mechanism is not a new type. It is `ICreate.Create` and `list<T>.Convert` — both of which already almost do this — going **async** and learning to **navigate** a source instead of demanding a materialized one. Drop the working name "FromSource": there is nothing to name.

---

## The incumbent (leaf trace)

Three doors already carry 90% of this. The change is what they accept and whether they can await.

### Door 1 — `ICreate.Create` (the target builds itself)
`PLang/app/type/item/ICreate.cs:30` — `static virtual TSelf? Create(item value, data)`, **sync**. The default (lines 42–68): lower the source to raw (`value.Clr<object>()`), run the type's own convert hook, and — for a `dict`/`list` source only — `value.Clr(typeof(TSelf))` to deserialize into a record (ICreate.cs:61–62). A **clr(json)** source is neither dict nor list, so it reaches neither branch and declines with "holds a item". That is the gap, exactly.
- `goal`, `step`, `action` implement `ICreate<@this>` with **no hand-written body** — they ride this default.
- `actions` has a hand-written `Create` (`.../actions/this.cs:29`) that accepts a `list` only. A clr(json) array declines here too.

### Door 2 — `list<T>.Convert` (the collection builds itself)
`PLang/app/type/list/this.Generic.cs:52` — `static Data Convert(object? value, kind, ctx)`, **sync**. Requires `value is IEnumerable` and walks each element through `catalog.TryConvert`. A clr(json) carrier is **not** IEnumerable (it wraps a JsonElement), so `list<action>.Convert(clr(json), …)` returns "expected a sequence." Yet the carrier already *knows* how to enumerate — `clr.Enumerate()` / `clr.EnumerateItems()` delegate to the json kind (`clr/this.cs:104,111`). The door just doesn't ask.

### Door 3 — the record's `Clr(System.Type)` (dict → record, the STJ round-trip)
`PLang/app/type/dict/this.cs:323` — `dict.Clr(target)` serializes the dict to UTF8 bytes with its own converter and `JsonSerializer.Deserialize(utf8, target, opts)` back into the record. This is the STJ round-trip the value model names as a smell ("internal round-trip is the smell"). It is what `ICreate.cs:62` calls today. The general mechanism replaces it — a record pulls its properties by navigation, no serialize-then-deserialize.

### The write site (where the blocker throws)
`PLang/app/variable/list/this.cs:364` `SetValueOnObject` — the navigated deep-write (`%goal.Steps[i].Actions% = …`). Its **CLR-property arm** (lines 466–481) already does CONVERT-first: `type.convert.OfStatic(clrProp.PropertyType, value, …)`, falling back to `iv.Clr(slotType)` (LOWER) only when the convert hook produces nothing. So the write path is *already* shaped right — it asks the target to convert first. The failure is downstream: the convert hook for `actions`/`list<action>` can't take a clr(json) (Door 2), so it falls back to LOWER (`clr.Clr(actions.@this)` → `ClrConvert(JsonElement, actions)` → `item/this.cs:363` throws).
- The **index arms** (lines 439–440, 455–456) still LOWER blindly (`iv.Clr(elementType)`), no convert-first. Same bug will bite an indexed element write. Fix them to CONVERT-first too.

### The Data-leaf seam (must stay byte-identical)
`action.Parameters` is `List<app.data.@this>` — the param values `{name,type,value}` are **Data leaves**, not record fields. They carry `%ref%`-born-as-variable, deferred source, template flag, and signing — all owned by `app/data/reader/this.cs`. When the record walk reaches `Parameters`, each element must be **read as a Data through that reader**, never converted to a value. This half does not change and must not.

### The async spine (already async everywhere except the two doors)
`Data.Value<T>()` (this.cs:503), `Data.Value()` (288), `Data.GetChild`/`Navigate` (this.Navigation.cs:17,33), `item.Navigate`, `clr.Navigate`, `kind.Navigate`, `kind.behavior.Navigate` — **all async `ValueTask`**. The dispatch is `T.Create(await Value(), this)` (this.cs:512): the value door is awaited, then Create is called **sync**. `ICreate.Create` and `list<T>.Convert` are the only two sync islands left in the spine.

---

## Decision 1 — `ICreate.Create` and `list<T>.Convert` go async

**Settled: yes. Make both async (`static virtual ValueTask<TSelf?> Create`, `ValueTask<Data> Convert`). Do not split the interface.**

The chain is forced. Navigation-pull needs `await source.GetChild("module").Value<text>()` per property. `GetChild`/`Value` are async. To await inside `Create`, `Create` must be async. To keep **one** construction door (the target builds itself — ICreate's whole contract), every implementor's signature goes async with it.

- **Cost:** ~40 `ICreate` implementors change signature. Mechanical: a sync leaf returns `new ValueTask<TSelf?>(result)` — **no `async` keyword, no state machine, no allocation**. Only the handful that actually navigate (records) use `async`. `list<T>.Convert` gains `async` because it now awaits per-element navigation.
- **Precedent:** this is exactly the truthiness move. `IBooleanResolvable` made the whole condition pipeline async (`IEvaluator.Evaluate`, `Operator.Evaluate`, `assert.IsTrue/IsFalse`) because one leaf capability (`path` existence) can be I/O. Same rule here: because a target can build itself by navigating (and navigation can resolve a `%ref%` / bracket-index — I/O), construction is async, uniformly, at the seam. The value model already chose this shape once.
- **Rejected — the interface split** (sync `ICreate` for scalars + a separate async navigate-create for records). Two doors for one concept ("the target builds itself from a source"). Records already flow through the *same* `Create` default today; splitting now regresses uniformity to dodge mechanical churn. Own the churn.
- **Rejected — pre-navigate in the caller, keep Create sync.** Pre-resolving all children into a bag = materializing the source = the exact round-trip smell we are removing. And whoever navigates must be async anyway (sync-over-async is banned), so the split only moves the boundary out one layer and adds a door.

Dispatch site becomes `await T.Create(await Value(), this)` (this.cs:512) — trivial.

---

## Decision 2 — reflective navigation-pull first; codegen is a deferred perf pass

**Settled: the mechanism lives in the default `ICreate.Create` (generic, navigation-driven). No source-generator work in this branch.**

The coder leaned "generate a `Create` per record." That is the *fast* form, not the mechanism. The mechanism is "pull each declared property from the source." A generic reflective default does that for **every** record with zero per-type code: read the target's properties (types + `[JsonPropertyName]`/camelCase wire keys), and for each, `await src.GetChild(wireName).Value<declaredType>()`; hand `Data`-typed properties to the Data reader (the seam); recurse nested records; route `list<T>` properties through `list<T>.Convert`.

- This is not more reflection than today — `dict.Clr` already reflects via STJ, and it round-trips. Navigation-pull reflects the property list once and pulls; no serialize→deserialize. Strictly better on the axis that matters.
- Records are `init`-only. A generic builder sets init-only via the same reflection metadata STJ uses (or positional-ctor construction once children are resolved). Coder's call on the exact construction primitive.
- **Codegen deferred.** When the mechanism is proven and we retire the read bridge (Stage 3+), the source generator can emit a per-record `Create` object-initializer to drop the reflection for hot types. It emits the record's **own** `Create` body (the record is already `partial` for lazy-params) — no new named type, no "FromSource". Not now: prove the mechanism before committing a generator surface.

So B ("general mechanism", Ingi's call) = the navigation-pull default. A ("per-type patch") is not what we ship — but Stage 1 does hand-write `action`/`actions` to prove the pull in the small before the generic default lands.

---

## Sequencing

Three stages. Stage 1 alone turns the builder green; 2–3 are the payoff.

**Stage 1 — unblock the builder (write path).**
- `ICreate.Create` + `list<T>.Convert` go async (Decision 1). Mechanical sweep across implementors.
- `list<T>.Convert` accepts a **navigable** source: when `value` is a clr/dict/list item, enumerate via the value's own `EnumerateItems`/`Enumerate` (the carrier already delegates to its kind) instead of demanding raw `IEnumerable`.
- `action` gets a hand-written navigation-pull `Create`: pull `module`/`action` as `text`, build `Parameters` as `List<Data>` **through `app/data/reader`** (the seam), recurse `Modifiers`. `actions.Create` accepts a clr(json)/navigable array (defer to `list<action>`).
- The write index-arms (`variable/list/this.cs:439,455`) go CONVERT-first, matching the property arm.
- **Blocker-2 (`goal.getTypes` List-lower)** is the same LOWER-instead-of-CONVERT bug at a native-`List` → `list<dict>` return. `list<dict>.Convert` already takes a native `List` (it is IEnumerable), so this is a convert-first routing fix at that return/read site, not navigation. Verify it falls out of the async convert door; if a distinct call site still lowers, fix it there.
- **Blocker-1 (`data/reader:79-80`)** — the deferred-read format guess (String→text/plain regardless of declared type) — is my clr-navigators demolition item #5, still open, and it is what makes `%plan%` reliably a clr(json). Route an `object`/`dict`/`list`/json-kind-declared wire value → json → clr(json). **Sensitive line** (the variable-as-value seam): a full-match `%var%` must still born a `variable` in `type.Build` (`type/this.cs:265`), a different branch — verify it is untouched. This is separable from Stage 1's convert work; sequence it first per the coder handoff ("START HERE") since without it the write never receives a clr(json).

**Stage 2 — generalize.**
- The default `ICreate.Create` navigates any source generically (Decision 2). `step`, `goal`, and every other record inherit it. The hand-written `action.Create` from Stage 1 either stays (if it carries real quirks) or collapses into the default — coder's call once the default exists.

**Stage 3 — retire the goal-as-type STJ bridge (read path).**
- `.pr` load routes through the navigation-pull path: a `.pr` is a clr(json) that builds itself into `goal`. Retire `goal/serializer/Reader.cs` `Deserialize<goal>` and the `dict.Clr` STJ round-trip (`dict/this.cs:323`). The Reader carries the note that says so ("goal rides as clr, this reader and the goal-as-type machinery go").
- The Data-leaf half (`app/data/reader`, `Wire.ReadOptions`) stays exactly as-is — the skeleton half is the only thing that changes.
- **Blocker-2's neighbour, demolition item #10** (`build/code/Default.cs` dual-path step readers reaching for a raw `JsonElement`/`dict`) collapses here: once steps navigate as Data uniformly, the `step is IDictionary || step is JsonElement` forks become one.

---

## Demolition worklist

Cross-checked against my `clr-navigators` demolition audit — nothing here contradicts its [stays]/[deferred] lists.

**Dies (by stage):**
- **Stage 1:** the sync signatures of `ICreate.Create` and `list<T>.Convert` (both go async). The `IEnumerable`-only guard in `list<T>.Convert` (`this.Generic.cs:54`) — replaced by "navigable source." The blind LOWER in the write index-arms (`variable/list/this.cs:440,456`).
- **Stage 2:** `actions`' bespoke `Create` list-only branch, *if* the generic default subsumes it.
- **Stage 3:** `goal/serializer/Reader.cs` `Deserialize<goal>` (the whole reader). `dict.Clr`'s STJ round-trip (`dict/this.cs:323`) once nothing calls it for record construction — verify no other caller (untyped dict→CLR still needs a lowering; keep that, kill only the record-deserialize use). `build/code/Default.cs` dual-path step readers (my demolition #10).

**Stays (trap list):**
- `app/data/reader/this.cs` — the Data-leaf reader. **Untouched by the mechanism** (Stage 1 fixes its format-routing line per demolition #5; that is a routing correction, not a rewrite). `%ref%`/template/signing stay byte-identical.
- `GoalReadOptions` / `Wire.ReadOptions` (`catalog/Conversion.cs:55`) — still the converter chain for the Data leaves during the read. Only the record-skeleton half stops using STJ.
- `dict.Clr` for genuine untyped dict→CLR lowering — keep. Only the record-deserialize call dies.
- `catalog.TryConvert` per-element authority — `list<T>.Convert` still routes each element through it; the element's own type still owns its build.
- `type.convert.OfStatic` (the CONVERT-first hook the write arm calls) — stays; it is the correct door. It just needs the async navigation door underneath it to succeed for records.

---

## app-model plang-types audit

The pulls read plang types even though the record's landing slots are the wire perimeter (`string`, `List<Data>`):
- `action.Module`/`ActionName` land as `string` (the record's wire shape — legitimate perimeter, the `.pr`-on-disk shape). The **pull** is `Value<text>()` (plang `text`), converted at the boundary. No CLR leaf smuggled into the model.
- `Parameters` lands as `List<Data>` — Data leaves, read through the Data reader. Correct: a parameter value is a Data, not a decomposed scalar.
- No new record fields are introduced, so there is no new CLR-vs-plang leaf to flag. If Stage 2/3 surfaces a record field typed as a raw CLR type where a plang type belongs, flag it then.

---

## OBP validation

| Surface | Shape check | Verdict |
|---|---|---|
| `ICreate.Create` → async | Same name, `TSelf? → ValueTask<TSelf?>`. One verb, no compound. One door kept (no split). | Clean |
| `list<T>.Convert` → async + navigable | Single verb `Convert`. Now asks the source to enumerate itself (behavior on the value) instead of type-switching on `IEnumerable`. | Clean — removes an outside type-switch |
| navigation-pull body | Uses existing `src.GetChild(k).Value<T>()`. No new API, no new name. | Clean |
| Data-leaf read | Reuses `app/data/reader`. The walk hands the child *to the reader* (does not decompose the Data into scalars). Rule #7/#8 respected — the courier does not open `.Value`. | Clean |
| write CONVERT-first arms | `variable/list/this.cs` index-arms match the property arm: target converts, no blind LOWER. Removes the "lower here / convert there" divergence between the three arms (Smell #4). | Clean — one write discipline |
| dropped "FromSource" name | Two-word preposition+noun name for a mechanism that is just `Create`. Not introduced. | Avoided |

No `GetX`/`IsX`/verb+noun surfaces added. The mechanism is two existing single-verb doors going async; the only new *behavior* (navigate a source) lands on the value that owns it, not in a caller switch.

---

## Open for Ingi before coder starts

1. **Stage 1 scope** — hand-write `action`/`actions` `Create` to unblock now (proves the pull small), or wait for the generic default and do it once? I lean hand-write-then-generalize; it gets the builder green fastest and de-risks the async sweep on two real records before touching all 40.
2. **The async sweep is the big mechanical cost** — ~40 `ICreate` implementors change signature in one branch. Confirm you want it in this branch and not split into a prep branch ("ICreate goes async") landed first, then the navigation on top. Cleaner history, one more merge.
3. **`dict.Clr` STJ round-trip** retires only in Stage 3 and only for record construction. Confirm we keep it for untyped dict→CLR lowering (I believe yes — it has non-record callers).
