# architect → coder — Validate trilogy: module-owns-action releases first (Q1); changes stay builder, verdicts move (Q2)

Answers `to-architect-validate-trilogy.md`. Settled with Ingi 2026-07-28.

> **You own this.** Rulings settled; mechanics yours.

## Q1 — do NOT build Validate against the strings; module-owns-action is RELEASED as the prerequisite

You caught the spec assuming an unreleased future. `context.App.Module[Module][ActionName]` inside `action.Validate` is the exact string-double-resolve that module-owns-action kills — fresh code with a scheduled death, and every line of it would land on that migration's sweep list. Three of Validate's four checks (required rows, catalog existence, `IBuildValidatable`) are module-owns-action surfaces by that doc's own rulings. Validate is the element's natural first consumer.

**So: `module-owns-action.md` is hereby released from discussion-context — implement its core first, then Validate on top.** Core scope for this purpose:

- `action.Module` becomes the `module.@this` element (throwing getter, never nullable; every construction door sets it — the doc's Create chain: static ICreate door → `app.Module.Create(dict)` → `module.Create(dict)` → catalog `Create()`; each owner reads exactly its one dict key).
- `ActionName → Name` rides along (the sweep is shared; qualified face = `ToString()`; templates `{{ a.ActionName }} → {{ a.Name }}`; `module.@this.ToString() => Name` keeps `{{ a.Module }}` rendering unchanged).
- The pr reader's wire order contract + catalog-routed minting (Position back from the registry).
- Dispatch through the held element; public `GetCodeGenerated(action, ctx)` dies.
- Action members: `Handler`-property REJECTED (middleman) — consumers' questions become members (`Return` exists; `Capabilities`; build-validate). `action.Validate` then reads its own element and its own members.

Read the full doc — the site sweep, demolition list, and verifications are all there. Note the doc predates several later rulings; where they conflict, the later ruling wins (e.g., the wiring loops in its sweep are now CONDEMNED bridge sites per the wiring-snag answer; `[Debug]`-attribute vocabulary per the same; your `44bdb11d6` LLM-wire key alignment already landed part of the reader work — reconcile, don't redo).

Pipeline: node-lists (finish) → **module-owns-action core** → **Validate trilogy** → backref-pass.

`action.Validate` post-element:

```csharp
// action.Validate(context): pure verdicts, diagnostics onto this.Warning
//   catalog    → Module[Name] != null            (the held element — no registry strings)
//   required   → the catalog element's rows      (non-null, no [Default], not emitted)
//   validatable→ its own build-validate member   (module-owns-action ruling — no fresh reflection)
//   goalcall   → CLR-name reject / dotted-name reject (the non-repair branch)
```

## Q2 — the line is: changes stay builder; verdicts move

**If it changes the action, it is construction — builder's job. If it only judges the action, it is `Validate` — the node's job.** Validate runs AFTER construction, so it judges the completed action (it never re-flags a name the builder already repaired or a default that's now filled).

- **Stays in the fold/normalize path (construction):** `:557` `a.Default = …GetDefaults(…)` (fills what the LLM didn't emit — note `GetDefaults` joins the element migration, stays fold-side); `:600-611` goal.call name-repair (`SetValue` + warn — the documented pragmatic constraint that rejection degrades the LLM retry holds; the repair warns visibly).
- **Moves onto `action.Validate` (verdicts):** required-param presence, catalog existence, `IBuildValidatable`, goal.call CLR-name reject (`:583`), goal.call dotted-name reject (`:612`).
- Both moments may write `Warning` — the channel is the node's diagnostics regardless of which moment writes.
- `NormalizeParameterTypes` stays put (normalize, pre-validate) as you said; `BuildResponse.Validate` bridges to recovery as already ruled.

Builder end-state: fold/normalize/repair → `goal.Validate(context)` → react to verdicts (FixValidation / abort). Checking is the node's; reacting is the builder's.
