# When a step fails

This page says how a build tells you something is wrong, and what to change. Only the first three failures below were seen running. The step-level lines come from the builder's output template and are unverified.

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

Unverified, from the output template. These lines need a live build:

- `Step <n> build error: <message>` means the builder could not turn that step into an action.
- `Planner validation failed: … — retrying...` and `Properties rejected — retrying: …` are retries, not failures yet.
- `Fix: <text>` after a `Build error` is a suggested change.

What to change is the step's sentence. Say which action and which values you mean, then run `plang build` again. Unverified: that this fixes a given step, and that steps you did not change are kept (`[≡]`) so only the changed one is sent to the model.
