# Plan — Unify value resolution: the item owns its output (one door, not two)

**For:** architect · **Branch:** context-never-null · **Author:** coder
**Sensitivity:** HIGH — touches `Data.Output` (the single write door for BOTH the
application/plang wire and text serializers), signing/hashing (Store view), and every
channel write. A regression hits `.pr` persistence, signatures, and all output.

---

## 1. The problem in one sentence

Resolving a `%ref%` or a template happens **two ways**: on the READ path each value
resolves **itself** (its `Value(data)` door); on the WRITE path values write **raw** and
the resolution is **bolted on upstream** as special-cases. Special-cases are the red flag —
a value shape no guard names slips through.

### The asymmetry (verified)

| | READ (`Value` door) | WRITE (`Output`) |
|---|---|---|
| text | resolves template — `text/this.cs:72` | writes RAW — `text/this.cs:94` (`w.String(_value)`) |
| variable | resolves ref — `variable/this.cs:83` | writes RAW — `variable/this.cs:254`; **no `Output` override** |
| source | materializes — `item/source.cs:95` | writes RAW passthrough — `item/source.cs:175` |
| dict/list | deep-renders — `dict:370`/`list:560` | walks children structurally — `dict:163`/`list:202` |

Resolution on the write path is instead done in **three bolted-on places**:
- `Data.Output` (`data/this.Output.cs:50`) — `_item is variable.@this && mode != Store` → `Variable.Get` + recurse. **Full-match `%x%` resolution.**
- `output.write.Run` (`module/output/write.cs:21`) — `Peek() is text.@this { Template: not null }` → `Variable.Resolve`. **Embedded-template resolution.**
- `mock/intercept` (`module/mock/intercept.cs:128`) — same template block, duplicated.

**The code already describes the target design and then violates it.** The base
`item.Output` doc comment (`type/item/this.cs:~431`) says: *"Containers and references
override: dict/list walk + await children, **variable resolves itself**, clr reflects its
host."* But `variable.@this` has **no `Output` override** — it writes raw and `Data.Output`
resolves it externally. The comment is the plan; the code is the smell.

### The bug this produces

A string value borns as `item.source` (the one source-maker, `type/this.cs:276`). A
source is neither `text.@this{Template}` nor `variable.@this`, so **both write-path guards
miss it** → a template born as a source is written **literally**. This is the failing
`StartGoalTests` embedded cases (`"NewVar: %newVarName%"`, `"Hello %user%!"`,
`"Value: %unknown%"`).

---

## 2. Target invariant

