using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Interfaces;
using PLang.Models;
using PLangTests;

namespace PLang.Services.IdentityService.Tests;

[TestClass]
public class PLangIdentityServiceTests : BasePLangTest
{
    private PLangIdentityService pis;
    private IPublicPrivateKeyCreator publicPrivateKeyCreator;

    [TestInitialize]
    public void Init()
    {
        Initialize();

        publicPrivateKeyCreator = Substitute.For<IPublicPrivateKeyCreator>();

        var settings = new List<Setting>();
        var identites = new List<Identity>
        {
            new("default", "Jgr2bN4rUi51cc44T0XOYIdsBx62kSSehj8IxBqhlgA=",
                "wDsnw/J1HfCj35ov/ysJbCh5Krj7rvNp3svxc0hoSjU=") { IsDefault = true },
            new("default2", "KuxObK4AAbOcKujrmv2MtULSHW7uRYumkXTWs8gHAHA=",
                "QsdEA952ti9f1km3x3bk7tnqZsLzOGno5QI1ae/cxig=") { IsDefault = false }
        };
        var setting = new Setting("1", typeof(PLangIdentityService).FullName, typeof(List<Identity>).ToString(),
            PLangIdentityService.SettingKey, JsonConvert.SerializeObject(identites));
        settings.Add(setting);

        settingsRepository = Substitute.For<ISettingsRepository>();

        settingsRepository.GetSettings().Returns(settings);
        settingsRepository.Get(typeof(PLangIdentityService).FullName, typeof(List<Identity>).ToString(),
                PLangIdentityService.SettingKey)
            .Returns(setting);

        publicPrivateKeyCreator.Create().Returns(new PublicPrivateKey("1234", "abcd"));


        pis = new PLangIdentityService(settingsRepository, publicPrivateKeyCreator, context);
    }


    [TestMethod]
    public void CreateIdentityTest_NoIdentityExists()
    {
        var settings = new List<Setting>();
        Setting? setting = null;

        settingsRepository.GetSettings().Returns(settings);
        settingsRepository.Get(typeof(PLangIdentityService).FullName, typeof(List<Identity>).ToString(),
                PLangIdentityService.SettingKey)
            .Returns(setting);


        settingsRepository
            .When(x => x.Set(Arg.Any<Setting>()))
            .Do(callInfo =>
            {
                settingsRepository.Get(typeof(PLangIdentityService).FullName, typeof(List<Identity>).ToString(),
                        PLangIdentityService.SettingKey)
                    .Returns(callInfo.Arg<Setting>());
            });

        publicPrivateKeyCreator.Create().Returns(new PublicPrivateKey("123", "abc"));

        var identity = pis.CreateIdentity("main");

        Assert.IsNotNull(identity);
        Assert.AreEqual("123", identity.Identifier);
        Assert.AreEqual("main", identity.Name);
        Assert.AreEqual(false, identity.IsDefault);
        Assert.AreEqual(false, identity.IsArchived);
        Assert.AreEqual(null, identity.Value);
    }

    [TestMethod]
    public void CreateIdentityTest_IdentityExistsNotDefault()
    {
        //settings.GetValues<Identity>(typeof(PLangIdentityService)).Returns(new List<Identity>() { new Identity("default", "aaa", null) });


        var identity = pis.CreateIdentity("myIdentity");

        Assert.IsNotNull(identity);
        Assert.AreEqual("1234", identity.Identifier);
        Assert.AreEqual("myIdentity", identity.Name);
        Assert.AreEqual(false, identity.IsDefault);
        Assert.AreEqual(false, identity.IsArchived);
        Assert.AreEqual(null, identity.Value);
    }

    [TestMethod]
    public void CreateIdentityTest_IdentityExistsSetNewAsDefault()
    {
        settings.GetValues<Identity>(typeof(PLangIdentityService)).Returns(
            new List<Identity>
            {
                new("default", "aaa", null) { IsDefault = true },
                new("default2", "aaaff", null) { IsDefault = false }
            });


        var identity = pis.CreateIdentity("myIdentity", true);

        Assert.IsNotNull(identity);
        Assert.AreEqual("1234", identity.Identifier);
        Assert.AreEqual("myIdentity", identity.Name);
        Assert.AreEqual(true, identity.IsDefault);
        Assert.AreEqual(false, identity.IsArchived);
        Assert.AreEqual(null, identity.Value);
    }

    [TestMethod]
    [ExpectedException(typeof(IdentityException))]
    public void CreateIdentityTest_IdentityAlreadyExists()
    {
        var identity = pis.CreateIdentity("default");
    }

