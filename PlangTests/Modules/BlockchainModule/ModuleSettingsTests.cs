using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Interfaces;
using PLangTests;
using static PLang.Modules.BlockchainModule.ModuleSettings;

namespace PLang.Modules.BlockchainModule.Tests;

[TestClass]
public class ModuleSettingsTests : BasePLangTest
{
    private ModuleSettings moduleSettings;

    [TestInitialize]
    public void Init()
    {
        Initialize();

        var rpcServers = new List<RpcServer>();
        rpcServers.Add(
            new RpcServer("Mumbai - Polygon testnet", "https://polygon-mumbai-bor.publicnode.com", 80001, true)
                { IsDefault = true });

        settings.GetValues<RpcServer>(typeof(ModuleSettings)).Returns(rpcServers);

        var wallets = new List<Wallet>();
        settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);


        moduleSettings = new ModuleSettings(settings, llmServiceFactory);
    }


    [TestMethod]
    public void GetWalletsTest()
    {
        var context = new PLangAppContext();

        settings.GetValues<RpcServer>(typeof(ModuleSettings)).Returns(new List<RpcServer>());
        settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(new List<Wallet>());
        var ms = new ModuleSettings(settings, llmServiceFactory);
    }

    [TestMethod]
    public void ModuleSettings_AddRpcUrl()
    {
        moduleSettings.AddRpcUrl("Test", "wss://test.com", 1, true);

        settings.Received(1).SetList(typeof(ModuleSettings), Arg.Is<List<RpcServer>>(p => p.Count == 2));

        var rpceServers2 = moduleSettings.GetRpcServers();
        Assert.AreEqual(2, rpceServers2.Count);
    }

    [TestMethod]
    public void ModuleSettings_RemoveRpcUrl()
    {
        moduleSettings.AddRpcUrl("Test", "wss://test.com", 1, true);

        settings.Received(1).SetList(typeof(ModuleSettings), Arg.Is<List<RpcServer>>(p => p.Count == 2));

        var rpceServers2 = moduleSettings.GetRpcServers();
        Assert.AreEqual(2, rpceServers2.Count);

        moduleSettings.RemoveRpcServer("wss://test.com");
        settings.Received(2).SetList(typeof(ModuleSettings), Arg.Is<List<RpcServer>>(p => p.Count == 1));
    }

    [TestMethod]
    public void CreateWallet_test()
    {
        var wallets = new List<Wallet>();
        settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);

        moduleSettings.CreateWallet("Main", true);

        settings.SetList(typeof(ModuleSettings), Arg.Is<Wallet>(p => p.Name == "Main"));
    }


    [TestMethod]
    public void GetArchivedWallet_test()
    {
        var wallets = new List<Wallet>();
        wallets.Add(new Wallet("Test1", "abc", ""));
        wallets.Add(new Wallet("Test2", "abc", "") { IsArchived = true });
        settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);

        var archivedWallets = moduleSettings.GetArchivedWallets();
        Assert.AreEqual(1, archivedWallets.Count);
    }


    [TestMethod]
    public void RenameWallet_test()
    {
        var wallets = new List<Wallet>();
        wallets.Add(new Wallet("Test1", "abc", ""));
        wallets.Add(new Wallet("Test2", "abc", "") { IsArchived = true });
        settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);

        moduleSettings.RenameWallet("Test2", "Test3");
        settings.SetList(typeof(ModuleSettings),
            Arg.Is<List<Wallet>>(p => p.Count == 2 && p.FindIndex(q => q.Name == "Test3") != -1));
    }
}