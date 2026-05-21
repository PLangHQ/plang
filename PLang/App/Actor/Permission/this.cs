using PermissionRecord = global::App.FileSystem.Permission.@this;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using PathT = global::App.FileSystem.Path;
using MatchMode = global::App.FileSystem.Permission.Match;

namespace App.Actor.Permission;

/// <summary>
/// Per-actor permission view — <c>actor.Permission.Find/Add/Revoke</c>.
/// Two homes unified behind one Find:
///   - <b>Session ("y")</b> — no expiry on signature, lives in an in-memory
///     list, dies when the App exits.
///   - <b>Persisted ("a")</b> — signature has an expiry, routed to
///     <c>App.SettingsStore</c> under the <c>permission</c> table.
/// The shared table is filtered to this actor's kind client-side.
/// Per-kind keying: <c>Permission.Path</c> is the natural key — granting the
/// same path twice overwrites.
/// </summary>
public sealed class @this
{
    private const string PermissionTable = "permission";
    private const string VerifiedFlag = "permission.verified";

    private readonly global::App.Actor.@this _actor;
    private readonly List<global::App.Data.@this<PermissionRecord>> _inMemory = new();
    private readonly object _lock = new();

    public @this(global::App.Actor.@this actor)
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
    public global::App.Data.@this<PermissionRecord>? Find(PathT path, Verb verb)
    {
        var request = new PermissionRecord(
            _actor.App.Id, _actor.Name, path.Absolute, verb, MatchMode.Exact);

        // 1) In-memory grants.
        lock (_lock)
        {
            foreach (var grantData in _inMemory)
            {
                if (TryCover(grantData, request)) return grantData;
            }
        }

        // 2) Persisted grants (client-side actor filter).
        var stored = _actor.App.SettingsStore.GetAll<global::App.Data.@this<PermissionRecord>>(PermissionTable)
            .GetAwaiter().GetResult();
        if (stored.Success && stored.Value is { } list)
        {
            foreach (var grantData in list)
            {
                if (grantData.Value is null) continue;
                if (!string.Equals(grantData.Value.Actor, _actor.Name, StringComparison.Ordinal)) continue;
                if (TryCover(grantData, request)) return grantData;
            }
        }

        return null;
    }

    /// <summary>
    /// Records a signed grant. Routes by signature expiry: present → sqlite,
    /// absent → in-memory. Same path twice overwrites (in either home).
    /// </summary>
    public void Add(global::App.Data.@this<PermissionRecord> signed)
    {
        if (signed.Value == null) return;
        var key = signed.Value.Path;

        // Heuristic for "persisted": signature with an Expires value. The
        // signing-layer expiry surface is text-only today; until that grows,
        // any signed grant is treated as persisted. In-memory grants come in
        // unsigned ("y" branch in Path.Authorize doesn't call EnsureSigned).
        var persisted = signed.RawSignature != null;
        if (persisted)
        {
            _actor.App.SettingsStore.Set(PermissionTable, key, signed).GetAwaiter().GetResult();
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
    public bool Revoke(PermissionRecord match)
    {
        bool removed = false;
        lock (_lock)
        {
            var idx = _inMemory.FindIndex(d =>
                d.Value != null
                && d.Value.AppId == match.AppId
                && d.Value.Actor == match.Actor
                && d.Value.Path == match.Path);
            if (idx >= 0) { _inMemory.RemoveAt(idx); removed = true; }
        }

        var sqliteResult = _actor.App.SettingsStore.Remove(PermissionTable, match.Path).GetAwaiter().GetResult();
        if (sqliteResult.Success) removed = true;
        return removed;
    }

    private bool TryCover(global::App.Data.@this<PermissionRecord> grantData, PermissionRecord request)
    {
        var grant = grantData.Value;
        if (grant == null) return false;
        if (!string.Equals(grant.Actor, request.Actor, StringComparison.Ordinal)) return false;
        if (!string.Equals(grant.AppId, request.AppId, StringComparison.Ordinal)) return false;
        if (!grant.Covers(request)) return false;

        // Signature check (cached per-Data via Properties).
        if (grantData.RawSignature == null) return true; // in-memory unsigned grant
        var cached = grantData.Properties[VerifiedFlag];
        if (cached?.Value is bool b) return b;

        var verified = VerifySignature(grantData);
        grantData.Properties[VerifiedFlag] = new global::App.Data.@this(VerifiedFlag, verified);
        return verified;
    }

    private bool VerifySignature(global::App.Data.@this<PermissionRecord> data)
    {
        try
        {
            var action = new global::App.modules.signing.verify { Data = data };
            var result = _actor.Context.App.RunAction(action, _actor.Context).GetAwaiter().GetResult();
            return result.Success;
        }
        catch { return false; }
    }
}
