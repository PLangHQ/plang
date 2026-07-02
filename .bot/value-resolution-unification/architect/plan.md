# Plan — The value owns itself: one resolution seam, one output door

**Author:** architect · **Branch:** `value-resolution-unification` (from `context-never-null`)
**Sensitivity:** HIGH — touches `Data.Output` (the single write door for both the
`application/plang` wire and the text serializers), signing/hashing (the Store view), the
`variable`/`source` carriers, `set` assignment semantics, and every channel write. A regression
hits `.pr` persistence, signatures, and all output.

> Supersedes the coder exploration doc `.bot/context-never-null/coder/output-resolution-unification-plan.md`.
> That doc's forks are all resolved here; its section numbers (§4, §9.2, §10, §11, §12) are
> referenced where useful.

---

## Why

Resolving a `%ref%` or a template happens **two ways** today. On the READ path a value resolves
**itself** through its `Value(data)` door. On the WRITE path values write **raw** and resolution
is **bolted on upstream** as type-specific special-cases (`Data.Output` tests `_item is variable`,
`output.write` Peeks for `text{Template}`, `mock/intercept` duplicates it). Special-cases are the
red flag: a value shape no guard names slips through and prints literally — the failing
`StartGoalTests` embedded cases.

The base `item.Output` doc comment already describes the target — *"variable resolves itself"* —
and then the code violates it (`variable` has no `Output` override; `Data.Output` resolves it
externally). The comment is the plan; the code is the smell.

The through-line of every decision below: **a value owns what happens to it.** It resolves
itself, it writes itself, it copies itself. Relay layers (the courier, `Data`, the wire) carry
the box and never open it or ask what type it holds.

---

## Target invariant

> **A value writes itself. `Data` never asks what the item IS — only which VIEW it was asked for.**
>
> `Data.Output` contains ZERO `_item is <Type>` questions. It owns exactly one switch — the
> **view** (Store = authored/raw, Out = live/resolved) — and delegates resolution to the item's
> `Value` door and raw emission to the item's `Output`. A variable jumps to its binding, a text
> fills its holes, a number prints — none of that is `Data`'s business. Any `is <ConcreteType>`
> on `_item` in a relay layer is the smell we remove, not relocate.

---

## Decisions (all settled)

| # | decision | status |
|---|---|---|
| 1 | **Absent user ref → throw, uniformly** (`VariableNotFound`, full OR partial); infra `%!x%` stays literal on absence | DONE `e8519aa60` |
| 2 | **`Data.Output` gate = `mode != Store && !_item.Cacheable`** — Data owns the view, item owns resolve+raw; zero type-tests | planned (this pass) |
| 3 | **Passthrough is not a decision** — dissolved by the `!Cacheable` gate (a no-template source is `Cacheable` → raw → byte-exact) | dissolved |
| 4 | **One cycle guard** — `variable.Value._resolveDepth`; delete `Data._outputDepth` | planned |
| 5 | **`skipInfrastructure` DELETED** — OBP smell; both callers removed | planned |
| 6 | **Model B** — a typed ref (`%x% as T`) rides `source`; `source` resolves the `%ref%` once, then coerces to `T`; scalars never learn about refs | settled earlier |
| 7 | **`Resolve` collision → constructor** — delete static `variable.@this.Resolve`; parse folds into `new variable.@this(raw)`; `list.Resolve` → `Render` | planned |
| 8 | **Carrier collapse — `variable` is name-only** — every value reference rides `source`; `variable.@this` is ONLY a name/write-target | planned |
| 9 | **Swift value semantics + deep copy-on-write** — `set %a% = %b%` gives `%a%` its own copy; deep split on mutate. REVERSES the 2026-06-10 C# reference-semantics ruling | planned (own pass) |
| 10 | **`file/read` stamps `template="plang"`** — drop `ResolveVariables` + the manual `Resolve` | planned |
| 11 | **One write verb: `Output`** — delete `Write`; fold each leaf's `Write` body into an `Output` override | planned |

---

## The design, per layer

### `Data.Output` — owns the view, nothing about type (decision #2)

