# To architect — templates read compiled values through the wrong door

**Branch:** goal-graph-singular
**From:** coder
**Status:** design question — I keep producing bad fixes because I think this is a layering/ownership question, not a patch.

## The symptom (the "retry flake")

The builder compiles a step, then renders that step back to the LLM as feedback
(`stepForLlm.template`, rendered at `BuildStep/Start.goal:19`). If a compiled
action carries an authored value with a runtime variable — e.g.
`output.write` with `Data = "Hello %name%"` — the render **throws
`VariableNotFoundException`** on `%name%`.

`%name%` is a *runtime* variable. At **build** time it is unset. It is correct
that it is unset. The render should show `Hello %name%` **literally** — it is
describing the compiled step to the LLM, not executing it.

## Why it throws — the actual mechanism

The template hand-walks the compiled actions and prints each param value:

```
actions: [{% for a in step.Action %}...{% for p in a.Parameter %}
  {"name":"{{ p.Name }}","value":{{ p.Value }}}   <-- here
```

`{{ p.Value }}` makes Fluid navigate to the value and **render it through the
resolve door**. The value's type is `{name: text, template: plang}`. `template:
plang` means "this text contains a `%ref%` that must be resolved when read" —
so reading it tries to resolve `%name%` → throws.

The value is doing exactly what it was told. The bug is that the **template
reads it through the resolve door at all**. It wanted the *authored* form.

## We already have the right concept — it just isn't wired to templates

A value already serializes two ways, by **view**:

- `text.Output(View.Out)` — resolves `%refs%` (the execute door).
- `text.Output(View.Store)` — writes the **raw authored form** (`this.cs:135`),
  `%refs%` kept literal. This is what gets written to a `.pr`.

So "show the compiled value without executing it" is not a new feature — it's
**Store view**. The wire/`.pr` path already reads values this way. Templates
don't: the Fluid member-access door (`Fluid.cs` `PlangDoorAccessor.GetAsync`,
~line 249) **always** calls `.Value()` (resolve), regardless of the render's
view.

The `render` action now *carries* a `View` input (default `out`) and threads it
down to the accessor — but the accessor doesn't act on it yet (I left a NOTE
there rather than guess). The compiled-step feedback render should run in
**Store** view.

## Why my patches keep being bad — the layers that fight it

This is where I need a design decision, because every place I touch has a reason
it can't be the fix alone:

1. **Can't just Peek at the accessor.** I tried `view==Store ? Peek() :
   Value()`. It breaks: `.Value()` does *two* jobs — (a) materialise structure
   (parse a source, index a list so `step.Action[0].Parameter` can be navigated)
   and (b) resolve `%refs%`. Peek skips **both**, so navigation collapses to
   null ("Cannot bind a null to text"). Store view still needs the structural
   materialisation; it only wants to skip the `%ref%` resolution **at the leaf**.

2. **Can't cleanly do it at output either (Fluid's sync/async split).** The
   honest place is: navigate structurally, and at the leaf **output through
   `Output(view)`** (Store → raw). But Fluid resolves the value at *member
   access* (`GetValueAsync`, async) — *before* the `{{ }}` output stage ever
   runs. And Fluid's **logic** methods (`ToBooleanValue`, `ToStringValue`,
   `.size`, `where:`) are **sync** — they can't run the async plang resolution
   at all. The current code sidesteps that by resolving eagerly at access — the
   very thing that resolves too early.

   - Out-view **logic** genuinely needs resolution (`{% if %isAdmin% %}` on a
     `template:plang` value is only meaningful resolved).
   - Store-view **logic** is structural (presence / count / raw compare) — no
     resolution needed.
   - Only **output** needs the view-driven choice, and output is the one hook
     that IS async (`WriteToAsync`).

3. **The specific value here is peeked to a source.** Iterating `a.Parameter`
   hands the template each param's *value* (a source), so `p.Value` navigates
   *into* a source, which materialises → resolves. So even a leaf-output fix has
   to reckon with param rows arriving pre-peeked.

## The design question

**Where does "read this value in Store view" belong when the reader is a
template?** Options I can see, none of which I want to pick without your call:

- **(a) View branches the accessor.** Out view = resolve eagerly at access
  (unchanged). Store view = navigate structurally but return the leaf as an
  *unresolved carrier* whose async output uses `Output(Store)`; sync logic reads
  structure. Mirrors what `text.Output` already does on view — arguably not a
  fork but "the view selecting the door." Risk: two navigation behaviours by
  view.

- **(b) A view-aware `FluidValue` wrapper.** The accessor returns a custom
  `FluidValue` carrying (plang value + view). `WriteToAsync` → `Output(view)`.
  Sync logic methods read structure (store) / must resolve (out — the sync
  wall). One path, but the sync wall for out-view logic is unsolved.

- **(c) Split `.Value()`'s two jobs.** Give the value a door that materialises
  structure **without** resolving `%refs%` at the leaf, so navigation works and
  Store output is raw. This feels closest to the real root (Value conflates
  materialise + resolve), but it's a change to the value model, so it's yours to
  bless.

- **(d) Something upstream** — e.g. the param value shouldn't arrive to the
  template as a bare `template:plang` text at all; it should be a *parameter*
  shape the template reads structurally. (The user floated "action parameters
  should be a `parameter` plang type" — a description of the param, like `type`
  is — so reading it never touches `%var%`. I don't know if that's the intended
  end-state.)

## What I need from you

One decision: **which layer owns "read in Store view" for templates** — the
value model (split materialise/resolve), the Fluid door (view branch or wrapper),
or the shape of a compiled parameter. Once that's fixed I can wire
`view=store` on the feedback render and it's done. I have a deterministic repro
test staged (`RenderStoreViewTests`): Store-view render of `p.Value` must yield
`Hello %name%` literal without throwing; Out-view still throws on the unset ref.
