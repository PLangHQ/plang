## architect — bugs 39 — 2026-10-25
**Target:** /CLAUDE.md, "Running plang Tests"
**Why:** stage 1 of bugs (decisions 4–8) made a test's LLM answers come from a file beside it, so `plang --test` no longer calls OpenAI on every run. Bots still follow the old habit, "LLM tests run only at the gate", and nothing in CLAUDE.md says the answers file is committed and must not be hand-edited or recorded by accident.
**Proposed change:** add after the stale-binary section:

```markdown
### A test's LLM answers
- A test that asks the llm reads its answers from a file beside it (`X.test.goal` → `X.test.llm.json`), committed with the test. A question not in it is asked live once and added; a passing run keeps the file to what it asked. So `plang --test` runs the llm tests with no key and no network, on every commit.
- A changed prompt is a new question: run the test once with a key to record it, and commit the file with the change.
- The gate runs them once live, `--test.setting.llm=live`, which asks OpenAI and writes nothing: it checks OpenAI still takes the requests.
- The file keeps the request body and the response, never a header (so never a key). It is written by the run, not by hand.
```
