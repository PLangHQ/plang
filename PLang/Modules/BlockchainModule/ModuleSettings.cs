using System.Text;
using NBitcoin;
using Nethereum.Web3.Accounts;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;

namespace PLang.Modules.BlockchainModule;

public class ModuleSettings : IModuleSettings
{
    private readonly ILlmServiceFactory llmServiceFactory;
    private readonly ISettings settingsService;


    public ModuleSettings(ISettings settings, ILlmServiceFactory llmServiceFactory)
    {
        settingsService = settings;
        this.llmServiceFactory = llmServiceFactory;
        var rpcServers = settings.GetValues<RpcServer>(typeof(ModuleSettings)) ?? new List<RpcServer>();
        if (rpcServers.Count == 0)
        {
            AddRpcUrl("Mumbai - Polygon testnet", "wss://polygon-mumbai-bor.publicnode.com", 80001, true, true);
            AddRpcUrl("Matic (Polygon) mainnet", "wss://polygon-bor.publicnode.com", 137, false);

            AddRpcUrl("Ethereum mainnet", "wss://ethereum.publicnode.com", 1, false);
            AddRpcUrl("Ethereum testnet (Goerli)", "wss://ethereum-goerli.publicnode.com", 5, true);

            AddRpcUrl("Binance mainnet", "wss://bsc.publicnode.com", 56, false);
            AddRpcUrl("Binance testnet", "wss://bsc-testnet.publicnode.com", 97, true);

            AddRpcUrl("Arbitrum One mainnet", "wss://arbitrum-one.publicnode.com", 42161, false);
            AddRpcUrl("Arbitrum Goerli testnet", "wss://arbitrum-goerli.publicnode.com", 421613, true);

            AddRpcUrl("Optimism (OP) mainnet", "wss://optimism.publicnode.com", 10, false);
            AddRpcUrl("Optimism (OP) Goerli testnet", "wss://optimism-goerli.publicnode.com", 420, true);

            AddRpcUrl("Avalanche C-Chain mainnet", "wss://avalanche-c-chain.publicnode.com", 43114, false);
            AddRpcUrl("Avalanche Fuji testnet", "wss://avalanche-fuji-c-chain.publicnode.com", 43113, true);

            AddRpcUrl("Gnosis mainnet", "wss://rpc.gnosischain.com/wss", 100, false);
            AddRpcUrl("Gnosis Chiado Testnet", "wss://rpc.chiadochain.net/wss", 10200, true);
        }

        var tokenList = settings.GetValues<Token>(typeof(ModuleSettings)) ?? new List<Token>();
        if (tokenList.Count == 0)
        {
            AddToken(tokenList, "USDC", "USDC Coin", 6, "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48", 1);
            AddToken(tokenList, "USDC", "USDC Coin", 6, "0x07865c6E87B9F70255377e024ace6630C1Eaa37F", 5);

            AddToken(tokenList, "USDC", "USDC Coin", 6, "0x2791Bca1f2de4661ED88A30C99A7a9449Aa84174", 137);
            AddToken(tokenList, "USDC", "USDC Coin", 6, "0x9999f7fea5938fd3b1e26a12c3f2fb024e194f97", 80001);

            AddToken(tokenList, "USDC", "USDC Coin", 6, "0xfd064A18f3BF249cf1f87FC203E90D8f650f2d63", 421613);
            AddToken(tokenList, "USDC", "USDC Coin", 6, "0xe05606174bac4A6364B31bd0eCA4bf4dD368f8C6", 420);
            AddToken(tokenList, "USDC", "USDC Coin", 6, "0x5425890298aed601595a70ab815c96711a31bc65", 43113);
        }

        var wallets = GetWallets();
        if (wallets.Count == 0)
        {
            var hdWallet = new Nethereum.HdWallet.Wallet(Wordlist.English, WordCount.TwentyFour);
            var wallet = new Wallet("First", hdWallet.Seed, "");
            wallet.IsDefault = true;

            wallets.Add(wallet);
            settings.SetList(typeof(ModuleSettings), wallets);
        }
    }

    public void AddToken(List<Token> tokenList, string symbol, string name, int @decimal, string contractAddress,
        int chainId)
    {
        // TODO: user should not send in symbol or decimal, this should be requested from chain using contractAddress
        var token = new Token(symbol, name, @decimal, contractAddress, chainId);
        tokenList.Add(token);

        settingsService.SetList(typeof(ModuleSettings), tokenList);
    }

    public List<Token> GetTokens()
    {
        return settingsService.GetValues<Token>(typeof(ModuleSettings));
    }

    private Nethereum.HdWallet.Wallet GetHdWallet(Wallet wallet)
    {
        var seed = Encoding.UTF8.GetBytes(wallet.Seed);
        return new Nethereum.HdWallet.Wallet(seed);
    }

