using PLang.Modules.BlockchainModule;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLangTests;
using static PLang.Modules.BlockchainModule.ModuleSettings;
using Nethereum.Signer;
using System.Globalization;
using Newtonsoft.Json;
using static PLang.Modules.BaseBuilder;
using PLang.Building.Tests;
using Newtonsoft.Json.Linq;
using System.Numerics;
using PLang.Utils;
using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace PLangTests.Modules.BlockchainModule
{
	[TestClass()]
	public class ProgramTests : BasePLangTest
	{
		Program p;
		[TestInitialize]
		public void Init()
		{
			Initialize();
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RPCServer")))
            {
                var rpcServer = new RpcServer("Default", Environment.GetEnvironmentVariable("RPCServer"), 80001, true);
                var rpcServers = new List<RpcServer>();
                rpcServers.Add(rpcServer);

                settings.GetValues<RpcServer>(typeof(ModuleSettings)).Returns(rpcServers);
            }
            else
            {
                settings.GetValues<RpcServer>(typeof(ModuleSettings)).Returns(new List<RpcServer>() { new RpcServer("Mumbai - Polygon testnet", "wss://polygon-bor.publicnode.com", 80001, true) { IsDefault = true } });
            }

            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WalletSeed")))
            {
				var wallet = new Wallet("Default", Environment.GetEnvironmentVariable("WalletSeed"), "");
				List<Wallet> wallets = new List<Wallet>();
				wallets.Add(wallet);


				settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);
			} else
            {
				var wallets = new List<Wallet>();
				wallets.Add(new Wallet("My wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));
				wallets.Add(new Wallet("My business wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));

				settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);
			}
			p = new Program(settings, llmServiceFactory, pseudoRuntime, engine, logger, contextAccessor);
			p.Init(container, null, null, null, null);
		}

		[TestMethod()]
		public async Task GetBalanceInWei()
		{

			var wallet = new Wallet("Default", Environment.GetEnvironmentVariable("WalletSeed"), "");
			List<Wallet> wallets = new List<Wallet>();
			wallets.Add(wallet);


			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);


			var decimals = await p.GetNativeBalanceOfAddressInWei("0xe1e78247c06191089126fc8dd55e1bd383dc8f22");
			Assert.AreNotEqual((uint)0, decimals);

			//
		}

		[TestMethod()]
		public async Task GetBalance()
		{

			var wallet = new Wallet("Default", Environment.GetEnvironmentVariable("WalletSeed"), "");
			List<Wallet> wallets = new List<Wallet>();
			wallets.Add(wallet);


			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);


			var decimals = await p.GetNativeBalanceOfAddressToDecimalPoint("0xe1e78247c06191089126fc8dd55e1bd383dc8f22");
			Assert.AreEqual(0.2M, decimals);

			//
		}

		[TestMethod()]
		public async Task CallFunction_Test()
		{
			/*
			var wallet = new Wallet("Default", Environment.GetEnvironmentVariable("WalletSeed"), "");
			List<Wallet> wallets = new List<Wallet>();
			wallets.Add(wallet);


			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);

			

		
			var genericFunction = JsonConvert.DeserializeObject<GenericFunction>(PLang.Modules.BlockchainModule.Tests.BuilderTests.CallFunctionInstructions);

			string abi = genericFunction.Parameters.FirstOrDefault(p => p.Name == "abi").Value.ToString();
			var contractAddress = genericFunction.Parameters.FirstOrDefault(p => p.Name == "contractAddress").Value.ToString();
			object[] functionInputs = ((JArray)genericFunction.Parameters.FirstOrDefault(p => p.Name == "functionInputs").Value).ToObject<object[]>();
        
			var p = new Program(settings, context, aiService, null, null, memoryStack, null);
			var decimals = await p.CallFunction(contractAddress, "[" + abi + "]", functionInputs);
			Assert.IsTrue(((BigInteger)decimals) > 0);*/
		}

		[TestMethod]
		public async Task ListenToTranferContractEvent()
		{
            // PLangTestToken - allows anybody to mint
            // 0x05FaaFA42C7AD9d3b72EC823F6a6BA16F161f63E
            var walletSeed = Environment.GetEnvironmentVariable("WalletSeed");
            if (string.IsNullOrWhiteSpace(walletSeed)) return;


			var wallet = new Wallet("Default", Environment.GetEnvironmentVariable("WalletSeed"), "");
			List<Wallet> wallets = new List<Wallet>();
			wallets.Add(wallet);

			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);

			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RPCServer")))
			{
				var rpcServer = new RpcServer("Default", Environment.GetEnvironmentVariable("RPCServer"), 80001, true);
				var rpcServers = new List<RpcServer>();
				rpcServers.Add(rpcServer);

				settings.GetValues<RpcServer>(typeof(ModuleSettings)).Returns(rpcServers);
			}
            context.AddOrReplace(ReservedKeywords.Goal, new Goal() { RelativeAppStartupFolderPath = Path.DirectorySeparatorChar.ToString() });
			
            

			BigInteger mintAmount = BigInteger.Parse("100") * BigInteger.Pow(10, 18);
			BigInteger transferAmount = BigInteger.Parse("90") * BigInteger.Pow(10, 18);
			await p.ListenToTransferEventOnSmartContract("0x05FaaFA42C7AD9d3b72EC823F6a6BA16F161f63E", "TransferGoal");

            var result = await p.MintSmartContract("0x05FaaFA42C7AD9d3b72EC823F6a6BA16F161f63E", wallet.Addresses[0], mintAmount);

			logger.LogWarning("result:" + JsonConvert.SerializeObject(result));
			
			await p.TransferSmartContract("0x05FaaFA42C7AD9d3b72EC823F6a6BA16F161f63E", "0x4aB611f42DDF97D4DAc87d3EC290BAe959D5D9Be", transferAmount, true);
			await Task.Delay(1000 * 60 * 5);
		}


		[TestMethod]
		public async Task ListenToBlockEvent()
		{
			var walletSeed = Environment.GetEnvironmentVariable("WalletSeed");
			if (string.IsNullOrWhiteSpace(walletSeed)) return;

			// PLangTestToken - allows anybody to mint
			// 0x05FaaFA42C7AD9d3b72EC823F6a6BA16F161f63E
			var wallet = new Wallet("Default", Environment.GetEnvironmentVariable("WalletSeed"), "");
			List<Wallet> wallets = new List<Wallet>();
			wallets.Add(wallet);

			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);

			if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("RPCServer")))
			{
				var rpcServer = new RpcServer("Default", Environment.GetEnvironmentVariable("RPCServer"), 80001, true);
				var rpcServers = new List<RpcServer>();
				rpcServers.Add(rpcServer);

				settings.GetValues<RpcServer>(typeof(ModuleSettings)).Returns(rpcServers);
			}
			var goal = new Goal() { RelativeAppStartupFolderPath = Path.DirectorySeparatorChar.ToString() };
			
			
			await p.ListenToBlock("TransferGoal");


			await Task.Delay(1000 * 60 * 5);
		}

		[TestMethod()]
		public async Task GetDecimal()
		{

			var wallet = new Wallet("Default", Environment.GetEnvironmentVariable("WalletSeed"), "");
			List<Wallet> wallets = new List<Wallet>();
			wallets.Add(wallet);


			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);


				var decimals = await p.GetDecimal("0x4aB611f42DDF97D4DAc87d3EC290BAe959D5D9Be");
			Assert.AreEqual((uint)18, decimals);

			//
		}




	}
}