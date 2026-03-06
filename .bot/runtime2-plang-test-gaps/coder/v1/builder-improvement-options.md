# Builder Consistency Framework — Options

## The Problem

The builder uses an LLM to convert PLang `.goal` syntax → `.pr` JSON. The LLM is inconsistent:
- Drops `onError` step properties silently
- Swaps parameter names (`Value`/`Container` in assert.contains)
- Same .goal input can produce different .pr output across builds

We need a framework that gives us confidence the builder is consistent, and lets us prove a new LLM meets the standard before switching.

---

## Option 1: Golden Test Suite (Recommended Starting Point)

**What:** A curated set of `.goal` files with known-correct `.pr` output. Every build run compares output against golden files.

**How it works:**
1. Create `Tests/Builder/` with one `.goal` per PLang pattern (on error call, on error retry, on error ignore, foreach, if/else, variable set, goal call with return, etc.)
2. For each, store a verified `.pr.expected` file — the correct output
3. After each build, diff the generated `.pr` against `.pr.expected`
4. Report: which patterns succeeded, which diverged, what specifically changed

**What it catches:** Parameter swaps, missing onError, wrong module/action mappings, missing return mappings.

**LLM switching:** Run the golden suite on the new LLM. If it passes 100%, switch. If not, you see exactly which patterns the new LLM fails on.

**Effort:** Low. We already have the test infrastructure. The golden files are the .pr files from working tests.

**Limitation:** Only catches patterns you've written tests for. Unknown patterns can still fail silently.

---

## Option 2: Schema Validator on .pr Output

**What:** A post-build validator that checks every generated .pr file against structural rules, independent of the LLM.

**Rules to validate:**
- If step text contains `on error`, the step MUST have an `onError` property
- If step text contains `write to %var%`, the action MUST have a `return` mapping
- Parameter names must match the handler's expected properties (check against a registry of module.action → expected params)
- `path` and `prPath` must include the correct subdirectory prefix
- `type` field values must be valid PLang types
- No duplicate step indices
- Goal name in .pr must match the goal name in .goal

**How it works:** Run after every build. Fail the build if validation fails. This catches the LLM silently dropping things.

**LLM switching:** Same validator runs regardless of LLM. If the new LLM passes validation, it's structurally correct.

**Effort:** Medium. Need to define the rules and write the validator. But it's deterministic — once written, it works forever.

**Limitation:** Validates structure, not semantics. Can't catch "the LLM mapped to the wrong module" unless you have a module registry.

---

## Option 3: Module Registry / Action Catalog

**What:** A machine-readable catalog of every module.action with its expected parameters, types, and return shape. The builder and validator use this as the source of truth.

**Example entry:**
```json
{
  "module": "assert",
  "action": "contains",
  "parameters": [
    {"name": "Value", "type": "object", "required": true, "semantics": "container"},
    {"name": "Container", "type": "object", "required": true, "semantics": "search-value"},
    {"name": "Message", "type": "string", "required": false}
  ]
}
```

**How it helps:**
- Builder prompt can include the exact parameter spec (no LLM guessing)
- Validator can check parameter names/types against the catalog
- When switching LLMs, the catalog is the contract — the LLM must produce output matching it

**Effort:** Medium-high. Need to extract the catalog from all handler .cs files (could be automated from source-generated records). But it's a one-time investment.

**Note:** This is partially what the source generator already does. The catalog could be generated from the same metadata.

---

## Option 4: Deterministic Post-Processing

**What:** Instead of relying on the LLM to get everything right, do a deterministic post-processing pass on the LLM output.

**Examples:**
- Scan step text for `on error ...` pattern → parse it deterministically → inject `onError` into the step JSON regardless of what the LLM produced
- Scan step text for `write to %var%` → inject `return` mapping
- Normalize parameter names against the module registry

**How it works:** LLM generates the best it can. Post-processor fixes known patterns. This separates "what the LLM is good at" (understanding intent, mapping to modules) from "what it's bad at" (consistent structural output).

**LLM switching:** Post-processor is LLM-independent. New LLM just needs to get the module/action right — the post-processor handles the rest.

**Effort:** Medium. Each pattern needs a parser. But patterns are finite and well-defined.

**Advantage:** Highest reliability. The LLM doesn't need to be perfect — just good enough at the core task (which module/action). Everything else is deterministic.

---

## Recommended Path

**Phase 1 (now):** Option 1 — Golden Test Suite. Cheapest, gives immediate visibility.
- Define one .goal per pattern
- Store verified .pr.expected files
- Run comparison after every build
- This gives you a "builder score" — 85/100 patterns correct, etc.

**Phase 2 (next):** Option 2 — Schema Validator. Catches the silent drops.
- `on error` in text → must have `onError` in .pr
- `write to` in text → must have `return` in action
- Run on every build, fail loudly

**Phase 3 (when switching LLMs):** Option 1 + 2 together form the acceptance test for any new LLM.

**Phase 4 (longer term):** Option 4 — Deterministic post-processing for the patterns the LLM consistently gets wrong. This is where you get to 99.99%.

Option 3 (module registry) supports all the others and could be built incrementally alongside them.

---

## Measuring Progress

Track a single number: **builder consistency rate**.

```
consistency = (golden tests passed) / (total golden tests) × 100
```

Start by measuring where you are today. Set targets:
- v0.1: establish baseline (probably 70-80%)
- v0.2: 90% (schema validator catches + fixes the obvious drops)
- v0.3: 95% (deterministic post-processing for known weak spots)
- v1.0: 99%+ (module registry + comprehensive golden suite)

When evaluating a new LLM: run the golden suite, compare the score. If it's higher, switch. If lower, don't.
