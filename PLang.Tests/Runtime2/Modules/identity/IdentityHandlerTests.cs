using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.DataSource;
using PLang.Runtime2.Engine.Errors;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.identity;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.identity;

public class IdentityHandlerTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_identity_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private PLangContext Ctx => _engine.System.Context;

    // --- create ---

    [Test]
    public async Task Create_GeneratesValidEd25519KeyPair()
    {
        var handler = new Create { Context = Ctx, Name = "test", SetAsDefault = true };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var identity = result.Value as IdentityVariable;
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
        var handler = new Create { Context = Ctx, Name = "test", SetAsDefault = false };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var identity = result.Value as IdentityVariable;
        await Assert.That(identity!.IsDefault).IsFalse();
    }

    [Test]
    public async Task Create_SetAsDefaultTrue_BecomesDefault()
    {
        var handler = new Create { Context = Ctx, Name = "test", SetAsDefault = true };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var identity = result.Value as IdentityVariable;
        await Assert.That(identity!.IsDefault).IsTrue();
    }

    [Test]
    public async Task Create_SetAsDefaultTrue_ClearsPreviousDefault()
    {
        var h1 = new Create { Context = Ctx, Name = "first", SetAsDefault = true };
        await h1.Run();

        var h2 = new Create { Context = Ctx, Name = "second", SetAsDefault = true };
        await h2.Run();

        // First should no longer be default
        var first = await IdentityVariable.LoadAsync(_engine, "first");
        await Assert.That(first!.IsDefault).IsFalse();

        var second = await IdentityVariable.LoadAsync(_engine, "second");
        await Assert.That(second!.IsDefault).IsTrue();
    }

    [Test]
    public async Task Create_StoresInSystemDataSource()
    {
        var handler = new Create { Context = Ctx, Name = "stored", SetAsDefault = false };
        await handler.Run();

        var loaded = await IdentityVariable.LoadAsync(_engine, "stored");
        await Assert.That(loaded).IsNotNull();
        await Assert.That(loaded!.Name).IsEqualTo("stored");
    }

    [Test]
    public async Task Create_DuplicateName_ReturnsError()
    {
        var h1 = new Create { Context = Ctx, Name = "dup", SetAsDefault = false };
        await h1.Run();

        var h2 = new Create { Context = Ctx, Name = "dup", SetAsDefault = false };
        var result = await h2.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("DuplicateName");
    }

    [Test]
    public async Task Create_DuplicateArchivedName_ReturnsError()
    {
        // Create and archive
        var h1 = new Create { Context = Ctx, Name = "archived", SetAsDefault = false };
        await h1.Run();

        var archiveH = new Archive { Context = Ctx, Name = "archived" };
        await archiveH.Run();

        // Try to create with same name — should fail
        var h2 = new Create { Context = Ctx, Name = "archived", SetAsDefault = false };
        var result = await h2.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("DuplicateName");
    }

    [Test]
    public async Task Create_EmptyOrWhitespaceName_ReturnsError()
    {
        var h1 = new Create { Context = Ctx, Name = "", SetAsDefault = false };
        var result1 = await h1.Run();
        await Assert.That(result1.Success).IsFalse();
        await Assert.That(result1.Error!.Key).IsEqualTo("ValidationError");

        var h2 = new Create { Context = Ctx, Name = "   ", SetAsDefault = false };
        var result2 = await h2.Run();
        await Assert.That(result2.Success).IsFalse();
        await Assert.That(result2.Error!.Key).IsEqualTo("ValidationError");
    }

    // --- get ---

    [Test]
    public async Task Get_NonExistentName_ReturnsError()
    {
        var handler = new Get { Context = Ctx, Name = "nosuch" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Get_NullName_NoDefaultExists_AutoCreates()
    {
        // Create two non-default identities
        var h1 = new Create { Context = Ctx, Name = "a", SetAsDefault = false };
        await h1.Run();
        var h2 = new Create { Context = Ctx, Name = "b", SetAsDefault = false };
        await h2.Run();

        // Get(null) should auto-create a default
        var handler = new Get { Context = Ctx, Name = null };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var identity = result.Value as IdentityVariable;
        await Assert.That(identity!.IsDefault).IsTrue();
        await Assert.That(identity.Name).IsEqualTo("default");
    }

    [Test]
    public async Task Get_ByName_ReturnsMatchingIdentity()
    {
        var create = new Create { Context = Ctx, Name = "alice", SetAsDefault = false };
        await create.Run();

        var handler = new Get { Context = Ctx, Name = "alice" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var identity = result.Value as IdentityVariable;
        await Assert.That(identity!.Name).IsEqualTo("alice");
    }

    [Test]
    public async Task Get_NullName_ReturnsDefaultIdentity()
    {
        var create = new Create { Context = Ctx, Name = "mydefault", SetAsDefault = true };
        await create.Run();

        var handler = new Get { Context = Ctx, Name = null };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var identity = result.Value as IdentityVariable;
        await Assert.That(identity!.Name).IsEqualTo("mydefault");
        await Assert.That(identity.IsDefault).IsTrue();
    }

    [Test]
    public async Task Get_NoIdentitiesExist_AutoCreatesDefault()
    {
        var handler = new Get { Context = Ctx, Name = null };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var identity = result.Value as IdentityVariable;
        await Assert.That(identity!.Name).IsEqualTo("default");
        await Assert.That(identity.IsDefault).IsTrue();
        await Assert.That(identity.PublicKey).IsNotNull();
    }

    [Test]
    public async Task Get_ReturnsIdentityVariable_WithAllProperties()
    {
        var create = new Create { Context = Ctx, Name = "full", SetAsDefault = true };
        await create.Run();

        var handler = new Get { Context = Ctx, Name = "full" };
        var result = await handler.Run();
        var identity = result.Value as IdentityVariable;

        await Assert.That(identity!.Name).IsEqualTo("full");
        await Assert.That(identity.PublicKey).IsNotNull();
        await Assert.That(identity.PrivateKey).IsNotNull();
        await Assert.That(identity.IsDefault).IsTrue();
        await Assert.That(identity.IsArchived).IsFalse();
        await Assert.That(identity.Created).IsNotEqualTo(default(DateTime));
    }

    // --- getAll ---

    [Test]
    public async Task GetAll_ReturnsOnlyNonArchived()
    {
        var h1 = new Create { Context = Ctx, Name = "active1", SetAsDefault = false };
        await h1.Run();
        var h2 = new Create { Context = Ctx, Name = "active2", SetAsDefault = false };
        await h2.Run();
        var h3 = new Create { Context = Ctx, Name = "archived", SetAsDefault = false };
        await h3.Run();

        var archiveH = new Archive { Context = Ctx, Name = "archived" };
        await archiveH.Run();

        var handler = new GetAll { Context = Ctx };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var list = result.Value as List<IdentityVariable>;
        await Assert.That(list!.Count).IsEqualTo(2);
        await Assert.That(list.Any(i => i.Name == "archived")).IsFalse();
    }

    [Test]
    public async Task GetAll_AllArchived_ReturnsEmptyList()
    {
        var h1 = new Create { Context = Ctx, Name = "only", SetAsDefault = false };
        await h1.Run();

        var archiveH = new Archive { Context = Ctx, Name = "only" };
        await archiveH.Run();

        var handler = new GetAll { Context = Ctx };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var list = result.Value as List<IdentityVariable>;
        await Assert.That(list!.Count).IsEqualTo(0);
    }

    // --- archive ---

    [Test]
    public async Task Archive_NonDefaultIdentity_SetsIsArchivedTrue()
    {
        var create = new Create { Context = Ctx, Name = "toarchive", SetAsDefault = false };
        await create.Run();

        var handler = new Archive { Context = Ctx, Name = "toarchive" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var loaded = await IdentityVariable.LoadAsync(_engine, "toarchive");
        await Assert.That(loaded!.IsArchived).IsTrue();
    }

    [Test]
    public async Task Archive_DefaultIdentity_ReturnsError()
    {
        var create = new Create { Context = Ctx, Name = "def", SetAsDefault = true };
        await create.Run();

        var handler = new Archive { Context = Ctx, Name = "def" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("CannotArchiveDefault");
    }

    [Test]
    public async Task Archive_NonExistentName_ReturnsError()
    {
        var handler = new Archive { Context = Ctx, Name = "nope" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Archive_AlreadyArchived_IsIdempotent()
    {
        var create = new Create { Context = Ctx, Name = "twice", SetAsDefault = false };
        await create.Run();

        var h1 = new Archive { Context = Ctx, Name = "twice" };
        await h1.Run();

        var h2 = new Archive { Context = Ctx, Name = "twice" };
        var result = await h2.Run();
        await Assert.That(result.Success).IsTrue();
    }

    // --- setDefault ---

    [Test]
    public async Task SetDefault_SwitchesDefault_ClearsOldDefault()
    {
        var h1 = new Create { Context = Ctx, Name = "old", SetAsDefault = true };
        await h1.Run();
        var h2 = new Create { Context = Ctx, Name = "new", SetAsDefault = false };
        await h2.Run();

        var handler = new SetDefault { Context = Ctx, Name = "new" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var oldId = await IdentityVariable.LoadAsync(_engine, "old");
        await Assert.That(oldId!.IsDefault).IsFalse();

        var newId = await IdentityVariable.LoadAsync(_engine, "new");
        await Assert.That(newId!.IsDefault).IsTrue();
    }

    [Test]
    public async Task SetDefault_ArchivedOrMissing_ReturnsError()
    {
        // Missing
        var h1 = new SetDefault { Context = Ctx, Name = "missing" };
        var r1 = await h1.Run();
        await Assert.That(r1.Success).IsFalse();
        await Assert.That(r1.Error!.Key).IsEqualTo("NotFound");

        // Archived
        var create = new Create { Context = Ctx, Name = "arch", SetAsDefault = false };
        await create.Run();
        var archive = new Archive { Context = Ctx, Name = "arch" };
        await archive.Run();

        var h2 = new SetDefault { Context = Ctx, Name = "arch" };
        var r2 = await h2.Run();
        await Assert.That(r2.Success).IsFalse();
        await Assert.That(r2.Error!.Key).IsEqualTo("ArchivedIdentity");
    }

    [Test]
    public async Task SetDefault_AlreadyDefault_IsIdempotent()
    {
        var create = new Create { Context = Ctx, Name = "already", SetAsDefault = true };
        await create.Run();

        var handler = new SetDefault { Context = Ctx, Name = "already" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var identity = result.Value as IdentityVariable;
        await Assert.That(identity!.IsDefault).IsTrue();
    }

    // --- unarchive ---

    [Test]
    public async Task Unarchive_RestoresArchivedIdentity()
    {
        var create = new Create { Context = Ctx, Name = "restore", SetAsDefault = false };
        await create.Run();

        var archiveH = new Archive { Context = Ctx, Name = "restore" };
        await archiveH.Run();

        var handler = new Unarchive { Context = Ctx, Name = "restore" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var loaded = await IdentityVariable.LoadAsync(_engine, "restore");
        await Assert.That(loaded!.IsArchived).IsFalse();
    }

    [Test]
    public async Task Unarchive_NonExistentName_ReturnsError()
    {
        var handler = new Unarchive { Context = Ctx, Name = "nope" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Unarchive_NotArchived_IsIdempotent()
    {
        var create = new Create { Context = Ctx, Name = "active", SetAsDefault = false };
        await create.Run();

        var handler = new Unarchive { Context = Ctx, Name = "active" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var identity = result.Value as IdentityVariable;
        await Assert.That(identity!.Name).IsEqualTo("active");
    }

    // --- rename ---

    [Test]
    public async Task Rename_ChangesName_KeepsKeys()
    {
        var create = new Create { Context = Ctx, Name = "oldname", SetAsDefault = false };
        var createResult = await create.Run();
        var originalKey = (createResult.Value as IdentityVariable)!.PublicKey;

        var handler = new Rename { Context = Ctx, Name = "oldname", NewName = "newname" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();

        var renamed = result.Value as IdentityVariable;
        await Assert.That(renamed!.Name).IsEqualTo("newname");
        await Assert.That(renamed.PublicKey).IsEqualTo(originalKey);

        // Old name should be gone
        var old = await IdentityVariable.LoadAsync(_engine, "oldname");
        await Assert.That(old).IsNull();
    }

    [Test]
    public async Task Rename_DuplicateNewName_ReturnsError()
    {
        var h1 = new Create { Context = Ctx, Name = "a", SetAsDefault = false };
        await h1.Run();
        var h2 = new Create { Context = Ctx, Name = "b", SetAsDefault = false };
        await h2.Run();

        var handler = new Rename { Context = Ctx, Name = "a", NewName = "b" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("DuplicateName");
    }

    [Test]
    public async Task Rename_NonExistentName_ReturnsError()
    {
        var handler = new Rename { Context = Ctx, Name = "nope", NewName = "whatever" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Rename_DefaultIdentity_UpdatesMyIdentity()
    {
        var create = new Create { Context = Ctx, Name = "def", SetAsDefault = true };
        await create.Run();

        var handler = new Rename { Context = Ctx, Name = "def", NewName = "renamed" };
        await handler.Run();

        // %MyIdentity% should reflect the new name
        var myIdentity = _engine.System.Identity.Value as IdentityVariable;
        await Assert.That(myIdentity!.Name).IsEqualTo("renamed");
    }

    [Test]
    public async Task Rename_EmptyNewName_ReturnsError()
    {
        var create = new Create { Context = Ctx, Name = "valid", SetAsDefault = false };
        await create.Run();

        var handler = new Rename { Context = Ctx, Name = "valid", NewName = "" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("ValidationError");
    }

    // --- export ---

    [Test]
    public async Task Export_NonExistentName_ReturnsError()
    {
        var handler = new Export { Context = Ctx, Name = "nosuch" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NotFound");
    }

    [Test]
    public async Task Export_ReturnsPrivateKeyString()
    {
        var create = new Create { Context = Ctx, Name = "exportme", SetAsDefault = true };
        var createResult = await create.Run();
        var expectedKey = (createResult.Value as IdentityVariable)!.PrivateKey;

        var handler = new Export { Context = Ctx, Name = "exportme" };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value as string).IsEqualTo(expectedKey);
    }

    [Test]
    public async Task Export_NullName_ReturnsDefaultPrivateKey()
    {
        var create = new Create { Context = Ctx, Name = "mydefault", SetAsDefault = true };
        var createResult = await create.Run();
        var expectedKey = (createResult.Value as IdentityVariable)!.PrivateKey;

        var handler = new Export { Context = Ctx, Name = null };
        var result = await handler.Run();
        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value as string).IsEqualTo(expectedKey);
    }

    // --- get by-name does NOT overwrite %MyIdentity% ---

    [Test]
    public async Task Get_ByName_DoesNotOverwriteMyIdentity()
    {
        var h1 = new Create { Context = Ctx, Name = "default", SetAsDefault = true };
        await h1.Run();
        var h2 = new Create { Context = Ctx, Name = "other", SetAsDefault = false };
        await h2.Run();

        // Fetch non-default by name
        var getOther = new Get { Context = Ctx, Name = "other" };
        await getOther.Run();

        // %MyIdentity% should still be the default, not "other"
        var myIdentity = _engine.System.Identity.Value as IdentityVariable;
        await Assert.That(myIdentity!.Name).IsEqualTo("default");
    }
}
