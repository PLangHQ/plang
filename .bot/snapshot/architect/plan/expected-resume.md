# Expected: the resume stage, a snapshot restored as an app of its own

What I expect the coder to write. It is shown to Ingi, not sent to the coder, and compared when the coder reports. It covers decisions 160, 166 and 167. Members not quoted from code are marked NEW.

## The goal step

```plang
- resume %snapshot%, write to %app%               / waits: the restored app's run goes to its end (or its next stop), then %app% is the app
- resume %snapshot%, don't wait, write to %app%   / %app% is a task<app>; the app goes on
```

A failed resumed run answers its error, all the way up (167), as a failed `call` does.

## The code

```csharp
// module/snapshot/resume.cs
/// <summary>Runs on its own ("don't wait"): the resume answers a task at once, the app going on. Left out, the
/// restored run goes to its end, and the resume answers the app.</summary>
public partial data.@this<global::app.type.item.parallel.@this>? Parallel { get; init; }     // NEW, as goal.call's (call.cs:34)

public async Task<data.@this> Start()
{
    if (await Refusal() is { } refused) return refused;                    // as now: unsigned, changed, another app's
    if (await Snapshot.Value() is not { } snapshot) return …;               // as now
    var parallel = Parallel != null && await Parallel.ToBooleanAsync();     // as goal.call (call.cs:84)
    return parallel
        ? Context.Ok(Context.Actor!.Task.Start(…, token => snapshot.Resume(Context, token)))   // a task<app>, as call.cs:94
        : await snapshot.Resume(Context);                                    // the app, or the run's error
}

// snapshot/this.Resume.cs: the snapshot restored as a sub app of the resumer's (166)
public async Task<data.@this> Resume(actor.context.@this context, CancellationToken token = default)
{
    var app = new global::app.@this(context.App.filesystem.root.Absolute, context.Actor!);   // as test/list/this.cs:185
    var started = await Start(app, context);              // every part into the sub app (was: Start(context.App, …))
    if (!started.Success) return started;
    var ran = await <the restored actor's context, the one whose stack holds where the run stopped>.call.Resume(…);
    return ran.Success ? context.Ok(app) : ran;           // the app, or its error all the way up (167)
}
```

## Checks

- The asker's app is never written. Its variables, settings and call stack are as they were before the resume. Today's `Start(context.App, …)` goes.
- The sub app is made by the resuming actor through the existing door, with no new way to make an app. It ends with its maker.
- "Don't wait" is goal.call's own option and task, so nothing new is asked of plang.
- The saved run goes on in the restored actor's context: the actor the snapshot was saved from, found by its name in the sub app.
- A failed run answers its error. Its `%!error.callback%` is that run's snapshot, so it can be resumed again after a fix.
- The surface: `resume`'s new `Parallel` is a catalog change (a plang-visible parameter), and it is re-pinned citing 160.

## Tests

- **Plang `FreshAppGoesOn`, rewritten.**
  - A run that resumes its own snapshot meets its own `resume` step again inside the restored app, under any "whole run" rule. So the test guards with what isn't in the snapshot: a marker file written after the save, before the resume. In the restored app, the saved Start's rest sees the marker and ends.
  - It asserts the restored app's state through `%app%`, and that the asker's own variables are unchanged.
- **Waited:** `%app%` holds the app after the run's end.
- **Don't wait:** `%app%` is a task, and waiting on it gives the app.
- **Failure:** a resumed run that fails answers its error, the resume step's `on error` runs, and the error's callback is the failed run's snapshot.
- **The asker untouched:** a variable the asker set before the resume holds the same value after it.
- **C#:** two resumes of one snapshot are two apps, neither seeing the other's writes.

## Open, for the coder to trace and say before building

- Which restored actor's stack holds where the run stopped, and how the resume finds it in the sub app: by its name.
- What `Task.Start`'s first argument is for a resume, since goal.call names a goal: the stopped run's root goal, or another overload.
- Who disposes the sub app: its maker's end, or the variable that holds it.
