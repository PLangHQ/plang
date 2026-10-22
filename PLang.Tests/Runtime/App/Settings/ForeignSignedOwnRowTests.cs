using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Identity = global::app.module.identity.Identity;
using IdData = global::app.data.@this<global::app.module.identity.Identity>;
using Grant = global::app.type.item.permission.@this;
using Verb = global::app.type.item.permission.Verb;

namespace PLang.Tests.Runtime.App.Settings;

/// Security (item-doors re-check, L(1)): an actor's own row is read only when it is signed — but a signature verifies
/// against the public key it carries, so a row signed by ANY key passes. An app shipped with its `.data/` store (a
/// cloned repo) can carry a permission row an attacker signed with their own key. Red if that row's grant answers.
public class ForeignSignedOwnRowTests
{
    private static readonly (string PublicKey, string PrivateKey) AttackerKeys = Generate();

    private static (string, string) Generate()
    {
        var (kp, _) = new global::app.module.signing.code.Ed25519().GenerateKeyPair();
        return (kp!.PublicKey, kp.PrivateKey);
    }

    // an identity provider holding the attacker's keypair
    private sealed class Attacker : global::app.module.identity.code.IIdentity
    {
        public string Name => "attacker-identity";
        public bool IsBuiltIn { get; set; }
        public string? Source { get; set; }
        private static Identity Mine(global::app.type.list.@this types) => new("default", types[typeof(Identity)])
        {
            PublicKey = AttackerKeys.PublicKey, PrivateKey = AttackerKeys.PrivateKey, IsDefault = true,
            Created = System.DateTimeOffset.FromUnixTimeSeconds(0),
        };
        private static IdData Ok(global::app.module.IContext a) => a.Context.Ok<Identity>(Mine(a.Context.App.type.list));
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
                new global::app.type.item.list.@this<Identity>(new global::app.data.@this[] { action.Context.Ok<Identity>(Mine(action.Context.App.type.list)) },
                    action.Context.App.type.list[typeof(global::app.type.item.list.@this<Identity>)])));
    }

    [Test]
    public async Task APermissionRow_SignedByAnotherKey_IsNotTheActorsGrant()
    {
        var root = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-foreign-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        var outside = System.IO.Directory.CreateDirectory(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-foreign-out-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;
        var secret = System.IO.Path.Combine(outside, "secret.txt");
        System.IO.File.WriteAllText(secret, "outside");

        // the attacker, in their own checkout, grants the user the secret file and ships the store
        await using (var shipped = new global::app.@this(root))
        {
            shipped.Code.Register<global::app.module.identity.code.IIdentity>(new Attacker { IsBuiltIn = true });
            shipped.Code.SetDefault<global::app.module.identity.code.IIdentity>("attacker-identity");
            var user = shipped.actor.list.User;
            await user.Permission.Add(new global::app.data.@this<Grant>("", Grant.Request(user, secret, Verb.read), context: user.Context), persist: true);
            await Assert.That(await user.Permission.Find(user.Context.File(secret), Verb.read)).IsNotNull();
        }

        // the victim opens the cloned app with their own identity: the shipped row isn't theirs
        await using var victim = new global::app.@this(root).TestIdentity();
        var victimUser = victim.actor.list.User;
        await Assert.That(await victimUser.Permission.Find(victimUser.Context.File(secret), Verb.read)).IsNull();
    }
}
