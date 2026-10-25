# When a step fails

This page says how a build tells you something is wrong, and what to change. Every output below was copied from a run, some trimmed.

## The build did not start

| You see | It means | Change |
|---|---|---|
| `NoAppFound (400): No app found at <folder>…` | The folder has no `.build/app.pr` and no one could answer the create question. | Run `plang build --app.setting.create=true`, or build from the app's root folder. |
| `GoalNotBuilt (404): Goal /X.goal isn't built; run plang build` | You ran a goal that was never built. | Run `plang build`. |
| `PrFormatOutdated (400)` | The `.pr` came from an older builder. | Rebuild the goal. |

## The build stopped on a goal

```
Building goal: Start
  Build error in Start: 403 Forbidden: {"detail":{"error_type":"authentication_error","message":"Must supply an API key! Check your request and try again."}}
HttpError (403): …
  at /system/builder/BuildGoal/Decide.goal:22
    - llm.decider State=%state%, Question=%questions%, write to %answer%
```

`Build error in <goal>:` is the goal's build failing. The line starting `at` names the builder's own step that failed, not yours. Here it is the model service refusing the request: no key. Set `TYPESAFE_API_KEY` and build again. See [install](install.md#the-key-a-build-needs).

Every build writes a trace to `.build/traces/<id>/<Goal>.json`. For a failed goal it holds a `buildError` with `key`, `message` and `goal`.

## A step could not be built

Add a vague third step to the goal in [your first goal](first-goal.md), `- make the data feel nicer`, and build (output trimmed):

```
Building goal: Start
  Properties rejected — retrying: step 2 ("make the data feel nicer") — step 2: %person% isn't in the step — use only the step's variables
  Saved Start (12.1s)
StepsBroken (400): the build left goals with steps it could not fix
  - step 2 ("make the data feel nicer") — step 2: %person% isn't in the step — use only the step's variables (in /Start.goal)
  at /system/builder/Build.goal:22
    - error.throw(Message="the build left goals with steps it could not fix", Key="StepsBroken", Status=400, Cause=%brokenGoals%, FixSuggestion="fix the words and build again")
  Fix: fix the words and build again
```

The build exits 1. Read it in three parts:

- `Properties rejected — retrying:` is a retry. The builder tries again before it gives up.
- `StepsBroken (400)` lists each step it could not fix, by its number (counted from 0) and its text. The `at` line names the builder's own step, not yours.
- `Fix: fix the words and build again` is the whole instruction.

The step count starts at 0, so `step 2` is the third step. The message gives the reason in the builder's terms. Here it is that the model used variables the sentence does not mention.

Running the goal after that build does not run a half-built program. It answers `GoalBroken (400)` and names the step:

```
GoalBroken (400): goal '/Start.goal' has 1 broken step(s): 2
  - step 2 ("make the data feel nicer") is broken
  in /Start.goal
  Fix: fix the words and build again
```

What to change is the step's sentence. Name the action and the values, and use the variables you mean. Replace the step with `- write out %person.age%`, build, and it builds and runs (`Ada`, then `36`).

Not seen: which earlier steps a rebuild keeps. The builder defines `[≡]` lines for them, but none was printed.
