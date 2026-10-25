# bugs — coder v1 plan

The plan is the architect's, placed at `test/plan/bugs/start.md` + `start.goal` (bugs 3–5). One stage at a time, red first.

## Stage 1 — a test's LLM answers come from a file (bugs 4, 5)

The file stands where OpenAI stands, not where the cache stands: LlmCache stays the one cache (in the test's memory store).

- `module/llm/code/IAnswer.cs`: `Task<data.@this> Ask(data.@this request, query action)`, where an llm request is answered.
- `module/llm/code/Live.cs`: shipped default. OpenAI itself: reads the endpoint and asks the key (moved from OpenAi.cs:67), sends through http.request.
- `module/llm/code/Saved.cs`: one test's answers, `X.test.llm.json` beside `X.test.goal`. A request in the file answers from it; a missing one is asked of Live and recorded. A streamed ask goes live. `Keep()` rewrites the file to the entries used (only on a pass, in saved mode).
- Chosen once in `test.Start` (test/this.cs): `test.setting.llm` (`saved | live`, default saved). Saved is registered as the test app's IAnswer; live leaves the shipped one.
- OpenAi.cs:228's send becomes `answer.Ask(body, action)`.
- Tests (red first): LlmAnswersFileTests — ANewQuestion_IsAskedOnce_ThenRead, AChangedPrompt_IsNotServedTheOldAnswer, TheKey_IsNeverInTheFile, TwoTestsInParallel_EachReadsItsOwnFile.
- Re-pin the surface golden naming bugs 4.
- Docs: the test docs say it's a `plang --test` feature; a streamed ask always goes live.
- Proof: the six llm tests run twice and once from another folder; varying bodies reported.

Stages 2–7 per the plan; N4's shape goes to the architect before coding.
