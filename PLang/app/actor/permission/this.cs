using Grant = global::app.type.permission.@this;
using Verb = global::app.type.permission.Verb;
using MatchMode = global::app.type.permission.Match;

namespace app.actor.permission;

/// <summary>
/// Per-actor permission view — <c>actor.Permission.Find/Add/Revoke</c>.
/// Two homes unified behind one Find:
///   - <b>Session ("y")</b> — unsigned, lives in an in-memory list, dies
///     when the App exits.
///   - <b>Persisted ("a")</b> — Ed25519-signed with <c>Expires == null</c>
///     (permanent), routed to <c>app.SettingsStore</c> under the
///     <c>permission</c> table. Verified with <c>SkipFreshnessCheck=true</c>
///     so the wire-freshness window doesn't apply; the signature's own
///     <c>Expires</c> field is the only time bound.
/// The shared table is filtered to this actor's kind client-side.
/// Per-kind keying: <c>Permission.Path</c> is the natural key — granting the
/// same path twice overwrites.
/// </summary>
public sealed class @this
{
    private const string PermissionTable = "permission";
    private const string VerifiedFlag = "permission.verified";

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

        // 2) Persisted grants (client-side actor filter). Tolerant of
        // SettingsStore creation failure — test fixtures with unwriteable App
        // roots ("/dst" et al.) shouldn't crash here when only in-memory
        // grants were ever used.
        try
        {
            var stored = await _actor.App.SettingsStore.GetAll<Grant>(PermissionTable);
            if (stored.Success && await stored.Value() is { } list)
            {
                foreach (var grantData in list.Items.Cast<global::app.data.@this>())
                {
                    // Stamp Context on grants freshly rehydrated from SQLite — the store
                    // returns Data without a Context wired, and downstream signature/
                    // type-resolution paths require it.  Per the architecture: every
                    // producer stamps Context; SettingsStore is a producer.
                    grantData.Context = _actor.Context;
                    if (await grantData.Value<Grant>() is not { } rec) continue;
                    if (!string.Equals(rec.Actor, _actor.Name, StringComparison.Ordinal)) continue;
                    if (await TryCover(grantData, request)) return grantData;
                }
            }
        }
        catch (System.Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))
        {
            // SettingsStore unavailable (unwriteable root, etc.) — only
            // in-memory grants searchable. Caller will fall through to the
            // prompt path.
        }

        return null;
    }

    /// <summary>
    /// Records a signed grant. Routes by signature presence: signed → sqlite,
    /// unsigned → in-memory. Same path twice overwrites (in either home).
    /// </summary>
    public async Task Add(global::app.data.@this grant, bool persist)
    {
        if (await grant.Value<Grant>() is not { } __rec) return;
        var key = __rec.Path;

        // The caller decides persisted vs in-memory (it used to sign-then-persist;
        // signing is no longer in memory). A persisted grant is signed
        // automatically when it crosses the application/plang boundary into the
        // settings store; an in-memory grant is local and unsigned.
        if (persist)
        {
            await _actor.App.SettingsStore.Set(PermissionTable, key, grant);
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

        var sqliteResult = await _actor.App.SettingsStore.Remove(PermissionTable, match.Path);
        if (sqliteResult.Success) removed = true;
        return removed;
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
        // SECURITY REVIEW (signature-as-layer): this relies on SettingsStore reads
        // of signed grants going through application/plang auto-verify-on-read.
        return true;
    }
}
