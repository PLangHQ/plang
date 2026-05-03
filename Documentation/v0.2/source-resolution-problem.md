# Source Resolution Problem

**Branch:** `runtime2-source-resolution`
**Parent:** `runtime2-callstack` @ `f662b0d7`
**Date opened:** 2026-05-03

## TL;DR

The PLang builder is itself written in PLang. When the builder reads
another goal's source, that source contains literal `%var%` references
(things like `%goal%`, `%subGoal%`, `%trace.subGoals%`, `%currentPass%`).
The runtime treats every string as a candidate for `%var%` substitution
and expands those references into the values of variables in the
*builder's* current scope. The result is a recursively-expanded, often
multi-megabyte LLM prompt that blows past context limits.

There is currently no mechanism in the runtime to say "this string is
opaque source code, not a substitution target." Fixing that is the goal
of this branch.

## How we got here

While diagnosing a separate LLM hallucination (`Actor: "this?(user|service|system)"`
baked into `os/system/builder/.build/buildgoal.pr`), we restarted a
builder rebuild and watched the LlmFixer call exceed 348,965 input
tokens — for a goal whose first-pass user message had been ~2.6 KB.

Capturing the LLM input via `--debug={"llm":{"system":true,"user":true},"maxLength":200000}`
showed nine LLM calls, with sizes (in chars) of:

```
block 0:   2,672   (BuildGoal first pass)
block 1:     304
block 2:     642
block 3:   5,190   (BuildGoalCore first pass)
block 4:   1,121
block 5:   1,460
block 6:     877   (BuildSubGoal first pass)
block 7: 279,981   (LlmFixer retry — 280 KB!)
block 8:   1,064
```

Block 7 is the fixer call. Looking at its content showed the goal
source for `BuildSubGoal` rendered into the prompt — but with embedded
`%subGoal%` references *expanded* to the entire `Goal.ToString()` of
the sub-goal being built, and `%currentPass%` expanded to the giant
JSON of the BuildGoalCore response that had just completed.

## Tracing the explosion

For `set %y% = "hello %name%"` today, current code stores `"hello world"`
in `%y%` (verified at `App/modules/variable/Set.Action.g.cs:29`,
`App/Data/this.cs` `AsCanonical`, `App/Variables/this.cs` `Resolve`).
That's the substitution at the boundary that should be fine.

The problem is that the substitution is applied *every time* a value
flows through an action's parameter binding, and substitutions compound
across the chain:

1. `render template` stores `%goalForLlm%` containing literal
   `- set %goal% = %subGoal%` (rendered template output preserves
   the literal `%var%` from `step.Text`).

2. `set %fixerMessages% = [..., Content:"...%goalForLlm%..."]` runs
   `variable.set`. Its `Value` parameter is bound through `AsCanonical`,
   which calls `WalkContainerVars` → `SubstitutePrimitive` over the dict.
   For `Content`'s string, it does one `Variables.Resolve` pass —
   `%goalForLlm%` is replaced by its stored string. The result has
   `%goal%`, `%subGoal%`, `%trace.subGoals%` literal, AND it's stored
   into `%fixerMessages%`.

3. `llm.query` reads `%fixerMessages%`. `Messages` parameter binding
   does its OWN `WalkContainerVars` pass — and now sees the embedded
   `%var%` references that step 2 unintentionally surfaced. Substitutes
   them too. `%subGoal%` becomes a multi-line goal text via
   `Goal.ToString()` (`App/Goals/Goal/this.cs:59`). `%currentPass%`
   becomes a giant pass JSON.

Each parameter binding does a single Regex.Replace pass (no internal
recursion), but two passes back-to-back on data carrying its own
`%var%` references compounds into the observed blow-up.

## The architectural insight

> The PLang builder is written in PLang, so when it reads source it
> thinks it should resolve `%var%`. The fundamental issue lies there.

Every `%var%` substitution in the runtime assumes the string is a
PLang expression in *the current scope*. A string that's *source code
being processed* (file content read by the builder, output of `render
template`, content fields of LLM messages) carries its own `%var%`
references that must remain literal — they're another goal's
identifiers, not the builder's.

