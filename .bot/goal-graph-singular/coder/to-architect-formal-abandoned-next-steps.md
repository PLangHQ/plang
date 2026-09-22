# Formal plang is abandoned — what survives, what gets deleted, what is next

**Branch:** `goal-graph-singular` · **Author:** coder · **Date:** 2026-09-22
**Decision (Ingi):** drop `formal` completely. Not "fix it", not "derive it" — remove it.

---

## 1. Why it is being abandoned

`formal` was a one-line render of a step's actions that the LLM wrote as a free string during
compile. It was never defined, never parsed, never checked. Sampling real `.pr` files showed it
already broken in four different ways:

- **prose stored as formal** — `"I will correct the action payload to include all required
  non-nullable parameters."` sits in `step.formal` on disk (the fix-loop answered its own
  correction prompt in prose and it was stored). 4 of 26 sampled steps.
- **instruction text leaked into the language** — `output.write(Data="…", channel omitted)`: the
  notes said *"OMIT channel from formal"*, so the model wrote "omitted" into formal.
- **one relationship, two renderings** — `a | b` and `a , b` both used for "write to %x%".
- **no model of what it represents** — an attempt to write its grammar produced four
  relationships between calls, three of which the existing shape got wrong or could not express.

The only thing that would have made it real was a renderer deriving it from `action[]` plus a
compare gate — and at that point it is a debug view of the action tree, not an input. Ingi's call:
it does not earn its keep. The action tree IS the program; there is no second notation.

`Documentation/v0.2/formal-plang.md` (the grammar attempt, five commits) is deleted with this
hand-off. The durable findings from it are carried in §3 below.

---

## 2. What deleting `formal` touches

Small. Nothing at runtime reads it.

| where | what |
|---|---|
| `PLang/app/goal/step/this.cs:116` | `public string? Formal { get; set; }` — delete |
| `step/this.Item.cs:34,70` | read + write of the `formal` wire key — delete |
| `step/serializer/Reader.cs:53,111` | both reader doors — delete the case |
| `os/system/builder/BuildStep/Start.goal:32,62` | `set %goal.step[…].Formal% = %compileResult.formal%` — delete |
| `Start.goal:43,60` | `formal: string` in the two `llm.query` schemas — delete the field |
| `os/system/builder/llm/Compile.llm` | "Write `formal` first", the MUST-match rule, the separator bullets, `:44` braces example — delete; the `modifier`/`child` teaching stays, it is about `action[]` |
| `os/system/modules/assert/module.notes.md`, `output/write.notes.md` | the sentences about `formal` — delete; the rest of each note stands |
| `.pr` files | carry `"formal"` — reader will `Skip()` unknown keys; regenerate on next build (Ingi: not chasing `.pr`) |

**Dead residue found while inventorying — same subsystem, already unreachable:**

| where | what |
|---|---|
| `PLang/app/type/spec/{Action,Example}.cs` + `spec/render/this.cs` | the catalog "e.g. …" formal-string renderer. **Zero production callers.** |
| `ExamplesForLlm()` static on 8 handlers (`error/handle.cs`, `math/subtract.cs`, …) | feeds the renderer above. **Nothing calls it.** Also contradicts the standing rule that action prose lives in markdown, not on handlers. |
| `PLang.Tests/Modules/App/Modules/ui/FormalFilterTests.cs`, `ActionFormalTemplateTests.cs` | tests of the above |

The Fluid `formal` filter in `ui/code/Fluid.cs:110` is a *different* thing — it renders a value in
the catalog's `Name([type] value)` shape for templates. Unrelated; stays.

Estimated size: one commit, ~15 files, all deletions. No behaviour change.

---

## 3. Findings that survive the abandonment (not formal-dependent)

These came out of the grammar attempt but are about the **action tree**, which is what exists.

### 3a. Four relationships between calls, and the one that is mis-modelled

| relationship | when the nested call runs | wire slot | status |
|---|---|---|---|
| **sequence** | next; left's result is `%!data%` | `step.action[]` | fine |
| **wrap** | around its host | `action.modifier[]` | fine |
| **body** | when the owning call decides | `action.child[]` | fine for conditions |
| **expression** | when the owner reads the parameter | a parameter's value | *grammar-legal; runtime has no evaluator* |

