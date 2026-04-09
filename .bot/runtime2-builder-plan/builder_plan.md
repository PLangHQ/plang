# PLang Builder — Architecture & Hardening Plan

> Living document. Last updated: 2026-04-09

## Why This Matters

The builder is the compiler for PLang. It translates natural language `.goal` files into structured `.pr` files via LLM. Every PLang program passes through it. If the builder is unreliable, PLang is unreliable. This document captures the plan for making it rock-solid.

---

## Part 1: Pipeline Redesign

### Current State

The builder uses a two-pass LLM strategy:

```
Pass 1: BuildGoal (whole-goal)
  → LLM sees compact action summary + all steps in the goal
  → Returns module/action/parameters per step
  → Flags needsDetail: true on steps it couldn't fully construct

Pass 2: BuildStep (per-step detail)
  → For each needsDetail step, one-by-one
  → LLM gets full parameter schema for the identified module
  → Returns complete parameters
```

Validation happens in `ApplyStep`: `ValidateActions` checks if module.action exists in the registry. Failed validation triggers a BuildStep retry with error context.

### Problems

1. **`needsDetail` is binary** — The LLM either says "I got it" or "I didn't." There's no middle ground. A step where the LLM is 60% confident gets treated the same as one where it's 99% confident.

2. **No confidence signal** — We have no way to know which steps the LLM struggled with. Steps that pass but are subtly wrong look the same as steps that are perfectly correct.

3. **No batching** — Each `needsDetail` step gets its own LLM call. If 5 steps in the same module need detail, that's 5 separate calls, each loading the same module documentation.

4. **Validation failures handled one-at-a-time** — A step that fails validation goes back through BuildStep alone. If multiple steps failed for related reasons (e.g., all using the wrong parameter name for the same module), each gets a separate retry with no shared learning.

### New Design: Formalization → Actions → Self-Assessment

The new pipeline has three distinct phases per step, produced in a single LLM pass:

#### Phase 1: Formalization (before actions)

The LLM first translates the natural language step into a **structured pseudo-syntax** that makes modules, data flow, and variable assignments explicit.

```
Step text:  "read file.txt and write it into %content%"
Formalized: ["file.read file.txt", "variable.set %content%"]

Step text:  "if %count% is greater than 5, call HandleOverflow"
Formalized: ["condition.if %count% > 5", "goal.call HandleOverflow"]

Step text:  "get http://api.example.com/users and set %users%"
Formalized: ["http.get http://api.example.com/users", "variable.set %users%"]
```

`formalized` is an array where each entry is one module operation. The array order is the pipeline — each entry’s output (available as `%__data__%`) flows into the next. The 1:1 mapping between `formalized` entries and `actions` makes validation straightforward. This is **structured chain-of-thought**: the LLM reasons about what the step means before committing to action parameters.

**Why formalization matters:**
- Forces the LLM to decompose multi-module steps explicitly
- Creates an evaluatable intermediate representation (we can check formalization independently of actions)
- Makes data flow visible — you can see exactly where each output goes
- Serves as human-readable documentation if stored in the `.pr` file

#### Phase 2: Actions (no more `return`)

Actions are built from the formalization. The key change: **`return` is removed from the action schema.** Setting a variable is just `variable.set` — there's no special "return" mechanism.

Today:
```json
{
  "actions": [{ "module": "file", "action": "read", "parameters": [...] }],
  "return": [{ "name": "%content%" }]
}
```

New:
```json
{
  "actions": [
    { "module": "file", "action": "read", "parameters": [{ "name": "path", "value": "file.txt" }] },
    { "module": "variable", "action": "set", "parameters": [{ "name": "name", "value": "%content%" }, { "name": "value", "value": "%__data__%" }] }
  ]
}
```

The formalization already showed this: `["file.read file.txt", "variable.set %content%"]`. Two entries, two actions, 1:1 mapping. No magic `return` property needed. No magic `return` property needed.

#### Phase 3: Self-Assessment (after actions)

After producing actions, the LLM assesses its own work with **both** a categorical level and a numeric confidence:

