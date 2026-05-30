# Proposal: Pre-parsed `%variables%` in .pr files

> **Architect note (2026-05-30):** Reviewed and reframed. The perf/span direction below is a **no-go** — the builder does no `%var%` parsing today, so the spans are not free to harvest and storing them creates a second parser plus a sync obligation. The piping instinct underneath it is the real idea, but it is not about storage: the builder compiles natural-language value-transforms into navigation expressions. See `architect/plan.md` for the verdict and the live direction. This file is kept as the original input.

**Status:** superseded — see `architect/plan.md`
**Branch:** `prevars-in-pr`
**Author:** Ingi (drafted with Claude)

## Summary

Today the runtime discovers `%variables%` inside step parameter strings by
scanning at execution time — every `Data.As<T>()` that crosses a string-shaped
parameter has to find `%...%` spans, look them up in the variable store, and
substitute. The builder already walks every parameter string while producing
the .pr (it has to, in order to validate names, types, and write targets), so
the runtime is repeating work the builder already did.

This proposal: have the builder emit a `variables: [...]` block per step
(or per parameter), recording exactly where each `%var%` reference sits and
which variable it resolves to. The runtime then stops scanning strings on the
hot path — it just walks the prebuilt list.

This is a pure perf + clarity refactor. No new surface area for the PLang
author, no change to step text, no change to what's legal in a step.

## Motivation

1. **Runtime perf.** Variable resolution happens on every step that reads a
   parameter containing `%...%`. Today that's regex/scanner work per call.
   For tight loops (`foreach`, data pipelines) it's measurable.
2. **Single source of truth.** Right now the builder *knows* the variable
   spans (it validated them) but discards that knowledge; the runtime
   rediscovers it. That's a smell — two parsers for the same surface,
   with the runtime parser being the one that ships to every user.
3. **Foundation for later work.** Variable pipes
   (`%name | upper | encrypt(twofish)%`) need a structured representation
   anyway. Landing the storage shape first means the pipe work is purely
   additive: more fields on each variable entry, same .pr location.
4. **Debuggability.** `--debug` can show "this step reads `%name%` and
   `%order.total%`" without inferring it from raw text.

## Proposed .pr shape

Per-step block, sibling to existing `parameters` / `return`:

```jsonc
{
  "Number": 3,
  "Text": "write %user.name% to %file%",
  "Module": "output",
  "Action": "write",
  "Parameters": { "content": "%user.name%", "target": "%file%" },
  "Variables": [
    {
      "Name": "user.name",     // canonical lookup key
      "Raw": "%user.name%",    // exact source span (for substitution)
      "ParamPath": "content",  // which parameter this lives in
      "Offset": 0,             // byte offset within the parameter string
      "Length": 11
    },
    {
      "Name": "file",
      "Raw": "%file%",
      "ParamPath": "target",
      "Offset": 0,
      "Length": 6
    }
  ]
}
```

Open questions for the architect:

- **Per-step vs per-parameter.** Above is per-step with `ParamPath` —
  simpler to iterate, one list per step. Alternative: nest under each
  parameter. Per-step wins for the "what does this step read" question;
  per-parameter wins for "give me the variables in *this* parameter."
  Lean per-step.
- **Bare-name parameters.** `Variable.set` and friends take a bare name
  (no `%`) as a write target. Those already flow through
  `IRawNameResolvable` / `Data<Variable>` and shouldn't appear in
  `Variables[]` — they're not reads. Keep this list **reads only**.
- **Code parameters.** `[Code] T` properties (lazy expressions) can
  contain `%var%` references too. Same shape applies; the
  `ParamPath` just points at a code-typed property. The code evaluator
  already knows it has lazy semantics, so it can either consume the
  prebuilt entries or keep scanning — both are fine.
- **Nested object parameters.** If a parameter is a JSON object/array,
  `ParamPath` becomes a dotted path (`headers.Authorization`). Same
  shape, just longer keys.

## Runtime changes

- `Data.As<T>()` on a string-typed slot: if the owning action has a
  `Variables[]` entry for this `ParamPath`, walk that list and splice
  values in. Else fall through to the current scanner (covers any
  back-compat .pr files and any case the builder couldn't analyze).
- Variable lookup itself is unchanged — same store, same name semantics.
- No change to `IRawNameResolvable` / `Data<Variable>` path; that's a
  different code path (bare-name, not span-substitution).

## Builder changes

- The validation pass that currently walks parameter strings to check
  variable names emits a list as it goes.
- Source generator: no change. This is .pr metadata, not action shape.
- LLM compile prompt: no change. The LLM still emits parameter strings
  with `%var%` syntax; the *post*-LLM validation step is what records
  the spans.

## Compatibility

- Old .pr files (no `Variables[]` block) keep working — runtime falls
  back to scan. New .pr files skip the scan.
- No author-visible change. Step text is identical.
- Rebuild produces new shape; no migration needed.

## Out of scope (deliberately)

- **Variable pipes** (`%name | upper | encrypt(twofish)%`). Real idea,
  separate proposal. Lands cleanly on top of this once `Variables[]`
  exists — pipe stages become additional fields on each entry.
- **Type narrowing.** Could record the inferred type of each variable
  reference. Useful, but separable — does not block this proposal.
- **Removing the runtime scanner.** Keep the fallback for at least one
  release cycle so stale .pr files don't break.

## Risk

Low. The work is mechanical: move what the builder already computes
into a serialized list, and teach the runtime to prefer it. The
fallback path means a bug in the prebuilt list degrades to current
behavior, not a hard failure.

## Rough sizing

- Builder: emit `Variables[]` during existing variable-validation walk.
- .pr schema: one new optional field per step.
- Runtime: one new code path in `Data.As<T>` (or wherever span
  substitution lives today), guarded by "do we have prebuilt entries
  for this param."
- Tests: round-trip (build → inspect .pr → run → same result),
  one perf microbench on a `foreach`-heavy goal.
