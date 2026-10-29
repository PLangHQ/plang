using Identity = global::app.module.identity.Identity;
using IdData = global::app.data.@this<global::app.module.identity.Identity>;

namespace PLang.Tests.Shared;

/// <summary>
/// In-memory <see cref="global::app.module.identity.code.IIdentity"/> for tests — one shared
/// identity (a real Ed25519 keypair generated once) held in memory, NOT round-tripped through the
/// signed settings store. The production provider saves the identity signed and re-reads+verifies
/// it on every resolution; a signed-Data canonicalization mismatch makes that read fail, so the
/// provider recreates the identity (keygen+sign+store) on every single call (~850ms). This skips
/// the store entirely so identity resolution is instant.
///
/// For fixtures that need an identity to sign with but don't test the identity PROVIDER itself
/// (those keep the real <see cref="global::app.module.identity.code.Default"/>).
/// </summary>
public sealed class TestIdentity : global::app.module.identity.code.IIdentity
{
    public string Name => "test-identity";
    public bool IsBuiltIn { get; set; }
    public string? Source { get; set; }

    // one keypair for every test (generating one is the slow part); each app gets its identity holding that app's type
    private static readonly (string PublicKey, string PrivateKey) _keys = Keys();

    /// <summary>The one identity's public key: who the no-crypto signer signs as, so an app holding this identity takes
    /// what it signed as its own (a resume's check).</summary>
    public static string PublicKey => _keys.PublicKey;

    private static (string, string) Keys()
    {
        var (kp, _) = new global::app.module.signing.code.Ed25519().GenerateKeyPair();
        return (kp!.PublicKey, kp.PrivateKey);
    }

    private static Identity Shared(global::app.type.list.@this types) => new("default", types[typeof(Identity)])
    {
        PublicKey = _keys.PublicKey,
        PrivateKey = _keys.PrivateKey,
        IsDefault = true,
        Created = System.DateTimeOffset.FromUnixTimeSeconds(0),
    };

    private static IdData Ok(global::app.module.IContext a) => a.Context.Ok<Identity>(Shared(a.Context.App.type.list));

    public Task<IdData> GetAsync(global::app.module.identity.Get action) => Task.FromResult(Ok(action));
    public Task<IdData> GetOrCreateDefaultAsync(global::app.module.IContext action) => Task.FromResult(Ok(action));
    public Task<IdData> CreateAsync(global::app.module.identity.Create action) => Task.FromResult(Ok(action));
    public Task<IdData> ArchiveAsync(global::app.module.identity.Archive action) => Task.FromResult(Ok(action));
    public Task<IdData> UnarchiveAsync(global::app.module.identity.Unarchive action) => Task.FromResult(Ok(action));
    public Task<IdData> SetDefaultAsync(global::app.module.identity.SetDefault action) => Task.FromResult(Ok(action));
    public Task<IdData> RenameAsync(global::app.module.identity.Rename action) => Task.FromResult(Ok(action));
    public Task<IdData> ExportAsync(global::app.module.identity.Export action) => Task.FromResult(Ok(action));

    public Task<global::app.data.@this<global::app.type.item.list.@this<Identity>>> ListAsync(global::app.module.identity.list action)
        => Task.FromResult(action.Context.Ok<global::app.type.item.list.@this<Identity>>(
            new global::app.type.item.list.@this<Identity>(new global::app.data.@this[] { action.Context.Ok<Identity>(Shared(action.Context.App.type.list)) },
                action.Context.App.type.list[typeof(global::app.type.item.list.@this<Identity>)])));
}