> **A value writes itself. `Data` never asks what the item is.**
>
> The governing rule: **`Data.Output` contains ZERO `_item is <Type>` questions.** Its body
> reduces to `await _item.Output(writer, mode, context)` (plus the mode/writer-level schema
> envelope, which is about the *view and writer*, not the item's type). `Data` holds a
> reference and says "output yourself"; the item — variable, text, number, dict — owns
> everything about how it renders, including `View.Store` (verbatim) vs `View.Out`
> (resolved). A variable jumps to its binding, a text fills its holes, a number prints —
> none of that is `Data`'s business.
>
> Consequences: resolution lives ONLY in each item's own door. `output.write` collapses to
> `channel.write(data)`. A value is resolved because it *is* that shape (reached by
> reference), never because an upstream guard type-tested it. **Any `is <ConcreteType>` on
> `_item` in a relay layer (`Data`, the courier, the wire) is the smell we are removing —
> not relocating.**

---

## 3. Duplications to collapse (verified, with sites)

1. **Full-match `%x%` Get+recurse — 4 live copies + 1 dead.** `text.Value:79`,
   `variable.Value:97`, `data.Output:60`, `condition/code/Default.cs:59` (tolerant), and
   **dead `data.AsCanonical:568`** (zero call sites — delete). Each re-implements
   `Get` + `IsInitialized` guard + recurse, and they **diverge on absent-handling**
   (throw / soft-Fail / null / recurse) — a real correctness risk.
2. **Embedded interpolation — one engine, five gates, three tests.** Engine:
   `variable/list/this.cs:704` (`Resolve`). Gates with inconsistent "is-template?" tests
   and inconsistent `skipInfrastructure`: `output/write.cs:21`, `mock/intercept.cs:128`
   (`text{Template}`), `GoalCall.cs:208` (`Name.Contains('%')`), `file/read.cs:91`
   (unconditional), `text/this.cs:74` (`Template==null`).
3. **`%…%` grammar hand-encoded 5+ times** (`FullVarMatchRegex data:436`, `RefRx
   text:195`, two inline in `variable/list:717,745`, `VarRefPattern debug:567`) **plus two
   ad-hoc `StartsWith("%")&&EndsWith("%")`** in `builder/code/Default.cs:942,961`.
4. **"Is this a ref?" detector in 4 forms** — `HasVariableReference` (`Template!=null`,
   `data:163`), `IsVariable` (`_item is variable`, `data:155`), raw `Peek() is variable`
   (many), `text.HasVariable` (content scan). Sites mix them; some use two together
   (`Peek() is variable || HasVariableReference`), implying neither alone is trusted.
5. **dict/list container deep-render duplicated** (`dict:370` / `list:560` — same
   copy-on-write + probe-Data + IsFinal algorithm; dict has a cycle guard, list doesn't).
6. **`json/writer.cs:BeginRecord`** re-implements the `Data.Output` envelope for the STJ
   nested-record path — kept in sync by hand.
7. **Naming collision:** `variable.@this.Resolve` (a FACTORY that builds the ref object)
   vs `variable.list.@this.Resolve` (the interpolation ENGINE). Opposite operations, same
   name.

---

## 4. The changes, per layer

**Move resolution INTO the items; delete the bolted-on guards.**

- **`variable.@this`** — add an `Output` override: `mode != Store` → `Get(Name)` then
  `resolved.Output(writer, mode, context)`; else `Write` the raw `%ref%`. (This is the
  branch lifted out of `Data.Output`.) Carries the cycle guard.
- **`text.@this`** — add an `Output` override: `Template != null && mode != Store` →
  render (its existing partial-interpolation path) then write; else `Write` raw.
- **`item.source`** — the born form of a string. Either (a) expose `_template` on the base
  `Template` property so `HasVariableReference` sees it and `source.Output` resolves when
  `mode != Store` (materialize → the materialized item's `Output`), else raw passthrough;
  or (b) `source.Output` checks `Mint().Template`. **A no-template source always writes raw
  (file-read passthrough preserved).**
- **`Data.Output`** — DELETE the `_item is variable.@this` branch. After this, the method
  must contain **zero `_item is <Type>` questions** — it delegates via
  `await _item.Output(writer, mode, context)`. Keep only the mode/writer-level schema
  envelope (Store name, `@schema`, type entity) — that is about the view and writer, not
  the item's type. This is the acceptance test for the whole change: grep `Data.Output` for
  `_item is` and expect nothing.
- **`output.write.Run`** — DELETE the template block → `return await Channel.WriteAsync(Data)`.
- **`mock/intercept`** — same block → route through the door.
- **DELETE dead `AsCanonical`.**
- **Born contract (prerequisite):** full-match `%x%` → `variable`; embedded `"hi %x%"` →
  `text` + template flag (the builder stamps it — decision `builder-detects-var-at-build`).
  Once this holds, `text.Value` NEVER sees a full-match → its full-match branch
  (`text:77-88`) becomes dead and is removed, killing duplication #1's text copy.

---

## 5. Decisions needed (Ingi / architect) — do NOT code before these

1. **Absent ref on Out-output — loud or soft?** `variable.Value` THROWS
   `VariableNotFoundException`; `text.Value` soft-`Fail`s + returns `Absent`; `Data.Output`
   THROWS. Unifying forces one rule. *Coder's lean:* a **full** reference to an unset var is
   a bug → throw (`VariableNotFound`); a **partial** interpolation renders the unset hole as
   empty (matches the existing test expectation `"Value: "` for `%unknown%`).
2. **Passthrough vs resolve-through-door for a raw file read.** `output.write`'s comment
   worries that opening the door reserializes a structured file read. Under the new model a
   **no-template source writes raw (passthrough)**; only a template/ref source resolves.
   Confirm this is the intended boundary: *template/ref ⟹ resolve, otherwise verbatim.*
3. **Cycle-guard placement.** Today there are two independent depth guards
   (`variable._resolveDepth`, `Data._outputDepth`, both 50) plus none on text. After the
   move, one guard on the shared resolve path. Confirm it lives on `variable.Output` /
   `variable.Value`.
4. **`skipInfrastructure` rule.** `output/write` and `file/read` pass it; `mock/intercept`
   and `GoalCall` don't. Decide the uniform rule for `%!infra%` refs on user-facing output.
5. **Scope of THIS pass.** Minimum = items #4-section changes (fixes the bug, removes the
   red-flag special-cases). Optional now vs deferred: consolidating the grammar detectors
   (#3), the ref-detector forms (#4), dict/list render (#5), the `Resolve` naming (#7).

---

## 6. Sensitivity & risk

- **Store view is load-bearing for signing/hashing.** The authored `%ref%` MUST round-trip
  verbatim under `View.Store` (history: `DataHashMismatch`, `data-serialize-cleanup`). Every
  Output change must leave Store behavior byte-identical. `json/writer.cs:BeginRecord` (the
  parallel STJ envelope) must stay in sync.
- **One door, wide blast radius.** `Data.Output` is THE value-write door for both the
  application/plang wire and the text serializer (verified: `serializer/plang/this.cs:179`
  and `serializer/Text.cs:43` both call it). A regression hits `.pr` persistence, signatures,
  channel output, and debug.
- **Async + cyclic.** Resolution is async and can cycle (`a=%b%, b=%a%`); the guard must
  survive the move or an SO replaces a typed error.
- **Container canonicalization.** dict/list render feeds signing canonicalization — do NOT
  change render *semantics*, only *where* resolution is triggered.

---

## 7. Test surface

- **Acceptance:** `StartGoalTests` — the 3 embedded cases + the full-match case go green.
- **Must stay green:** signing/round-trip (`Store` verbatim), `Wire`/`Data` born-native
  round-trip, channel/output suites, `condition` (tolerant absent), `mock/intercept`.
- **New tests to add:** (a) a source-born template resolves on Out-output; (b) a plain
  file-read source writes raw (passthrough) on output; (c) `View.Store` preserves `%ref%`
  and the template string verbatim; (d) absent full ref on output → `VariableNotFound`
  (per decision #1).
- Guard this with a `decisions.md` entry (`output-resolves-via-item-door`) once the shape
  is agreed.

---

## 8. Sequencing (each step a green, independently-verified commit)

1. **Born contract** — full-match `%x%` → variable, embedded → text+template (builder side +
   confirm the two doors are the sole resolvers). Removes `text.Value` full-match branch.
2. **`variable.Output` override + delete `Data.Output` variable-branch.** Must be
   behavior-neutral — verify full-suite failure counts identical pre/post (same method used
   for the `IName` rename).
3. **`text.Output` override + `source` template exposure.** Fixes the embedded bug.
4. **Delete `output.write` block + `mock/intercept` block → `channel.write(data)`; delete
   dead `AsCanonical`.**
5. **(optional follow-up)** consolidate grammar detectors, ref-detector forms, dict/list
   render, and the `Resolve` naming collision.

Steps 2-4 each need a full-suite baseline diff (WIP branch has ~127 pre-existing reds;
only the delta matters). Store/signing tests are the tripwire on every step.