Plus a **reference** — a value that is neither evaluated on read nor run by its owner, but
stored for later (an event handler's goal). Rides as the `goal.call` TYPE (`{name:"X"}`), never as
a nested action.

The rule that separates these, which Ingi settled: **every call is CONSTRUCTED at load; only its
EVALUATION is deferred.** A `%var%` is the model — constructed at load as a Data holding a name,
evaluated on read. A nested action in a value can be the same. What may never happen is lazy
*construction*, because then nobody holds the step at birth.

**`error.handle`'s recovery is a body written as an expression.** Nobody reads `Action` to obtain
a value; the handler runs those actions on its own condition. Being in a value slot means it is
constructed lazily, which is why `handle.cs` still carries the last stamp on the graph
(`action.Step = enclosingStep`). This is the same conclusion as your Addendum 3 Q3(a), reached
from the language side.

**`channel.set(Name="audit", Goal=goal.call(…))` is a reference written as an expression.** The
`Goal=` parameter holds a nested `goal.call` action record where it should hold a goal reference.
Ingi has flagged wanting to fix `goal.call`; this is a concrete site.

### 3b. `"object"` is leaking onto the wire as a type name

`type/list/this.cs` answers `"object"` as a fallback whenever it cannot name a CLR type:

```csharp
if (type == null) return "object";                    // :123, :429
if (type == typeof(data.@this)) return "object";      // :151
```

Counted across every `.pr` in the repo: `"name": "object"` **353** times vs `"name": "text"` 350.
Roughly half the type annotations in the corpus are values whose real type was never determined,
wearing a CLR word as their plang type. `object` and `string` are not plang words (`item` is the
base, `text` is the type). May partly resolve on rebuild; the fallback code path will keep
producing it until it is fixed.

### 3c. The builder is teaching a parameter name that no longer exists

`os/system/modules/error/handle.notes.md` still says *"the recovery action goes INSIDE
`error.handle.Actions`"*. `Actions` → `Action` in `dd7e28ef2`. The builder will emit a name that
does not bind. This is the same rename that silently killed the four `ErrorHandleTests` (fixed in
`0c2c74fab`). Goes away with step (3) below, since the parameter itself goes away.

---

## 4. Next steps, in order

### Step A — delete `formal` (§2). One commit, no behaviour change.

### Step B — recovery becomes a body: the structural slot (your Q3(a), Ingi's "it is a program, not data")

Now unblocked from the language side too. Shape:

- `modifier.@this` (or `action.@this`) gets a structural action-list slot — precedent `Modifier`.
  Read at load through `Populate` with `step` in hand; recovery actions born with the ENCLOSING
  step (real, not invented).
- `error.handle` runs it as `await Recovery.Run(context)` — the normal `action.list` runner, so
  recovery gains condition support and `%!data%` flow for free.
- Dies: `RunRecovery`, `RunRecoveryWithErrorScope`, the `row.Value<Action>()` lazy door, the last
  `action.Step` stamp, reader door 2 (the `ITypeReader` registry entry for actions — grep-confirm no
  other customer), the `Action` parameter on `handle`, and the stale `handle.notes.md` sentence.
- Wire: the builder emits recovery under the new key instead of the `Action` parameter.
  `Compile.llm` §"Conditions carry their body in `child`" generalises to two body-carriers.

**Blocked on one word: the slot's name.** Ingi + you. `recovery` is coder's pick (names why the
actions are there; matches the code's vocabulary). The `child` reuse was considered and has a
real argument (a body is a body) but you ruled against overloading it and I agree the two wire
shapes differ today (`child` wraps a step record with `text`; recovery is bare actions).

### Step C — Validate trilogy (Stage D). Unchanged from your order.

### Step D — the `"object"` leak (§3b). Trace where the 353 come from: builder not determining
types, or the writer losing them at egress. Fix the fallback at the source, not by rebuild alone.

### Step E — `goal.call` as a reference (§3a). Ingi wants this; `channel.set Goal=` is the
concrete site. Sized separately.

### Queued, not next

- **Expressions** (a call in a value, evaluated on read — `condition.if(Left=file.exists(…))`).
  Grammar-legal, runtime has no evaluator, and the pipe already expresses the same computation
  flat. Ingi likes the shape; needs a sizing before it is a step.
- **Modifier subframes.** `modifier.Wrap` never pushes, so a modifier's own failure lands on no
  frame at all. Real gap; changes the call-tree render and snapshot shape, so its own step.
- **Backlog** (`open-items.md`): Wire suite stack overflow (every Wire number on this branch is
  from a truncated run — treat as unknown); `plang --test` loads but runs zero tests (stale
  `os/system/.build/test.pr`; builder has never self-built here); `Property.Rows` middleman;
  `goal.list` late stamp.

---

## 5. State of the tree (for orientation)

Landed on this branch since your Addendum 3, all pushed:

- `1e5c67c63` birth-fact pass — readers shell-first, all stamps/repair getters gone, `step.Goal` is `init`
- `1d33f8309` one frame per action — `action.Run` owns the Push; `%!error%` reads `CallStack.Error`
- `8d97a9e3b` `app.Error` + `error.trail` deleted; diff scope rehomed as `CallStack.DiffScope`; your `App.Run` seam fix (`Step = context.CallStack.Current?.Action.Step`)
- `0c2c74fab` `ErrorHandleTests` recovered (stale `"actions"` param name)
- `action.Step` nullable, per your Addendum 3 Q1

C# suites: Modules 63 / Types 27 / Data 52 / Generator 19 / Runtime 44 — all at or below the
pre-branch baseline, failure sets diffed by name. Wire: unknown (pre-existing overflow).
