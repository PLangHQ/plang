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

## Slice 2: the programmer

Scratch apps `p1`, `p2`, `p3` were built with the decider key saved and run with `plang`. "Tests" are `cd test && plang --test <path>`; each passed with the counts given.

| # | Page | Claim | Check | Result |
|---|---|---|---|---|
| 40 | goals-and-steps | steps run top to bottom; `Start` first; `plang Name` runs `Name` | p1 output order; claim 7 | pass |
| 41 | goals-and-steps | the file counts as `Found 1 goals`, the rest build as `Building sub-goal:`, all in one `.build/start.pr` | p1 build output; `ls .build` | pass |
| 42 | goals-and-steps | `GoalUnreached` printed when a built step did not reach a goal | p2 first builds: `'Flaky'`/`'Early' … not reached` when their calls were mis-built | pass for that case; the plain "no step calls it" case not run |
| 43 | goals-and-steps | indented `on error` lines belong to the step | p2 step 5 built to `goal.call` + `on.error` | pass |
| 44 | goals-and-steps | `/ ` starts a comment | test goals begin with `/ ` lines and pass | pass (by test); not run in a scratch goal alone |
| 45 | variables | `set`, `%name%` in text, dict/list literals, `%person.name%`, `%person.langs[1]%`, `[i]` | p1 output `Hello Ada`, `Ada`, `is`, `en` | pass |
| 46 | variables | `%x% + 1` builds as `math.add` then `variable.set`, with a retry message | p3 build output; p3 `marks` counted | pass |
| 47 | variables | `add "alpha" to list %items%` | `test/module/variable/scoping` 1/1 | pass (by test) |
| 48 | variables | unset reads null; `is empty` true for unset, empty list, empty dict, missing field | `test/module/condition/if` 7/7 (`MissingIsEmpty`) | pass |
| 49 | variables | `%!data%` holds the last result; `write to` keeps it | p1 `.pr`: `variable.set` Value `%!data%`; p1 output `42` | pass |
| 50 | variables | scoping: called goal sees caller's variables; fresh names land with the caller; parameters end with the call; loop item ends with the loop | tests `module/variable/scoping` 1/1, `module/goal/call` 5/5, `module/loop/foreach` 4/4 | pass |
| 51 | variables | `%!error.message%` is the handled error's text | p2 output `caught: boom` | pass |
| 52 | conditions | `if %n% is 3, call Three`, `if %n% > 2`, only the true one runs | p1 `n is three`; p2 `n is big` | pass |
| 53 | conditions | `and`, `or`, `==` | `test/module/condition/compound` 2/2 | pass (by test) |
| 54 | conditions | `else call` on one line; indented body skipped when false | `test/module/condition/if` 7/7 (`elsebranch`, `substepsfalse`) | pass (by test) |
| 55 | conditions | `return` inside `if`: `early`, else `late` | p2 output `early` for `done=true`; `IfReturnReturns` passes | pass (the `late` branch by test only) |
| 56 | conditions | operator list is on the condition module's page | `os/system/modules/condition/start.md` exists; `module/condition/operator` 12/12 | pass |
| 57 | loops | `foreach %list%, call Say item=%item%` prints each | p1 `lang: en`, `lang: is` | pass |
| 58 | loops | body writes reach the caller; item ends; dict with item and key; empty list | `module/loop/foreach` 4/4 | pass (by test) |
| 59 | calling-goals | `call Greet`; `call Double n=21, write to %answer%` gives 42 | p1, p2 outputs | pass |
| 60 | calling-goals | `return "x"` answers the goal | p2 output `early` | pass |
| 61 | calling-goals | plain call waits; `Parallel=true` answers a task | `module/goal/call` 5/5 | pass (by test, action form); the words that make the builder pick `Parallel` not tried |
| 62 | calling-goals | a call built as `Name="Early done=true"` stopped the run with `GoalNotFound (404)`; renaming the variable and rebuilding fixed it | p2, two builds | pass (one occurrence; cause unknown) |
| 63 | errors | unhandled error prints key, status, step; exit 1 | p2 run 1 | pass |
| 64 | errors | `read missing.txt, on error 404, write out …` prints and goes on | p2 output `no such file` and later lines | pass |
| 65 | errors | separate `on error retry 2 times` and `on error ignore` lines: `%attempts%` = 3 | p2 output `attempts: 3` | pass |
| 66 | errors | both clauses on one line fail to build (`StepsBroken`, Order not given) | p2 first build | pass |
| 67 | errors | `module/on/error` has 14 passing tests (chains, GoalFirst, nested) | test run 14/14 | pass |
| 68 | events | `before action output.write call Mark` runs Mark before writes (`marks: 2`) | p3 output | pass; why 2 and not 3 not traced |
| 69 | events | `before each goal call X` fires for runtime goals too | p2 first run printed `before a goal` 3 times before `Greet ran` | pass as observed; cause unverified |
| 70 | events | the other bind forms | `os/system/modules/on/event.description.md`; `module/on/event` 3/3 | pass for the text and the tests; which forms the tests bind not checked |
| 71 | events | `on.unbind` removes a binding | `os/system/modules/on/unbind.description.md` only | unverified |
| 72 | how-a-step-becomes-an-action | step to action table | `jq` over `.build/start.pr` of p1, p2, p3 | pass |
| 73 | how-a-step-becomes-an-action | the `jq` line prints index, text and actions | ran it | pass |
| 74 | how-a-step-becomes-an-action | action form `module.action(Param=value)` with `;` | tests such as `PlainCallWaits.test.goal` pass | pass (by test) |
| 75 | how-a-step-becomes-an-action | four modules have a generated `start.md` | `ls os/system/modules/*/start.md`: condition, file, loop, screen | pass |
| 76 | how-a-step-becomes-an-action | a rebuild with nothing changed builds nothing | claim 18c | pass |