```json
{
  "steps": [
    {
      "index": 0,
      "formalized": ["file.read file.txt", "variable.set %content%"],
      "actions": [...],
      "level": "high",
      "confidence": 0.95
    },
    {
      "index": 1,
      "formalized": ["condition.if %count% > 5", "goal.call HandleOverflow"],
      "actions": [...],
      "level": "medium",
      "confidence": 0.7
    }
  ]
}
```

**Why both level and confidence?** LLMs are better at verbalizing assessment than producing calibrated numbers. A `"level": "low"` is a more honest signal than `"confidence": 0.3` — the LLM knows it's uncertain, but can't reliably quantify *how* uncertain. The level is the LLM's real judgment; the confidence number is the machine-readable approximation for thresholding and automation.

| Level | Meaning | Typical action |
|-------|---------|----------------|
| **high** | LLM is confident in module, action, and all parameters | Accept (if validation passes) |
| **medium** | Module/action likely right, but parameters may be incomplete or uncertain | Candidate for pass 2 |
| **low** | LLM is guessing — unclear which module, ambiguous step | Definitely needs pass 2 |

**Triage**

After pass 1 + validation, steps are categorized:

| Category | Condition | Action |
|----------|-----------|--------|
| **Done** | level=high AND passes validation | Accept as-is |
| **Needs refinement** | level=medium OR low | Send to pass 2 |
| **Validation failed** | ValidateActions returned error (any level) | Send to pass 2 with error context |

**Grouping for Pass 2**

