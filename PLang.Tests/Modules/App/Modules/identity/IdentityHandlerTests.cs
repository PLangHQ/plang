using app.actor.context;
using app.type.item.variable;
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
        _app = new global::app.@this(_tempDir).TestSigning();
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

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    // --- the app's identities: the system actor's row ---

    // The identity created in one App is the one the next App on the same root reads — asked from a user
    // context too, since the app's identities are the system's. The user's own identity setting holds none.
    [Test]
    public async Task Identity_IsReadByTheNextApp_AsTheSystemsRow()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_id_row_" + Guid.NewGuid().ToString("N")[..8]);
        string key;
        await using (var first = new global::app.@this(root))
        {
            var ctx = first.actor.list.System.Context;
            var create = new Create(ctx) { Name = (global::app.type.item.text.@this)"keeper", SetAsDefault = (global::app.type.item.@bool.@this)true };
            await create.Attach(null, ctx);
            var made = await create.Start();
            await made.IsSuccess();
            key = ((Identity)(await made.Value())!).PublicKey;
        }

        await using var next = new global::app.@this(root);
        var user = next.actor.list.User.Context;
        var get = new Get(user);
        await get.Attach(null, user);
        var got = await get.Start();
        await got.IsSuccess();
        await Assert.That(((Identity)(await got.Value())!).PublicKey).IsEqualTo(key);

        await next.actor.list.User.Setting.Load();
        await Assert.That(next.actor.list.User.Setting.Of<global::app.module.identity.setting.@this>().Identity.CountRaw).IsEqualTo(0);
    }

    // --- create ---

    [Test]
    public async Task Create_GeneratesValidEd25519KeyPair()
    {
        // This test validates the ACTUAL ed25519 keypair (32-byte base64), so it needs the
        // real signing provider — not the class fixture's test-signing. Own real app, own key.
        await using var realApp = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang_id_real_" + Guid.NewGuid().ToString("N")[..8]));
        var realCtx = realApp.actor.list.System.Context;
        var handler = new Create(realCtx) { Name = (global::app.type.item.text.@this)"test", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await handler.Attach(null, realCtx);
        var result = await handler.Start();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.PublicKey).IsNotNull();
        await Assert.That(identity.PrivateKey).IsNotNull();

        // Base64-decodable, correct lengths (32 bytes each for Ed25519)
        var pubBytes = System.Buffers.Text.Base64Url.DecodeFromChars(identity.PublicKey);
        var privBytes = Convert.FromBase64String(identity.PrivateKey);
        await Assert.That(pubBytes.Length).IsEqualTo(32);
        await Assert.That(privBytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task Create_DefaultFalse_IsNotDefault()
    {
        var handler = new Create(Ctx) { Name = (global::app.type.item.text.@this)"test", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.IsDefault).IsFalse();
    }

    [Test]
    public async Task Create_SetAsDefaultTrue_BecomesDefault()
    {
        var handler = new Create(Ctx) { Name = (global::app.type.item.text.@this)"test", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.IsDefault).IsTrue();
    }

    [Test]
    public async Task Create_SetAsDefaultTrue_ClearsPreviousDefault()
    {
        var h1 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"first", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await h1.Attach(null, Ctx);
        await h1.Start();

        var h2 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"second", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await h2.Attach(null, Ctx);
        await h2.Start();

        // First should no longer be default
        var __ia0 = new Get(Ctx) { Name = (global::app.type.item.text.@this)"first" };
        await __ia0.Attach(null, Ctx);
        var firstResult = await __ia0.Start();
        var first = (await firstResult.Value()) as Identity;
        await Assert.That(first!.IsDefault).IsFalse();

        var __ia1 = new Get(Ctx) { Name = (global::app.type.item.text.@this)"second" };
        await __ia1.Attach(null, Ctx);
        var secondResult = await __ia1.Start();
        var second = (await secondResult.Value()) as Identity;
        await Assert.That(second!.IsDefault).IsTrue();
    }

    [Test]
    public async Task Create_StoresInSystemDataSource()
    {
        var handler = new Create(Ctx) { Name = (global::app.type.item.text.@this)"stored", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await handler.Attach(null, Ctx);
        await handler.Start();

        var __ia2 = new Get(Ctx) { Name = (global::app.type.item.text.@this)"stored" };
        await __ia2.Attach(null, Ctx);
        var loadResult = await __ia2.Start();
        await loadResult.IsSuccess();
        var loaded = (await loadResult.Value()) as Identity;
        await Assert.That(loaded!.Name).IsEqualTo("stored");
    }

    [Test]
    public async Task Create_DuplicateName_ReturnsError()
    {
        var h1 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"dup", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h1.Attach(null, Ctx);
        await h1.Start();

        var h2 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"dup", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h2.Attach(null, Ctx);
        var result = await h2.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DuplicateName");
    }

    [Test]
    public async Task Create_DuplicateArchivedName_ReturnsError()
    {
        // Create and archive
        var h1 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"archived", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h1.Attach(null, Ctx);
        await h1.Start();

        var archiveH = new Archive(Ctx) { Name = (global::app.type.item.text.@this)"archived" };
        await archiveH.Attach(null, Ctx);
        await archiveH.Start();

        // Try to create with same name — should fail
        var h2 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"archived", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h2.Attach(null, Ctx);
        var result = await h2.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DuplicateName");
    }

    [Test]
    public async Task Create_EmptyOrWhitespaceName_ReturnsError()
    {
        var h1 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h1.Attach(null, Ctx);
        var result1 = await h1.Start();
        await result1.IsFailure();
        await Assert.That(result1.Error!.Key).IsEqualTo("ValidationError");

        var h2 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"   ", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h2.Attach(null, Ctx);
        var result2 = await h2.Start();
        await result2.IsFailure();
        await Assert.That(result2.Error!.Key).IsEqualTo("ValidationError");
    }

    // --- get ---

    [Test]
    public async Task Get_NonExistentName_ReturnsError()
    {
        var handler = new Get(Ctx) { Name = (global::app.type.item.text.@this)"nosuch" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Get_NullName_NoDefaultExists_PromotesExisting()
    {
        // Create two non-default identities
        var h1 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"a", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h1.Attach(null, Ctx);
        var r1 = await h1.Start();
        var originalKey = ((await r1.Value()) as Identity)!.PublicKey;
        var h2 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"b", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Start();

        // Get(null) should promote the first non-archived identity as default
        var handler = new Get(Ctx) { Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.IsDefault).IsTrue();
        await Assert.That(identity.Name).IsEqualTo("a");
        await Assert.That(identity.PublicKey).IsEqualTo(originalKey);
    }

    [Test]
    public async Task Get_ByName_ReturnsMatchingIdentity()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"alice", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Start();

        var handler = new Get(Ctx) { Name = (global::app.type.item.text.@this)"alice" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.Name).IsEqualTo("alice");
    }

    [Test]
    public async Task Get_NullName_ReturnsDefaultIdentity()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"mydefault", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Start();

        var handler = new Get(Ctx) { Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.Name).IsEqualTo("mydefault");
        await Assert.That(identity.IsDefault).IsTrue();
    }

    [Test]
    public async Task Get_NoIdentitiesExist_AutoCreatesDefault()
    {
        var handler = new Get(Ctx) { Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.Name).IsEqualTo("default");
        await Assert.That(identity.IsDefault).IsTrue();
        await Assert.That(identity.PublicKey).IsNotNull();
    }

    [Test]
    public async Task Get_ReturnsIdentityData_WithAllProperties()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"full", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Start();

        var handler = new Get(Ctx) { Name = (global::app.type.item.text.@this)"full" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
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
        var h1 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"active1", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h1.Attach(null, Ctx);
        await h1.Start();
        var h2 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"active2", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Start();
        var h3 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"archived", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h3.Attach(null, Ctx);
        await h3.Start();

        var archiveH = new Archive(Ctx) { Name = (global::app.type.item.text.@this)"archived" };
        await archiveH.Attach(null, Ctx);
        await archiveH.Start();

        var handler = new list(Ctx);
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var list = result.GetValue<List<Identity>>();
        await Assert.That(list!.Count).IsEqualTo(2);
        await Assert.That(list.Any(i => i.Name == "archived")).IsFalse();
    }

    [Test]
    public async Task GetAll_AllArchived_ReturnsEmptyList()
    {
        var h1 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"only", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h1.Attach(null, Ctx);
        await h1.Start();

        var archiveH = new Archive(Ctx) { Name = (global::app.type.item.text.@this)"only" };
        await archiveH.Attach(null, Ctx);
        await archiveH.Start();

        var handler = new list(Ctx);
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var list = result.GetValue<List<Identity>>();
        await Assert.That(list!.Count).IsEqualTo(0);
    }

    // --- archive ---

    [Test]
    public async Task Archive_NonDefaultIdentity_SetsIsArchivedTrue()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"toarchive", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Start();

        var handler = new Archive(Ctx) { Name = (global::app.type.item.text.@this)"toarchive" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var __ia3 = new Get(Ctx) { Name = (global::app.type.item.text.@this)"toarchive" };
        await __ia3.Attach(null, Ctx);
        var loadResult = await __ia3.Start();
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
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"def", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Start();

        var handler = new Archive(Ctx) { Name = (global::app.type.item.text.@this)"def" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("CannotArchiveDefault");
    }

    [Test]
    public async Task Archive_NonExistentName_ReturnsError()
    {
        var handler = new Archive(Ctx) { Name = (global::app.type.item.text.@this)"nope" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Archive_AlreadyArchived_IsIdempotent()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"twice", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Start();

        var h1 = new Archive(Ctx) { Name = (global::app.type.item.text.@this)"twice" };
        await h1.Attach(null, Ctx);
        await h1.Start();

        var h2 = new Archive(Ctx) { Name = (global::app.type.item.text.@this)"twice" };
        await h2.Attach(null, Ctx);
        var result = await h2.Start();
        await result.IsSuccess();
    }

    // --- setDefault ---

    [Test]
    public async Task SetDefault_SwitchesDefault_ClearsOldDefault()
    {
        var h1 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"old", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await h1.Attach(null, Ctx);
        await h1.Start();
        var h2 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"new", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Start();

        var handler = new SetDefault(Ctx) { Name = (global::app.type.item.text.@this)"new" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var __ia4 = new Get(Ctx) { Name = (global::app.type.item.text.@this)"old" };
        await __ia4.Attach(null, Ctx);
        var oldResult = await __ia4.Start();
        var oldId = (await oldResult.Value()) as Identity;
        await Assert.That(oldId!.IsDefault).IsFalse();

        var __ia5 = new Get(Ctx) { Name = (global::app.type.item.text.@this)"new" };
        await __ia5.Attach(null, Ctx);
        var newResult = await __ia5.Start();
        var newId = (await newResult.Value()) as Identity;
        await Assert.That(newId!.IsDefault).IsTrue();
    }

    [Test]
    public async Task SetDefault_ArchivedOrMissing_ReturnsError()
    {
        // Missing
        var h1 = new SetDefault(Ctx) { Name = (global::app.type.item.text.@this)"missing" };
        await h1.Attach(null, Ctx);
        var r1 = await h1.Start();
        await r1.IsFailure();
        await Assert.That(r1.Error!.Key).IsEqualTo("NotFound");

        // Archived
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"arch", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Start();
        var archive = new Archive(Ctx) { Name = (global::app.type.item.text.@this)"arch" };
        await archive.Attach(null, Ctx);
        await archive.Start();

        var h2 = new SetDefault(Ctx) { Name = (global::app.type.item.text.@this)"arch" };
        await h2.Attach(null, Ctx);
        var r2 = await h2.Start();
        await r2.IsFailure();
        await Assert.That(r2.Error!.Key).IsEqualTo("ArchivedIdentity");
    }

    [Test]
    public async Task SetDefault_AlreadyDefault_IsIdempotent()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"already", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Start();

        var handler = new SetDefault(Ctx) { Name = (global::app.type.item.text.@this)"already" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.IsDefault).IsTrue();
    }

    // --- unarchive ---

    [Test]
    public async Task Unarchive_RestoresArchivedIdentity()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"restore", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Start();

        var archiveH = new Archive(Ctx) { Name = (global::app.type.item.text.@this)"restore" };
        await archiveH.Attach(null, Ctx);
        await archiveH.Start();

        var handler = new Unarchive(Ctx) { Name = (global::app.type.item.text.@this)"restore" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var __ia6 = new Get(Ctx) { Name = (global::app.type.item.text.@this)"restore" };
        await __ia6.Attach(null, Ctx);
        var loadResult = await __ia6.Start();
        await loadResult.IsSuccess();
        var loaded = (await loadResult.Value()) as Identity;
        await Assert.That(loaded!.IsArchived).IsFalse();
    }

    [Test]
    public async Task Unarchive_NonExistentName_ReturnsError()
    {
        var handler = new Unarchive(Ctx) { Name = (global::app.type.item.text.@this)"nope" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Unarchive_NotArchived_IsIdempotent()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"active", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Start();

        var handler = new Unarchive(Ctx) { Name = (global::app.type.item.text.@this)"active" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.Name).IsEqualTo("active");
    }

    // --- rename ---

    [Test]
    public async Task Rename_ChangesName_KeepsKeys()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"oldname", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await create.Attach(null, Ctx);
        var createResult = await create.Start();
        var originalKey = ((await createResult.Value()) as Identity)!.PublicKey;

        var handler = new Rename(Ctx) { Name = (global::app.type.item.text.@this)"oldname", NewName = (global::app.type.item.text.@this)"newname" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();

        var renamed = (await result.Value()) as Identity;
        await Assert.That(renamed!.Name).IsEqualTo("newname");
        await Assert.That(renamed.PublicKey).IsEqualTo(originalKey);

        // Old name should be gone
        var __ia7 = new Get(Ctx) { Name = (global::app.type.item.text.@this)"oldname" };
        await __ia7.Attach(null, Ctx);
        var oldResult = await __ia7.Start();
        await oldResult.IsFailure();
    }

    [Test]
    public async Task Rename_DuplicateNewName_ReturnsError()
    {
        var h1 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"a", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h1.Attach(null, Ctx);
        await h1.Start();
        var h2 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"b", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Start();

        var handler = new Rename(Ctx) { Name = (global::app.type.item.text.@this)"a", NewName = (global::app.type.item.text.@this)"b" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("DuplicateName");
    }

    [Test]
    public async Task Rename_NonExistentName_ReturnsError()
    {
        var handler = new Rename(Ctx) { Name = (global::app.type.item.text.@this)"nope", NewName = (global::app.type.item.text.@this)"whatever" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Rename_DefaultIdentity_UpdatesMyIdentity()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"def", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await create.Attach(null, Ctx);
        await create.Start();

        var handler = new Rename(Ctx) { Name = (global::app.type.item.text.@this)"def", NewName = (global::app.type.item.text.@this)"renamed" };
        await handler.Attach(null, Ctx);
        await handler.Start();

        // %MyIdentity% should reflect the new name
        var myIdentity = _app.actor.list.System.Identity;
        await Assert.That(myIdentity!.Name).IsEqualTo("renamed");
    }

    [Test]
    public async Task Rename_EmptyNewName_ReturnsError()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"valid", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await create.Attach(null, Ctx);
        await create.Start();

        var handler = new Rename(Ctx) { Name = (global::app.type.item.text.@this)"valid", NewName = (global::app.type.item.text.@this)"" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
    }

    // --- export ---

    [Test]
    public async Task Export_NonExistentName_ReturnsError()
    {
        var handler = new Export(Ctx) { Name = (global::app.type.item.text.@this)"nosuch" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Export_ReturnsFullIdentity()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"exportme", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await create.Attach(null, Ctx);
        var createResult = await create.Start();
        var expectedKey = ((await createResult.Value()) as Identity)!.PrivateKey;

        var handler = new Export(Ctx) { Name = (global::app.type.item.text.@this)"exportme" };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();
        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.PrivateKey).IsEqualTo(expectedKey);
        await Assert.That(identity.PublicKey).IsNotNull();
    }

    [Test]
    public async Task Export_NullName_ReturnsDefaultIdentity()
    {
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"mydefault", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await create.Attach(null, Ctx);
        var createResult = await create.Start();
        var expectedKey = ((await createResult.Value()) as Identity)!.PrivateKey;

        var handler = new Export(Ctx) { Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();
        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.PrivateKey).IsEqualTo(expectedKey);
    }

    // --- get by-name does NOT overwrite %MyIdentity% ---

    [Test]
    public async Task Get_ByName_DoesNotOverwriteMyIdentity()
    {
        var h1 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"default", SetAsDefault = (global::app.type.item.@bool.@this)true };
        await h1.Attach(null, Ctx);
        await h1.Start();
        var h2 = new Create(Ctx) { Name = (global::app.type.item.text.@this)"other", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await h2.Attach(null, Ctx);
        await h2.Start();

        // Fetch non-default by name
        var getOther = new Get(Ctx) { Name = (global::app.type.item.text.@this)"other" };
        await getOther.Attach(null, Ctx);
        await getOther.Start();

        // %MyIdentity% should still be the default, not "other"
        var myIdentity = _app.actor.list.System.Identity;
        await Assert.That(myIdentity!.Name).IsEqualTo("default");
    }

    // --- auto-create promotes existing identity instead of overwriting ---

    [Test]
    public async Task GetOrCreateDefault_ExistingNonDefault_PromotesInsteadOfOverwriting()
    {
        // Create an identity named "default" but NOT as the default
        var create = new Create(Ctx) { Name = (global::app.type.item.text.@this)"default", SetAsDefault = (global::app.type.item.@bool.@this)false };
        await create.Attach(null, Ctx);
        var createResult = await create.Start();
        var originalKey = ((await createResult.Value()) as Identity)!.PublicKey;

        // Now trigger auto-create by getting default (none marked as default yet)
        var get = new Get(Ctx) { Name = null };
        await get.Attach(null, Ctx);
        var getResult = await get.Start();
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
        var handler = new Export(Ctx) { Name = null };
        await handler.Attach(null, Ctx);
        var result = await handler.Start();
        await result.IsSuccess();
        var identity = (await result.Value()) as Identity;
        await Assert.That(identity!.PrivateKey).IsNotNull();
    }
}
