# snapshot — coder v3: stage 5, `snapshot.save` and the proof in plang

Expected first (written before building, per snapshot 29). Stage 5 of `.bot/snapshot/architect/plan/start.md:37`.

## What a plang developer writes

```plang
Start
- set %name% = "Alice"
- call Sub city="Reykjavik"              / a call's parameter: %city% lives in Sub's goal frame

Sub
- foreach %items%, call Each item=%it%   / not in this proof: going on mid-items waits for question 3
- save snapshot, write to %snapshot%     / NEW: snapshot.save answers the run's state as a value
- save %snapshot% to 'run.snapshot'      / file.save: the developer's step, not the verb's
- write out "%name% in %city%"           / the step a resumed run goes on to
```

…in a fresh app:

```plang
- read file 'run.snapshot', write to %snapshot%
- resume %snapshot%                      / stays `resume` until Ingi's question 5
```

## The action (expected)

`PLang/app/module/snapshot/save.cs`:

```csharp
/// <summary>The run's state as it is now, as a value: the app and every part under it, the asker's calls among them
/// (decision 1's walk). Saving it is the developer's own step (a file, a db row). Refused inside a task
/// (SnapshotInTask): a task's frames go on in the task's own stack.</summary>
[Action("save", Cacheable = false)]
public partial class save : IContext
{
    public Task<data.@this<global::app.snapshot.@this>> Start()
        => System.Threading.Tasks.Task.FromResult(Context.App.Snapshot(Context));
}
```

- Returns `Data<snapshot>` (catalog: `→ returns snapshot`). There are no parameters.
- Teaching: `os/system/modules/snapshot/save.{description,examples}.md`. The module description changes from "Resume a captured snapshot" to the module's two verbs.
- This is a module action at the edge, not the locked core (the module registry gains an action, not a surface member). The decider fixtures and the snapshot module page re-pin (the teaching-edit rule).

## What resume does with it (as built, said out loud)

The save happens inside `snapshot.save`'s own action frame, so the stored `current` is that frame. Resume ends each frame outward and runs its `Rest`. The current frame's Rest runs its own action **again** (`again: true`, the rule an ask's resume needs). So a resumed run first runs `save snapshot` again, binding a new %snapshot% (the resumed state), and then goes on to the next step (`save %snapshot% to 'run.snapshot'` writes the file again, then `write out`).

- Harmless for the proof, but it's a behaviour a developer sees: the file is written again on resume.
- OPEN-A: should `snapshot.save`'s frame resume from the action **after** it (the save is done; only an ask re-runs to take its answer)? That would be a virtual on the action ("goes on again" vs "goes on after"), and the action's surface is locked. Lean: keep it as built for stage 5 and name it in the test.

## The fresh app (the open part)

Nothing in plang makes an app. Each test already runs in an App of its own (`test/list/this.cs:179`).

- (a) **A committed `run.snapshot` beside the resume test** (as a test keeps its llm answers beside it). The save test writes and checks the shape; the resume test reads the committed file in its own, fresh app and resumes. Real fresh app, no new door. The file pins goal hashes, so a changed test goal needs it written again (by the save test, documented as with `.test.llm.json`). The two halves are separate tests.
- (b) The test goal saves and writes, then runs `test.start` on one discovered helper goal, which runs in an App of its own and reads and resumes. One test, but it nests a test session inside a test, and I haven't checked that a session tolerates it.
- (c) A new plang door that makes a sub app. That's the core's (app), and Ingi's.
- Lean (a).

## The tests (expected, at `test/plan/snapshot/module/snapshot/…`, the paths `test/plan/snapshot/start.goal` names)

`resume/FreshAppGoesOn`:
- **saved state:** Start sets %name%, calls Sub with `city`. Sub, at step N, does `save snapshot` and writes run.snapshot.
- **fresh app:** read the file, then `resume %snapshot%`.
- **asserted in the resumed run, after the save step:**
  - `%name%` is "Alice" (the context's variables);
  - `%city%` is "Reykjavik" (the goal frame's parameter);
  - the resumed run goes on at step N+1, not step 0 (a marker variable set only by step N+1, and step 0 not run again: a counter set before the save is unchanged);
  - on return, Start's post-call step runs (the frames unwind).
- **A loop's %item%:** a separate assertion that the binding frame comes back. C# already proves it (CallStackSnapshotTests through text). In plang: save inside `Each`, the foreach's callee goal, and assert %item% after the resume, without going on to the next items.

Stage 1–4 index entries (`save/EachPartAtItsPlace`, `resume/StartsInTheSubGoal`, …) stay as they are: stage 5 only.

## Not in this stage

- Going on mid-items in a loop (question 3).
- The resume verb's name (question 5).
- The three unbuilt ask tests in `test/plan/snapshot/module/callback/` move to the callback plan's list, unbuilt (start.md:37). I'll move them in the stage's commit.

## OPEN for the architect

- **A:** resume runs `snapshot.save` again, as built. Keep it for now?
- **B:** the fresh-app road: (a), (b) or (c)?
