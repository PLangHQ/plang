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
7. **Naming collision — DECIDED (Ingi).** `variable.@this.Resolve` (a static FACTORY that
   builds the ref object) vs `variable.list.@this.Resolve` (the interpolation ENGINE) —
   opposite operations, same name. Resolution: **delete the static `Resolve`; the name-grammar
   parse folds into `new variable.@this(raw)`** — a constructor, not a static, no
   `Convert(...).Peek()!` unwrap. This makes `variable` symmetric with every other type's
   reader (`new text.@this(...)`, `new datetime.@this(...)`, `num.From(...)` — variable was the
   only one detouring through a static). `Convert` stays as the one-line family hook
   (`ctx.Ok(new @this(raw))`); the reader becomes `new variable.@this(reader.String())`; the
   engine `variable.list.@this.Resolve` → **`Render`**. See §5.7 for the verified call-site
   count + safety proof.

---

## 4. The changes, per layer

**Do NOT add per-type `Output` resolution.** (First-draft instinct — see §9.1: it just
re-implements the `Value` door inside `Output`, the same "two ways" smell.) The form is
**`Data.Output` owns one branch on the VIEW, gated by the item's own live-ness; resolution
stays in the `Value` door:**

```
Data.Output(writer, mode, context):
    … envelope (Store name / @schema / type — about the VIEW+WRITER, not the item's type) …
    if (mode == Store || _item.Cacheable)                            // authored form, OR nothing live to resolve
        await _item.Output(writer, mode, context);                   // raw — pass mode THROUGH (a container resolves per child)
    else                                                             // a LIVE leaf (Cacheable==false)
        await (await this.Value()).Output(writer, mode, context);    // resolve via the DOOR, then write
```