## After decider-key (runtime2 b4a549d1c)

Rebuilt the CLI on b4a549d1c, then ran in a fresh scratch folder with no saved key and no `TYPESAFE_API_KEY`. Rows 10 and 26 describe the 403 the CLI gave before this merge; install.md and when-a-step-fails.md now describe what follows.

| # | Page | Claim | Check | Result |
|---|---|---|---|---|
| 77 | install | a first build with no key asks `Building needs a TypeSafe key for the decider. Paste it:` | ran `plang build --app.setting.create=true` with the key on stdin; the prompt printed, build saved, `plang` printed `Ada` | pass |
| 78 | install | the key is saved and not asked again | ran `plang build` again with empty stdin: no prompt, `Found 1 goals` | pass |
| 79 | install | with no one to answer, `MissingDeciderKey (401)` naming both ways; nothing is sent | ran with empty stdin: output as quoted; no request is evidenced by the failure coming before any HTTP error | pass for the output; "nothing sent" is the architect's statement and was not traced |
| 80 | install | in a non-app folder, `plang build` with no flag and no stdin gives `NoAppFound` | ran it | pass |
| 81 | when-a-step-fails | the trace holds `buildError` with key `MissingDeciderKey` | read `.build/traces/*/Start.json` | pass |
| 10b | install | the 403 of rows 10 and 26 no longer occurs with no key | the runs above | pass (replaced) |

## Cold reader, run 1 (a fresh agent given only doc/)

The agent installed (`dotnet build`), proved the install (`plang`, `plang --version`), wrote a goal that reads a JSON file and prints a field (`Ada`), and built and ran it. It used all 4 allowed builds. It did not reproduce the failed build of rows 29-29c.

| # | Page | Claim | Check | Result |
|---|---|---|---|---|
| 82 | when-a-step-fails | `- make the data feel nicer` as a third step always fails the build with `StepsBroken` | the agent built it: exit 0, `Saved Start`, the step's own text printed | **fail**: not always; the page now says one build failed and later ones did not |
| 83 | when-a-step-fails | `- make it better` | built exit 0; `plang` printed `Ada`, then `[ServiceError] NullReferenceException` | pass (observed once; page says so) |
| 84 | when-a-step-fails | a build that says `Saved` may still hold a wrong step | rows 82, 83 | pass |
| 85 | install | the key prompt prints when the key is piped on standard input | the agent's build 1 and my run (row 77) | pass |
| 86 | first-goal | `.build/start.pr` and `.build/traces/` exist after a build | the agent listed with `ls -a` and saw `.build` and `.data` only, not inside | pass (rows 19, 20) |

## Slice 3

Scratch apps `s1` and `s2`, built with the key from stdin, run with `plang`. Local servers on 127.0.0.1 (a static file server and a small echo server) served the http checks.

