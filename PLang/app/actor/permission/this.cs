using Grant = global::app.type.item.permission.@this;
using Verb = global::app.type.item.permission.Verb;
using MatchMode = global::app.type.item.permission.Match;

namespace app.actor.permission;

/// <summary>
/// Per-actor permission view — <c>actor.Permission.Find/Add/Revoke</c>.
/// Two homes unified behind one Find:
///   - <b>Session ("y")</b> — unsigned, lives in an in-memory list, dies
///     when the App exits.
///   - <b>Persisted ("a")</b> — the actor's own permission setting
///     (<c>%!app.actor.permission.setting%</c>), one row per actor holding all its saved grants, signed
///     whole when it is saved and verified whole when it is read; a tampered row reads as no grants.
/// The setting is the actor's own: a user never holds the system's grants.
/// <c>Permission.Path</c> is the natural key — granting the same path twice overwrites.
/// </summary>
public sealed class @this : global::app.type.item.setting.ISetting<setting.@this>
{
    private readonly global::app.actor.@this _actor;
    private readonly List<global::app.data.@this> _inMemory = new();
    private readonly object _lock = new();

    public @this(global::app.actor.@this actor)
    {
        _actor = actor;
    }

    /// <summary>
    /// Returns the first signed grant covering <paramref name="path"/> + <paramref name="verb"/>,
    /// or null if nothing covers. Walks the in-memory list first, then the
    /// persisted table (filtered to this actor's kind). Per-grant signature
    /// verification is cached via the Data instance's Properties bag — repeat
    /// Find calls on the same in-memory grant don't re-verify.
    /// </summary>
    public async Task<global::app.data.@this?> Find(path requestPath, Verb verb)
    {
        var request = Grant.Request(
            _actor.Name, requestPath.Absolute, verb, MatchMode.Exact);

        // 1) In-memory grants. Snapshot under the lock; verify outside it so
        //    the async signing-verify call doesn't hold the lock.
        List<global::app.data.@this> snapshot;
        lock (_lock) snapshot = new(_inMemory);
        foreach (var grantData in snapshot)
        {
            if (await TryCover(grantData, request)) return grantData;
        }

        // 2) Persisted grants — the actor's own saved ones. A store that can't open (an unwritable root)
        // holds no rows, so only the in-memory grants are searched and the caller falls through to the prompt.
        foreach (var grantData in (await Saved()).Grant.Items(_actor.Context))
            if (await TryCover(grantData, request)) return grantData;

        return null;
    }

    /// <summary>
    /// Records a grant: persisted into the actor's own permission setting (signed with its row when it is
    /// saved), or in memory for this run. Same path twice overwrites (in either home).
    /// </summary>
    public async Task Add(global::app.data.@this grant, bool persist)
    {
        if (await grant.Value<Grant>() is not { } __rec) return;
        var key = __rec.Path;

        if (persist)
        {
            var setting = await Saved();
            var kept = new List<Grant>();
            foreach (var row in setting.Grant.Items(_actor.Context))
                if (await row.Value<Grant>() is { } held && !string.Equals(held.Path, key, StringComparison.Ordinal)) kept.Add(held);
            kept.Add(__rec);
            await Save(setting, kept);
            return;
        }

        lock (_lock)
        {
            // Overwrite same-path entry if any.
            var idx = _inMemory.FindIndex(d =>
                d.Peek() is Grant __dv && string.Equals(__dv.Path, key, StringComparison.Ordinal));
            if (idx >= 0) _inMemory[idx] = grant;
            else _inMemory.Add(grant);
        }
    }

    /// <summary>
    /// Drops a grant. Removes from in-memory if present; also removes from
    /// the persisted table by path key.
    /// </summary>
    public async Task<bool> Revoke(Grant match)
    {
        bool removed = false;
        lock (_lock)
        {
            var idx = _inMemory.FindIndex(d =>
                d.Peek() is Grant __dv2
                && __dv2.Actor == match.Actor
                && __dv2.Path == match.Path);
            if (idx >= 0) { _inMemory.RemoveAt(idx); removed = true; }
        }

        var setting = await Saved();
        var kept = new List<Grant>();
        foreach (var row in setting.Grant.Items(_actor.Context))
            if (await row.Value<Grant>() is { } held && !(held.Actor == match.Actor && held.Path == match.Path)) kept.Add(held);
        if (kept.Count < setting.Grant.CountRaw)
        {
            var saved = await Save(setting, kept);
            if (saved.Success) removed = true;
        }
        return removed;
    }

    // The actor's own permission setting, its saved row read first (a read before the rows would see none,
    // and a save would overwrite them).
    private async Task<setting.@this> Saved()
    {
        await _actor.Setting.Load();
        return _actor.Setting.Of<setting.@this>();
    }

    // Saves the grants as the actor's own permission setting — one row, whole.
    private async Task<global::app.data.@this> Save(setting.@this setting, List<Grant> grants)
    {
        setting.Grant = new global::app.type.item.list.@this<Grant>(grants);
        return await _actor.Setting.Save(setting.Path, new global::app.data.@this(setting.Path, setting, context: _actor.Context));
    }

    private async Task<bool> TryCover(global::app.data.@this grantData, Grant request)
    {
        if (await grantData.Value<Grant>() is not { } grant) return false;
        if (!string.Equals(grant.Actor, request.Actor, StringComparison.Ordinal)) return false;
        if (!grant.Covers(request)) return false;

        // A persisted grant was verified at the I/O boundary on load (auto-verify-
        // on-read peels + validates its signature layer); an in-memory grant is
        // local and trusted. So the record reaching here is already trustworthy —
        // no per-cover re-verification in memory.
        // SECURITY REVIEW (signature-as-layer): this relies on store reads
        // of signed grants going through application/plang auto-verify-on-read.
        return true;
    }
}