**The gate is `!Cacheable`, not "always `Value()` on Out" (architect decision — corrects the
first draft).** `Cacheable == false` already means "I hold live `%refs%`" and is declared on
every item: `text.Cacheable => Template == null`, `source.Cacheable => _template == null`,
`variable.Cacheable => false`, base `=> true`. So the gate reads item STATE the item owns
(same family as `IsLeaf`/`IsFinal`) — **not** a type-test and **not** `.Value` (Rule #7 clean).
Two consequences:
- **§9.3 passthrough dissolves — it was never a blocker.** A plain file read (`source`,
  `_template==null`) is `Cacheable` → the raw branch → byte-exact bytes out, no materialize,
  no re-serialize, no `Peek()` in the courier. Only a template/ref source (`Cacheable==false`)
  resolves. This is exactly the boundary decision #2 asked for, answered by the item's own flag.
- **Pass `mode` through, do NOT force `Store` down.** Forcing `Store` into a container would
  suppress its children's resolution on the Out view. A `Cacheable` dict with live children
  takes the raw branch and walks children in `mode`; each live child hits its own `Data.Output`
  gate and resolves. Resolution is per-leaf, never "resolve the container whole."

- **`Data.Output`** — DELETE the `_item is variable.@this` branch. After this the method has
  **zero `_item is <Type>` questions**. It owns the Store/Out switch above; the item only
  ever provides `Write` (raw form) + `Value` (resolved form), both of which already exist.
  Acceptance test: grep `Data.Output` for `_item is` → nothing. DELETE `_outputDepth` too —
  the cycle guard now rides only on `variable.Value`'s `_resolveDepth` (decision #3 / §9.5).
- **`Value` door is the single resolution seam** (already present): `text.Value` (template),
  `source.Value` (materialize + resolve — the Model-B seam, §decision-6), `variable.Value`
  (untyped ref), `dict/list.Value` (deep render), scalars (`this`). **No new `Output`
  overrides on any type** — `text.Output`/`source.Output`/`variable.Output` should NOT be
  written; if you find yourself adding one, resolution has leaked out of the door again.
- **`source.Value`** — the Model-B work: resolve the `%ref%` ONCE (via the store engine),
  then `T.Create(resolved)`. A no-template source (`Cacheable`) never reaches here on output —
  the `Data.Output` gate writes it raw, so passthrough is byte-exact (see §9.3, now dissolved).
- **`output.write.Run`** — DELETE the template block → `return await Channel.WriteAsync(Data)`.
- **`mock/intercept`** — DELETE its duplicate template block → let the value resolve itself.
- **DELETE dead `AsCanonical`.**
- **Born contract (prerequisite):** full-match `%x%` → `variable`; embedded `"hi %x%"` →
  `text` + template flag (the builder stamps it — decision `builder-detects-var-at-build`).
  Once this holds, `text.Value` NEVER sees a full-match → its full-match branch
  (`text:77-88`) becomes dead and is removed, killing duplication #1's text copy.

---

## 5. Decisions needed (Ingi / architect) — do NOT code before these

1. **Absent ref → DECIDED: throw, uniformly (Ingi); DONE on branch (`e8519aa60`).** An unset
   **user** variable is a hard error at the reference site (`VariableNotFound`) — full OR
   partial, engine-wide, like any typed language. **Infrastructure refs (`%!x%`) are
   optionally-present** (a `%!error%` with no error) → stay literal on absence, never throw.
   (The engine now throws at `variable/list/this.cs:726`; `text.Value`/`variable.Value` already
   did. Exception→error translation preserves `Key`+`StatusCode` so it surfaces as
   `VariableNotFound`, not `ServiceError`.)
2. **Passthrough vs resolve-through-door → DISSOLVED (see §4 gate).** Not a decision — the
   item's own `Cacheable` flag answers it. A no-template source is `Cacheable` → the
   `Data.Output` raw branch → byte-exact passthrough; a template/ref source is
   `Cacheable==false` → resolves. The boundary *template/ref ⟹ resolve, otherwise verbatim* is
   exactly `!Cacheable`, read off the item, no courier state/type question.
3. **Cycle-guard placement → DECIDED.** One guard, on `variable.Value`'s `_resolveDepth`.
   `Data._outputDepth` is deleted (all output resolution now flows through `Value`, which is
   guarded). No guard on text — a text template can't self-cycle; only a variable chain can.
4. **`skipInfrastructure` → DECIDED: DELETE the parameter (OBP smell — Ingi).** A bool threaded
   from call site into the engine to switch behavior is exactly the smell. It has only two
   callers, both removed by the redesign, so the param + both engine branches
   (`variable/list/this.cs:720,748`) delete:
   - **`output/write.cs:23`** — deleted by §4 (the value resolves itself; no manual `Resolve`).
   - **`file/read.cs:91`** — the `ResolveVariables` opt-in + manual `Resolve(content,
     skipInfrastructure:true)` is **replaced**: when a file read should interpolate, the action
     stamps **`template="plang"`** on the source it creates (the builder-recorded intent, like
     every other template), and resolution flows through `source.Value` — the one seam. No
     `ResolveVariables` param, no manual engine call. See §12.
   **Security note (state, don't skip):** without `skipInfrastructure`, a `template="plang"`
   file read resolves *all* refs including `%!infra%`. This is safe because the interpolation is
   **authored in the goal, in plain sight** — `- read file.txt and load vars, write to %content%`
   — which is what stamps `template="plang"` at build. The trust anchor is the authored PLang, not
   the file's content: "load vars" is an explicit, readable instruction, so `%!infra%` resolving
   there is the developer's stated intent, never a hidden default. (Infra vars are runtime state —
   `%!app%`/`%!callStack%`/`%!data%`/`%!error%` — not the secrets/settings namespace.) Confirmed
   acceptable (Ingi).
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

7. **`Resolve` naming collision → DECIDED: born by constructor (Ingi).** Delete the static
   `variable.@this.Resolve` (`variable/this.cs:224`) — it is a pure alias for
   `(@this)Convert(raw, null, ctx).Peek()!`, and the `context` it takes is wrapped then
   discarded (`Ok(...).Peek()`), so the operation needs no context. The name-grammar parse
   (`%x%` / bare `x` / `%!flag%` / `%x.age%` / `%x!cost%`→Name+Property / `%x!!cost%`→malformed)
   folds into **`new variable.@this(string raw)`** — the current 1-arg ctor's body becomes the
   parse. End state:
   ```
   public @this(string raw)                 // 1-arg: NOW parses the name grammar
   public @this(string name, string raw, bool wasWrapped)   // 3-arg: explicit parts, stays
   public static @this Convert(object? v, string? kind, ctx) => ctx.Ok(new @this(v as string ?? v?.ToString() ?? ""));
   // Reader: new variable.@this(reader.String())   — symmetric with text/datetime/number readers
   // Resolve: DELETED.  variable.list.@this.Resolve → Render
   ```
   **Verified safe — behavior-neutral.** Real callers of the static: **3 production**
   (`type/this.cs:267`, `type/this.cs:290`, `variable/serializer/Reader.cs:17`) + ~19 test
   sites (`VariableResolveTest.cs`, `VariablesTests/VariableResolveTests.cs`,
   `BornTypedDeclineTests.cs`); nothing in `PlangConsole`. All become `new variable.@this(raw)`.
   The 1-arg ctor today stores its arg **verbatim, no parse** (`:57`), so folding parse in only
   diverges for a `%`/`!`/`.` argument — and a grep for any 1-arg call with such an argument
   returns **empty**: every existing 1-arg call passes a clean bare name (`"myList"`, `"x"`, …)
   for which parse is a no-op. The 3-arg ctor stays for the 2 explicit `("x","%x%",true)` tests.

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

*All forks settled: #6 (source seam), #2/§9.3 (passthrough — dissolved by the `!Cacheable`
gate), #3 (one cycle guard), #7 (`Resolve` → constructor), §9.2 (collapse — `variable` is
name-only, DECIDED; worklist §10). Sequencing: the output fix lands first on the current
carriers (steps 0-3), then the carrier collapse as its own isolated pass (step 4 / §10).*

