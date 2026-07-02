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

**Do NOT add per-type `Output` resolution.** (First-draft instinct — see §9.1: it just
re-implements the `Value` door inside `Output`, the same "two ways" smell.) The minimal
form is **`Output = Value()-then-write`, owned once by `Data.Output`:**

```
Data.Output(writer, mode, context):
    … envelope (Store name / @schema / type — about VIEW+WRITER, not the item's type) …
    if (mode == Store)  await _item.Output(writer, Store, context);               // verbatim raw
    else                await (await this.Value()).Output(writer, Store, context); // resolve via the DOOR, then write raw
```

- **`Data.Output`** — DELETE the `_item is variable.@this` branch. After this the method has
  **zero `_item is <Type>` questions**. It owns the Store/Out switch above; the item only
  ever provides `Write` (raw form) + `Value` (resolved form), both of which already exist.
  Acceptance test: grep `Data.Output` for `_item is` → nothing.
- **`Value` door is the single resolution seam** (already present): `text.Value` (template),
  `source.Value` (materialize + resolve — the Model-B seam, §decision-6), `variable.Value`
  (untyped ref), `dict/list.Value` (deep render), scalars (`this`). **No new `Output`
  overrides on any type** — `text.Output`/`source.Output`/`variable.Output` should NOT be
  written; if you find yourself adding one, resolution has leaked out of the door again.
- **`source.Value`** — the Model-B work: resolve the `%ref%` ONCE (via the store engine),
  then `T.Create(resolved)`. This is where a no-template source must still pass through raw
  (see §9.3 — the passthrough fork; may forbid the naive `Value()-then-write` for a plain
  file read).
- **`output.write.Run`** — DELETE the template block → `return await Channel.WriteAsync(Data)`.
- **`mock/intercept`** — DELETE its duplicate template block → let the value resolve itself.
- **DELETE dead `AsCanonical`.**
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

6. **How does a TYPED reference born — and who resolves it? → DECIDED: Model B (Ingi).**
   ANY type can be a `%var%`. `set %numb% = %userInput% as number` → the value's declared
   type is `number`, so it borns as a `number` reference (`%numb%` reads as a number).
   **Implemented via the universal carrier `source`, NOT by teaching every scalar about
   refs.** The carrier already exists (`type.Build:276` borns `%x% as T` as
   `source{ raw, type=T, template }`); the gap is that resolution is done TWO ways:
   - `text`'s reader honors `ReadContext.Template` and resolves the `%ref%`;
   - `number`/`bool`/`datetime`/`image` readers IGNORE it and just parse their token — so
     `%userInput% as number` fails today (`number/serializer/Reader.cs` parses raw
     `"%userInput%"` → `@null`).

   **The rule for Model B:** `source` resolves its `%ref%` **once, uniformly** (via the
   variable engine), THEN coerces the resolved value to the declared type `T`:
   ```
   source.Value / source.Output (mode != Store):
       1. resolved = resolve(raw, context)      // ONE place — source. Uniform for every T.
       2. return T.Create(resolved)             // number/bool/datetime stay concrete & dumb
   ```
   Scalars are never touched — they never learn about `%ref%`. `variable.@this` stays as
   the carrier for an UNTYPED bare full-ref (`write out %var%`, type = whatever `var`
   holds); `%x% as T` rides `source{T}`. `text` (embedded template) and `path`
   (`_location`) either fold their resolution into this single `source` seam or remain as
   already-materialized-form resolvers — architect to decide whether they collapse into
   `source` or stay as post-materialization refiners.

   This is upstream of everything: it says the ONE reference-resolution seam is `source`
   (+ `variable` for the untyped ref), and §4's `Output` work delegates to it.

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

*Resolve §9's forks FIRST — decision #6 (source seam), #2/§9.3 (passthrough), §9.2 (collapse
`variable`-value-ref into `source`?). They change what the steps below even are.*

1. **`source.Value` is the Model-B seam** — resolve `%ref%` once, then `T.Create(resolved)`,
   so `%x% as number` resolves (fixes the per-type-reader gap, `number/serializer/Reader`).
   Settle the passthrough rule (§9.3) here.
2. **`Data.Output = Value()-then-write`** (§4) — delete the `_item is variable.@this` branch;
   Store→raw, Out→`Value()`-then-write. Must be behavior-neutral: verify full-suite failure
   counts identical pre/post (same method as the `IName` rename). **No per-type `Output`
   overrides added** (§9.1).
