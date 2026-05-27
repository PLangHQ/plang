using PermissionRecord = global::app.types.path.permission.@this;
using Verb = global::app.types.path.permission.verb.@this;
using MatchMode = global::app.types.path.permission.Match;

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
    private readonly List<global::app.data.@this<PermissionRecord>> _inMemory = new();
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
    public async Task<global::app.data.@this<PermissionRecord>?> Find(path requestPath, Verb verb)
    {
        var request = new PermissionRecord(
            _actor.Name, requestPath.Absolute, verb, MatchMode.Exact);

        // 1) In-memory grants. Snapshot under the lock; verify outside it so
        //    the async signing-verify call doesn't hold the lock.
        List<global::app.data.@this<PermissionRecord>> snapshot;
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
            var stored = await _actor.App.SettingsStore.GetAll<global::app.data.@this<PermissionRecord>>(PermissionTable);
            if (stored.Success && stored.Value is { } list)
            {
                foreach (var grantData in list)
                {
                    if (grantData.Value is null) continue;
                    if (!string.Equals(grantData.Value.Actor, _actor.Name, StringComparison.Ordinal)) continue;
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
    public async Task Add(global::app.data.@this<PermissionRecord> signed)
    {
        if (signed.Value == null) return;
        var key = signed.Value.Path;

        // Heuristic for "persisted": signature present. In-memory grants come
        // in unsigned ("y" branch in Path.Authorize doesn't call EnsureSigned).
        if (signed.RawSignature != null)
        {
            await _actor.App.SettingsStore.Set(PermissionTable, key, signed);
            return;
        }

        lock (_lock)
        {
            // Overwrite same-path entry if any.
            var idx = _inMemory.FindIndex(d =>
                d.Value != null && string.Equals(d.Value.Path, key, StringComparison.Ordinal));
            if (idx >= 0) _inMemory[idx] = signed;
            else _inMemory.Add(signed);
        }
    }

    /// <summary>
    /// Drops a grant. Removes from in-memory if present; also removes from
    /// the persisted table by path key.
    /// </summary>
    public async Task<bool> Revoke(PermissionRecord match)
    {
        bool removed = false;
        lock (_lock)
        {
            var idx = _inMemory.FindIndex(d =>
                d.Value != null
                && d.Value.Actor == match.Actor
                && d.Value.Path == match.Path);
            if (idx >= 0) { _inMemory.RemoveAt(idx); removed = true; }
        }

        var sqliteResult = await _actor.App.SettingsStore.Remove(PermissionTable, match.Path);
        if (sqliteResult.Success) removed = true;
        return removed;
    }

    private async Task<bool> TryCover(global::app.data.@this<PermissionRecord> grantData, PermissionRecord request)
    {
        var grant = grantData.Value;
        if (grant == null) return false;
        if (!string.Equals(grant.Actor, request.Actor, StringComparison.Ordinal)) return false;
        if (!grant.Covers(request)) return false;

        // Signature check (cached per-Data via Properties).
        if (grantData.RawSignature == null) return true; // in-memory unsigned grant
        if (grantData.Properties[VerifiedFlag] is bool b) return b;

        var verified = await VerifySignature(grantData);
        grantData.Properties[VerifiedFlag] = verified;
        return verified;
    }

    private async Task<bool> VerifySignature(global::app.data.@this<PermissionRecord> data)
    {
        try
        {
            // SkipFreshnessCheck=true: grants are long-lived artifacts. The
            // Created-age wire-freshness check (and the paired nonce-replay
            // check) is for transient signed messages; applying it to grants
            // would expire "always allow" after 5 minutes and would reject
            // re-reads of the same stored nonce. The grant's own Expires
            // (null = permanent today) is the only time bound that applies.
            var action = new global::app.modules.signing.verify
            {
                Data = data,
                SkipFreshnessCheck = new global::app.data.@this<bool>("", true),
            };
            var result = await _actor.Context.App.RunAction(action, _actor.Context);
            return result.Success;
        }
        // Filter rule: signing failures (bad key, tampered Data) surface as
        // `result.Success == false` and don't throw. Anything that does throw
        // here is a contract break — surface via debug, return false (deny).
        catch (Exception ex) when (ex is not (NullReferenceException
            or OutOfMemoryException or StackOverflowException or OperationCanceledException))
        {
            _ = _actor.Context.App.Debug.Write($"[Permission.VerifySignature] swallowed {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }
}