0. **`Resolve` → `new variable.@this(raw)`** (§5.7) — delete the static, fold parse into the
   ctor, `list.Resolve` → `Render`. Standalone, behavior-neutral, unblocks nothing else; land
   it first or fold into the source-collapse pass.
1. **`source.Value` is the Model-B seam** — resolve `%ref%` once, then `T.Create(resolved)`,
   so `%x% as number` resolves (fixes the per-type-reader gap, `number/serializer/Reader`).
2. **`Data.Output` gate on `!Cacheable`** (§4) — delete the `_item is variable.@this` branch
   and `_outputDepth`; `Store || Cacheable → raw`, else `Value()`-then-write, mode passed
   through. Behavior-neutral: verify full-suite failure counts identical pre/post (same method
   as the `IName` rename). **No per-type `Output` overrides added** (§9.1).
3. **Delete `output.write` block + `mock/intercept` block → `channel.write(data)`; delete
   dead `AsCanonical`.** This is where the embedded + source-born bugs actually go green.
4. **Carrier collapse (§9.2 / §10)** — `variable` becomes name-only; value refs ride `source`.
   Return path → `Set("!data", result)` whole; delete the `!data` DynamicData; re-express
   `variable-ref-binds-instance` + condition-tolerance + build-validation on `source`. Then
   consolidate grammar detectors (§dup-3), ref-detector forms (§dup-4), dict/list render (§dup-5).

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

**9.2 — Collapse to one carrier → DECIDED: `variable` is a NAME, never a value (Ingi).**
`variable`-as-value-reference was a mistake — it has been a pain and it goes. `source` carries
*every* value reference: `%x% as number` → `source{type=number}` (resolve, coerce); bare `%x%`
→ `source{type=item}` (resolve, no coercion — `item` is the apex, nothing to narrow; there is
no "dynamic" type). `variable.@this` is left doing ONE job: naming a write-target (`set %n% =`,
foreach item-name) — it must NEVER resolve. This unwinds the `variable-ref-binds-instance` work,
which **re-expresses on `source`** (not deleted). Full worklist: §10.

**9.3 — Verbatim passthrough → RESOLVED (not a blocker).** The worry was that
`Value()-then-write` MATERIALIZES + RE-SERIALIZES a plain file read (`read big.json` →
`source`, no template). The fix isn't a "is this raw?" fast-path (which would be §9.1's
state/type smell) — it's the item's own `Cacheable` flag, which already answers "have I any
live `%ref%`?". The §4 gate is `mode != Store && !_item.Cacheable`: a no-template source is
`Cacheable` → raw branch → byte-exact, no materialize. Reading `Cacheable` is asking the item
about itself (family of `IsLeaf`/`IsFinal`), not opening `.Value` or type-testing. Decision #2
dissolves into that flag.

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

---

