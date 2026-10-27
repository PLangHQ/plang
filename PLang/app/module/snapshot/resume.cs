namespace app.module.snapshot;

/// <summary>
/// PLang's <c>- resume %snap%</c> verb. The parameter is declared
/// <c>Data&lt;snapshot.@this&gt;</c>, so the runtime converts whatever
/// <c>%snap%</c> holds — typically the wire bytes just read off disk — into the
/// snapshot object at the action boundary (the lazy-param <c>As&lt;T&gt;</c>
/// path → <c>snapshot.@this.Create</c>, which reads the wire through the plang
/// serializer). Read stays dumb; the type-driven rebuild is pulled here by
/// declaring the type.
///
/// <para>A run goes on only from a snapshot this app signed, as it was saved: one with no signature, one whose
/// signature doesn't verify (a changed value, a moved view), or one signed by an identity that is not one of the app's
/// own (another app's, or an archived one) is refused before anything is restored.</para>
///
/// <para>Every resume comes through here (<c>start %callback%</c> too), so every one is checked. The run goes on in an
/// app of its own, a sub app of the asker's (<see cref="global::app.snapshot.@this.Resume"/>), from the captured
/// position — deterministic, no live LLM — and the asker's own memory is never touched: the step answers what the run
/// answers.</para>
/// </summary>
[Action("resume", Cacheable = false)]
public partial class resume : IContext
{
    /// <summary>The snapshot to replay. Converted from the read bytes on access.</summary>
    [IsNotNull]
    public partial data.@this<global::app.snapshot.@this> Snapshot { get; init; }

    /// <summary>Runs on its own (<c>resume %snapshot%, don't wait</c>): the resume answers a <c>task</c> at once and the
    /// step goes on; the task's wait gives what the run answers. Left out, the run goes on to its end first.</summary>
    public partial data.@this<global::app.type.item.parallel.@this>? Parallel { get; init; }

    /// <summary>Resumes the snapshot once its signature says this app saved it as it is. A value that isn't one is the
    /// answer: the typed ask's own reason when it gave one, else that it holds none (a text its reader reads as nothing)
    /// and the step that reads a snapshot from text.</summary>
    public async Task<data.@this> Start()
    {
        if (await Refusal() is { } refused) return refused;
        if (await Snapshot.Value() is { } snap) return await snap.Resume(Context, Parallel);
        if (Snapshot.Error is { } error) return Context.Error(error);
        return Context.Error(new global::app.error.@this(
            $"%{Snapshot.Name}% holds no snapshot: read one where it is kept, in a .plang file (`read file 'run.plang', write to %{Snapshot.Name}%`)",
            "NoSnapshot", 400));
    }

    // Why the snapshot can't be resumed, or null when this app signed it as it is.
    private async Task<data.@this?> Refusal()
    {
        // what the step gives, as it is now: a %variable% is the signed Data it holds
        if ((await Snapshot.Follow(Context))?.Signature is not { } signature)
            return Context.Error(new global::app.error.@this(
                $"%{Snapshot.Name}% is not signed: a run goes on only from a snapshot this app saved (`save snapshot`), kept in a .plang file or a db row",
                "SnapshotUnsigned", 403));

        var verified = await new global::app.goal.step.action.composed.@this(
            new global::app.module.signing.verify(Context) { Data = Snapshot }, Context).Start(Context);
        if (!verified.Success)
            return Context.Error(new global::app.error.@this(
                $"%{Snapshot.Name}%'s signature doesn't hold: it was changed after it was signed. A change made on purpose is signed again",
                "SnapshotSignatureInvalid", 403) { list = verified.Error is { } why ? [why] : [] });

        var (identities, missing) = Context.App.Code.Get<global::app.module.identity.code.IIdentity>();
        if (identities == null) return Context.Error(missing!);
        // the app's identities: the one it signs as now (a sub app's is who made it, stored nowhere of its own) and every
        // other it holds that isn't archived (a rotated default still opens what it signed)
        var signing = await identities.GetOrCreateDefaultAsync(new global::app.module.identity.Get(Context));
        if (await signing.Value() is not { } current) return signing;
        if (current.Made(signature)) return null;
        var listed = await identities.ListAsync(new global::app.module.identity.list(Context));
        if (await listed.Value() is not { } own) return listed;
        if (own.Items().Any(identity => identity.Made(signature))) return null;
        return Context.Error(new global::app.error.@this(
            $"%{Snapshot.Name}% was signed by '{signature.Identity}', which is none of this app's identities: a snapshot goes on only in the app that saved it",
            "SnapshotNotTheApps", 403));
    }
}