Steps that need pass 2 are grouped by module. Each group gets a single LLM call with:
- Full parameter schema/documentation for that module (and related modules if steps span multiple)
- All steps in the group (including their formalization — the LLM already knows what it's trying to build)
- For validation-failed steps: the specific error messages

```
Group 1: [step 1, step 4] → both use file module → one LLM call with file module docs
Group 2: [step 3]         → uses condition module → one LLM call with condition module docs
Group 3: [step 2, step 5] → validation failures → one LLM call with error context + relevant module docs
```

**Why this is better:**
- **Formalization as CoT** — the LLM thinks before it acts, producing better actions
- **No `return`** — one less special case, variable assignment is always explicit
- **Level + confidence** — honest self-assessment the LLM can actually produce reliably
- **Fewer LLM calls** — batching by module
- **Better context per call** — LLM sees related steps together
- **Confidence feeds evals** — track which modules/patterns produce low confidence
- **Validation errors grouped** — the LLM can learn from one error to fix related ones

### Decisions

- **`formalized` is stored in the .pr file.** The `.pr` file is runtime-independent bytecode — like IL or JVM bytecode. The underlying runtime engine can be in any language (currently C#). The formalization is part of this portable contract: it tells any runtime what the step *means*, independent of implementation.

- **`%__data__%` is implicit.** The runtime always sets `%__data__%` after each module execution. The array order in `formalized` represents this flow — there is no explicit wiring needed. Every action can read `%__data__%` to get the previous action's result.

- **Grouping starts simple.** No merging of groups initially (low-confidence and validation-failed stay separate). Once evals are in place, we can experiment with merging and measure the impact.

- **Pass 2 failures get 2 retries, then build error.** On validation failure, the error feeds back to the LLM (onValidation). Two retry attempts max. If it still fails, surface as a build error — don't spiral.

- **Formalization syntax should be standardized.** Since .pr files are portable bytecode, the formalization syntax needs a defined grammar: `module.action [params] | module.action [params]`. This enables any runtime to parse and understand the formalization, and enables structural validation of the formalization itself.

### Remaining Open Questions

- **Formalization grammar**: Will emerge from real LLM output. Run the builder, collect examples, then formalize the grammar bottom-up from what the LLM actually produces. Don't define the spec before we have data.

### Additional Decisions

- **`level` and `confidence` are stored in the `.pr` file.** They are part of the build artifact, not transient metadata. This enables evals to analyze builder confidence patterns from existing `.pr` files without re-running the builder.

---

## Part 2: Eval Strategy

### What We're Building Now: Golden Eval Suite

A curated set of `.goal` files paired with human-verified `.pr` output. Build, compare, score. That's it.

**Structure:**
```
Tests/Builder/Evals/
  {pattern-name}.goal        → build → .build/{name}.pr → verify → save as .golden
```

### Eval Catalogue

Each eval is one `.goal` file testing one builder pattern. The step text column shows exactly what the PLang developer writes. The expected mapping column shows what the builder should produce.

#### Core: Variable Module

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 1 | `variable-set-string` | `set %name% = "hello"` | `variable.set` name=%name%, value="hello", type=string |
| 2 | `variable-set-number` | `set %count% = 42` | `variable.set` name=%count%, value=42, type=int |
| 3 | `variable-set-json` | `set %user% = {name:"john", age:20}` | `variable.set` name=%user%, value={...}, type=object |
| 4 | `variable-set-list` | `set %items% = ["a", "b", "c"]` | `variable.set` name=%items%, value=[...], type=list |

#### Core: Output Module

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 5 | `output-write` | `write out "hello world"` | `output.write` content="hello world" |
| 6 | `output-write-var` | `write out %message%` | `output.write` content=%message% |

#### Core: File Module

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 7 | `file-save` | `save "hello world" to file 'test.txt'` | `file.save` content="hello world", path=test.txt |
| 8 | `file-read` | `read file 'test.txt', write to %content%` | `file.read` path=test.txt → return %content% |
| 9 | `file-exists` | `check if file 'test.txt' exists, write to %info%` | `file.exists` path=test.txt → return %info% |
| 10 | `file-copy` | `copy file 'source.txt' to 'dest.txt'` | `file.copy` source=source.txt, destination=dest.txt |
| 11 | `file-move` | `move file 'old.txt' to 'new.txt'` | `file.move` source=old.txt, destination=new.txt |
| 12 | `file-delete` | `delete file 'test.txt'` | `file.delete` path=test.txt |
| 13 | `file-list` | `list files in '.', write to %files%` | `file.list` path=. → return %files% |

#### Core: Condition Module

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 14 | `condition-equals` | `if %x% == 5, call WhenEqual` | `condition.if` left=%x%, operator===, right=5, goalIfTrue=WhenEqual |
| 15 | `condition-greater` | `if %x% > 10, call WhenBig` | `condition.if` left=%x%, operator=>, right=10, goalIfTrue=WhenBig |
| 16 | `condition-contains` | `if %text% contains "hello", call Found` | `condition.if` left=%text%, operator=contains, right="hello", goalIfTrue=Found |
| 17 | `condition-else` | `if %x% > 5, call WhenBig, else call WhenSmall` | `condition.if` left=%x%, operator=>, right=5, goalIfTrue=WhenBig, goalIfFalse=WhenSmall |
| 18 | `condition-substeps` | `if %ready% is true` (with indented steps below) | `condition.if` left=%ready%, operator===, right=true (sub-steps execute via __condition__) |

#### Core: Goal Module

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 19 | `goal-call` | `call ProcessData` | `goal.call` name=ProcessData |
| 20 | `goal-call-return` | `call ComputeAnswer, write to %answer%` | `goal.call` name=ComputeAnswer → return %answer% |
| 21 | `goal-call-params` | `call SendEmail to=%email%, subject="hi"` | `goal.call` name=SendEmail, parameters=[to=%email%, subject="hi"] |

#### Core: Loop Module

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 22 | `loop-foreach` | `foreach %items%, call ProcessItem item=%item%` | `loop.foreach` list=%items%, goal=ProcessItem, item=%item% |
| 23 | `loop-foreach-empty` | `foreach %empty%, call DoThing item=%item%` | `loop.foreach` list=%empty%, goal=DoThing, item=%item% |

#### Core: Math Module

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 24 | `math-add` | `add 5 and 3, write to %sum%` | `math.add` a=5, b=3 → return %sum% |
| 25 | `math-divide` | `divide 10 by 4, write to %result%` | `math.divide` a=10, b=4 → return %result% |
| 26 | `math-round` | `round 3.14159 to 2 decimal places, write to %rounded%` | `math.round` value=3.14159, decimals=2 → return %rounded% |

#### Error Handling (Step Modifiers)

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 27 | `error-call` | `call DoWork, on error call HandleError` | `goal.call` name=DoWork + onError.goal=HandleError |
| 28 | `error-retry` | `call DoWork, on error retry 3 times` | `goal.call` name=DoWork + onError.retryCount=3 |
| 29 | `error-retry-call` | `call DoWork, on error retry 2 times, then call HandleError` | `goal.call` + onError.retryCount=2, onError.goal=HandleError |
| 30 | `error-ignore` | `call DoWork, on error ignore` | `goal.call` + onError.ignoreError=true |

#### Multilingual (Builder Must Handle Any Language)

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 31 | `error-icelandic` | `call DoWork, a villu kalla i HandleError` | `goal.call` + onError.goal=HandleError |
| 32 | `error-japanese` | `call DoWork, エラー時に HandleError を呼ぶ` | `goal.call` + onError.goal=HandleError |

#### Convert Module

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 33 | `convert-tojson` | `convert %data% to json, write to %json%` | `convert.toJson` value=%data% → return %json% |
| 34 | `convert-fromjson` | `convert %json% from json, write to %obj%` | `convert.fromJson` value=%json% → return %obj% |

#### List Module

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 35 | `list-add` | `add "item" to %myList%` | `list.add` list=%myList%, value="item" |
| 36 | `list-count` | `get count of %myList%, write to %total%` | `list.count` list=%myList% → return %total% |

#### Cache (Step Modifier)

| # | Eval name | Step text | Expected mapping |
|---|-----------|-----------|-----------------|
| 37 | `cache-simple` | `read file 'config.txt', cache for 5 minutes` | `file.read` + cache.durationSeconds=300 |
| 38 | `cache-sliding` | `read file 'config.txt', cache for 10 minutes sliding` | `file.read` + cache.durationSeconds=600, cache.sliding=true |

**Total: 38 eval cases** covering all current Runtime2 modules, error handling, caching, and multilingual support. The suite grows from failures — every builder bug we find becomes a new eval.

**How it works:**
1. Write a `.goal` file for a PLang pattern
2. Build it, manually verify the `.pr` output is correct, save as `.golden`
3. After any builder change, rebuild and compare field-by-field against `.golden`
4. Report what matched and what didn't

**What we compare per step:**
- Correct module?
- Correct action?
- Parameter names match?
- Parameter values match?
- `onError` present/correct when expected?
- `formalized` array makes sense? (once implemented)

**Metrics:**
- **Output Accuracy**: proportion of golden files where ALL fields correct
- **Field Accuracy**: proportion of individual fields correct (shows where the builder struggles)

**When to run:** After changing builder prompts, before switching LLMs, or when something feels off.

The suite grows over time — every failure we discover becomes a new golden test case.

### Future (When We Need It)

These are ideas for later, not now. Documented in detail in `.bot/runtime2-plang-test-gaps/coder/v1/builder-improvement-options.md`.

- **Structural validation expansion** — Expand `ValidateActions` to check parameter names/types, not just module.action existence
- **Consistency scoring** — Build the same `.goal` N times, measure how much the output varies. For evaluating new LLMs.
- **LLM-as-judge** — Second LLM validates the first LLM's output. For semantic checks the golden suite can't cover.

---

## Implementation Order

1. **This document** — Get alignment on the vision (done)
2. **Golden eval suite** — Write ~20-30 `.goal` files, verify `.pr` output, build comparison tool
3. **Formalization + return removal** — Add formalization phase to builder prompt, remove return from schema
4. **Pipeline redesign** — Replace needsDetail with level/confidence + grouping

---

## References

- Builder-improvement-options analysis: `.bot/runtime2-plang-test-gaps/coder/v1/builder-improvement-options.md`
- Builder architecture memory: `/home/claude/.claude/projects/-workspace-plang/memory/architecture_builder.md`
- Builder validation design: `.bot/runtime2-builder-validation/architect/v1/plan.md`
- STED framework (structured output consistency): arxiv.org/abs/2512.23712
- Cleanlab structured output benchmarks: cleanlab.ai/blog/structured-output-benchmark/