## 10. Collapse-pass demolition & leaf-trace (§9.2 — `variable` becomes name-only)

Every site that assumes a **value** can be a `variable.@this`. `variable` stays ONLY as a
name/write-target; a value reference is a `source`. Traced from `is variable.@this` /
`Peek() is variable` / `HasVariableReference` / `IsVariable`.

### Dies outright

| site | what it is | disposition |
|---|---|---|
| `action/this.cs:285-288` | return-path snapshot: `Peek() is variable \|\| HasVariableReference ? new Data("!data", await result.Value()) : result` | → **`await context.Variable.Set("!data", result)`**. Whole Data, no Peek, no `Value()`, no HasVariableReference, no snapshot. The loop-fear is handled lazily at the read-time `_resolveDepth` guard, not eagerly here. |
| `context/this.cs:182` | `!data` DynamicData `() => Variable.Peek("data")?.Peek()` — aliases a never-set `data` var, `.Peek()` opens the box, clobbered by the direct set anyway | **delete**. `%!data%` is born by the first action's `Set` (above). Name stays `!data` — the `!` infra prefix keeps it out of the user's own `%data%`. |
| `data/this.Output.cs:50` | `_item is variable.@this` resolve branch | already deleted in §4 (the `!Cacheable` gate). |
| `data.cs:566` (`AsCanonical`) | `if (_item is variable.@this v)` | dead method (§3 dup #1) — **delete whole**. |
| `data.cs:155` (`IsVariable`) | `_item is variable.@this` detector | **delete** — zero callers after collapse (confirm: no `.IsVariable` references remain). |
| `variable/list/this.cs:739` | interpolation engine: `else if (Peek() is variable.@this)` re-resolves a stored ref | dies — a stored value is never a bare ref now. |

### Re-express on `source` (the real work — NOT delete)

| site | what it is | re-expression |
|---|---|---|
| `variable/list/this.cs:120-125` (`Set`) | `variable-ref-binds-instance`: `set %a% = %b%` binds `%b%`'s Data INSTANCE, currently C# **reference** semantics (a later `add to %b%` is visible through `%a%`) | **flips to Swift value semantics (COW) — DECIDED, see §11.** `set %a% = %b%` gives `%a%` its own logical copy: physically share `%b%`'s instance, mark it shared, **split on first in-place mutation**. `%b% as number` builds a new value (never shared). Independence is total — editing one never changes the other. |
| `module/condition/code/Default.cs:58` (`TolerateAbsentVariable`) | `Peek() is variable.@this` → condition tolerates an absent ref (`if %x% is null`) | tolerate an absent **`source` ref** the same way. |
| `module/builder/validateResponse.cs:174` | `Peek() is variable.@this \|\| HasVariableReference` → build-time validation skips ref values | the `is variable` half → `source` ref; keep the template half. |

### Stays (genuine NAME role — correct, do not touch)

| site | why it stays |
|---|---|
| `data.cs:471` (`As<variable>`) | a name slot wants the reference ITSELF (its name), not its value — this IS the name-only role we keep. |
| `type/this.cs:265-267, 287-290` | born-as-variable in `Create` — now fires ONLY for `type:variable` name slots (`set` target, foreach item). |
| `variable/serializer/Reader.cs` | reads a name-slot off the wire → `new variable.@this(reader.String())` (§5.7). |
| `module/variable/set.cs:35,54` (`HasVariableReference`) | skips kind-validation for a value that still holds a `%ref%` — template-based (`Template != null`), survives a `source{template}`. Review under ref-detector consolidation (dup #4), not here. |

### Open sub-questions for this pass
- **`!data` can now hold an unresolved template** (no eager `Value()`). Leaf-trace any consumer that assumes `%!data%` is already concrete.
- **Ref-detector consolidation (dup #4)** intersects here — `IsVariable` dies, `HasVariableReference` (template) stays; fold the two into one detector during this pass or immediately after.
- **`%!data%` before the first action → CONFIRMED throws (Ingi).** The deleted DynamicData made an early `%!data%` read null; without it `%!data%` is *absent* until the first action's `Set`, and under decision #1 (absent full ref → throw) that early read throws. There is nothing before the first action — throw is correct. (Other infra DynamicData `!error`/`!step`/`!event`/`!test` are untouched.)

---

## 11. Value semantics — the Swift rule → DECIDED (Ingi)

**`set %a% = %b%` gives `%a%` its own copy. Editing one never changes the other — ever.** One
rule, no scalar-vs-collection exception, no alias-vs-snapshot exception. This **reverses** the
current C# reference-semantics choice (`list/add.cs:29-33` comment) and is a cross-cutting
change larger than the carrier collapse — likely its own pass, but the `set` re-expression in
§10 depends on it, so it is recorded here.

- **The mechanism is copy-on-write.** `set %a% = %b%` shares `%b%`'s storage and marks it shared
  (no eager copy). The first *in-place mutation* through either binding splits off a private copy,
  then mutates that. If nobody edits, nothing is copied — scalars (immutable) never split.
- **What changes vs today:** collections stop being C# reference types. `list`/`dict` become
  value types with a COW guard. `set` still rebinds (unchanged); the difference is that a shared
  instance now *splits on edit* instead of being edited in place under everyone's feet.
- **The in-place mutators that must become COW-aware** (check-shared → copy-then-mutate):
  `list.Add / Insert / RemoveAt / SetAt / Remove / Reverse` (`type/list/this.cs:272-336`),
  `dict.Set` (`type/dict/this.cs:259-271`). Functional list ops that already return a *new* list
  (`unique`, `where`, `group`, `flatten`, `range`, `split`) are unaffected.
- **Coercion is orthogonal:** `%b% as T` builds a new value — never shared under any rule.
- **Ref-detector / cost note:** COW is a structural mechanism (a shared-storage flag + split),
  not a Data-decompose — it does not touch the OBP box rules.

Worked example the rule must satisfy:
```plang
set %b% = ["apple"]
set %a% = %b%
add "banana" to %b%
write out %a%          # → ["apple"]      (Swift: %a% independent; C# today gives ["apple","banana"])
```

**Deep split → DECIDED: deep, follow Swift (Ingi).** A nested edit must also stay private:
```plang
set %b% = [ {name: "Ann"} ]
set %a% = %b%
set %a%[0].name = "Bob"     # must NOT change %b%[0].name
write out %b%               # → [ {name:"Ann"} ]
```
Swift value types are value-semantic *all the way down*, so the split propagates along the
mutated path — split each still-shared level from the list down to the touched dict, not just the
top container, and per-level on the write path (never an eager deep copy).

---

## 12. `file/read` interpolation → stamp `template="plang"`, drop `ResolveVariables` (DECIDED — Ingi)

`file/read` today interpolates on an explicit bool opt-in and a manual engine call:
```csharp
if (await ResolveVariables.ToBooleanAsync()) {
    …
    var resolved = await Context.Variable.Resolve(content.ToString()!, skipInfrastructure: true);  // bolt-on
    return new data.@this(read.Name, resolved, read.Type, context: Context);
}
```
That is the last `skipInfrastructure` caller and a second resolution path outside the door.

**New shape:** a file read that should interpolate produces a `source` stamped
**`template="plang"`** (the builder-recorded intent — the same stamp every other template
carries). Resolution then flows through `source.Value` / the `Data.Output` gate — the ONE seam.
Consequences:
- **Delete the `ResolveVariables` parameter** and the manual `Resolve` block.
- **No `skipInfrastructure`** — a `plang` template resolves all refs (see §5.4 security note).
- A plain read (no stamp) stays a no-template `source` → `Cacheable` → byte-exact passthrough
  (§4), unchanged.
- The interpolation is no longer eager/one-off — it is lazy at the door, resolving against live
  variables at each use, like any template.

Fits the born-contract: *the trust is the mode the builder stamped, never the content.* A file's
own bytes are never inspected for `%…%` — only the developer's stamped intent decides.

---

### Status note (landed vs planned)
`e8519aa60` shipped a **minimal** output-resolution fix — `output.write` detects templates via
`HasVariableReference` (source now exposes `Template` on the base property), and unset user vars
throw (decision #1). It explicitly defers the OBP "right fix" to this plan: §4 (the `!Cacheable`
gate, `Data.Output` owns the view, `output.write` collapses to `channel.write`) **supersedes**
the minimal `output.write` block, and §12 supersedes its `skipInfrastructure` use.
