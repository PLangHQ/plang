using PermissionRecord = global::App.FileSystem.Permission.@this;
using Verb = global::App.FileSystem.Permission.Verb.@this;
using PathT = global::App.FileSystem.Path;

namespace App.Actor.Permission;

/// <summary>
/// Per-actor permission view — <c>actor.Permission.Find/Add/Revoke</c>.
/// Stage 2b lands the surface only; <c>Find</c> returns null, <c>Add</c> is a
/// no-op (in-memory list grows but is not yet queried), <c>Revoke</c> is a
/// no-op. Stage 3 fills in the real two-home (in-memory + sqlite) storage.
/// </summary>
public sealed class @this
{
    private readonly List<global::App.Data.@this<PermissionRecord>> _inMemory = new();
    private readonly object _lock = new();

    /// <summary>
    /// Returns a signed grant Data covering the request, or null when no grant
    /// is found. Stage 2b stub: always null (callers go through Authorize).
    /// Stage 3 walks in-memory + sqlite, returns the first cover.
    /// </summary>
    public global::App.Data.@this<PermissionRecord>? Find(PathT path, Verb verb)
    {
        lock (_lock)
        {
            foreach (var grantData in _inMemory)
            {
                var grant = grantData.Value;
                if (grant == null) continue;
                var request = new PermissionRecord(
                    grant.AppId, grant.Actor, path.Absolute, verb, FileSystem.Permission.Match.Exact);
                if (grant.Covers(request)) return grantData;
            }
        }
        return null;
    }

    /// <summary>
    /// Records a signed grant. Stage 2b stores in-memory only; stage 3 routes
    /// persistent grants (those with a far-future expiry) into sqlite.
    /// </summary>
    public void Add(global::App.Data.@this<PermissionRecord> grant)
    {
        lock (_lock) _inMemory.Add(grant);
    }

    /// <summary>
    /// Drops a grant. Stage 2b: removes from in-memory list by AppId/Actor/Path.
    /// Stage 3: also removes from sqlite when persisted.
    /// </summary>
    public bool Revoke(PermissionRecord match)
    {
        lock (_lock)
        {
            var idx = _inMemory.FindIndex(d =>
                d.Value != null
                && d.Value.AppId == match.AppId
                && d.Value.Actor == match.Actor
                && d.Value.Path == match.Path);
            if (idx < 0) return false;
            _inMemory.RemoveAt(idx);
            return true;
        }
    }
}