```
Data.Output(writer, mode, context):
    … envelope (Store name / @schema / type — about the VIEW+WRITER, not the item's type) …
    if (mode == Store || _item.Cacheable)             // authored form, OR nothing live to resolve
        await _item.Output(writer, mode, context);    // raw — pass mode THROUGH (a container resolves per child)
    else                                              // a LIVE leaf (Cacheable == false)
        await (await this.Value()).Output(writer, mode, context);   // resolve via the DOOR, then write
```

- **The gate is `!Cacheable`, not "always `Value()` on Out".** `Cacheable == false` already means
  "I hold live `%refs%`" and is declared on every item — `text.Cacheable => Template == null`,
  `source.Cacheable => _template == null`, `variable.Cacheable => false`, base `=> true`. It reads
  item STATE the item owns (same family as `IsLeaf`/`IsFinal`) — **not** a type-test, **not**
  `.Value` (Rule #7 clean).
- **Pass `mode` through; never force `Store` down a container** — that would suppress a child's
  resolution on the Out view. Resolution is per-leaf; a `Cacheable` container takes the raw branch
  and walks its children in `mode`, each live child hitting its own `Data.Output` gate.
- **Delete the `_item is variable.@this` branch and `_outputDepth`.** Acceptance test: grep
  `Data.Output` for `_item is` → nothing. The cycle guard now rides only on `variable.Value`.

### The `Value` door — the single resolution seam

`source.Value` is THE seam. It resolves the `%ref%` once (via the store engine), then `T.Create`
coerces to the declared type. Bare `%x%` → `source{type=item}` (item = apex, identity coercion —
there is no "dynamic" type). `%x% as number` → `source{type=number}`. Embedded `"hi %x%"` →
`source{template}`. Scalars (`number`/`bool`/`datetime`) stay concrete and never learn about refs.
**No new `Output` overrides that resolve** — if a PR adds `text.Output`/`source.Output` that
resolves, resolution has leaked back out of the door.

### `output.write` collapses

`output.write.Run()` → `return await Channel.WriteAsync(Data)`. No Peek, no manual `Resolve`. The
courier hands the whole Data on; the value resolves itself at the door. `mock/intercept`'s
duplicate template block deletes the same way. Delete dead `AsCanonical`.

### One write verb: `Output` (decision #11)

Today `item.Output(writer, mode, ctx)` (async, view-aware) has a base default that bounces to
`item.Write(writer)` (sync leaf primitive). ~24 leaves override `Write` and rely on that bounce.
That bounce is the context switch — `v.Output(...)` lands on the base and jumps to `text.Write(...)`.

Collapse: every leaf overrides `Output` directly; the base `Output` default throws (the loud
"not a leaf" that lived on `Write`); `Write` is deleted.

```csharp
// base — the loud default, moved off Write
public virtual ValueTask Output(IWriter w, View mode, ctx)
    => throw new NotSupportedException($"{GetType().Name} has no wire form — not a leaf value.");

// text — was `override void Write(w) => w.String(_value);`
public override ValueTask Output(IWriter w, View mode, ctx) { w.String(_value); return default; }
```

- The STJ `Write(Utf8JsonWriter, value, options)` in the `*/Json.cs` files is a **different**
  method (`System.Text.Json`'s `JsonConverter` API) — untouched, no collision.
- Only genuine sync callers: the `signature` field composer (already `async` — awaits children's
  `Output`) and three static serializer helpers (`path`/`directory`/`permission` `Default.Write`),
  which sit directly above the already-async `IOutput.Output` boundary — a tiny, terminating
  async ripple.
- A leaf carrying an unused `mode`/`ctx` is correct: under decision #2 the Store/Out decision is
  made in `Data.Output` before the leaf is reached, so a leaf always emits its raw form regardless
  of view. Cost per leaf: `return default;` (allocation-free `ValueTask`).

### Return path — `%!data%` flows whole

```csharp
if (result.Success)
    await context.Variable.Set("!data", result);   // whole Data — no Peek, no Value(), no HasVariableReference, no snapshot
```

The loop-fear that justified the old eager snapshot is handled lazily at the read-time
`_resolveDepth` guard (decision #4). Delete the `!data` DynamicData (`context/this.cs:182`) — it
aliases a never-set `data` var and `.Peek()`s the box; `%!data%` is born by the first action's
`Set`. Name stays `!data` (the `!` infra prefix keeps it out of the user's own `%data%`);
`%!data%` before the first action is absent → throws (decision #1) — there is nothing before the
first action.

### `file/read` — stamp `template="plang"` (decision #10)

A file read that should interpolate produces a `source` stamped `template="plang"` (the
builder-recorded intent, like every other template); resolution flows through `source.Value` — the
one seam. Delete the `ResolveVariables` parameter and the manual `Resolve(content,
skipInfrastructure:true)`. A plain read (no stamp) stays a no-template source → `Cacheable` →
byte-exact passthrough.

**Security:** without `skipInfrastructure`, a `template="plang"` file resolves all refs including
`%!infra%`. This is safe because the interpolation is **authored in the goal, in plain sight** —
`- read file.txt and load vars, write to %content%` — which is what stamps `template="plang"`. The
trust anchor is the authored PLang, not the file's content; `%!infra%` resolving there is the
developer's stated intent. (Infra vars are runtime state — `%!app%`/`%!callStack%`/`%!data%` — not
the secrets/settings namespace.)

---

## Carrier collapse — demolition & leaf-trace (decision #8)

`variable.@this` becomes name-only; every value reference is a `source`. Traced from
`is variable.@this` / `Peek() is variable` / `HasVariableReference` / `IsVariable`.

### Dies outright

| site | what it is | disposition |
|---|---|---|
| `action/this.cs:285-288` | return-path snapshot | → `Set("!data", result)` whole (above) |
| `context/this.cs:182` | `!data` DynamicData | delete |
| `data/this.Output.cs:50` | `_item is variable.@this` resolve branch | deleted by decision #2 gate |
| `data.cs:566` (`AsCanonical`) | `if (_item is variable.@this v)` | dead method — delete whole |
| `data.cs:155` (`IsVariable`) | `_item is variable.@this` detector | delete — zero callers after collapse |
| `variable/list/this.cs:739` | interpolation engine `else if (Peek() is variable.@this)` | dies — a stored value is never a bare ref |

### Re-express on `source` (the real work — NOT delete)

| site | what it is | re-expression |
|---|---|---|
| `variable/list/this.cs:120-125` (`Set`) | `variable-ref-binds-instance` (currently C# reference semantics) | flips to Swift COW (decision #9) — `set %a% = %b%` shares the instance with a shared-flag, splits on first mutation |
| `condition/code/Default.cs:58` (`TolerateAbsentVariable`) | `Peek() is variable.@this` → tolerate absent ref | tolerate an absent **`source`** ref the same way |
| `builder/validateResponse.cs:174` | `Peek() is variable.@this \|\| HasVariableReference` | the `is variable` half → `source`; keep the template half |

### Stays (genuine NAME role — correct)

| site | why it stays |
|---|---|
| `data.cs:471` (`As<variable>`) | a name slot wants the reference ITSELF (its name), not its value |
| `type/this.cs:265-267, 287-290` | born-as-variable in `Create` — now fires ONLY for `type:variable` name slots |
| `variable/serializer/Reader.cs` | reads a name-slot off the wire → `new variable.@this(reader.String())` (decision #7) |
| `variable/set.cs:35,54` (`HasVariableReference`) | template-based check — survives; review under ref-detector consolidation |

### `Resolve` → constructor (decision #7)

Delete the static `variable.@this.Resolve` (a pure alias for `Convert(...).Peek()!` whose
`context` is wrapped-then-discarded). The name-grammar parse (`%x%` / `x` / `%!flag%` / `%x.age%`
/ `%x!cost%`→Name+Property / `%x!!cost%`→malformed) folds into `new variable.@this(string raw)`:

```csharp
public @this(string raw)                                 // 1-arg: NOW parses the name grammar
public @this(string name, string raw, bool wasWrapped)   // 3-arg: explicit parts, stays
public static @this Convert(object? v, string? kind, ctx) => ctx.Ok(new @this(v as string ?? v?.ToString() ?? ""));
// Reader: new variable.@this(reader.String())  — symmetric with text/datetime/number readers
// list.Resolve → Render.  static Resolve: DELETED.
```

Verified behavior-neutral: 3 production + ~19 test callers of the static, all become `new
variable.@this(raw)`; the 1-arg ctor stores verbatim today, and a grep for any 1-arg call with a
`%`/`!`/`.` argument returns empty — every existing call passes a clean bare name, for which the
parse is a no-op.

---

## Value semantics — the Swift rule + copy-on-write (decision #9)

> **`set %a% = %b%` gives `%a%` its own copy. Editing one never changes the other — ever.** One
> rule; no scalar-vs-collection exception, no alias-vs-snapshot exception.

This **reverses** the 2026-06-10 `data-value-model` ruling (list/dict as C# reference types, "COW
todo dead"). Reason: reference semantics is an aliasing footgun for the PLang audience, and the
current split (`set` rebinds / `add` mutates-in-place) is itself the confusion — two ops
disagreeing about identity. Under COW they agree.

- **Mechanism = copy-on-write.** `set` shares `%b%`'s storage and marks it shared (no eager copy);
  the first *in-place mutation* through either binding splits off a private copy, then mutates
  that. Scalars (immutable) never split. Coercion is orthogonal — `%b% as T` builds a new value,
  never shared. COW is a structural mechanism (shared-storage flag + split), not a Data-decompose.
- **Deep, all the way down.** A nested edit stays private too — `set %a%[0].name = "Bob"` must not
  touch `%b%`. The split propagates along the mutated path (list → touched dict), per-level on the
  write path, never an eager deep copy.
- **In-place mutators that become COW-aware** (check-shared → copy-then-mutate): `list.Add /
  Insert / RemoveAt / SetAt / Remove / Reverse` (`type/list/this.cs:272-336`), `dict.Set`
  (`type/dict/this.cs:259-271`). Functional ops that already return a new list (`unique` / `where`
  / `group` / `flatten` / `range` / `split`) are unaffected.

Worked example:
```plang
set %b% = ["apple"]
set %a% = %b%
add "banana" to %b%
write out %a%          # → ["apple"]   (Swift: %a% independent; C# today gives ["apple","banana"])
```

Bigger than the carrier collapse — its own pass. `flag-cost-never-justifies-decompose` still holds;
the `data-value-model` memory stays on the OLD reference semantics until this code lands.

---

## The flow — `- write out %x%`, end to end (target)

```
 PLANG      - write out %x%
 BUILD      compiler → action output.write; %x% is a value ref → source{ type:item, value:"%x%", template:plang }
 .pr        output.write { Data: <that source> }                       ← builder stamps it a reference
 LOAD       born source, lazy/unresolved; the Data box just holds it
 RUN        output.write.Run(): return Channel.WriteAsync(Data)        ← whole Data, courier never opens the box
 SERIALIZE  Channel → writer → Data.Output(writer, view=Out, ctx)
 GATE       view==Store || Cacheable ?  source.Cacheable=(template==null)=false → RESOLVE   ← Data owns the VIEW, asks nothing about type
 RESOLVE    v = await Data.Value() → source.Value: resolve "%x%" (unset → throw), coerce to item → x's value ("hello")
 WRITE      await v.Output(writer, Out) → text.Output: writer.String("hello")               ← one verb, no hop
 CHANNEL    Output channel → stdout / http body / …
```

Same door, other view: `Data.Output(writer, view=Store)` → gate takes the raw branch → the source
writes itself verbatim `"%x%"` (authored ref preserved for signing/round-trip). The **view** is
the only switch, and it lives in one place.

---

## Sequencing (each step a green, independently-verified commit)

0. **`Resolve` → `new variable.@this(raw)`** (decision #7) + `list.Resolve` → `Render`. Standalone,
   behavior-neutral.
1. **`source.Value` is the Model-B seam** — resolve `%ref%` once, then coerce to `T`
   (`%x% as number` resolves; fixes the per-type-reader gap).
2. **`Data.Output` gate on `!Cacheable`** (decision #2) — delete the `_item is variable` branch and
   `_outputDepth`; pass `mode` through. Behavior-neutral: verify full-suite failure counts
   identical pre/post (the `IName`-rename method).
3. **Collapse `output.write` + `mock/intercept` → `channel.write(data)`; delete dead `AsCanonical`.**
   The embedded + source-born bugs go green here.
4. **One write verb** (decision #11) — fold leaf `Write` into `Output`; delete `Write`; base default
   throws. Wide but behavior-neutral; verify serialization/signing suite.
5. **Carrier collapse** (decision #8) — `variable` name-only; return path → `Set("!data", result)`;
   delete the `!data` DynamicData; re-express instance-binding / condition-tolerance /
   build-validation on `source`; delete `skipInfrastructure`; `file/read` stamps `template="plang"`
   (decision #10).
6. **Value semantics — Swift COW** (decision #9) — its own pass: list/dict become value types with a
   shared-flag + per-level split; the mutators above become COW-aware.

Each step needs a full-suite baseline diff (Store/signing tests are the tripwire on every step).
`e8519aa60` already shipped a **minimal** stopgap (source exposes `Template`; `output.write` uses
`HasVariableReference`; decision #1 done) — steps 2–3 supersede its `output.write` block, step 5
supersedes its `skipInfrastructure` use.

---

## Sensitivity & risk

- **Store view is load-bearing for signing/hashing** (history: `DataHashMismatch`). The authored
  `%ref%` MUST round-trip verbatim under `View.Store`; every Output change leaves Store
  byte-identical. `json/writer.cs:BeginRecord` (the parallel STJ envelope) stays in sync.
- **One door, wide blast radius.** `Data.Output` is THE value-write door for both
  `application/plang` and the text serializer. A regression hits `.pr`, signatures, channel output,
  debug.
- **Out is now non-deterministic** (resolved against live variables); Store is raw. Every
  hash/signature MUST be computed on the Store view only — state it as an invariant + a test.
- **`Peek()` ≠ `Value()` type under Model B** — `%numb% as number` Peeks as `source`, Values as
  `number`. Audit `Peek() is` sites for the assumption that the peeked type equals the resolved type.

---

## Test surface

- **Acceptance:** `StartGoalTests` — the 3 embedded cases + the full-match case green.
- **Must stay green:** signing/round-trip (`Store` verbatim), `Wire`/`Data` born-native round-trip,
  channel/output suites, `condition` (tolerant absent), `mock/intercept`.
- **New tests:** (a) a source-born template resolves on Out; (b) a plain file-read source writes raw
  (passthrough); (c) `View.Store` preserves `%ref%` verbatim; (d) absent user ref → `VariableNotFound`;
  (e) `set %a% = %b%; add to %b%` leaves `%a%` unchanged (Swift COW); (f) deep COW — `set %a%[0].x`
  does not touch `%b%`.
- Guard with a `decisions.md` entry (`value-owns-its-output`, `variable-is-name-only`,
  `collections-are-value-types`).

---

## OBP validation pass (new/renamed surfaces)

| surface | verb+noun check | object-decomposition check |
|---|---|---|
| `item.Output` (renamed from `Write`) | one word, the O of I/O — clean | writes the whole item; no field extraction |
| `variable.list.Render` (renamed from `Resolve`) | one word — clean | renders a template string; owns the holes |
| `new variable.@this(raw)` | constructor, not a static verb — clean | parses its own raw form; no external parser |
| `source.Value` (the one seam) | established door name | resolves + coerces itself; no caller decomposition |
| `Data.Output` gate reads `_item.Cacheable` | property, not `Get*` | reads item STATE, not `.Value`; not a type-test |
| COW shared-flag + split | structural | value copies itself; no decompose |

No verb+noun compounds introduced; no value decomposed at a call site; every behavior lands on the
element that owns it.