    [TestMethod]
    public void GetIdentityTest()
    {
        var identity = pis.GetIdentity("default");

        Assert.IsNotNull(identity);
        Assert.AreEqual("Jgr2bN4rUi51cc44T0XOYIdsBx62kSSehj8IxBqhlgA=", identity.Identifier);
        Assert.AreEqual("default", identity.Name);
        Assert.AreEqual(true, identity.IsDefault);
        Assert.AreEqual(false, identity.IsArchived);
        Assert.AreEqual(null, identity.Value);

        var identity2 = pis.GetIdentity("default2");
        Assert.IsNotNull(identity2);
        Assert.AreEqual(identity2.Identifier, "KuxObK4AAbOcKujrmv2MtULSHW7uRYumkXTWs8gHAHA=");
        Assert.AreEqual(identity2.Name, "default2");
        Assert.AreEqual(identity2.IsDefault, false);
        Assert.AreEqual(identity2.IsArchived, false);
        Assert.AreEqual(identity2.Value, null);
    }

    [TestMethod]
    [ExpectedException(typeof(IdentityException))]
    public void GetIdentityTest_NotFound()
    {
        var identity = pis.GetIdentity("default22");
    }

    [TestMethod]
    public void SetIdentityTest()
    {
        pis.SetIdentity("default2");

        var identity = pis.GetCurrentIdentity();
        Assert.IsNotNull(identity);
        Assert.AreEqual("KuxObK4AAbOcKujrmv2MtULSHW7uRYumkXTWs8gHAHA=", identity.Identifier);
        Assert.AreEqual("default2", identity.Name);
        Assert.AreEqual(false, identity.IsDefault);
        Assert.AreEqual(false, identity.IsArchived);
        Assert.AreEqual(null, identity.Value);
    }

    [TestMethod]
    public void GetCurrentIdentityTest_NonHasBeenSet_UsesDefault()
    {
        settings.GetValues<Identity>(typeof(PLangIdentityService)).Returns(
            new List<Identity>
            {
                new("default", "aaa", null) { IsDefault = true },
                new("default2", "aaaff", null) { IsDefault = false }
            });

        var identity = pis.GetCurrentIdentity();
        Assert.IsNotNull(identity);
        Assert.AreEqual("default", identity.Name);
        Assert.AreEqual("Jgr2bN4rUi51cc44T0XOYIdsBx62kSSehj8IxBqhlgA=", identity.Identifier);
        Assert.AreEqual(true, identity.IsDefault);
        Assert.AreEqual(false, identity.IsArchived);
        Assert.AreEqual(null, identity.Value);
    }

    [TestMethod]
    public void GetCurrentIdentityTest_NoDefaultExistsSelectFirst()
    {
        var identity = pis.GetCurrentIdentity();
        Assert.IsNotNull(identity);
        Assert.AreEqual("default", identity.Name);
        Assert.AreEqual("Jgr2bN4rUi51cc44T0XOYIdsBx62kSSehj8IxBqhlgA=", identity.Identifier);
        Assert.AreEqual(true, identity.IsDefault);
        Assert.AreEqual(false, identity.IsArchived);
        Assert.AreEqual(null, identity.Value);
    }

    [TestMethod]
    public void GetCurrentIdentityTest_CreateNewIdentity()
    {
        var settings = new List<Setting>();
        settingsRepository = Substitute.For<ISettingsRepository>();
        settingsRepository.GetSettings().Returns(settings);
        settingsRepository
            .When(x => x.Set(Arg.Any<Setting>()))
            .Do(callInfo =>
            {
                settingsRepository.Get(typeof(PLangIdentityService).FullName, typeof(List<Identity>).ToString(),
                        PLangIdentityService.SettingKey)
                    .Returns(callInfo.Arg<Setting>());
            });


        var pis = new PLangIdentityService(settingsRepository, publicPrivateKeyCreator, context);
        var identity = pis.GetCurrentIdentity();
        Assert.IsNotNull(identity);
        Assert.AreEqual("1234", identity.Identifier);
        Assert.AreEqual("MyIdentity", identity.Name);
        Assert.AreEqual(true, identity.IsDefault);
        Assert.AreEqual(false, identity.IsArchived);
        Assert.AreEqual(null, identity.Value);
    }


    [TestMethod]
    public void ArchiveIdentityTest()
    {
        var identity = pis.ArchiveIdentity("default2");

        Assert.IsNotNull(identity);
    }

    [TestMethod]
    [ExpectedException(typeof(IdentityException))]
    public void ArchiveIdentityTest_IsDefaultCannotBeArchived()
    {
        settings.GetValues<Identity>(typeof(PLangIdentityService)).Returns(
            new List<Identity>
            {
                new("default", "aaa",
                        "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40")
                    { IsDefault = true },
                new("default2", "aaaff",
                        "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40")
                    { IsDefault = false }
            });

        var pis = new PLangIdentityService(settingsRepository, publicPrivateKeyCreator, context);
        var identity = pis.ArchiveIdentity("default");
    }
}