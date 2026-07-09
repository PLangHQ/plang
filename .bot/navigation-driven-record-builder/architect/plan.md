# Type creation unification + catalog removal — one construction door, no god-object

**Branch:** `navigation-driven-record-builder`. The name is now a subset — record-building is one stage of a larger collapse: every type builds itself through one door, the `catalog` god-object is deleted, and module discovery becomes a plang collection.
**Absorbs:** coder's `catalog-removal-and-create-unification.md` (Decisions B/C, converged independently), coder's `from-source-spec.md` (the record-builder that started it) and `plan-review.md`, and the conversion-door census (this session). The six open items the coder raised are resolved inline (§ Resolved items).

**You (coder/test-designer) own the final code and test shape.** Every body, signature, and stage boundary below pins the mechanism and the intent, not the exact lines. If a cleaner shape or a better cut falls out while implementing, take it — flag it back if it moves a seam or a decision.

---

## Why

The builder dies writing a clr(json) onto a plang-typed slot (`set %goal.Steps[i].Actions% = %compileResult.actions%`): the clr(json) tries to *lower* to `actions.@this`, hits the terminal LOWER door, throws, and the built goal has no actions. That is the symptom. Censusing every conversion path found the disease: **six mechanisms turn a source into a typed value, and not one is the single door** — `ICreate.Create`, the reflective `convert.OfStatic`, the 15-stage `TryConvert`, `type.Convert`, `dict.Clr`'s STJ round-trip, `type.Build`'s lift. They reach the same operation through reflection, STJ serialize-loops, and hand-dispatch instead of through the type. The clr(json) gap exists because none of them navigates. Two more god-objects sit next to them: `catalog` (`type.catalog.@this`) staples type-identity + LLM-schema-fold + a bag of eight unrelated registries; and module discovery leaks bare strings that `BuildTypeEntries`/`Describe()` re-reflect into a C# schema.

Settled with Ingi (2026-07-08): collapse all of it into one shape. **One construction primitive each type owns; the registry of all types is just `list<type>`; module discovery is just `list<module>`.** No reflective hub above the types, no STJ round-trip, no invented parent node. Behavior on the owner, reflection only at the leaf that holds the CLR type, everything above it plang values.

---

## The model — three decisions, one shape

### A. One construction door — `Type.Create(source)`

Each type builds itself from a source; it parses its own input and declines (returns null) when it can't. No `convert.OfStatic`, no reflective `Discover`, no STJ serialize→deserialize. The one operation that today wears three faces —

```
convert.OfStatic(clrType, value, kind, ctx)   // reflective, CLR-keyed        ← the OBP violation
type.@this.Convert(value, ctx)                // entity router → OfStatic
ICreate<T>.Create(value, data)                // the target builds itself       ← the real one
```

— collapses to `Create`. "Convert into myself from another" *is* `Create`. Reached three ways, all landing on the same door:

| you ask by | door | lands on | example |
|---|---|---|---|
| static type (compile-time) | `Value<T>()` | `T.Create(self)` | a handler declares `Data<number>` |
| kind (runtime) | `data.Convert(k)` | `k.Type.Create(self)` (kind on `data.Type`) | `text.convert("mp3")`, `"html"` |
| birth (from a `.pr`) | the read path | `T.Create(source)` over `{name, type, value}` | building `goal` from clr(json) |

**`Convert(kind)` dispatches to the kind's own converter (`kind.behavior.Convert`) — which the kind owns, and which may delegate to `Create`.** A kind knows its type (`kind.Type`: `json→item`, `csv→table`, `mp3→audio`). A kind that is just "build the type from raw" (`mp3` = build `audio`) delegates to `Type.Create`; a kind that is a real transform of another kind (`html` from `md`) does the render itself, in the html kind's converter ("a converter belonging to the html kind knows md→html"). So Convert crosses type *because the kind carries its type*, and the transform lives on the kind it produces (outbound owns it). This fills the currently-stub `kind.behavior.Convert` (only `dict` has a real converter today). See `code-draft.md` § Stage 2 for the shape.

**The other two directions stay distinct — they are not construction:**
- **lower** — plang value → CLR — is `item.Clr`. The exit door. Stays.
- **write a child** — `set %x.y% = v` — the value owns (the `kind.Set` shape). `SetValueOnObject`'s reflection dies.

### B. Delete `catalog` — the registry of all types *is* `list<type>`

