# Verification

Each claim in `doc/`, how it was checked, and the result. `plang` is `PlangConsole/bin/Debug/net10.0/plang`, built from commit a4eb3f046. Scratch folders are outside the repo.

| # | Page | Claim | Check | Result |
|---|---|---|---|---|
| 1 | install | `dotnet build PlangConsole/PLangConsole.csproj -c Debug` builds the program to `PlangConsole/bin/Debug/net10.0/plang` | ran it (0 errors); `./dev.sh build` first made the binary | pass (incremental; a clean clone not run) |
| 2 | install | .NET 10 SDK is needed | `PlangConsole/PLangConsole.csproj:5` is `net10.0`; `dotnet --version` is 10.0.402 | pass |
| 3 | install | latest release is v0.1.17.3 from Feb 2025 and is the older generation | `gh api repos/PLangHQ/plang/releases/latest` gives tag `v0.1.17.3`, `2025-02-12` | pass for tag and date; the release was not run, so the page makes no claim about what it contains |
| 3b | install | `git clone https://github.com/PLangHQ/plang.git` | the repo's `origin` is that repo; not cloned fresh | unverified |
| 4 | install | `plang --version` is refused with `UnknownSetting` | ran it, exit 1, message as quoted | pass |
| 5 | install | `plang` in a folder with no build answers `GoalNotBuilt (404)` | ran it | pass |
| 6 | install | `plang exec` reads `exec` as a goal | ran it: `Goal /exec.goal isn't built`; `PLang/app/start/this.cs:37` | pass |
| 7 | install | `plang Name.goal` equals `plang Name` | ran both in a built folder, same output, exit 0 | pass |
| 8 | install | `ReadIntoIsADict.test` is refused as a goal name | ran it: `ArgumentRefused`; `PLang/app/start/this.cs:128-131` | pass |
| 9 | install | the decider is a separate service keyed by `TYPESAFE_API_KEY` or `llm.decider.setting.key` | `PLang/app/module/llm/decider.setting.cs:20-29`; `PLang/app/module/llm/code/TypeSafe.cs:28-41` | pass (source) |
| 10 | install | with no decider key the build fails 403 "Must supply an API key" | `OPENAI_API_KEY` set (valid, `curl` to OpenAI gave 200), `TYPESAFE_API_KEY` unset; ran `plang build`; output as quoted | pass |
| 11 | install | `OPENAI_API_KEY` does not unlock the build | same run as 10 | pass |
| 12 | install | `OPENAI_API_KEY` is read by run-time llm steps | `PLang/app/module/llm/setting/this.cs:19-22` | pass (source); no llm step run |
| 13 | install | how to get a `TYPESAFE_API_KEY`; build cost; first-build payment prompt | needs a live key | unverified |
| 14 | first-goal | first build with no `.build/app.pr` asks to create the app | `PLang/app/module/build/this.cs:44-66` | pass (source) |
| 15 | first-goal | with input redirected it stops `NoAppFound (400)` | ran `plang build` with no flag from a non-tty shell | pass |
| 16 | first-goal | `--app.setting.create=true` skips the question | ran `plang build --app.setting.create=true`, it went on to build | pass |
| 17 | first-goal | build prints `Building path:`, `Found N goals`, `Building goal:` | ran it | pass |
| 18 | first-goal | a good build ends `  Saved Start (3.4s)` | ran `plang build` with the decider key saved in the app's settings | pass |
| 18b | first-goal | `[✓]` / `[≡]` step lines | defined in `os/system/builder/templates/output/build-output.template`; not printed in any of 4 builds | unverified (page says they did not appear) |
| 18c | first-goal | rebuilding unchanged prints only `Building path:` and `Found N goals`, builds nothing | ran it | pass; "nothing charged" is inferred from no model call being made, not measured |
| 19 | first-goal | a build writes `.build/<goal>.pr` (lowercase) and `.build/traces/` | listed `.build` after builds: `.build/start.pr`, `.build/traces/<id>/Start.json` | pass (the trace from a failed build; `.pr` from the repo's built goals) |
| 20 | first-goal | the `Start.goal` here builds and `plang` prints `Ada` | built and ran in a scratch folder; `.build/start.pr` has `file.read` then `variable.set` for step 0; output `Ada`, exit 0 | pass |
| 20b | first-goal | a third step `write out %person.age%` prints `36` after `Ada` | rebuilt, ran: `Ada`, `36` | pass |
| 21 | first-goal | a built goal runs with no key, prints each `write out` line | copied `test/app/deepnavigation` (built) to scratch, ran `plang`: `found`, `2`, `3`, exit 0, `TYPESAFE_API_KEY` unset | pass |
| 22 | first-goal | a built `read config.json, write to %cfg%` then `%cfg.alpha%` reads the field | copied `test/plan/kind-is-type/app/type/json` to scratch; `plang ReadIntoIsADict.test.goal` exit 0 (its assert on `%cfg.alpha%` passed) | pass |
| 23 | first-goal | a JSON file reads into a dict | the test's `is dict` check ran with exit 0 | pass |
| 24 | first-goal | `PrFormatOutdated (400)` when the `.pr` is from an older builder | ran `plang` at the repo root: `/.build/start.pr: key 'description' isn't in this .pr format` | pass |
| 25 | when-a-step-fails | the three start-of-build failures and their changes | claims 5, 15, 16, 24 | pass |
| 26 | when-a-step-fails | `Build error in <goal>:` then the `HttpError` and the `at` line | output copied from the run in 10 | pass |
| 27 | when-a-step-fails | the `at` line names the builder's own step | `/system/builder/BuildGoal/Decide.goal:22` is in `os/`, not the user's folder | pass |
| 28 | when-a-step-fails | the trace holds `buildError` with `key`, `message`, `goal` | read `.build/traces/*/Start.json` after run 10 | pass |
| 29 | when-a-step-fails | a vague step gives `Properties rejected — retrying`, `StepsBroken (400)`, `Fix: fix the words and build again`, exit 1 | ran with `- make the data feel nicer`; output as quoted (trimmed) | pass |
| 29b | when-a-step-fails | running after that build gives `GoalBroken (400)` naming the step | ran `plang`, exit 1 | pass |
| 29c | when-a-step-fails | steps count from 0 (`step 2` is the third) | same run | pass |
| 30 | when-a-step-fails | replacing the step with `write out %person.age%` and rebuilding builds and runs | ran: `Ada`, `36` | pass |
| 30b | when-a-step-fails | which unchanged steps a rebuild keeps | no `[≡]` line printed | unverified |
| 30c | when-a-step-fails | `Step n build error`, `Planner validation failed` lines | template only; never printed | unverified |
| 31 | start | a run never builds; an unbuilt goal answers `GoalNotBuilt` | `PLang/app/goal/list/this.cs:60-63`; claim 5 | pass |
| 32 | start | the build output is `.pr` files in `.build/` | claim 19 | pass |
