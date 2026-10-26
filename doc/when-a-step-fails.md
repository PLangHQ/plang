# When a step fails

This page says how a build tells you something is wrong, and what to change. Every output below was copied from a run, some trimmed. A build can also succeed with a step that means the wrong thing; see [a vague step does not always fail the build](#a-vague-step-does-not-always-fail-the-build).

## The build did not start

| You see | It means | Change |
|---|---|---|
| `NoAppFound (400): No app found at <folder>…` | The folder has no `.build/app.pr` and no one could answer the create question. | Run `plang build --app.setting.create=true`, or build from the app's root folder. |
| `GoalNotBuilt (404): Goal /X.goal isn't built; run plang build` | You ran a goal that was never built. | Run `plang build`. |
| `PrFormatOutdated (400)` | The `.pr` came from an older builder. | Rebuild the goal. |

## The build stopped on a goal

```
Building goal: Start
Building needs a TypeSafe key for the decider. Paste it:
  Build error in Start: Building needs a TypeSafe key for the decider, and no one gave one: set %!llm.decider.setting.key% or the TYPESAFE_API_KEY environment variable.
MissingDeciderKey (401): Building needs a TypeSafe key for the decider, and no one gave one: set %!llm.decider.setting.key% or the TYPESAFE_API_KEY environment variable.
  - Channel 'input' has no interactive answerer (stream EOF)
  at /system/builder/BuildGoal/Decide.goal:22
    - llm.decider State=%state%, Question=%questions%, write to %answer%
```

`Build error in <goal>:` is the goal's build failing. The line starting `at` names the builder's own step that failed, not yours. Here the key is missing and no one answered the question. Give the key (see [install](install.md#the-key-a-build-needs)) and build again.

Every build writes a trace to `.build/traces/<id>/<Goal>.json`. For a failed goal it holds a `buildError` with `key`, `message` and `goal`.

## A step could not be built

A step the builder cannot make into an action fails the build. This is what one such build printed, for a vague third step `- make the data feel nicer` added to the goal in [your first goal](first-goal.md) (output trimmed):

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

The build exited 1. Read it in three parts:

- `Properties rejected — retrying:` is a retry. The builder tries again before it gives up.
- `StepsBroken (400)` lists each step it could not fix, by its number (counted from 0) and its text. The `at` line names the builder's own step, not yours.
- `Fix: fix the words and build again` is the whole instruction.

The step count starts at 0, so `step 2` is the third step. The message gives the reason in the builder's terms. Here it is that the model used variables the sentence does not mention.

Running the goal after that build did not run a half-built program. It answered `GoalBroken (400)` and named the step:

```
GoalBroken (400): goal '/Start.goal' has 1 broken step(s): 2
  - step 2 ("make the data feel nicer") is broken
  in /Start.goal
  Fix: fix the words and build again
```

## A vague step does not always fail the build

The same step did not fail every time. A later build of `- make the data feel nicer`, and a build of `- make it better`, both ended `Saved Start` with exit 0. The goal then ran: the first printed the step's own text as output, the second stopped with `[ServiceError] NullReferenceException: Object reference not set to an instance of an object.`

So a build that says `Saved` does not prove the step does what you meant. Run the goal and check its output. When a run does something other than your words say, or stops with an error you did not expect, treat the step as the cause and change its sentence.

## What to change

The sentence of the step. Name the action and the values, and use the variables you mean. Replacing the vague step with `- write out %person.age%` and building again built and ran (`Ada`, then `36`) every time it was tried.

Not seen: which earlier steps a rebuild keeps. The builder defines `[≡]` lines for them, but none was printed.
