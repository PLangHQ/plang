# Stage 5 shape — the builder offers kinds (ruling 10)

Trace of how the decider offers **types** today, what it takes to offer each type **with its kinds under it**
(`number: int, long, double, decimal`) and to ask a list **"a list of what?"**, and which parts are the builder's
(teaching) vs the coder's (runtime, locked-core). **No change made — shape first, per the architect.**

## The headline

Stage 5 is **not** a teaching-only change. The offered set and the Type answer are produced and parsed in **C#**, in
**locked-core** areas (`app/type/list`, `app/goal/step/pick`, `app/goal/step/action/formal`). A kind is simply not
reachable until that seam carries kinds — the decider picks from a closed `criteria`, and today that set is a flat list
of top-level type **names** with no kind in it (baseline-kinds.md: "a kind or an element type is simply not reachable").
So stage 5 needs a **runtime touch (the coder's, red-first test)**; the builder's teaching (prompt C's Types section,
the type page, the `ask:` wording) renders what the seam exposes and re-pins the goldens, but can't conjure kinds alone.

## How a type is offered today (the flow)

1. **The Type question** is not a dedicated kind — it is a `Kind.Option` stage-2 question about **`variable.set`'s `Type`
   property** (and any action property whose notes carry `ask:`). Its wording lives in teaching:
   `os/system/modules/variable/set.notes.md:3` (`Type — … · ask: which type does the step coerce the value to …?`).
2. **The offered set** = `property.Type.Offers(step)`. For the Type property the property's type is the `type` registry,
   whose `Offers` (`PLang/app/type/list/Registry.cs:60-64`) returns **every non-internal type by its `Name`** as a flat
   `List<text>` — names only, no kinds, no generics. `PickOptionTests.ATypeOffersThePlangTypeNames` pins this in-code.
3. **The question** (`PLang/app/goal/step/pick/list/this.cs:584-595`) wraps those names as
   `question.Values` — an `IReadOnlyList<string>` — plus `"none"`. `question.Values` is a flat string list
   (`pick/question/this.cs:52-55`); it cannot carry structure.
4. **The render** (`os/system/builder/llm/templates/decider2.template:52-62`, the `Option` case) writes a **flat choice**:
   `"criteria": { "number": null, "text": null, … }`. The model picks one string.
5. **The answer** (one picked string) is folded into the action's formal line and read by
   `PLang/app/goal/step/action/formal/reader.cs` (via `pick/list/this.cs:349`); the Type is stored flat, `{ "name": "number" }`.
6. **Prompt C's Types section** (`os/system/builder/llm/templates/properties.template:74-93`) lists each type's
   **name + Description + a choice's Values + Example — no kinds** (`:86`).
7. **The type page** (`docs/templates/type.template:11-37`) shows name/description/guide/members — **no kinds**.
8. **Kinds already exist and navigate** as `kind<T>`: `app.type["number"].kind.list` works
   (`PLang/app/type/kind/this.Generic.cs`, `kind/empty/this.cs:115-117`; number's 15 kinds under
   `PLang/app/type/item/number/kind/<name>/`, e.g. `decimal/this.cs: base("decimal")`). The data is there; the decider
   just isn't shown it and can't express it.

## The two seams that must change (both C#, both locked-core → coder)

| seam | file | today | stage 5 needs |
|---|---|---|---|
| **the offered set** | `app/type/list/Registry.cs:60-64` (`type.Offers`) | flat `List<text>` of type names | each offered type carries its `kind.list` (and list/dict carry "accepts any type") |
| **the question + render** | `app/goal/step/pick/` (`question.Values : IReadOnlyList<string>`, `list/this.cs:584-595`) + `decider2.template:52-62` | flat `string` choice | express kinds under a type, and for `list`/`dict` an element follow-up ("a list of what?") |
| **the answer** | `app/goal/step/action/formal/reader.cs` (via `pick/list/this.cs:349`) | reads one name → `{name}` | reads nested `{name, kind}` and `{list, kind: <Type>}` (stage-6 F8.1 already touches this reader) |

`app/type/list` and `app/type/kind` are the **locked type system**; `app/goal/step/pick` and `action/formal` are core
runtime. None is mine. The builder teaching (points 1, 6, 7 above) is mine.

## The design fork (for the architect / Ingi) — how to offer kinds + the element question

The runtime shape is the coder's to trace, but the **intent** choice is a decision:

- **S1 — structured answer + element follow-up (faithful to ruling 10).** The criteria presents each type with its kinds
  nested (`number: int | long | double | decimal`); the answer is `{name, kind?}`; picking `list`/`dict` fires a second
  question **"a list of what?"** offering the element types, recursively (`{list, kind: {list, kind: path}}` nests). This
  is exactly ruling 10's words. Cost: a kind-aware offered set, a question that nests, and a nested-answer reader — the
  most C#, in three locked-core files.
- **S2 — flattened offer strings (smaller, less faithful).** `Offers` additionally yields kind-qualified offer strings;
  the model picks one string that a reader maps to `{name, kind}`. Avoids a new question flow, but the generic
  ("a list of what?") has no clean home — it degrades to combinatorial `list of <T>` strings or a second ad-hoc pick.

**My recommendation: S1.** It is what ruling 10 asks for, it keeps the element question first-class (the flaky
`list<path>` baseline, 2/5 vs 3/5, is precisely the case S2 handles worst), and the nested reader is already on stage 6's
path. I'll leave the C# shape to the coder's trace.

## What is mine (teaching) and the goldens that move (teaching-edit rule)

Once the seam carries kinds, these are the builder-side edits, each re-pinning on the same commit:

- **`decider2.template`** (the Option render) — render kinds under a type / the element question. → moves **pick_golden.json**
  (PickListTests) and the in-code asserts in **PickOptionTests** (`ATypeOffersThePlangTypeNames` etc.).
- **`os/system/modules/variable/set.notes.md`** (the `ask:` wording) — teach the kind/element choice. → teaching edit:
  re-run **PickListTests, PickOptionTests, ConfirmTemplateTests, LineTwinTests** and re-pin whichever move
  (pick_golden.json for certain; confirm_golden/line_golden only if they carry a Type question — to confirm at build).
- **`properties.template:74-93`** (prompt C Types section) — show each type's kinds (`number: int, long, double, decimal`).
  → moves **pick_golden.json**'s `user` field (prompt C is pinned there: PickListTests `ThePromptCUserMessage_IsThePinnedOne`).
- **`docs/templates/type.template`** (the type page) — show a type's kinds. → moves the ` ```markdown ` golden block in
  **`Documentation/v0.2/type-reference-generation.md`** (TypePageTests). ModulePageTests unaffected unless a module page changes.
- Per-type teaching under **`os/system/type/<t>/`** (description/guide) may gain a line naming the kinds, if we teach them there.

All of the above is the teaching-edit rule: the decider fixtures + page goldens re-run and re-pin **in the same commit**,
diff bounded to my edits, each moved pin read before commit.

## Paths vs the coder

The coder is on stage-4 json producers (url/render, runtime). Stage 5's **runtime** seams (`Registry.cs` Offers,
`pick/`, `formal/reader.cs`) are different files but the **same branch and the same locked-core neighbourhood** — the
coder will hold the C# for stage 5 too (red-first test, routed via the architect). My work is the teaching + goldens,
downstream of that C#, and does not touch the json producers. No file crossing expected; the dependency is sequential
(C# seam first, then my teaching re-pins).

## Surface addendum (after the architect confirmed S1 and assigned me the C# seams)

The architect gated on the before/after of `type.Offers` and the question's `Values`, plus the expected surface-golden
diff. Traced against `PLang.Tests/Wire/App/Surface/surface_golden.txt`:

- **The registry's `Offers` is `internal override`** (`Registry.cs:62`) — the surface golden's `app.type.list.this`
  block (golden 3149–3171) does **not** list it (internal members aren't pinned). Changing WHAT it returns does not move
  the golden.
- **`question.@this.Values`** lives in `app.goal.step.pick.question` — build-time-only, "never stored in the .pr", and
  **not in the surface golden at all** (no `pick.question` line). Changing it moves no golden.
- **The base `type.@this.Offers` virtual** (golden 3320) and `type<T,L>.Offers` (3330) are pinned — but their
  **signature is unchanged** (`ValueTask<IReadOnlyList<item.@this>>`), so they don't move.
- **`list.kind.element`** already exists as a `kind<list>` (golden 1875) — the element-carrying mechanism is in place.

**So: no surface-golden diff, no locked-surface addition.** Stage 5 is a behaviour change inside locked core (what the
registry offers) plus build-time-internal changes (the question, the render, the follow-up) — within 749/774.

### Before / after signatures

- `type.list.@this.Offers` (internal override, `Registry.cs:60-64`):
  - **before:** returns each non-internal type's **name** wrapped as `text.@this` — `… .Select(t => t.Name) … new text.@this(name)`.
  - **after:** returns the **types themselves** (each a `type.@this`, which IS an `item.@this`, carrying its `.kind.list`);
    signature `ValueTask<IReadOnlyList<item.@this>>` **unchanged**, elements changed from name-texts to types.
- `question.@this.Values` (`pick/question/this.cs:55`, build-time, unpinned):
  - **before:** `public IReadOnlyList<string> Values { get; init; } = [];`
  - **after:** `public IReadOnlyList<app.type.item.@this> Values { get; init; } = [];` — the offered items whole (kinds ride
    on the types); the render does `Shown()` and nests a type's kinds. Non-type Option offers (separator, a choice) still
    render byte-identically.

### The answer rides as the type's real face (not S2 masking)

The formal reader already writes/reads `name<kind>` faces (`action/formal/reader.cs:282-283` `Face`; `list<action>` at
`:221`) and the `.pr` stores `{name, kind}`. The decider's Type answer is that same real face — `number<decimal>`,
`list<path>` — parsed by the existing reader; the only reader change is **nesting** (`list<list<path>>`), as the architect
scoped ("reading the structured Type answer, nothing else there"). This is the type's own written form, not an invented
token mapped back.

### The element follow-up

A `list`/`dict` Type answer triggers a conditional follow-up that re-offers the types (reusing `list.kind.element`), giving
`{list, kind: path}` and nesting `{list, kind: {list, kind: path}}`. One-shot richer schema vs a follow-up decider round
is a build-time-internal choice (Decide.goal + pick); **if it turns out to need a new public/virtual member on a locked
type, I stop and bring it to the architect** — the plan is to use only existing members (`type.kind.list`,
`list.kind.element`).

### Goldens that move (teaching-edit rule — NOT the surface golden)

`pick_golden.json` (decider2.template + `set.notes.md` + prompt C in `properties.template`) and the type-page markdown
block in `Documentation/v0.2/type-reference-generation.md` (if the page shows kinds). Re-run PickListTests, PickOptionTests
(in-code asserts), ConfirmTemplateTests, LineTwinTests + TypePageTests; re-pin the movers in the same commit; read each diff.

## DONE WHEN

The surface answer is with the architect (no golden move, no locked addition — confirmed-or-to-Ingi is the architect's
call). Then: tests-first (red for their own reason), build the seams + teaching, re-pin, measure the five sentences 5/5
with the guards.
