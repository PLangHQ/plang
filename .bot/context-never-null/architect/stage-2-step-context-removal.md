# Stage 2: Delete Step.Context

**Goal:** Remove the `Step.Context` field entirely. The disabled state it backed becomes context-parameterized, so per-execution state is reached by passing the running context, not by stashing it on a shared build entity.
**Scope:** Mechanism D. Included: `Step.Context`, `Step.Disabled`, the AnchorScope save/restore of Step.Context, the steps-collection stamps. Excluded: everything else (this stage is self-contained).
**Deliverables:**
- `goal/steps/step/this.cs` — delete the `Context` field (`:16`). Replace the `Disabled` property (`:24-38`) with `Disabled(context)` (query), `Disable(context)` / `Enable(context)` (mutation). Same key `step:{Goal?.PrPath}:{Index}:disabled`, same context data bag.
- `actor/context/this.cs` — `AnchorScope` keeps `Step = action.Step` and `Goal`/`Event` save-restore, but drops the `Step.Context = this` set (`:278`), the `_previousStepContext` field (`:290`), its capture (`:299`), and its restore (`:307`).
- `goal/steps/this.cs` — drop the `step.Context = Context` / `_items[i].Context = context` stamps (`:53,128`); pass the local context to the new `Disabled(context)` / `Disable`/`Enable(context)` calls (`:55-58,129`).
**Dependencies:** None structural — context was always passable here. Can run in parallel with Stage 1; sequence after it to keep diffs clean.

## Design

`Step.Context`'s only consumer is `Step.Disabled`, and every caller of `Disabled` already holds the running context as a local one line above (`steps/this.cs:53,128`). The field was pure choreography — stash, read back, save/restore per dispatch. Passing the context to the step is the OBP-clean shape: per-execution state keyed by step identity, reached by handing over the whole context.

Names: drop the `Is`/`Set` verb+noun. `Disabled(context)` reads as `if (step.Disabled(context))`; `Disable`/`Enable` are real-work verbs. The one bool-driven set site (`steps/this.cs:129`, `_items[i].Disabled = disabled`) branches at the call: `if (disabled) step.Disable(context); else step.Enable(context);`.

After this stage, re-grep `action.Step?.Context` / `step.Context` to confirm no reader survived (the trace says none does).

Full detail: `plan/step-context.md`.

## You own this

Final method names and whether the disabled state reads better on `Step` or on the steps collection are yours. The contract: `Step` holds no `Context` field, and the names are not verb+noun.
