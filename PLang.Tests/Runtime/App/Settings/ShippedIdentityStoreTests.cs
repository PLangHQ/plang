using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Grant = global::app.type.item.permission.@this;
using Verb = global::app.type.item.permission.Verb;

namespace PLang.Tests.Runtime.App.Settings;

/// Security (item-doors re-check 3): an [Own] row is read only when one of the app's identities signed it — but the
/// app's identities are themselves a row of the same store. DOCUMENTATION TESTS, green by design today: they pin what
/// the R1 fix does and does not cover. A cloned app shipping its whole <c>.data/</c> (identity row AND permission
/// row, both the author's) is read as the victim's own: indistinguishable from the app's own store reopened (test 1),
/// until the app's identity lives outside the store (open design question, Ingi). Test 3 pins that a System row
/// copied under the User's key is not the User's.
public class ShippedIdentityStoreTests
{
    private static string Temp(string tag) => System.IO.Directory.CreateDirectory(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "plang-" + tag + "-" + System.Guid.NewGuid().ToString("N")[..8])).FullName;

    [Test]
    public async Task TheAppsOwnStore_ReopenedWithItsRealIdentity_StillGrants()
    {
        var root = Temp("own");
        var secret = System.IO.Path.Combine(Temp("own-out"), "secret.txt");
        System.IO.File.WriteAllText(secret, "outside");
        await using (var first = new global::app.@this(root))
        {
            var user = first.actor.list.User;
            await user.Permission.Add(new global::app.data.@this<Grant>("", Grant.Request(user, secret, Verb.read), context: user.Context), persist: true);
        }
        await using var again = new global::app.@this(root);
        var u = again.actor.list.User;
        await Assert.That(await u.Permission.Find(u.Context.File(secret), Verb.read)).IsNotNull();
    }

    [Test]
    public async Task AStoreShippedWithItsOwnIdentityRow_IsReadAsTheVictimsOwn_UntilTheIdentityLivesOutsideTheStore()
    {
        var root = Temp("ship");
        var secret = System.IO.Path.Combine(Temp("ship-out"), "secret.txt");
        System.IO.File.WriteAllText(secret, "outside");
        // the attacker, in their checkout with the REAL identity provider: the grant is signed with the identity the
        // app made for them, and that identity row is in the same store they ship
        await using (var shipped = new global::app.@this(root))
        {
            var user = shipped.actor.list.User;
            await user.Permission.Add(new global::app.data.@this<Grant>("", Grant.Request(user, secret, Verb.read), context: user.Context), persist: true);
        }
        // the victim clones it (store included) and opens it with no identity of their own
        await using var victim = new global::app.@this(root);
        var v = victim.actor.list.User;
        var answered = false;
        try { answered = await v.Permission.Find(v.Context.File(secret), Verb.read) != null; }
        catch (global::app.error.AppException) { answered = false; }
        await Assert.That(answered).IsTrue();   // documents the residual: closes only with the design change
    }

    [Test]
    public async Task ASystemRow_CopiedUnderTheUsersKey_IsNotTheUsersGrant()
    {
        var root = Temp("replay");
        var secret = System.IO.Path.Combine(Temp("replay-out"), "secret.txt");
        System.IO.File.WriteAllText(secret, "outside");
        await using (var first = new global::app.@this(root))
        {
            var system = first.actor.list.System;
            await system.Permission.Add(new global::app.data.@this<Grant>("", Grant.Request(system, secret, Verb.read), context: system.Context), persist: true);
        }
        var db = System.IO.Path.Combine(root, ".data", "setting", "data.sqlite");
        var keys = new System.Collections.Generic.List<string>();
        using (var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + db))
        {
            c.Open();
            using (var q = c.CreateCommand())
            {
                q.CommandText = "select key from settings";
                using var r = q.ExecuteReader();
                while (r.Read()) keys.Add(r.GetString(0));
            }
            var from = keys.Find(k => k.StartsWith("system!") && k.Contains("permission"));
            await Assert.That(from).IsNotNull();
            var to = "user!" + from!["system!".Length..];
            using var cp = c.CreateCommand();
            cp.CommandText = "insert or replace into settings(key,data) select @to, data from settings where key=@from";
            cp.Parameters.AddWithValue("@to", to);
            cp.Parameters.AddWithValue("@from", from);
            cp.ExecuteNonQuery();
        }
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        await using var again = new global::app.@this(root);
        var u = again.actor.list.User;
        var answered = false;
        try { answered = await u.Permission.Find(u.Context.File(secret), Verb.read) != null; }
        catch (global::app.error.AppException) { answered = false; }
        await Assert.That(answered).IsFalse();
    }
}