3. **Delete `output.write` block + `mock/intercept` block → `channel.write(data)`; delete
   dead `AsCanonical`.** This is where the embedded + source-born bugs actually go green.
4. **(optional follow-up)** collapse `variable`-value-ref into `source` (§9.2), consolidate
   grammar detectors (§dup-3), ref-detector forms (§dup-4), dict/list render (§dup-5), the
   `Resolve` naming collision (§dup-7).

Each step needs a full-suite baseline diff (WIP branch has ~127 pre-existing reds; only the
delta matters). Store/signing tests are the tripwire on every step.

---

## 9. Known weaknesses & open forks — ARCHITECT READ THIS

The invariant (§2) is right; the *first-draft flow was not the minimal expression of it*,
and two forks below are unsettled design, not chosen. Do not treat §4 as final until these
are resolved.

**9.1 — `Output` must NOT re-implement the `Value` door (the trap I fell into first).**
The obvious move is `text.Output`/`source.Output`/`variable.Output` each resolving. That
*duplicates* their `Value` doors — the same "two ways" smell, reintroduced one layer over.
The minimal form is `Data.Output = Store?raw : Value()-then-write` (§4): resolution lives
ONLY in `Value`; `Output` calls it. **Red flag during implementation:** if a PR adds an
`Output` override that resolves, resolution has leaked out of the door again.

**9.2 — Two reference carriers is a fork; `variable` has a split personality.** `source`
carries typed refs (`%x% as T`); `variable.@this` carries the untyped bare ref (`%x%`). But
a bare `%x%` is just `%x% as <unknown>` — the same resolve minus a coercion, so
`variable`-as-value-ref ≈ `source{ T = dynamic }`. Worse, `variable.@this` is ALSO the
name/write-target type (`set %numb% =`) which must NEVER resolve. Same type, opposite
behaviors. **Cleaner end state:** `source` carries *all* value references; `variable.@this`
is *only* names/write-targets. **Caveat:** this partly unwinds the just-landed
born-as-variable value-slot work (`variable-ref-binds-instance` decision) — architect must
decide whether to collapse now or keep the two carriers.

**9.3 — The clean `Value()-then-write` breaks verbatim passthrough (unresolved).** A plain
file read (`read big.json` → `source`, no template) would be MATERIALIZED and RE-SERIALIZED
on output — exactly what today's `output.write` Peeks to avoid ("writes raw bytes, not a
re-serialised object"). So the elegant unification conflicts with byte-exact passthrough.
Any "is this already raw / has no ref?" fast-path risks reintroducing a state/type question
(§9.1's smell). **This is decision #2 and it may block the minimal design — resolve it
before building §4.**

**9.4 — "Store-verbatim vs Out-resolved" is one rule; keep it in one place.** If the
Store/Out branch lives per-type (`mode==Store ? raw : resolve` in each carrier), one type
forgetting the check silently leaks a resolved value into a `.pr` / signature. §4's
`Data.Output`-owns-the-switch form avoids this; a per-type design must not.

**9.5 — The resolve primitive + cycle guard have one home, not three.** `resolve(raw)` is
the STORE's engine (`variable.list.Resolve`/`Get`); `source` and `variable` are callers. The
cycle guard (`a=%b%,b=%a%`) must live at that primitive, ONCE — today it is copied
(`variable._resolveDepth`, `Data._outputDepth`, `dict._renderDepth`). Any design leaving two
guards will drift into an SO on one path and a typed error on another.

**9.6 — Signing boundary is now sharper and more dangerous.** Post-change, Out is *resolved*
(non-deterministic — depends on live variable state); Store is *raw*. Every hash/signature
MUST be computed on the Store view only. This was already a landmine (`DataHashMismatch`
history); the change makes an Out-view hash catastrophically wrong. State it as an invariant
+ a test, do not assume it.

**9.7 — `Peek()` ≠ `Value()` type under Model B.** `%numb% as number` Peeks as a `source`
but Values as a `number`. Any consumer that branches on the *peeked* type sees the wrong
type. This is lazy-by-design, but it is a live footgun — several sites branch on `Peek()`
(the `variable-ref-binds-instance` reach-in in `variable/list` is one). Audit `Peek() is`
sites for assumptions that the peeked type equals the resolved type.
