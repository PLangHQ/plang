using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Interfaces;
using PLang.Modules.MessageModule;

namespace PLangTests.Modules.MessageModule;

[TestClass]
public class ModuleSettingsTest : BasePLangTest
{
    private ModuleSettings moduleSettings;

    [TestInitialize]
    public void Init()
    {
        Initialize();

        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
    }


    [TestMethod]
    public void GetDefaultRelays_Test()
    {
        var relays = moduleSettings.GetRelays();

        Assert.AreEqual(4, relays.Count);
    }

    [TestMethod]
    public void AddRelay_Test()
    {
        settings = Substitute.For<ISettings>();
        var relayServer = new List<string>
        {
            "wss://relay.damus.io"
        };
        settings.GetValues<string>(typeof(ModuleSettings)).Returns(relayServer);

        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
        moduleSettings.AddRelay("wss://relay.damus.io");
        moduleSettings.AddRelay("wss://relay.damus.com");

        settings.Received(1).SetList(typeof(ModuleSettings),
            Arg.Is<List<string>>(p => p.Contains("wss://relay.damus.com")));
    }

    [TestMethod]
    public void RemoveRelay_Test()
    {
        settings = Substitute.For<ISettings>();
        var relayServer = new List<string>
        {
            "wss://relay.damus.io",
            "wss://relay.damus.com"
        };
        settings.GetValues<string>(typeof(ModuleSettings)).Returns(relayServer);

        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
        moduleSettings.RemoveRelay("wss://relay.damus.io");

        settings.Received(1).SetList(typeof(ModuleSettings), Arg.Is<List<string>>(p => p.Count == 1));
    }


    [TestMethod]
    public void CreateAccount_Test()
    {
        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
        moduleSettings.CreateNewAccount();

        settings.Received(1).SetList(typeof(ModuleSettings), Arg.Any<List<NostrKey>>());
    }


    [TestMethod]
    public void ArchiveAccount_SetSecondAccountAsDefault_Test()
    {
        settings = Substitute.For<ISettings>();
        settings.GetValues<NostrKey>(typeof(ModuleSettings)).Returns(new List<NostrKey>
        {
            new("Default", "a", "b", "c")
            {
                IsDefault = true
            },
            new("Default", "g", "e", "f")
        });
        ;


        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
        moduleSettings.ArchiveAccount("c");

        settings.Received(1).SetList(typeof(ModuleSettings), Arg.Any<List<NostrKey>>());
    }

    [TestMethod]
    public void ArchiveAccount_Test()
    {
        settings = Substitute.For<ISettings>();
        settings.GetValues<NostrKey>(typeof(ModuleSettings)).Returns(new List<NostrKey>
        {
            new("Default", "a", "b", "c")
            {
                IsDefault = true
            },
            new("Default", "g", "e", "f")
        });
        ;


        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
        moduleSettings.ArchiveAccount("f");

        settings.Received(1).SetList(typeof(ModuleSettings), Arg.Any<List<NostrKey>>());
    }

    [TestMethod]
    public void ArchiveAccount_CreatesNewAccount_Test()
    {
        settings = Substitute.For<ISettings>();
        settings.GetValues<NostrKey>(typeof(ModuleSettings)).Returns(new List<NostrKey>
        {
            new("Default", "a", "b", "c")
        });
        ;


        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
        moduleSettings.ArchiveAccount("c");

        settings.Received(2).SetList(typeof(ModuleSettings), Arg.Any<List<NostrKey>>());
    }

    [TestMethod]
    public void SetAsDefault_Test()
    {
        settings = Substitute.For<ISettings>();
        settings.GetValues<NostrKey>(typeof(ModuleSettings)).Returns(new List<NostrKey>
        {
            new("Default", "a", "b", "c")
            {
                IsDefault = true
            },
            new("Default2", "g", "e", "f")
        });
        ;


        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
        moduleSettings.SetDefaultAccount("f");

        moduleSettings.SetDefaultAccount("Default");


        settings.Received(2).SetList(typeof(ModuleSettings), Arg.Any<List<NostrKey>>());
    }

    [TestMethod]
    public void GetAccounts()
    {
        settings = Substitute.For<ISettings>();
        settings.GetValues<NostrKey>(typeof(ModuleSettings)).Returns(new List<NostrKey>
        {
            new("Default", "a", "b", "c")
            {
                IsDefault = true
            },
            new("Default2", "g", "e", "f")
        });
        ;


        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
        var accounts = moduleSettings.GetAccounts();
        Assert.IsTrue(accounts[0].PrivateKeyBech32 == "");
        Assert.IsTrue(accounts[1].PrivateKeyBech32 == "");
    }

    [TestMethod]
    public void SetLastDmAccess()
    {
        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
        moduleSettings.SetLastDmAccess(DateTimeOffset.UtcNow);

        settings.Received(1).Set(typeof(ModuleSettings), ModuleSettings.NostrDMSince, Arg.Any<DateTimeOffset>());
    }
}