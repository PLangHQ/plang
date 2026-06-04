# Builder → coder handoff: the 5 "planner-blocked" goals are a runtime render bug

From: builder. Re your `builder/handoff.md` — the 5 blocked LazyDeserialize goals are
**not** a planner-robustness problem. They're one runtime bug: **LLM response JSON is
being run through variable rendering when it shouldn't be.** Confirmed by repro + debug.
This is a C# (runtime) fix — out of my lane — so it's back to you. Diagnosis below.

## The bug in one line

A `%...%` token that appears *inside an `llm.query` result* gets interpolated when that
result is materialized / stored (`write to %x%` → `variable.set type=json`). The LLM
response is **data** (the developer's source code), not a template — its `%var%` tokens
must stay verbatim.

## Why it looked non-deterministic / quoted-JSON-related (it isn't)

It only corrupts when the token resolves to a **live object** at build time:

- bare `%!error%` → resolves to the ambient error object → injected mid-string → JSON
  breaks → the value collapses to `{valuekind:Object}` (1 key, no navigable children).
- `%!error.Message%` (scalar → empty string) and plain `%x%` (unbound → left as-is) both
  survive **by accident** — that's the whole illusion of flakiness.

So `assert %x% is not null` builds; `assert %!error% is not null` never does. Every one of
the 5 blocked goals contains a `CaughtIt`-style step that reads bare `%!error%`.

## Repro (deterministic, 5/5)

```bash
cat > Tests/LazyDeserialize/_probe.goal <<'EOF'
Start
- assert %!error% is not null
EOF
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang '--build={"files":["LazyDeserialize/_probe.goal"]}'
# → BuilderPlannerFailed first (planner stage), or with description removed:
# → ValidationErrors: "Step[0]: no actions" (compiler stage)
```

`- throw %!error%` reproduces identically on a true rebuild (its existing green `.pr`
under `os/system/builder/**/.build` is just a stale goal-level cache — see cache note).

## It hits TWO stages, same mechanism

1. **Planner** (`BuildGoal/Plan.goal` → `llm.query ... write to %plan%`). The model echoes
   the step's `%!error%` into the free-text `description` field; rendering it on store
   breaks the plan JSON → `%plan.steps%` is null → `Validate.goal` throws
   `"Planner returned %plan.steps.Count% step plans..."` (note: the message's own
   `%plan.steps.Count%` is unrendered because `plan.steps` is null — that's the tell).
   - *Partial mitigation possible builder-side* (drop `description` from the planner
     schema so there's no echo vector) — but **do not rely on it**, because stage 2 is
     unavoidable.

2. **Compiler** (`BuildStep/Start.goal` → `QueryAndVerify` → `llm.query ... write to
   %compileResult%`). The compiled action's parameter *value* legitimately **is**
   `%!error%` (that's the developer's code). RawResponse is perfectly valid JSON:
   ```json
   {"formal":"assert.isNotNull(Value=%!error%)",
    "actions":[{"module":"assert","action":"isNotNull",
      "parameters":[{"name":"Value","value":"%!error%","type":{"name":"object"}}]}],
    "confidence":"High","explanation":"","errors":[]}
   ```
   After `write to %compileResult%` (type=json) it collapses to `{valuekind:Object}` (1
   key) → `%compileResult.actions%` is empty → goalsSave fails "Step[0]: no actions".
   **No prompt/.goal change can fix this** — the stored value has to stay verbatim.

## Where to look (runtime)

The render happens during materialization of the lazy json result you added this branch.
`%!data%` is *already* `{valuekind:Object}` coming out of `llm.query` — i.e. the
corruption is at result-construction / lazy-materialize time, before the `variable.set`
even runs. Candidates:
- the lazy `Wire.Read` / `FromRaw` materialize path that re-resolves `%var%` on touch
- `data.@this<object>.From(...)` of the OpenAi result (`OpenAi.cs`) and/or the
  `type=json` set path that scans string content for `%...%`.

Design intent (Ingi, this session): **an llm.query result must not be interpolated — its
`%...%` tokens are payload, not variables.** Probably scope the "resolve `%var%` in string
values" pass so it never runs over an llm result / a value flagged as raw LLM output.

A good acceptance test once fixed: the repro above builds green, and so does
`Tests/LazyDeserialize/TamperedSignedData_FailsVerify` /
`NavigationOnTypeUnknown_AsksForAsType` (both have `CaughtIt: assert %!error% is not null`).

## ⚠️ Separate cache bug — clear, don't fix (per Ingi)

`--build={"cache":false}` does **not** bust the LLM cache — `%!build.cache%` stays `True`
and `compileResult.Cached=True` regardless. So you'll get stale green results while
testing. **To re-test for real, delete the `LlmCache` rows** from the relevant settings
sqlite before each run:

- builder's own cache: `os/system/.db/system.sqlite`
- when building under `Tests/`: `Tests/.db/system.sqlite`
- table name: `LlmCache` (see `PLang/app/module/llm/code/OpenAi.cs:36`)

```bash
sqlite3 Tests/.db/system.sqlite "DELETE FROM LlmCache;"
sqlite3 os/system/.db/system.sqlite "DELETE FROM LlmCache;"
```

Ingi: notify only — the `cache=false`-not-propagating bug is a known separate issue, not
for this pass.

## State

No builder changes landed. I made + reverted a probe edit (dropping `description` from the
planner schema) — repo is clean (`git status`: only the pre-existing untracked
`Tests/Snapshot/`). The 5 blocked test goals are unchanged and correct as written; they'll
go green once the render-on-materialize is scoped off the llm result.