| # | Page | Claim | Check | Result |
|---|---|---|---|---|
| 87 | goals-in-other-folders | `call /tools/Shout` runs `tools/Shout.goal`, prints `from tools`; `Found 2 goals`; saved `Name="/tools/Shout"` | s1 | pass |
| 88 | goals-in-other-folders | `call tools/Shout` saved `Name="Shout"` and the run gave `GoalNotFound (404): Goal 'Shout' not found.` | s1 first build and run | pass (one build; cause untraced) |
| 89 | goals-in-other-folders | `os/system/modules/goal/call.notes.md` allows `Folder/Goal` | read at that file | pass (the text); contradicted by 88 |
| 90 | goals-in-other-folders | the repo's `sub/SubGoal` test is switched off | `test/module/goal/relative/GoalCallRelative.test.goal2` | pass |
| 91 | goals-in-other-folders | `call %which%` picks the goal; `test/module/goal` 8/8 | s1 printed `picked`; test run | pass |
| 92 | reading-a-file-line-by-line | read, `split … into lines`, foreach prints each line plus an empty one for the final newline | s1 output `name: ` | pass |
| 93 | reading-a-file-line-by-line | `drop the empty ones` in the sentence wrote no `Empty` | s2 `.pr`: `list.split` with Value and Separator only; output kept the empty line | pass (as seen) |
| 94 | reading-a-file-line-by-line | `list.split(…, Empty="drop")` drops it | s2 rebuild output | pass |
| 95 | http | `get` a local JSON file, `%weather.city%` | s1 `city: Reykjavik` | pass |
| 96 | http | `post` with headers and a JSON body arrives as POST, `Authorization: Bearer abc`, body `{"x":1}` | s2 against echo server | pass |
| 97 | http | `http.download` is for `download` steps | `os/system/modules/http/request.notes.md` | pass (text); not run |
| 98 | http | a public address works | not tried | unverified |
| 99 | timeouts | `timeout after 100ms, on error …` prints `timed out` | s1 | pass |
| 100 | timeouts | retry gets a fresh deadline; 3 attempts | `test/module/on/timeout` 2/2 | pass (by test) |
| 101 | timeouts | other duration forms (`10 seconds`, `2 minutes`) | `os/system/modules/on/timeout.description.md` | pass (text); not run |
| 102 | caching | `cache for 5 minutes` returns `first` twice after the file changed | s1 | pass |
| 103 | caching | `test/module/on/cache` 2/2 pass; recovery is never cached | test run | pass (by test) |
| 104 | caching | key and sliding forms | `os/system/modules/on/cache.examples.md`; `test/module/on/cache/basic/ReadWithKey.goal` exists | pass (text); sliding not run |
| 105 | caching | expiry; cache across runs | not tried | unverified |
| 106 | databases | no `db` module exists | `ls PLang/app/module`: no `db`; `os/system/modules/db/` holds only `Builder/llm` | pass |
| 107 | databases | settings live in `.data/setting/data.sqlite` | `ls -R .data` in a scratch app | pass |
| 108 | databases | a goal can or cannot query that file | not tried | unverified |

## Cold reader, run 2 (a fresh agent given only doc/, slices 2 and 3)

Five tasks, 6 builds, key piped from a file and never printed. Local servers on 127.0.0.1.

| # | Page | Claim | Check | Result |
|---|---|---|---|---|
| 109 | goals-in-other-folders | `call /utils/Greet` runs a goal in a subfolder | agent task 1: `hello from utils` | pass |
| 110 | reading-a-file-line-by-line | the `list.split(…, Empty="drop")` form leaves no blank last line | agent task 2: three lines, no blank | pass |
| 111 | http | GET a JSON answer, read `%weather.city%` | agent task 3 | pass |
| 112 | http | POST sends the JSON body | agent task 3: body `{"hello":"world"}` arrived | pass |
| 113 | http | a header other than `Authorization` arrives | the test server reports only `Authorization`; the agent sent `X-Token` | unverified |
| 114 | timeouts | `timeout after 200ms, on error write out …` | agent task 4: `gave up waiting` | pass |
| 115 | caching | cached read returns the old value after the file changed | agent task 5: `first-value` twice, file held `second-value` | pass |
| 116 | first-goal | a build prints only the folder, the count, and `Saved` lines | agent saw also sub-goal lines, the key prompt and `Properties rejected` lines | **fail** as an implied limit; page now lists the extra lines |
| 117 | http | the page showed the echo server's fields as if PLang's | agent guessed `%reply.headers%` and got `VariableNotFound (404)` | **fail** (unclear); page now says the fields belong to the test server |
