# Coder review — typed-value-model plan + 7 stages (v2)

Read `plan.md`, all seven stage files, `plan/test-strategy.md`, and `plan/test-coverage.md`,
then grounded every load-bearing claim against the real code on `compare-redesign`. This is
peer feedback before I pick up Stage 1 — **not a blocker**. My v1 review was against the
*raw-CLR* draft you've since abandoned, so this is effectively a fresh read of the re-pivoted
(typed-value) spine.

## Verdict

**Build it.** The spine is right and it works *with* the grain of the code, not against it.
The foundations the model leans on already exist and are real:

- `item.@this.Write(IWriter)` (`PLang/app/type/item/this.cs:71`) — the value already owns its
  wire shape, so "write-out is type-owned serialization" (rule 8) is mostly *present*, not net-new.
- The lazy `_raw` + sync `Materialize()` + `FromRaw` machinery (`PLang/app/data/this.cs:36,316,284`)
  is exactly the rung the door formalizes.
- `Compare.Order` returns a raw `int` and `throw`s `NotOrderableException`
  (`PLang/app/data/Compare.cs:34`); `Operator.NormalizeTypes` is a symmetric coercion pass — the
  sign-bearing, coercion-in-a-separate-pass shape the redesign correctly removes.
- The typed-model pivot also *dissolved* my v1 hazard #4 (throw-on-`GetHashCode`/`Equals` colliding
  with live keying): the value slot already holds the wrapper today, so there's no "flip to raw" to
  sequence per-type. Good call — worth one line in Stage 6 noting the pivot removed that hazard.

What follows is where the **re-carve** left stages thinner than the code they have to land on.
Three of these are demolition the additive language hides; one is a v1 win that regressed; one is
an invariant the plan asserts that today's code doesn't meet.

---

## 1. The async value *source* regressed from a named sub-stage back to one bullet (v1 win lost)

My v1 review won an explicit **Stage 2 Part A — "the lazy value source"** (you recorded it in
`summary.md`: *"one source, one chain reference → read (async) → raw → parse (sync) → value"*).
The re-carve to the typed model dropped it. Current `stage-2-value-door.md` compresses the whole
thing into two bullets: *"`_raw` removed … `Materialize` becomes binary/text → parsed"* and the
`ValueTask Value()` door.

The gap is the same one as v1, and it's the single largest chunk of net-new work on the branch:

- **Today there is no async I/O read path on Data.** `Materialize()` is **sync** (`this.cs:316`) —
  it parses an *already-in-memory* `_raw`. The only async content load is the **per-type**
  `ILoadable.LoadAsync()` (`ILoadable.cs`), implemented by **exactly one type** (`image`).
- So `ValueTask<object?> Value()` conflates **two different async needs** that the stage should
  separate explicitly:
  - **(a) reading I/O** — fetch a file/url's bytes. *Genuinely async, net-new, ~nothing exists.*
  - **(b) parsing in-memory raw** — bytes/string → dict/list. *Sync today, and navigation depends
    on it staying reachable synchronously.*
- `GetChildValue` (`this.Navigation.cs:238`) is a **sync** method that calls `ForceMaterialize()`
  /reads `Value` to parse-on-touch. If the door is the *only* materialize seam and it's async, sync
  navigation loses its parse path. The model only needs the door async for (a); (b) must stay a sync
  parse. The stage doesn't draw that line.

**Ask:** re-instate the explicit source sub-stage (or a named Stage 2 section) that (1) defines the
async **read** abstraction `ILoadable` folds into, and (2) states that **parse stays sync** and
remains reachable off the held value, so navigation isn't forced async. As written, Stage 2 reads as
if `await _source.Read()` exists — it doesn't.

## 2. The `!` plane redefinition collides with the existing `!` = Data-infrastructure meaning

This is the biggest **unspecified mechanism** in the plan. Stage 2/rule 3 define `!` as *"the value's
typed property plane"* — `%text!length%`, `%file!size%`, `%list!count%`. But `!` **already exists**
with a *different* owner:

