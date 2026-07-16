# Parity-gate methodology ruling — (A) as the enforced gate, plus the free slice of (B) as reference evidence

Answer to `coder/to-architect.md` (the 4d parity methodology fork), settled with Ingi 2026-07-16. Your deviation flag was right to raise and is accepted — with one addition that costs almost nothing.

> **You own this.** Test shapes yours; the ruling is the methodology.

## The ruling

1. **(A) is the enforced gate — both halves:**
   - **Param-desc parity, C#-level:** for every catalog action+param, `Describe()`'s desc string equals the row-composed desc (`Type` face + `?` if Nullable + `= x` if Default + `%var%` if IsVariable), read directly in C#, never through Fluid. This is STRONGER than a whole-string diff — it names exactly which param drifted, and it targets the named risk (the `?`/`= x` reconstruction). Runs with the **named-exception list** (the `"this"` → `clr<goal>` re-pins; every exception line-itemed, nothing waved through in bulk).
   - **New-template snapshot golden** over the pinned catalog cases (module WITH prose, WITHOUT, `[Code]` action, choice param, nullable, `[Default]` — and add one host-typed param so the `clr<goal>` face is pinned deliberately).
2. **The (B) slice — one real `plang build` BEFORE the swap, as reference evidence, nearly free:** the builder already records the prompts — `Plan.goal:20-21` stashes `%plan.system%`/`%plan.user%`, `Start.goal:32` adds the compile user message to `%trace.stepPasses%`. Pull `%actionSummary%`/`%actionDetails%` content out of the trace of one Sanity build and save the strings under `coder/v2/` as REFERENCE artifacts — not byte-diffed gates, the human-inspectable "what the LLM actually saw" for the final read after the swap. This answers your own "real departure" concern with evidence instead of wording.
3. **(C) is rejected** — spending unknown time resurrecting deleted shapes (`clr<StepActions>` + data-valued params through the door) solely to snapshot them is wasted motion; the shapes' inability to render in isolation is not a gap in the gate, it's the stage working.

## Also settled by your trace (fold into 4e's demolition)

- **`v2/summary.md` and v1 `summary.md` are DEAD** (no render caller) — delete at 4e; the earlier "verify who renders v2/summary.md" item closes as "nobody."
- The live template set is exactly two: `summary.planner.md` (Plan) + `stepActionDetails.template` (Compile) — the 4d rewrite scope is those two, per the tree/filtered-list ruling.

## Acceptance restated (so the plan's gate line matches reality)

The 4d gate = param-desc parity green (zero unexplained diffs) + the new-template snapshot golden pinned + the pre-swap trace reference saved and eyeballed after the swap. Then 4e proceeds.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| per-param C# equality | the gate tests the FACT (desc reconstruction), not a rendering pipeline | ok |
| named-exception list | intentional deltas line-itemed; no bulk waivers | ok |
| trace-sourced reference | evidence from the live system's own records; nothing resurrected | ok |
| dead templates deleted, not rewritten | two live templates define 4d's scope | ok |
