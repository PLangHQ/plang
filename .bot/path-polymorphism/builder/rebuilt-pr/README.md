# Rebuilt builder .pr snapshot

Output of a self-rebuild on `path-polymorphism` (after the runtime2 merge) — these are the actual `.pr` files emitted, **not hand-patched**. Use them as the evidence base for the regressions described in `../report.md`.

Folder structure mirrors the source so you can diff against the working hand-patched version on `runtime2` commit `1910f1bc5`:

```
rebuilt-pr/os/system/builder/.build/build.pr            ← orchestrator
rebuilt-pr/os/system/builder/.build/buildgoal.pr        ← Class 1 (slash-qualified)
rebuilt-pr/os/system/builder/BuildGoal/.build/start.pr  ← Class 1 (nested)
rebuilt-pr/os/system/builder/BuildGoal/.build/plan.pr   ← Class 1 (LlmFixer prPath)
rebuilt-pr/os/system/builder/BuildStep/.build/start.pr  ← Class 1 + Class 2 (builder.actions / write to)
...
```

## Reproduction recipe

```bash
cd os/
plang '--build={"cache":false,"files":["Build.goal","BuildGoal.goal","BuildGoal/Start.goal","BuildGoal/Plan.goal","BuildGoal/Validate.goal","BuildGoal/LlmFixer.goal","BuildStep/Start.goal","BuildStep/Validate.goal"]}' '--app={"create":true}' build
```

(The `--app=create` is a workaround for the inverted `File.Exists` check at `PLang/app/modules/builder/this.cs:110`, called out in `../report.md`.)

## What to grep for

- **Class 1 (prPath):** `"prPath": null` inside a `goal.call` GoalName value.
- **Class 1 (slash-name + prPath set):** `"name": "BuildGoal/Start"` or `"BuildStep/Start"` paired with a non-null `prPath` (the dispatcher would 404 these).
- **Class 2 (builder.actions):** in `BuildStep/.build/start.pr`, Compile step 1, the `builder.actions` action has `"parameters": []` and the trailing `variable.set` has `"value": "%planStep.actions%"` (should be `%!data%`).
- **Class 3 (json-string %var%):** absent from this snapshot run — that one is intermittent. Look for `variable.set` with `Type=json` where `Value` is a string starting with `[{` or `{`.

The audit script in `../report.md` flags all three classes in under 50 lines.
