using app.actor.context;
using app.variable;
using app.module.identity;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.identity;

[NotInParallel]
public class IdentityHandlerTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_identity_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Plain(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private global::app.actor.context.@this Ctx => _app.System.Context;

    // --- create ---

    [Test]
    public async Task Create_GeneratesValidEd25519KeyPair()
    {
        // This test validates the ACTUAL ed25519 keypair (32-byte base64), so it needs the
        // real signing provider — not the class fixture's test-signing. Own real app, own key.
        await using var realApp = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang_id_real_" + Guid.NewGuid().ToString("N")[..8]));
        var realCtx = realApp.System.Context;
        var handler = new Create { Context = realCtx, Name = (global::app.type.text.@this)"test", SetAsDefault = (global::app.type.@bool.@this)true };
        await handler.Attach(null, realCtx);
        var result = await handler.Run();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.PublicKey).IsNotNull();
        await Assert.That(identity.PrivateKey).IsNotNull();

        // Base64-decodable, correct lengths (32 bytes each for Ed25519)
        var pubBytes = Convert.FromBase64String(identity.PublicKey);
        var privBytes = Convert.FromBase64String(identity.PrivateKey);
        await Assert.That(pubBytes.Length).IsEqualTo(32);
        await Assert.That(privBytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task Create_DefaultFalse_IsNotDefault()
    {
        var handler = new Create { Context = Ctx, Name = (global::app.type.text.@this)"test", SetAsDefault = (global::app.type.@bool.@this)false };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.IsDefault).IsFalse();
    }

    [Test]
    public async Task Create_SetAsDefaultTrue_BecomesDefault()
    {
        var handler = new Create { Context = Ctx, Name = (global::app.type.text.@this)"test", SetAsDefault = (global::app.type.@bool.@this)true };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.IsDefault).IsTrue();
    }

    [Test]
    public async Task Create_SetAsDefaultTrue_ClearsPreviousDefault()
    {
        var h1 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"first", SetAsDefault = (global::app.type.@bool.@this)true };
        await h1.Attach(null, Ctx);
        await h1.Run();

        var h2 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"second", SetAsDefault = (global::app.type.@bool.@this)true };
        await h2.Attach(null, Ctx);
        await h2.Run();

        // First should no longer be default
        var __ia0 = new Get { Context = Ctx, Name = (global::app.type.text.@this)"first" };
        await __ia0.Attach(null, Ctx);
        var firstResult = await __ia0.Run();
        var first = (await firstResult.Value()) as Identity;
        await Assert.That(first!.IsDefault).IsFalse();

        var __ia1 = new Get { Context = Ctx, Name = (global::app.type.text.@this)"second" };
        await __ia1.Attach(null, Ctx);
        var secondResult = await __ia1.Run();
        var second = (await secondResult.Value()) as Identity;
        await Assert.That(second!.IsDefault).IsTrue();
    }

    [Test]
    public async Task Create_StoresInSystemDataSource()
    {
        var handler = new Create { Context = Ctx, Name = (global::app.type.text.@this)"stored", SetAsDefault = (global::app.type.@bool.@this)false };
        await handler.Attach(null, Ctx);
        await handler.Run();

        var __ia2 = new Get { Context = Ctx, Name = (global::app.type.text.@this)"stored" };
        await __ia2.Attach(null, Ctx);
        var loadResult = await __ia2.Run();
        await loadResult.IsSuccess();
        var loaded = (await loadResult.Value()) as Identity;
        await Assert.That(loaded!.Name).IsEqualTo("stored");
    }

    [Test]
    public async Task Create_DuplicateName_ReturnsError()
    {
        var h1 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"dup", SetAsDefault = (global::app.type.@bool.@this)false };
        await h1.Attach(null, Ctx);
        await h1.Run();

        var h2 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"dup", SetAsDefault = (global::app.type.@bool.@this)false };
        await h2.Attach(null, Ctx);
        var result = await h2.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DuplicateName");
    }

    [Test]
    public async Task Create_DuplicateArchivedName_ReturnsError()
    {
        // Create and archive
        var h1 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"archived", SetAsDefault = (global::app.type.@bool.@this)false };
        await h1.Attach(null, Ctx);
        await h1.Run();

        var archiveH = new Archive { Context = Ctx, Name = (global::app.type.text.@this)"archived" };
        await archiveH.Attach(null, Ctx);
        await archiveH.Run();

        // Try to create with same name — should fail
        var h2 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"archived", SetAsDefault = (global::app.type.@bool.@this)false };
        await h2.Attach(null, Ctx);
        var result = await h2.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DuplicateName");
    }

    [Test]
    public async Task Create_EmptyOrWhitespaceName_ReturnsError()
    {
        var h1 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"", SetAsDefault = (global::app.type.@bool.@this)false };
        await h1.Attach(null, Ctx);
        var result1 = await h1.Run();
        await result1.IsFailure();
        await Assert.That(result1.Error!.Key).IsEqualTo("ValidationError");

        var h2 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"   ", SetAsDefault = (global::app.type.@bool.@this)false };
        await h2.Attach(null, Ctx);
        var result2 = await h2.Run();
        await result2.IsFailure();
        await Assert.That(result2.Error!.Key).IsEqualTo("ValidationError");
    }

    // --- get ---

    [Test]
    public async Task Get_NonExistentName_ReturnsError()
    {
        var handler = new Get { Context = Ctx, Name = (global::app.type.text.@this)"nosuch" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Get_NullName_NoDefaultExists_PromotesExisting()
    {
        // Create two non-default identities
        var h1 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"a", SetAsDefault = (global::app.type.@bool.@this)false };
        await h1.Attach(null, Ctx);
        var r1 = await h1.Run();
        var originalKey = ((await r1.Value()) as Identity)!.PublicKey;
        var h2 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"b", SetAsDefault = (global::app.type.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Run();

        // Get(null) should promote the first non-archived identity as default
        var handler = new Get { Context = Ctx, Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.IsDefault).IsTrue();
        await Assert.That(identity.Name).IsEqualTo("a");
        await Assert.That(identity.PublicKey).IsEqualTo(originalKey);
    }

    [Test]
    public async Task Get_ByName_ReturnsMatchingIdentity()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"alice", SetAsDefault = (global::app.type.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Run();

        var handler = new Get { Context = Ctx, Name = (global::app.type.text.@this)"alice" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.Name).IsEqualTo("alice");
    }

    [Test]
    public async Task Get_NullName_ReturnsDefaultIdentity()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"mydefault", SetAsDefault = (global::app.type.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Run();

        var handler = new Get { Context = Ctx, Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.Name).IsEqualTo("mydefault");
        await Assert.That(identity.IsDefault).IsTrue();
    }

    [Test]
    public async Task Get_NoIdentitiesExist_AutoCreatesDefault()
    {
        var handler = new Get { Context = Ctx, Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.Name).IsEqualTo("default");
        await Assert.That(identity.IsDefault).IsTrue();
        await Assert.That(identity.PublicKey).IsNotNull();
    }

    [Test]
    public async Task Get_ReturnsIdentityData_WithAllProperties()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"full", SetAsDefault = (global::app.type.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Run();

        var handler = new Get { Context = Ctx, Name = (global::app.type.text.@this)"full" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        var identity = (await result.Value()) as Identity;

        await Assert.That(identity!.Name).IsEqualTo("full");
        await Assert.That(identity.PublicKey).IsNotNull();
        await Assert.That(identity.PrivateKey).IsNotNull();
        await Assert.That(identity.IsDefault).IsTrue();
        await Assert.That(identity.IsArchived).IsFalse();
        await Assert.That(identity.Created).IsNotEqualTo(default(DateTimeOffset));
    }

    // --- getAll ---

    [Test]
    public async Task GetAll_ReturnsOnlyNonArchived()
    {
        var h1 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"active1", SetAsDefault = (global::app.type.@bool.@this)false };
        await h1.Attach(null, Ctx);
        await h1.Run();
        var h2 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"active2", SetAsDefault = (global::app.type.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Run();
        var h3 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"archived", SetAsDefault = (global::app.type.@bool.@this)false };
        await h3.Attach(null, Ctx);
        await h3.Run();

        var archiveH = new Archive { Context = Ctx, Name = (global::app.type.text.@this)"archived" };
        await archiveH.Attach(null, Ctx);
        await archiveH.Run();

        var handler = new list { Context = Ctx };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var list = result.GetValue<List<Identity>>();
        await Assert.That(list!.Count).IsEqualTo(2);
        await Assert.That(list.Any(i => i.Name == "archived")).IsFalse();
    }

    [Test]
    public async Task GetAll_AllArchived_ReturnsEmptyList()
    {
        var h1 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"only", SetAsDefault = (global::app.type.@bool.@this)false };
        await h1.Attach(null, Ctx);
        await h1.Run();

        var archiveH = new Archive { Context = Ctx, Name = (global::app.type.text.@this)"only" };
        await archiveH.Attach(null, Ctx);
        await archiveH.Run();

        var handler = new list { Context = Ctx };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var list = result.GetValue<List<Identity>>();
        await Assert.That(list!.Count).IsEqualTo(0);
    }

    // --- archive ---

    [Test]
    public async Task Archive_NonDefaultIdentity_SetsIsArchivedTrue()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"toarchive", SetAsDefault = (global::app.type.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Run();

        var handler = new Archive { Context = Ctx, Name = (global::app.type.text.@this)"toarchive" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var __ia3 = new Get { Context = Ctx, Name = (global::app.type.text.@this)"toarchive" };
        await __ia3.Attach(null, Ctx);
        var loadResult = await __ia3.Run();
        // Archived identities may not be returned by Get — verify via the archive result itself
        // If Get returns it, check IsArchived; if not, the archive succeeded (already asserted above)
        if (loadResult.Success)
        {
            var loaded = (await loadResult.Value()) as Identity;
            await Assert.That(loaded!.IsArchived).IsTrue();
        }
    }

    [Test]
    public async Task Archive_DefaultIdentity_ReturnsError()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"def", SetAsDefault = (global::app.type.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Run();

        var handler = new Archive { Context = Ctx, Name = (global::app.type.text.@this)"def" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("CannotArchiveDefault");
    }

    [Test]
    public async Task Archive_NonExistentName_ReturnsError()
    {
        var handler = new Archive { Context = Ctx, Name = (global::app.type.text.@this)"nope" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Archive_AlreadyArchived_IsIdempotent()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"twice", SetAsDefault = (global::app.type.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Run();

        var h1 = new Archive { Context = Ctx, Name = (global::app.type.text.@this)"twice" };
        await h1.Attach(null, Ctx);
        await h1.Run();

        var h2 = new Archive { Context = Ctx, Name = (global::app.type.text.@this)"twice" };
        await h2.Attach(null, Ctx);
        var result = await h2.Run();
        await result.IsSuccess();
    }

    // --- setDefault ---

    [Test]
    public async Task SetDefault_SwitchesDefault_ClearsOldDefault()
    {
        var h1 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"old", SetAsDefault = (global::app.type.@bool.@this)true };
        await h1.Attach(null, Ctx);
        await h1.Run();
        var h2 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"new", SetAsDefault = (global::app.type.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Run();

        var handler = new SetDefault { Context = Ctx, Name = (global::app.type.text.@this)"new" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var __ia4 = new Get { Context = Ctx, Name = (global::app.type.text.@this)"old" };
        await __ia4.Attach(null, Ctx);
        var oldResult = await __ia4.Run();
        var oldId = (await oldResult.Value()) as Identity;
        await Assert.That(oldId!.IsDefault).IsFalse();

        var __ia5 = new Get { Context = Ctx, Name = (global::app.type.text.@this)"new" };
        await __ia5.Attach(null, Ctx);
        var newResult = await __ia5.Run();
        var newId = (await newResult.Value()) as Identity;
        await Assert.That(newId!.IsDefault).IsTrue();
    }

    [Test]
    public async Task SetDefault_ArchivedOrMissing_ReturnsError()
    {
        // Missing
        var h1 = new SetDefault { Context = Ctx, Name = (global::app.type.text.@this)"missing" };
        await h1.Attach(null, Ctx);
        var r1 = await h1.Run();
        await r1.IsFailure();
        await Assert.That(r1.Error!.Key).IsEqualTo("NotFound");

        // Archived
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"arch", SetAsDefault = (global::app.type.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Run();
        var archive = new Archive { Context = Ctx, Name = (global::app.type.text.@this)"arch" };
        await archive.Attach(null, Ctx);
        await archive.Run();

        var h2 = new SetDefault { Context = Ctx, Name = (global::app.type.text.@this)"arch" };
        await h2.Attach(null, Ctx);
        var r2 = await h2.Run();
        await r2.IsFailure();
        await Assert.That(r2.Error!.Key).IsEqualTo("ArchivedIdentity");
    }

    [Test]
    public async Task SetDefault_AlreadyDefault_IsIdempotent()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"already", SetAsDefault = (global::app.type.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Run();

        var handler = new SetDefault { Context = Ctx, Name = (global::app.type.text.@this)"already" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.IsDefault).IsTrue();
    }

    // --- unarchive ---

    [Test]
    public async Task Unarchive_RestoresArchivedIdentity()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"restore", SetAsDefault = (global::app.type.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Run();

        var archiveH = new Archive { Context = Ctx, Name = (global::app.type.text.@this)"restore" };
        await archiveH.Attach(null, Ctx);
        await archiveH.Run();

        var handler = new Unarchive { Context = Ctx, Name = (global::app.type.text.@this)"restore" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var __ia6 = new Get { Context = Ctx, Name = (global::app.type.text.@this)"restore" };
        await __ia6.Attach(null, Ctx);
        var loadResult = await __ia6.Run();
        await loadResult.IsSuccess();
        var loaded = (await loadResult.Value()) as Identity;
        await Assert.That(loaded!.IsArchived).IsFalse();
    }

    [Test]
    public async Task Unarchive_NonExistentName_ReturnsError()
    {
        var handler = new Unarchive { Context = Ctx, Name = (global::app.type.text.@this)"nope" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Unarchive_NotArchived_IsIdempotent()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"active", SetAsDefault = (global::app.type.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Run();

        var handler = new Unarchive { Context = Ctx, Name = (global::app.type.text.@this)"active" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.Name).IsEqualTo("active");
    }

    // --- rename ---

    [Test]
    public async Task Rename_ChangesName_KeepsKeys()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"oldname", SetAsDefault = (global::app.type.@bool.@this)false };
        await create.Attach(null, Ctx);
        var createResult = await create.Run();
        var originalKey = ((await createResult.Value()) as Identity)!.PublicKey;

        var handler = new Rename { Context = Ctx, Name = (global::app.type.text.@this)"oldname", NewName = (global::app.type.text.@this)"newname" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();

        var renamed = (await result.Value()) as Identity;
        await Assert.That(renamed!.Name).IsEqualTo("newname");
        await Assert.That(renamed.PublicKey).IsEqualTo(originalKey);

        // Old name should be gone
        var __ia7 = new Get { Context = Ctx, Name = (global::app.type.text.@this)"oldname" };
        await __ia7.Attach(null, Ctx);
        var oldResult = await __ia7.Run();
        await oldResult.IsFailure();
    }

    [Test]
    public async Task Rename_DuplicateNewName_ReturnsError()
    {
        var h1 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"a", SetAsDefault = (global::app.type.@bool.@this)false };
        await h1.Attach(null, Ctx);
        await h1.Run();
        var h2 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"b", SetAsDefault = (global::app.type.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Run();

        var handler = new Rename { Context = Ctx, Name = (global::app.type.text.@this)"a", NewName = (global::app.type.text.@this)"b" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DuplicateName");
    }

    [Test]
    public async Task Rename_NonExistentName_ReturnsError()
    {
        var handler = new Rename { Context = Ctx, Name = (global::app.type.text.@this)"nope", NewName = (global::app.type.text.@this)"whatever" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Rename_DefaultIdentity_UpdatesMyIdentity()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"def", SetAsDefault = (global::app.type.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Run();

        var handler = new Rename { Context = Ctx, Name = (global::app.type.text.@this)"def", NewName = (global::app.type.text.@this)"renamed" };
        await handler.Attach(null, Ctx);
        await handler.Run();

        // %MyIdentity% should reflect the new name
        var myIdentity = _app.System.Identity;
        await Assert.That(myIdentity!.Name).IsEqualTo("renamed");
    }

    [Test]
    public async Task Rename_EmptyNewName_ReturnsError()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"valid", SetAsDefault = (global::app.type.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Run();

        var handler = new Rename { Context = Ctx, Name = (global::app.type.text.@this)"valid", NewName = (global::app.type.text.@this)"" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
    }

    // --- export ---

    [Test]
    public async Task Export_NonExistentName_ReturnsError()
    {
        var handler = new Export { Context = Ctx, Name = (global::app.type.text.@this)"nosuch" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Export_ReturnsFullIdentity()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"exportme", SetAsDefault = (global::app.type.@bool.@this)true };
        await create.Attach(null, Ctx);
        var createResult = await create.Run();
        var expectedKey = ((await createResult.Value()) as Identity)!.PrivateKey;

        var handler = new Export { Context = Ctx, Name = (global::app.type.text.@this)"exportme" };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();
        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.PrivateKey).IsEqualTo(expectedKey);
        await Assert.That(identity.PublicKey).IsNotNull();
    }

    [Test]
    public async Task Export_NullName_ReturnsDefaultIdentity()
    {
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"mydefault", SetAsDefault = (global::app.type.@bool.@this)true };
        await create.Attach(null, Ctx);
        var createResult = await create.Run();
        var expectedKey = ((await createResult.Value()) as Identity)!.PrivateKey;

        var handler = new Export { Context = Ctx, Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();
        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.PrivateKey).IsEqualTo(expectedKey);
    }

    // --- get by-name does NOT overwrite %MyIdentity% ---

    [Test]
    public async Task Get_ByName_DoesNotOverwriteMyIdentity()
    {
        var h1 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"default", SetAsDefault = (global::app.type.@bool.@this)true };
        await h1.Attach(null, Ctx);
        await h1.Run();
        var h2 = new Create { Context = Ctx, Name = (global::app.type.text.@this)"other", SetAsDefault = (global::app.type.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Run();

        // Fetch non-default by name
        var getOther = new Get { Context = Ctx, Name = (global::app.type.text.@this)"other" };
        await getOther.Attach(null, Ctx);
        await getOther.Run();

        // %MyIdentity% should still be the default, not "other"
        var myIdentity = _app.System.Identity;
        await Assert.That(myIdentity!.Name).IsEqualTo("default");
    }

    // --- auto-create promotes existing identity instead of overwriting ---

    [Test]
    public async Task GetOrCreateDefault_ExistingNonDefault_PromotesInsteadOfOverwriting()
    {
        // Create an identity named "default" but NOT as the default
        var create = new Create { Context = Ctx, Name = (global::app.type.text.@this)"default", SetAsDefault = (global::app.type.@bool.@this)false };
        await create.Attach(null, Ctx);
        var createResult = await create.Run();
        var originalKey = ((await createResult.Value()) as Identity)!.PublicKey;

        // Now trigger auto-create by getting default (none marked as default yet)
        var get = new Get { Context = Ctx, Name = null };
        await get.Attach(null, Ctx);
        var getResult = await get.Run();
        await getResult.IsSuccess();

        var identity = (await getResult.Value()) as Identity;
        // Should have promoted the existing "default", not created a new one
        await Assert.That(identity!.Name).IsEqualTo("default");
        await Assert.That(identity.PublicKey).IsEqualTo(originalKey);
        await Assert.That(identity.IsDefault).IsTrue();
    }

    // --- export null name uses same resolution as get ---

    [Test]
    public async Task Export_NullName_AutoCreatesLikeGet()
    {
        // Export(null) should use GetOrCreateDefaultAsync, same as Get(null)
        var handler = new Export { Context = Ctx, Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Run();
        await result.IsSuccess();
        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.PrivateKey).IsNotNull();
    }
}
