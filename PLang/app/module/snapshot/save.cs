namespace app.module.snapshot;

/// <summary>
/// PLang's <c>- save snapshot, write to %snapshot%</c> verb: the run's state as it is now, as a value — the app and
/// every part under it, the asker's calls among them. Keeping it (a file, a db row) is the developer's own step. A run
/// resumed from it goes on from this action, so the save answers again, with a snapshot of the resumed run. Inside a
/// task it is refused (<c>SnapshotInTask</c>): a task's frames go on in the task's own stack.
/// <para>The state is taken once, by the app (written in the snapshot's own view, held as those bytes, signed by the
/// app's own identity), so whatever carries it later — a file, a db row — carries exactly them, and a resume takes only a
/// snapshot this app signed.</para>
/// </summary>
[Action("save", Cacheable = false)]
public partial class save : IContext
{
    public System.Threading.Tasks.Task<data.@this<global::app.snapshot.@this>> Start() => Context.App.Snapshot(Context);
}