`GetInfrastructureValue` (`this.Navigation.cs:356`) resolves `!` against **Data's own
infrastructure** — Properties first, then reflection over `Data` + its subclass (`Name`, `Error`,
`Success`, `Type`, `Llm`, …). Live usages depend on that meaning: `%result!Error%`, `%result!Llm%`,
`%x!cost%` (a `Properties` key), `%!data.branchIndex%`.

`%text!length%` does **not** resolve today: `length` lives on the `text` **value wrapper**
(`text/this.cs:74`), not on `Data` or its subclass — and `GetInfrastructureValue` never reflects the
*value*. So the plan's `!` is a genuine **change of owner** (Data-infra → the value's type surface),
not just "fill in the resolver." The stages never say how the two coexist:

- Where do `Error`/`Success`/the `Properties` bag live once `!` means "the value's properties"?
  Are they still `!`, or do they move? (`%x!type%` happens to work either way — `Type` is Data-infra
  *and* conceptually the value's type — which masks the collision.)
- The plan calls `!` *"not new syntax — it already exists in the grammar."* True for the **sigil**,
  but its **meaning** is being repointed, and ~real call sites ride the old meaning.

**Ask:** one stage (likely 2, maybe its own) must specify the coexistence: does `!` dispatch to the
value's type surface *first* and fall back to Data-infra (Error/Success/Properties), or is there a
split? This needs to be pinned before the resolver is touched, or the migration has no contract.

## 3. `path` already holds content — the "path = location, file = content" split is demolition, not addition

Stage 3 reads additively ("`file`/`directory`/`url` are **new** types"). But the split it requires is
subtractive on `path`:

- `path.@this` today carries `public object? Content` (`path/this.cs:169`) and `public string? Source`
  (`:173`), and `ToString()` returns **`Content?.ToString()` first** (`:177-185`). So today a `path`
  *is* the content-bearing thing after `file.read` — exactly what rule 3 says it must **not** be
  (*"a `path` serializes as its location string — one face, no content"*).
- Standing up `file` as "the content-bearing `path` subtype" therefore means **removing** `Content`/
  `Source` from `path` and moving them to `file`, then repointing every consumer (9 `.Content` sites
  in path/file/read code, plus `ToString`'s content-first branch). `image` already models the target
  shape (`image` holds bytes + a `Path` facet) — good precedent, but the base-class surgery is real.

**Ask:** Stage 3 should name this as demolition: "`path` loses `Content`/`Source`; they become the
content facet on `file`; `path.ToString` becomes location-only." Otherwise the first thing the
implementer hits is "wait, `path` already has content" and the stage looks wrong.

## 4. `read` returns lazy *content* today, not a `file` reference — that inversion needs a migration note

`file.read` (`file/read.cs:29-52`) returns a **lazy `Data` whose value is the content** (string/dict/
image/bytes), stamped with `{type, kind}` from MIME. It does **not** return a `path`/`file` handle
(except the `image` branch, which returns an `image` carrying `Path`). The plan inverts this: `read X`
→ a **stable `file`** that *holds* lazy content (rule 6/Stage 3).

That's a real semantic shift for the common pattern `set %c% = read file.txt` → use `%c%` as content.
Under the plan `%c%` is now a `file`, and content comes via `.`-forwards-to-content / `!content`. The
integration cuts cover `write out %file%` and `%file.field%`, but the **bare scalar** case
(`%c%` interpolated as a string after `read`) must stay green via the `.`/scalar → content forward.

**Ask:** Stage 3 (or a test cut) should state the bare-scalar contract explicitly: `read` returns a
`file`, and `%file%` used as a scalar/`write out` still yields **content**, so existing `read`-then-use
goals don't silently start emitting a location string.

## 5. `item.ToRaw()` is load-bearing in `Data.Type`, not just Pile-2 decompose sugar

Stage 6 deletes "generic `item.ToRaw()`" and frames the fallout as ~22–30 Pile-2 decompose sites that
become typed-method calls. But `ToRaw()` has a **core** consumer that isn't a decompose site:

`Data.Type` getter (`this.cs:390`) derives the CLR mate of a born-native leaf via
`leaf.ToRaw() is { } rawLeaf ? rawLeaf.GetType()` — that's how `number`/`text`/`bool` report their
precise CLR type (and the `number` kind tower). Of the 23 `.ToRaw()` call sites, this one drives type
derivation. Delete `ToRaw` and you must replace that derivation (a leaf already knows its CLR type
some other way, or `Type` reads it directly).

**Ask:** Stage 6 should call out `Data.Type`'s `ToRaw` use as a **named** conversion (not a grep-list
entry), since getting it wrong silently corrupts every born-native value's reported type.

## 6. Stage 7's "each step is local" undersells 51 interior path-math sites

The gate's scope line ("public `item`-subtype member returning CLR → error; private/internal
untouched") is the right boundary. The optimism is in *"each step is local (one member → its PLang
type)."* For `path` specifically it isn't:

- 51 sites read `.Relative` / `.Extension` / `.Absolute` and do **string math** on the result
  (`Relative.StartsWith(root)`, `Extension` → MIME, `Absolute` into `System.IO` post-AuthGate). These
  are **interior consumers**, not gated-interop edges — flipping `public string Extension` → `text`/
  `path` ripples into all 51, and they need the raw string to do their math.
- That sits awkwardly with the model's "raw survives only at the interop inch": interior path math
  isn't interop, but it genuinely needs raw. The clean resolutions are (a) make those properties
  `internal` raw (out of the public-only gate) and add a *public* PLang-typed sibling for the `!`
  surface, or (b) move the math onto `path`. Either is fine — but Stage 7 should **name** how the 51
  interior string-math consumers are handled, because "local, member-by-member" hides them.

## 7. The "door always returns a typed `item`, never raw CLR" invariant isn't true today

Rule 1 / Stage 2 assert the value slot always holds a PLang typed value (`set %x%=5` → `number`).
That's true for **born-native literals**, but `_value` legitimately holds **raw CLR** in live paths:

- a **`string`** for every `%var%` reference and partial interpolation (`VarString => _value as string`,
  `this.cs:146`),
- raw **`List<object?>` / `Dictionary<string,object?>`** containers off JSON ingestion (the `Walk*`
  paths, `EnumerateItems` `this.cs:539-553`).

So `Value()` *cannot* promise "always an `item` subtype" without Stage 2 also owning the normalization
of var-ref strings and raw containers into typed values — which is a non-trivial scope the stage
doesn't mention. Either (a) scope the promise to "returns the typed value when the slot is typed;
var-refs/containers normalize at the door," or (b) make that normalization an explicit Stage 2
deliverable. As written it reads as already-true and isn't.

---

## Smaller / confirmations

- **2–6 as one green unit** (my v1 #3) carried correctly into the plan — good. Worth keeping the
  green gate at the 2→6 boundary explicit in the stage headers (it's in `plan.md` but easy to miss).
- **Membership never errors** (my v1 #5) is pinned cleanly in Stage 1's table and Stage 6 — no notes.
- **`Peek`/`Diff` renames** are clean: `ScalarValue` (`this.cs:247`) → `Peek`, golden-diff
  `this.Compare.cs` → `Diff` (~14 test sites, no production callers). Confirmed.
- **11 types implement `IEquatableValue`/`IOrderableValue`** (binary/bool/choice/date/datetime/dict/
  duration/list/null/text/time). Stage 4's "prove text+number+cross-pair then replicate" is the right
  order; just note the replicate list is 11, and `item` itself must **not** implement the unified
  interface (it deliberately doesn't today — `item/this.cs:23-25` — so `dict : item` doesn't inherit
  an order it can't honor).

## Bottom line

Nothing here says *don't build it*. The model is coherent and the foundations are real. The four
things I'd want fixed in the stages **before** implementation, in order: **(1)** re-stage the async
source + the async-read/sync-parse split; **(2)** pin the `!` plane coexistence with Data-infra;
**(3)** mark the `path` Content/Source removal as demolition in Stage 3; **(5)** name `Data.Type`'s
`ToRaw` use in Stage 6. **(4)/(6)/(7)** are "add a sentence so the implementer isn't surprised."
</content>
</invoke>
