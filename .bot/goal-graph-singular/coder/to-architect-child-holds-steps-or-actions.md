# to architect — what does `action.Child` hold: steps or actions?

**Context.** Ingi rejected the post-compile fold (`step.FoldChain` / `goal.FoldBlock`) as
an obpv — a runtime step fabricating and restructuring synthetic steps. His directive:

> "change the compiler to give us the correct structure, not [give it in] wrong
> structure. i don't want to see any fold, shouldn't be necessary."

So the tree (`action.Child`) must be **born correct** — the parser + LLM Compile emit it
directly; nothing re-nests a flat list afterward. The runtime tree is done and firing
(`action.list.Run`: `IsCondition && truthy → Child.Run; break`). This note is the one
model decision that blocks the compile change.

## What Ingi clarified about condition syntax (this is the input that forces the fork)

There is **no `- else` step**. Conditions are never sibling steps at differing indent
(my Python-shaped mental model was wrong). Every condition is **one step**, in one of
three forms:

```
INLINE if/else:      - if %x% > 2 then call DoStuff, else call DoThat
INLINE if/elseif/else: - if %x% then call Xx, elseif %y% then call Xcx, else call Xxc
SUBSTEP (if only):   - if %x% then
                         - do stuff
                         - do more
```

- **Inline** bodies are **actions** (`call DoStuff` — a `goal.call`). Multiple conditions
  live in one step, each with its own body → the body **must** hang on the *action*, not
  the step. (This kills the "`step.Child`" alternative outright.)
- **Substep** form exists **only for a bare `if`** (no elseif/else). Its body is **real
  `.goal` steps** (each `- ...` line).

Today (`Compile.llm:22,31`) inline `if X, call Y` compiles to **two flat peer actions in
one step**: `[condition.if, goal.call]`. The tree pulls the body *inside* the condition.

## The fork

`action.Child` is currently typed `step.list`. The two source forms disagree about what
the body naturally *is*:

**(A) `Child : step.list`** — child is steps.
```
if %x% then call DoStuff, else call DoThat
  → [ condition.if{ child:[ step{ goal.call DoStuff } ] },
      condition.else{ child:[ step{ goal.call DoThat } ] } ]
```
- Substep form is native (those really are steps).
- Inline body becomes a **synthetic one-action step**. Concern: is a compile-time
  body-wrap the same "synthetic step" smell Ingi rejected? (I read it as *not* the same —
  that was a runtime domain method re-folding; this is the builder producing wire, which
  is its job — but you own the model call.)

**(B) `Child : action.list`** — child is actions.
```
if %x% then call DoStuff, else call DoThat
  → [ condition.if{ child:[ goal.call DoStuff ] },
      condition.else{ child:[ goal.call DoThat ] } ]
```
- Inline body is native (no synthetic step).
- Substep form: each substep contributes its action(s) — a multi-action or multi-line
  substep body **loses its step identity** (step-level lifecycle events, Text, LineNumber,
  Indent all flatten away). That's a real loss for the `if %x% then \n\t- a \n\t- b` form.

**Trade in one line:** A pays a synthetic-step tax on the (common) inline form; B pays a
lost-step-identity tax on the (rarer) substep form. A keeps step identity everywhere; B
keeps the inline shape literal.

## Mechanism (same under either answer — no fold)

- **parser** nests substeps under their `if` step from `Indent` (structural, parse-time —
  it already reads `Indent`); `%plan.steps%` becomes a tree, BuildStep recurses.
- **LLM Compile** emits inline branch bodies as the condition's `child` directly — change
  `Compile.llm` + `BuildResponse` to carry `child`, drop the trailing-peer form.

Nothing re-nests a flat list. Structure is born from the parser (indent) + LLM (inline).

## My lean

**(A) `Child : step.list`.** Reasons: (1) it's the model you already shipped and the
runtime/reader/serializer are all built on `step.list`; (2) it keeps a single uniform
"a branch body is a sequence of steps" everywhere, so `Child.Run` is one code path;
(3) the synthetic-step-wrap happens **at build time in the compiler** (its literal job is
to emit `.pr` structure), which is categorically different from the runtime-fold Ingi
rejected. The inline body just gets a step with `Synthetic=true` and one action.

But (B) is defensible if you'd rather the wire mirror the source literally (inline body =
action, never a fabricated step). Your call — it decides the `Child` type, the reader,
the serializer's `child` element kind, and the Compile prompt shape.

**One question back:** steps (A) or actions (B)?

— coder