    public void AddRpcUrl(string name, string url, int chainId, bool isTestNet, bool setAsDefault = false)
    {
        if (!url.StartsWith("wss://")) throw new ArgumentException("Url must start with wss://.");
        var rpcServers = settingsService.GetValues<RpcServer>(typeof(ModuleSettings)) ?? new List<RpcServer>();

        var rpcServer = rpcServers.FirstOrDefault(p => p.Url == url);
        if (setAsDefault)
        {
            rpcServer = rpcServers.FirstOrDefault(p => p.IsDefault);
            if (rpcServer != null) rpcServer.IsDefault = false;
        }

        if (rpcServer != null) rpcServers.Remove(rpcServer);

        rpcServer = new RpcServer(name, url, chainId, isTestNet)
        {
            IsDefault = setAsDefault
        };
        rpcServers.Add(rpcServer);

        settingsService.SetList(typeof(ModuleSettings), rpcServers);
    }

    public void RemoveRpcServer(string url)
    {
        var rpcServers = settingsService.GetValues<RpcServer>(typeof(ModuleSettings)) ?? new List<RpcServer>();
        var rpcServer = rpcServers.FirstOrDefault(p => p.Url == url);
        if (rpcServer != null) rpcServers.Remove(rpcServer);
        settingsService.SetList(typeof(ModuleSettings), rpcServers);
    }

    public List<RpcServer> GetRpcServers()
    {
        return settingsService.GetValues<RpcServer>(typeof(ModuleSettings)) ?? new List<RpcServer>();
    }


    public List<Wallet> GetWallets()
    {
        var wallets = settingsService.GetValues<Wallet>(typeof(ModuleSettings)) ?? new List<Wallet>();
        wallets = wallets.Where(p => !p.IsArchived).ToList();

        foreach (var wallet in wallets)
            if (!string.IsNullOrEmpty(wallet.Seed))
            {
                var hdWallet = GetHdWallet(wallet);

                wallet.Addresses.AddRange(hdWallet.GetAddresses());
            }
            else
            {
                var account = new Account(wallet.PrivateKey);
                wallet.Addresses.Add(account.Address);
            }

        var sensoredWallets = wallets.Select(p => new Wallet(p.Name, "", "") { Addresses = p.Addresses }).ToList();
        return sensoredWallets;
    }

    public List<Wallet> GetArchivedWallets()
    {
        var wallets = settingsService.GetValues<Wallet>(typeof(ModuleSettings)).Where(p => p.IsArchived).ToList();

        var sensoredWallets = wallets.Select(p => new Wallet(p.Name, "", "")).ToList();
        return sensoredWallets;
    }


    public void CreateWallet(string name, bool isDefault = false)
    {
        var wallets = GetWallets();
        var wallet = wallets.FirstOrDefault(p => p.Name.ToLower() == name.ToLower());
        if (wallet != null)
            throw new ModuleSettingsException($"A wallet with the name {name} already exists. Choose different name");

        if (isDefault)
        {
            var defaultWallet = wallets.FirstOrDefault(p => p.IsDefault);
            if (defaultWallet != null) defaultWallet.IsDefault = false;
        }

        var hdWallet = new Nethereum.HdWallet.Wallet(Wordlist.English, WordCount.TwentyFour);
        wallet = new Wallet(name, hdWallet.Seed, "");
        wallet.IsDefault = isDefault;
        wallets.Add(wallet);

        settingsService.SetList(typeof(ModuleSettings), wallets);
    }

    public void ImportWallet(string name, string privateKey, bool isDefault = false)
    {
        var wallets = GetWallets();
        var wallet = wallets.FirstOrDefault(p => p.Name.ToLower() == name.ToLower());
        if (wallet != null)
            throw new ModuleSettingsException($"A wallet with the name {name} already exists. Choose different name");

        if (isDefault)
        {
            var defaultWallet = wallets.FirstOrDefault(p => p.IsDefault);
            if (defaultWallet != null) defaultWallet.IsDefault = false;
        }

        wallet = new Wallet(name, "", privateKey);
        wallet.IsDefault = isDefault;
        wallets.Add(wallet);

        settingsService.SetList(typeof(ModuleSettings), wallets);
    }

    // This will mean that we need to go through all the code and search for name, maybe not allow this.
    public void RenameWallet(string name, string newName)
    {
        var wallets = GetWallets();
        var wallet = wallets.FirstOrDefault(p => p.Name.ToLower() == name.ToLower());
        if (wallet != null)
        {
            var newWallet = new Wallet(newName, wallet.Seed, wallet.PrivateKey);
            wallets.Remove(wallet);
            wallets.Add(newWallet);

            settingsService.SetList(typeof(ModuleSettings), wallets);
        }
    }


    public void SetArchiveStatusOnWallet(string name, bool isArchived)
    {
        var wallets = GetWallets();
        var wallet = wallets.FirstOrDefault(p => p.Name.ToLower() == name.ToLower());
        if (wallet != null)
        {
            wallet.IsArchived = isArchived;
            settingsService.SetList(typeof(ModuleSettings), wallets);
        }
    }

    public record Token(string symbol, string name, int @decimal, string contractAddress, int chainId);

    public record Wallet(string Name, string Seed, string PrivateKey)
    {
        public List<string> Addresses = new();
        public bool IsArchived;
        public bool IsDefault;
    }

    public record RpcServer(string Name, string Url, int ChainId, bool IsTestNet)
    {
        public bool IsDefault;
        public string HexChainId => ChainId.ToString("X");
        public Setting Setting { get; set; }
    }
}