There is no signal in the type system today that distinguishes
"opaque source data" from "expression-in-current-scope."

## Two design directions

### Option 1 — Single resolution boundary (deeper)

Today, every action's parameter binding walks its value and substitutes
`%var%`. Plumbing actions (`variable.set`, `list.add`, `render template`)
substitute on read AND store the substituted value. Consumer actions
(`llm.query`, `output.write`, `file.save`) substitute on read again.
A value flowing through three actions can be substituted three times.

Single-boundary rule:

- **Storage is always raw text.** `variable.set` stores the literal
  value with `%var%` references intact. No walk on write.
- **Substitution fires exactly once, at the consume boundary** — when
  a leaf handler that interprets the value as a payload (LLM message,
  file content, output line, HTTP body) reads its parameter.
- Plumbing handlers operate on raw form. `set %y% = %x%` aliases or
  copies `%x%`'s raw value into `%y%`; the embedded `%var%` references
  travel with it untouched.

What this gives:

- `render template` stores `%goalForLlm%` raw.
- `set %messages% = [..., Content:"...%goalForLlm%..."]` doesn't walk —
  `%messages%` stores Content with `%goalForLlm%` literal too.
- `llm.query` is a consume boundary — walks ONCE. `%goalForLlm%`
  expands to the rendered template; that text contains `%subGoal%`
  etc. but the single pass doesn't recurse, so they stay literal.
- LLM sees the source code as written. No 280k blowup.

What it costs:

- Need to classify actions as "plumbing" vs "consume" (or invert: tag
  the few consume points). Today every action goes through the same
  generator-emitted resolution.
- Some current behavior changes — `set %y% = "hello %name%"` would
  store `"hello %name%"` and each *reader* resolves at its boundary.
  Probably what we want, but it's a semantic shift.
- Variable mutation timing changes — if `%name%` changes between
  set and read, the read sees the new value. (Today it's frozen
  at set time.)

### Option 2 — Source-typed string (`tsource` / opt-in opaqueness)

Mint a new PLang type — `tsource` (or similar) — that means
"literal source, never resolved." The runtime's `SubstitutePrimitive`
checks the wrapping `Data`'s declared `Type` and skips
`Variables.Resolve` when it's the source type.

Two paths produce `tsource` values today by intent:

- `render template` (the rendered output is "what the LLM should see")
- `file.read` of a `.goal` file or any source-as-data load

`file.read` mints `Data<bytes>` / `Data<string>` today; it would mint
`Data<tsource>` when the caller asks for source data. `render template`
already produces a string with `Type` available — would tag it.

Consumers downstream (`llm.query`, `output.write`) read whatever they
read; the type just tells `SubstitutePrimitive` to leave the string
alone. No semantic change to existing flows.

What this gives:

- Localized, opt-in fix. The producers that mint source-as-data
  declare it explicitly.
- No change to current substitution semantics for non-source values.
- Type system carries the signal — discoverable, testable.

What it costs:

- Doesn't fully solve the *general* PLang-builds-PLang problem. If a
  future action emits source-shaped data without minting `tsource`,
  the bug recurs. Less disciplined than Option 1.
- Requires every relevant producer to opt in.

### Verdict

**Ingi's preference (in conversation): Option 2.** Lower-risk,
narrower change, compatible with existing call sites.

Option 1 remains worth exploring as a longer-term cleanup; the
single-boundary model aligns the runtime with how PLang code itself
should read (resolve at use, not at store). But the scope is large
and the semantics shift could surface surprises across the corpus.

This branch starts with Option 2.

## What's already on this branch

Three pieces of work were carried over from the diagnosis session
(commit pending). They're band-aids that point at the same problem:

1. **`PLang/App/modules/builder/validateResponse.cs`** — added a
   value-to-type convertibility check after the existing structural
   validation. Loops through `BuildResponse`'s parameters, runs
   `TypeMapping.TryConvertTo` on each `(value, declared-type)`, and
   surfaces a clear message to LlmFixer when the LLM emits a value
   that can't be coerced to its declared type. Also normalizes
   LLM-emitted `""` → `null` for nullable parameter slots
   (`IsNullableSchemaProp` helper).

2. **`PLang/App/modules/builder/providers/DefaultBuilderProvider.cs`**
   — same `""` → `null` normalization in `NormalizeParameterTypes`,
   which is what BuildStep detail-passes go through (they bypass
   `validateResponse`). Both pipelines now treat `""` on a nullable
   slot the same way.

3. **`os/system/builder/llm/BuildGoal.llm`** + **`BuildStep.llm`** —
   prompt rule additions instructing the LLM to never emit `""` as a
   placeholder; omit nullable slots entirely or emit `null`.

   Plus hand-edits to `os/system/builder/.build/buildgoal.pr` and
   `buildstep.pr` to clear two persisted `Actor` slots that had
   captured the original `"this?(user|service|system)"` schema-
   description hallucination from a long-ago build.

These don't *solve* the source-resolution problem — they patch the
symptom (LLM-emitted `""` for a constrained-type slot) and the
audit trail (corrupted persisted values). The real fix is whichever
option above we land on.

`Documentation/v0.2/todos.md` also gained a new entry on a build-time
variable type registry — orthogonal but adjacent: when the builder
walks parameters it could record `%var%` → declared-type mappings and
feed them into subsequent step prompts so the LLM has typed-variable
context.

## Suggested next steps

1. **Reproduce the 280k blowup deterministically.** A C# test that
   sets up a Variables instance with a string value containing nested
   `%var%` references, then traces how many times `Resolve` fires
   along a representative chain (`render template` → `variable.set` →
   `llm.query`). This gives a regression target before changing the
   model.

2. **Pick the design.** Option 2 unless something surfaces in step 1
   that argues against it.

3. **Implementation seed for Option 2:**
   - Define `tsource` in `App/Utils/TypeMapping.cs` (alongside
     `tstring`). Map to `string` CLR-side.
   - Update `App/Catalog/this.cs` so the type is documented in the
     LLM-facing schema block.
   - In `App/Data/this.cs` `SubstitutePrimitive` (and the partial-
     match branch in `AsCanonical`), check the wrapping Data's `Type`
     and short-circuit when it's `tsource`.
   - Tag `render template`'s output Data with `Type = "tsource"`.
   - For `file.read`, decide whether to opt-in via a parameter
     (`AsSource=true`) or keep file content as `tstring` and let
     callers re-tag if they want source semantics.

4. **Verify with builder rebuild.** From `os/system/builder/`:
   ```bash
   plang '--build={"files":["Build.goal","BuildGoal.goal","BuildStep.goal","ApplyStep.goal","ValidateBuildResponse.goal"]}'
   ```
   The fixer-call user message should never exceed double-digit-KB.

5. **Backstop with a runtime guardrail** — `llm.query` could refuse
   to send a request whose total prompt exceeds a configurable cap
   (default ~32 KB?), so that the next regression of this kind fails
   loudly at the boundary instead of inside the model API.

## Adjacent issues spotted along the way

- `BuildStep.goal` does not pipe its detail-pass response through any
  fixer. Step 14 (`builder.validate`) runs and a failure throws
  immediately. Worth wrapping in a fixer-style retry, or at least
  surfacing the same convertibility errors `validateResponse` does.

- The `Actor` parameter on `goal.call` has no real consumer in any
  goal we found — `app.run` already handles actor switching. Keeping
  it on `goal.call` was kept (per Ingi's call) but it's the slot that
  the LLM keeps hallucinating into. Worth revisiting when this branch
  is closed.

- The codeanalyzer's findings on `runtime2-callstack` (commit
  `bdfa1ab7`) are unaddressed — five MINORs documented at
  `.bot/runtime2-callstack/codeanalyzer/v1/result.md`. Independent of
  this branch but in the same neighborhood.
