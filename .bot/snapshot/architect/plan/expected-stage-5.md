# Expected: stage 5, `snapshot.save`

What I expect the coder to write, before its plan (Ingi's rule: shown to Ingi, not to the coder, and compared when the coder reports).

## The action

`PLang/app/module/snapshot/save.cs`, NEW, beside `resume.cs` (`[Action("resume", Cacheable = false)]`, `partial class resume : IContext`):

```csharp
namespace app.module.snapshot;

/// <summary>PLang's <c>- save snapshot, write to %snap%</c>: the run as it stands, as a value. Saving it somewhere is the
/// developer's next step.</summary>
[Action("save", Cacheable = false)]
public partial class save : IContext
{
    public Task<data.@this<global::app.snapshot.@this>> Start()
    {
        return Task.FromResult(Context.App.Snapshot(Context));
    }
}
```

- No parameters. It answers `data<snapshot>`, the catalog's `→ returns snapshot`.
- It goes through `app.Snapshot(context)` (`app/this.Snapshot.cs:10`), the one door. Not through `IContextSnapshotExtensions.Snapshot(handler)`: that is a helper on the handler for what the app owns (a stray helper), and the branch should remove it if nothing else needs it.
- Its refusals are the app's: SnapshotInTask, SnapshotWhileTaskRuns, GoalHasNoAddress.
- Teaching in `os/system/modules/snapshot/save.{description,examples}.md`, which re-runs the decider fixtures (the teaching-edit rule).

## The proof in plang

`test/plan/snapshot/module/snapshot/…`:
- a goal sets variables, calls a sub goal with parameters, and in it saves and writes the snapshot to a file;
- a fresh app reads the file and resumes it;
- the resumed run has the same variables and the call's parameters, goes on at the step after the save, and returns through the caller.

## The point I expect to come back

On resume, the stopped frame is the save's own action, and `Rest` runs it again (`again`). So a resumed run sees `save` answer again, now with a snapshot of the resumed run, and then goes on: continuation semantics. That makes a goal like this write the file a second time after a resume:

```
- save snapshot, write to %snap%
- write %snap% to /run.json
```

A program that wants to tell "just saved" from "resumed" has nothing to ask yet. That belongs to Ingi's question 5 (the verb) and the callback plan, and the coder should raise it, not solve it.