`App.Type` (`type.catalog.@this`) staples three unrelated jobs: type-identity (`[name]→entity`, `[clr]→entity`), the LLM schema-fold (`BuildTypeEntries`/`ComplexSchemas`), and a bag of eight sub-registries it only *parents* (`Choices`, `Scheme`, `KindHooks`, `Kinds`, `Conversions`, `Compares`, `Renderers`, `Readers`). `type` is an item (`type/this.cs:33`, settled in the value model), so **the list of all types is `list<type>`** — an instance of the native list value, reached as `app.type.list`. No name collision: the registry is an *instance* of the list value type, `list` appearing as one element is data self-reference.

- **type identity** → `app.type.list` = `list<type>` + a **keyed name→entity index on the collection** (open item #2: the index lives *on* the list, not a revived side-registry). This is the only runtime use of the list — a `.pr` read resolves `type:{name:"text"}` → the `text` type to pick which `Create` to call. Enumerate for the LLM; look up one by name on a read.
- **`Conversions`** → **dissolves into `Create`** (Decision A). Not a move, a deletion.
- **The other seven** → rehome from `catalog`-parented to `app.type.*`-direct. `Kinds`/`Readers` travel with the stages that transform them; `Renderers`/`KindHooks`/`Compares`/`Scheme`/`Choices` are pure reparenting (open item #3, § Sequencing).

Net: `type.catalog.@this` is deleted. `App.Type` becomes the `type` collection node — select `app.type["number"]`, enumerate `app.type.list`.

### C. Module discovery is `app.module.list : list<module>`

The module registry owns "what modules/actions/properties exist" and hands back plang types, not bare strings. `BuildTypeEntries(modules)` and `Describe()` (C# schema builders that reflect action shapes into `StepActions`/`List<data>`) delete.

```
app.module.list : list<module>          // the ACTION modules — dispatchable verbs, not C# infra folders
  module.Actions    : list<action>      // module = namespace, action = class (variable/set.cs)
    action.Properties : list<type>      // keyed by property name; value = the prop's plang type
```

- **Views over reflection, one source (coder Decision 3(a)).** `module`/`action` are thin `item` views over the namespace / handler `System.Type` — the clr-navigator idea applied to type metadata. Nothing materialized, no drift. Rejected (b) a stored `ActionDefinition` mirror (two sources).
- **No `field` type; reflection at the leaf.** A property's type is a `type.@this` with `.Name` already set — `Data<text> Name` → key `name`, value `type{name:"text"}`. Consumers read `type.Name`; they never see a `System.Type` or call `GetTypeName`. The reflection (unwrap `Data<T>`/`[Code]T`, map the CLR property to its plang type) happens *once, inside the `action` view* — the one leaf that holds the class. Above it, plang values all the way up. `GetTypeName(typeof(x))` at a consumer site is the violation and goes; it survives only as the `action` view's internal leaf mechanic.
- **The only consumer is build-time LLM teaching.** So `list<module>` is a discovery view, not runtime-hot — `BuildTypeEntries`/`Describe()`/`StepActions` collapse to a Fluid render over the collection. The builder does `get all modules → %modules%` (a `list<module>`), `ui.render 'template' → %doc%` (the .md the LLM reads), and drills in with `filter %modules% where action=… → render`. (Runtime *dispatch* — resolve one action by name to execute — is a separate keyed read over the same handler class, not this list.)

**Why one branch:** the three decisions share the same call sites (every `convert`/`type.Convert`/`catalog.X` site) and the same principle (behavior on the owner, reflection at the leaf, one door). Splitting would tangle the same edits across branches.

---

## Correction — the `.pr` graph is clr hosts, not items (item ⟺ ICreate)

**Rule (Ingi, 2026-07-08):** a type is a plang value **iff** it implements `ICreate`. Present → item, builds itself via `Create`. Absent → a **clr host**, built by deserialization, navigated/written by its kind (reflection). This makes item-ness explicit and enforced by a contract, not ad-hoc.

**goal / step / action / actions (the whole `.pr` graph) are hosts.** They currently declare `: item.@this, ICreate<@this>` — the *bridge* (`goal/serializer/Reader.cs`: *"goal is really a host CLR object, not a plang value type… rides as clr, this machinery goes"*). They drop `item.@this` + `ICreate` → plain C# classes carried as `clr(goal)`, navigated by the reflection (`*`) kind. This reframes several stages:

- **Read — Stage 5 FLIPS.** `Deserialize<goal>` (the `.pr`→C# builder, gated on the `.pr` extension at `path/file/this.Operations.cs:66`) **STAYS** — it is the legitimate host builder, not a smell. What retires is only the goal-as-plang-**type** façade: the `goal`/`actions` ITypeReaders that dress them as `Data` values + the `item.@this`/`ICreate` declarations. The Data-leaf params (`action.Parameters : List<Data>`) still ride the Wire converter in `GoalReadOptions` — unchanged.
- **The blocker collapses.** `set %goal.Steps[i].Actions% = %compileResult.actions%` — goal is a host; the write reflects to the C# `Actions` property, and clr(json) → `List<action>` is **STJ-deserialize into the host property**. No navigate-pull, no `list<action>.Create`, no targeted `Create` for this. **Stage 1's `list<action>`/`action.Create` navigate-pull work evaporates.**
- **`Data<goal>`/`Data<action>` → `Data<clr<goal>>`.** Two sites (`goal/getTypes.cs:34`, `environment/run.cs:15`); `clr<app>` (goalsSave) is the precedent.
- **The WRITE side already works — and consolidates.** The `.pr` is *written* by `goal.Output(View.Store)` (`build/this.cs:37`, `Default.cs:289`) — `item.OutputTagged` looping `Tagged.PropertiesFor(type, mode)`. The reflection (`*`) kind's `Output` (`reflection.cs`) runs the **identical** loop over the **same** `Tagged.PropertiesFor(type, mode)` (View.Store → `[Store]`). So a host goal writes correctly through `clr.Output` → reflection kind with **no new work** — and `item.OutputTagged` + `reflection.Output` are duplicate implementations that **collapse to one** (the `.pr`-graph hosts drop `OutputTagged`, use the reflection kind; `item.OutputTagged` likely dissolves).
- **Audit the other bridge-items** with the rule: `snapshot`, `GoalCall`, `catalog/view` (and check `app`) also declare `item.@this` + `ICreate` but may be hosts — decide each: value or host?

**Net:** the navigate-pull record builder (the from-source-spec that named this branch) was aimed mostly at the `.pr` graph, which isn't items — it **largely evaporates**. What survives: **Create-unification** (one door, `convert.OfStatic` dissolves) for *real* value items (number/text/dict/list/permission/…), **catalog removal**, **module discovery**. The branch trims.

---

## `Create`'s contract

```csharp
static virtual ValueTask<TSelf?> Create(@this value, data data)   // DIM default; keep `virtual`
```

- **Two runtime faces, one static per-type `Create`; a three-layer dispatch (coder review #2, mechanism pinned with Ingi — option A).** `Value<T>()` names `T` at compile time (reflection-free, the hot path). A runtime caller holds a token, not a `T` — and a `System.Type` **cannot** invoke a `static virtual` interface member. So the runtime door is three layers: **(1) the parse** — `text.@this.Create(value, data)`, static, one per type, the logic; **(2) one shared generic thunk** — `Builder<T>() => (v,d) => T.Create(v,d)`, registry plumbing, logic-free, NOT per type; **(3) a closed delegate ON each `type.@this` entity** — `Func<item,data,ValueTask<item?>> Create`, produced once per type at registration via `Builder<T>` + `MakeGenericMethod` (the single reflective touch — option A; a generator-emitted table is the reflection-free B, deferred). Callers: `app.type["text"].Create(v,d)` / `app.type[typeof(text)].Create(v,d)` / the write arm `app.type[elementType].Create(v,d)` — dict lookup + direct delegate call, no per-call reflection. This is the **targeted** door (target preserved), distinct from the **polymorphic** `type.Create(raw)` at `type/this.cs:439` (infers from raw, discards target). **It replaces `OfStatic`'s CLR-targeted construction and must exist before Stage 2 deletes `OfStatic`.** Why it's not `OfStatic` renamed: logic on the type, delegate on the entity, reflection once at registration — vs the hub's per-call `MethodInfo.Invoke`. See `code-draft.md` § Stage 2.
- **`static virtual` (DIM default) is retained** — ~50 types declare `ICreate<T>` and inherit it; only the 6 real overrides change signature in Stage 0. Dropping `virtual` breaks the inheritors.
- **No `kind`/`strict` param — they ride on `data.Type`.** `value` (arg 1) is the *source*; `data` (arg 2) is the Data being built, and `data.Type` is the *target* descriptor `{name, kind, strict}`. `Create` reads `data.Type.Kind` / `data.Type.Strict` — as today's default already does (`convert.OfStatic(…, data.Type?.Kind?.Name, …)`). The kind you want is the target's, never the source value's.
- **The type parses its own input** (Ingi): `number.Create("42")` parses the string, picks precision, coerces — the body relocated verbatim from today's `number.Convert`. A private ctor takes the clean CLR value; `Create` holds "is this even a number?" because `Create` can decline (null) and a ctor can only throw.
- **Strict: read uniformly off `data.Type`, enforce by timing.** `Create` owns the eager check (built value's kind vs `data.Type.Kind` when strict); a lazy/byte-backed value (image `strict:jpg`) enforces at its load seam — you can't validate a kind you haven't read. Don't collapse `variable.set`'s three enforcement moments (build / run / materialization) into `Create`.
- **Declines with null, reason on `data.Fail`** — the existing ICreate contract, unchanged.
- **Records navigate-pull each declared property**: `module`/`action` as `text`, `Parameters` as `List<Data>` through the Data reader (the seam, below), nested records recurse `Create`, `list<T>` properties route through `list<T>`'s own `Create`. Scalars coerce their raw. Same door, different bodies.
- **Async.** Navigation can be I/O (`%var%` resolution, bracket-index) — same rule as `IBooleanResolvable` making the condition pipeline async because one leaf can be I/O. `Value<T>()` is already async; the dispatch becomes `await T.Create(await Value(), this)`.

---

## The incumbent (leaf trace) — what collapses, with disposition

Grounded at file:line; each reaches the one operation through a redundant route.

1. **`ICreate.Create` default** — `item/ICreate.cs:30`. Today funnels: pass-through → facet → `convert.OfStatic` → `type.Create(raw)` → `dict/list.Clr(typeof(TSelf))`. **→ becomes the one primitive.** Keep pass-through + facet; the tail is replaced by "record → navigate-pull; scalar → the type's own `Create`."
2. **`convert.OfStatic`/`Of`/`Invoke`/`Discover`** — `convert/this.cs`. Reflectively finds+invokes a per-type `static Convert`. **→ dies.** The per-type `Convert` bodies relocate onto each type as its `Create`. `OwnerOf`/`_ownership`/`BuildOwnership`/`OwnedClrTypes` (the raw-CLR→family map) is **NOT dead** — `type.Create(raw)` needs "a `long` → number"; it survives as a private index behind `type.Create(raw)`, not a public door.
3. **`TryConvert`** — `catalog/Conversion.cs:128-522`, 15 stages, only 4 external callers (`type.Convert` `:225`, type/this.cs:602, `list<T>` this.Generic.cs:64, `setting/this.cs:102`). **→ collapses.** Construction stages fold into `Create`; primitive `ChangeType` lowering survives inside `item.Clr`. Verify the two non-obvious callers route through `Create`.
4. **`type.@this.Convert(value, ctx)`** — `type/this.cs:187`. **→ dies into `Create`.** (`type.Convert(string raw)` `:576`, the wire/`FromWire` reader, is a *different* method — verify snapshot/crypto callers; likely stays.)
5. **`dict.Clr(Type)`** — `dict/this.cs:323`. STJ serialize→deserialize to build a record (same round-trip as TryConvert stages 9/14 — three homes for one smell). **→ record-build use dies into `Create`;** the untyped dict→`Dictionary<string,T>`/map lowering STAYS (non-record callers verified).
6. **`type.@this.Build` / `type.@this.Create(raw)`** — `type/this.cs:249`/`:439`. `Create(raw)` (the polymorphic lift) **folds into the one `Create`.** `Build`'s **deferred-source born rule** (string/bytes → lazy source; full-match `%var%` → variable) is read-time laziness, a separate concern — **stays**.
7. **`catalog`** — `type/catalog/this.cs`. **→ deleted** (Decision B): identity → `list<type>` + index; `Conversions` → `Create`; the other 7 rehome.
8. **module discovery** — `module.@this.list:IEnumerable<string>`, `Describe()`, `BuildTypeEntries(modules)`, `StepActions`. **→ deleted** (Decision C): `list<module>` views + a Fluid render.

**The write site** (`variable/list/this.cs:364` `SetValueOnObject`) — its CLR-property arm already CONVERT-firsts; its index-arms (`:440,:456`) blind-LOWER. **→ all arms route through the slot type's own `Create`;** the seven reflection arms die (the clr(json) `kind.Set` delegation at `:389` is already the right shape).

**The Data-leaf seam (must stay byte-identical).** `action.Parameters` is `List<app.data.@this>` — param values `{name,type,value}` are **Data leaves**, not record fields. They carry a full-match `%var%`-born-as-variable, deferred source, template flag, signing — owned by `app/data/reader/this.cs`. When `Create` reaches a `Data`-typed property it hands the child **to that reader** (via a new JsonElement-input door reusing the byte path's `FromRaw` deferral tail — coder review I3), never converts it to a value. `%var%`/template/signing stay identical.

**The async spine** is already async everywhere except the two sync islands (`ICreate.Create`, `list<T>.Convert`). `data.Convert(kind)` already exists (`data/this.cs:135`, `=> to.Convert(this, _context)`).

---

## Resolved design items (the coder's six)

| # | resolution |
|---|---|
| 1 `list<type>` bootstrap | No true cycle. `list<T>` is a re-tag (no element conversion at birth); type entities `Promote()` lazily. Born with System.Context (`app/this.cs:287`, before Type). Populate by assembly reflection (CLR→entity, no name-lookup) → derive the index → lazy promote on read. Must stay lazy + runtime-extendable (module choices + `code.load` register after Type is born) — the current catalog already has this shape. The `list`/`type` self-reference is harmless data self-reference. |
| 2 index home | On the collection (`app.type.list` owns its name→entity index), not a revived side-registry. Only `list<type>` needs it; `list<module>` doesn't (build-time). |
| 3 rehoming | Transform-with-stage: `Conversions`→`Create` (Stage 2), identity→`list<type>` (Stage 3), `Kinds` (Stage 2 fills its Convert), `Readers` (Stage 1/5 touch it). Pure reparent: `Renderers`/`KindHooks`/`Compares`/`Scheme`/`Choices` — a detachable mechanical tail; the only thing safe to split to a follow-on if the branch is too big. |
| 4 kind/strict | On `data.Type`; `Create` reads them, no extra param. Strict-enforce distributed by timing. Callers that "pass a kind" set `data.Type.Kind` instead. |
| 5 schema-fold | Not a fold — a Fluid render over `list<module>` + types self-describing. `BuildTypeEntries`/`Describe()`/`StepActions` delete. Confirm the render doesn't regress LLM teaching (examples/defaults/return-types currently folded in `Describe()`). |
| 6 module-tree | Reflection view at the leaf (Decision C). Confirm the `module`/`action` view classes one at a time; `action.Properties`' keyed `(name→type)` navigation reuses dict/clr enumeration, not a new keyed-list type. |

---

## Sequencing

Six stages. Stage 1 is the builder-green milestone; 2–5 complete the collapse. The hub stays alive for scalars until Stage 2, so Stage 1 can unblock without waiting for the full relocation.

**Stage 0 — prep branch (async), landed first.** `ICreate.Create` (the `static virtual` default + its **6 overrides**: snapshot, list<T>, permission, actions, clr<T>, variable — *not* ~40; the rest inherit the DIM and are untouched) + `list<T>.Convert` → `ValueTask`; dispatch at `data/this.cs:512` → `await T.Create(…)`. Signature sweep, isolated from the design. (The doomed convert hub is *not* made async — deleted in Stage 2, leave it sync; Stage 1 routes records/lists through the async `Create`, scalars still through the sync hub until Stage 2.) NB (coder review #1): the write-path async — `SetValueOnObject` (`variable/list/this.cs:364`) going async because its arms route through async `Create` — lands in **Stage 1**, not here; its caller `Set` (`:111`) is already async, so mechanical.

**Stage 1 — unblock the builder (write path).** On merged Stage 0. Milestone: builder green.
- `list<T>` accepts a navigable source (clr/dict/list) via its own `EnumerateItems`/`Enumerate`, not raw `IEnumerable`.
- Hand-write `action.Create`: `module`/`action` as `text`, `Parameters` as `List<Data>` through `app/data/reader`, recurse `Modifiers`. `actions.Create` accepts a navigable array (defer to `list<action>`).
- Write index-arms (`variable/list/this.cs:440,456`) → route through the slot type's `Create`.
- Blocker-1 (`data/reader/this.cs:79-80`, clr-navigators demolition #5): route `object`/`dict`/`list`/json-kind-declared → clr(json). Rule (review I4): **String token → unchanged (text/plain, incl `%var%`); non-String or json-kind-declared → clr(json).** The String branch is out of scope — a full-match `%var%` borns a variable in `type.Build` (`type/this.cs:265`). Sequence first.
- Blocker-2 (`goal.getTypes` List-lower): same bug at a native-`List`→`list<dict>` return; confirm it routes the async convert door.
- Reader JsonElement-input door (review I3) reusing the `FromRaw` tail. DoD: a round-trip test that a navigation-built param signs identically to a byte-read one (review I7 — sign fires in `Wire.Write`).

**Stage 2 — collapse to one `Create`.** The core of Decision A.
- Relocate the 14 per-type static `Convert` hooks → async `Create` on each type (`number`, `text`, `datetime`, `date`, `time`, `duration`, `bool`, `binary`, `guid`, `image`, `path`, `dict`, `list`, `GoalCall`). The DOOR moves (hub→`Create`), the kind source (param→`data.Type.Kind`), the return convention (`context.Ok/Error`→`return`/`data.Fail`).
- **Construction that varies by KIND lives on the KIND — uniformly, no exceptions (golden rule: the pattern never diverges).** A type whose build varies by kind delegates to the kind; it never switches. Findability is the point — "the int parser" is always at `type[number].kind[int]`. The audit found **`number` is the only offender** — `path` (scheme registry), `kind.behavior` (format registry), `image`, `choice`, `clr` already delegate; every other type has no kind axis or kind-is-metadata (`text`/`binary`). So the rule mostly *documents the existing pattern*; only `number` needs work.
  - **`number` is its OWN follow-on scope, not this branch.** Its kind-switch is 4 sites deep (`CoerceToKind` construction + serializer read + serializer write + `FromDoubleAsKind` arithmetic) — self-contained and tangential to catalog-removal / the Create door. This branch notes it; the refactor lands separately.
  - **When it lands:** `number.Create` becomes a thin dispatcher; precisions move to `type[number].kind[<k>]`, the switch family dissolves. **Kind = storage type** (int/long/decimal/double/float/bigint), chosen by declaration → source (db double / lib float) → app default (`long`, a setting — not hardcoded). **Precision (decimal places) is separate — an edge op** (max in every calculation, round only at output / explicit request), NOT a construction/kind concern. Overflow + double⊕decimal policy already exist (settings-carried `Overflow.Promote` / `Precision.Error`) and stay. Full model: [[plang-value-and-type-model]] "The number model".
- Delete `convert.OfStatic`/`Of`/`Invoke`/`Discover`; keep `OwnerOf`/`_ownership` as a private index behind `type.Create(raw)`.
- Collapse `TryConvert`: construction stages fold into `Create`; primitive lowering stays in `item.Clr`; route the 4 callers to `Create`.
- Delete `type.Convert(value)`; callers → `Create`.
- `data.Convert(kind)` becomes the front: resolve `kind.Type` → `Create`; fill `kind.behavior.Convert` with real converters.
- Generic record navigate-pull default; the Stage-1 hand-written `action.Create` collapses in unless it carries a real quirk.
- `SetValueOnObject` reflection arms → the value owns its child-write.

**Stage 3 — delete `catalog`.** Decision B.
- Type identity → `app.type.list` = `list<type>` + keyed name→entity index on the collection. **Runtime-hot, sequence and test APART from the reparent tail** (coder review #4): `[name]→entity` is the lookup hit on *every `.pr` read* to pick which `Create` to call — Decision A depends on it. This is the Stage-3 item that can regress runtime; the reparents cannot.
- `Kinds`/`Readers` rehome to `app.type.*` (they were already touched in 1/2/5).
- Mechanical tail (no runtime risk): `Renderers`/`KindHooks`/`Compares`/`Scheme`/`Choices` reparent to `app.type.*` — isolated commits so the rename reviews as noise; the designated release valve, splittable to a follow-on if size demands.
- Delete `type.catalog.@this`.

**Stage 4 — module discovery → `list<module>`.** Decision C.
- `app.module.list:list<module>`, `module.Actions:list<action>`, `action.Properties:list<type>` (keyed by name, reflection at the `action` leaf).
- Delete `BuildTypeEntries(modules)`/`Describe()`/`StepActions`; discovery becomes a projection over `list<module>`, the compile prompt a `Fluid(list<module>)` render + types self-describing.

**Stage 5 — retire the goal-as-plang-TYPE façade (NOT the STJ builder — see the item⟺ICreate correction).**
- `Deserialize<goal>` **STAYS** — goal is a clr host, STJ is its legitimate builder. Delete only the goal-as-**type** façade: the `goal`/`actions` ITypeReaders (`goal/serializer/Reader.cs` + `Default.cs`) that dress the host as a `Data` value, and `item.@this`/`ICreate` on goal/step/action/actions.
- `GoalReadOptions` (`catalog/Conversion.cs:55`) **stays** — STJ still needs the Wire converter chain to read the Data-leaf params (`action.Parameters`).
- The `.pr` **write** side already works via the reflection kind (identical `Tagged.PropertiesFor` loop). Consolidate: the `.pr`-graph hosts drop `item.OutputTagged` and use the reflection kind's `Output`; dedupe `item.OutputTagged` (its only users are the hosts + test).
- `dict.Clr`'s record-build use (`ICreate.cs:61-62`) retires with the Create-collapse (Stage 2) for real items; the STJ method stays for maps.
- Collapse `build/code/Default.cs` dual-path step readers (`GetString` `:855-862`, `SetValue` `:868-877`) — `step is JsonElement` vs `IDictionary` unify once steps are navigated uniformly as a clr host.

---

## Code to remove (demolition worklist)

Tagged **[dead]** delete / **[replace]** rewrite / **[relocate]** move onto the owning type / **[candidate]** collapses—verify / **[stays]** do not touch. Cross-checked against the clr-navigators audit.

### Stage 0 (async prep)
- **[replace]** `ICreate.Create` sig — `item/ICreate.cs:30`, keep `static virtual` → `ValueTask<TSelf?>`. Touches the default + its **6 overrides** (snapshot/list<T>/permission/actions/clr<T>/variable); the ~50 declare-and-inherit types are untouched. Sync leaves `return new(result)`.
- **[replace]** `list<T>.Convert` sig — `list/this.Generic.cs:52` → `ValueTask<Data>`.
- **[replace]** dispatch — `data/this.cs:512` → `await T.Create(...)`.

### Stage 1 (unblock)
- **[replace]** `list<T>.Convert` IEnumerable-only guard — `list/this.Generic.cs:54` → navigable source.
- **[replace]** blind LOWER in write index-arms — `variable/list/this.cs:440,456`.
- **[replace]** deferred-read format guess — `data/reader/this.cs:79-80`.
- **[new]** `app/data/reader` JsonElement-input door reusing the `FromRaw` tail.

### Stage 2 (collapse to Create)
- **[relocate]** 14 per-type static `Convert` hooks → each type's async `Create`: `type/*/this.Convert.cs` (number, text, datetime, date, time, duration, bool, binary, guid, image, path, dict, list) + `goal/GoalCall.cs:60`.
- **[dead]** `convert.OfStatic`, `Of`, `Invoke`, `Discover` — `convert/this.cs`.
- **[relocate/stays]** `OwnerOf`, `_ownership`, `BuildOwnership`, `OwnedClrTypes` — NOT dead. Becomes the cached invoker behind **both** runtime Create faces: the **targeted** `type[clrType].Create(value, data)` (write arms, `.pr` name-resolution, `data.Convert(kind)`) and the polymorphic `type.Create(raw)`. The targeted face must land before `OfStatic` is deleted (coder review #2) — it's what carries the target type the write arm holds.
- **[candidate]** `TryConvert` + `ConvertElementsInto` — `catalog/Conversion.cs:128,94`. Construction stages fold into `Create`; keep primitive lowering in `item.Clr`; verify callers `type/this.cs:602`, `setting/this.cs:102`.
- **[dead]** `type.Convert(value, ctx)` — `type/this.cs:187`. (`type.Convert(string)` `:576` separate — verify FromWire.)
- **[replace]** `data.Convert(kind)` — `data/this.cs:135` → resolve `kind.Type`, call `Create`; `kind.behavior.Convert` gains real converters.
- **[dead]** `SetValueOnObject` — `variable/list/this.cs`, the whole method (8 arms type-switching on the target's C# shape). It is the write-side **obpv**: arm 3 (`clr → clrTarget.Kind.Set`, `:389`) is already the right shape; the other 7 do the same operation from *outside* the value. A write becomes: navigate to the target, call `target.Kind.Set(key, value)` — symmetric with read (`Kind.Navigate`), enumerate (`Kind.Enumerate`), output (`Kind.Output`).
- **[new]** the reflection (`*`) kind gains a `Set` — `kind/behavior/reflection.cs` (today only `json` overrides `Set`; base throws). Mirror of its `Navigate`: reflect the property, convert the incoming value to its type, set it. This is what lets a clr host (goal/step/action) be written through `clr.Kind.Set`, so `SetValueOnObject` dies **including for hosts**.

### Stage 3 (delete catalog)
- **[replace]** type identity (`[name]→entity`, `[clr]→entity`, `Get`/`Clr`/`GetTypeName`) → `app.type.list` = `list<type>` + keyed index on the collection.
- **[relocate]** `Kinds`, `Readers`, `Renderers`, `KindHooks`, `Compares`, `Scheme`, `Choices` → `app.type.*`-direct (the last five a mechanical, detachable tail).
- **[dead]** `type.catalog.@this` — the node, once nothing parents on it.

### Stage 4 (module discovery)
- **[dead]** `module.@this.list:IEnumerable<string>`, `GetActions`-as-strings, `Describe()`, `StepActions`.
- **[dead]** `BuildTypeEntries(modules)` — becomes a projection over `list<module>`.
- **[replace]** the compile prompt → `Fluid(list<module>)` + types self-describing.
- **[dead]** `GetTypeName(typeof(...))` at consumer/description sites — the `action` view resolves names at its leaf; consumers read `type.Name`.

### Stage 5 (retire read bridge)
- **[dead]** `goal/serializer/Reader.cs` + `Default.cs` (`Deserialize<goal>`); `GoalReadOptions` (`catalog/Conversion.cs:55-59`); goal dispatch (`:282`).
- **[dead]** `dict.Clr` record-build use — `item/ICreate.cs:61-62` (the STJ method at `dict/this.cs:323` STAYS for maps).
- **[candidate]** `build/code/Default.cs` dual-path step readers — `GetString` `:855-862`, `SetValue` `:868-877`.

### [stays] — do NOT remove
- `app/data/reader/this.cs` — Data-leaf reader. Stage 1 *adds* a JsonElement door + corrects the format line; `%var%`/template/signing byte-identical.
- `item.Clr`/`ClrConvert` — the plang→CLR lower exit. A different direction; stays.
- `dict.Clr`'s STJ method for untyped dict→map/CLR lowering; `list.Clr` element-wise lowering.
- `type.Build`'s deferred-source born rule (`%var%`→variable, string/bytes→lazy source).
- `type.Convert(string)` / `FromWire` — verify snapshot/crypto callers; likely stays (wire reconstruction, not construction).
- `kind.Navigate`/`Enumerate`/`Set`/`Load`/`Output` — value-plane behaviors; untouched.
- `OwnerOf`/`_ownership` — survives as the `type.Create(raw)` index (see Stage 2).

---

## app-model plang-types audit

- `action.Module`/`ActionName` land as `string` (the `.pr`-on-disk perimeter); the pull is `Value<text>()`. No CLR leaf in the model.
- `action.Properties` values are `type.@this` with `.Name` set — no `System.Type` above the `action` view leaf.
- `Parameters` lands as `List<Data>` — Data leaves through the Data reader. Correct.
- No new record fields introduced. If Stage 2–5 surfaces a record field typed raw-CLR where a plang type belongs (`DateTime`→`datetime`, `int`→`number`), flag it then.

---

## OBP validation

| Surface | Shape check | Verdict |
|---|---|---|
| `Type.Create(source)` | Single verb, one door per type, owned. kind/strict off `data.Type`, not decomposed params. | Clean |
| per-type `Create` (from `Convert`) | Behavior moves ONTO the type from a reflective static — removes the outside registry type-switch. | Clean — fixes the smell |
| `data.Convert(kind)` front | Single verb; resolves kind→type, delegates to `Create`. No parallel mechanism. | Clean |
| `app.type.list` = `list<type>` | The registry IS an instance of the native collection; index on the collection (owner holds its own index). | Clean |
| `list<module>` / `action.Properties:list<type>` | Views over reflection; reflection at the leaf; consumers read `type.Name`, never CLR. Producer-hands-raw (bare strings) smell removed. | Clean |
| Data-leaf read | Reuses `app/data/reader`; hands the child *to the reader* (no decompose into scalars — Rule #7/#8). | Clean |
| write via `Create`/child-write | Value owns its set (`kind.Set` shape); `SetValueOnObject`'s seven arms go — removes lower-here/convert-there divergence (Smell #4). | Clean — one write discipline |
| `item.Clr` kept as lower | A distinct direction (plang→CLR), correctly NOT folded into `Create`. | Clean |
| dropped `catalog` / "FromSource" names | God-object parent + a two-word preposition-noun for what is `list<type>` / `Create`. Not kept. | Avoided |

No `GetX`/`IsX`/verb+noun surfaces added. The only new behavior (a type parsing itself, a source navigating, a view reflecting) lands on the owner, never in a caller switch.

---

## Open at implementation time (not blocking Stage 0/1)
- The two non-obvious `TryConvert` callers (`type/this.cs:602`, `setting/this.cs:102`) — confirm they route through `Create` before deleting the pipeline (Stage 2).
- `type.Convert(string)` / `FromWire` — confirm snapshot/crypto reconstruction stays outside the cut (Stage 2).
- Item #5 — confirm the Fluid render replaces `Describe()` without regressing LLM teaching (examples, defaults, return types) (Stage 4).
- Item #6 — confirm the `module`/`action` view classes (coder shows them one at a time) and the keyed `(name→type)` navigation seam (Stage 4).
- Stages 2–5 are large; each can split further if the diff gets unreviewable — coder's call once Stage 1 is green. The five pure-reparent registries (Stage 3) are the designated release valve.
