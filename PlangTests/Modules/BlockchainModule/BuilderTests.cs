using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using PLang.Utils;
using PLangTests;
using PLangTests.Utils;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.BlockchainModule.ModuleSettings;
using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

namespace PLang.Modules.BlockchainModule.Tests
{
	[TestClass()]
	public class BuilderTests : BasePLangTest
	{
		Builder builder;

		[TestInitialize]
		public void Init()
		{
			base.Initialize();
			var rpcServers = new List<RpcServer>();
			rpcServers.Add(new RpcServer("Mumbai - Polygon testnet", "wss://polygon-bor.publicnode.com", 80001, true) { IsDefault = true });

			settings.GetValues<RpcServer>(typeof(ModuleSettings)).Returns(rpcServers);

			var wallets = new List<Wallet>();
			settings.When(p => p.SetList(typeof(ModuleSettings), Arg.Any<List<Wallet>>()))
				.Do((callback) =>
				{
					wallets = callback.Arg<List<Wallet>>();
				});
			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(p =>
			{
				return wallets;
			});

			var tokens = new List<Token>();
			settings.GetValues<Token>(typeof(ModuleSettings)).Returns(tokens);
			settings.When(p => p.SetList(typeof(ModuleSettings), Arg.Any<List<Token>>()))
				.Do((callback) =>
				{
					tokens = callback.Arg<List<Token>>();
				});
			LoadOpenAI();
			
			var moduleSettings = new ModuleSettings(settings, llmServiceFactory);
			
			//var fileSystem = new PLangMockFileSystem();
			//typeHelper = new TypeHelper(fileSystem, settings);

			builder = new Builder(settings, context, llmServiceFactory);
			builder.InitBaseBuilder(GetStep("- "), fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);

			
		}

		private void SetupResponse(string stepText, [CallerMemberName] string? caller = null, Type? type = null)
		{
			var llmService = GetLlmService(stepText, caller, type);
			if (llmService == null) return;

			var moduleSettings = new ModuleSettings(settings, llmServiceFactory);

			builder = new Builder(settings, context, llmServiceFactory);
			builder.InitBaseBuilder(step, fileSystem, llmServiceFactory, typeHelper, memoryStack, context, variableHelper, logger);
		}

		public GoalStep GetStep(string text)
		{
			step.Text = text;
			step.ModuleType = "PLang.Modules.BlockchainModule";
			return step;
		}

		[DataTestMethod]
		[DataRow("get rpc servers, write to %servers%")]
		public async Task GetRpcServers_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			Assert.AreEqual("GetRpcServers", gf.Name);
			AssertVar.AreEqual("%servers%", gf.ReturnValues[0].VariableName);
		}

		[DataTestMethod]
		[DataRow("set mumbai as current rpc server")]
		public async Task SetCurrentRpcServer_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text,instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("SetCurrentRpcServer", gf.Name);
			Assert.AreEqual("Mumbai - Polygon testnet", gf.Parameters[0].Value);
			Assert.AreEqual("nameOrUrl", gf.Parameters[0].Name);
			
		}
		
		[DataTestMethod]
		[DataRow("[block] call function (0xe4ddb4233513498b5aa79b98bea473b01b101a67) on contract 0x326C977E6efc84E512bB9C30f76E30c160eD06FB, write to %balance%"
+ " abi:{\"constant\":true,\"inputs\":[{\"name\":\"_owner\",\"type\":\"address\"}],\"name\":\"balanceOf\",\"outputs\":[{\"name\":\"balance\",\"type\":\"uint256\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"}"
		)]
		public async Task CallFunction(string text)
		{
			var wallets = new List<Wallet>();
			wallets.Add(new Wallet("Default", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));
			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);
			
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text,instruction.LlmRequest[0].RawResponse);
			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("CallFunction", gf.Name);
			Assert.AreEqual("contractAddressOrSymbol", gf.Parameters[0].Name);
			Assert.AreEqual("0x326C977E6efc84E512bB9C30f76E30c160eD06FB", gf.Parameters[0].Value);
			Assert.AreEqual("abi", gf.Parameters[1].Name);

			Assert.AreEqual("functionInputs", gf.Parameters[2].Name);
			Assert.AreEqual("0xe4ddb4233513498b5aa79b98bea473b01b101a67", ((JArray)gf.Parameters[2].Value)[0].ToString());
		}





		[DataTestMethod]
		[DataRow("get currenct rpc server, write to %rpcServer%")]
		public async Task GetCurrentRpcServer_Test(string text)
		{
			SetupResponse(text);
			LoadStep(text);
			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("GetCurrentRpcServer", gf.Name);
			AssertVar.AreEqual("rpcServer", gf.ReturnValues[0].VariableName);
		}


		[DataTestMethod]
		[DataRow("get wallets, write to %wallets%")]
		public async Task GetWallets_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;
			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("GetWallets", gf.Name);
			AssertVar.AreEqual("wallets", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("set business wallet as current wallet")]
		public async Task SetCurrentWallet_Test(string text)
		{
			var wallets = new List<Wallet>();
			wallets.Add(new Wallet("My wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));
			wallets.Add(new Wallet("My business wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));

			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);

			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;
			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("SetCurrentWallet", gf.Name);
			Assert.AreEqual("walletName", gf.Parameters[0].Name);
			Assert.AreEqual("My business wallet", gf.Parameters[0].Value);

		}


		[DataTestMethod]
		[DataRow("get current wallet, write to %wallet%")]
		public async Task GetOrCreateWallet_Test(string text)
		{
			var wallets = new List<Wallet>();
			wallets.Add(new Wallet("My wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));
			wallets.Add(new Wallet("My business wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));

			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);

			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;
			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("GetOrCreateWallet", gf.Name);
			AssertVar.AreEqual("wallet", gf.ReturnValues[0].VariableName);

		}

		[DataTestMethod]
		[DataRow("set current address as 0xeC146d8c6D51d547D09548A9D9B4451AeC3df285")]
		public async Task SetAddress_Test(string text)
		{
			var wallets = new List<Wallet>();
			wallets.Add(new Wallet("My wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));
			wallets.Add(new Wallet("My business wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));

			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);

			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;
			Store(text, instruction.LlmRequest[0].RawResponse);

			Assert.AreEqual("SetCurrentAddress", gf.Name);
			Assert.AreEqual("address", gf.Parameters[0].Name);
			Assert.AreEqual("0xeC146d8c6D51d547D09548A9D9B4451AeC3df285", gf.Parameters[0].Value);
		}


		

		[DataTestMethod]
		[DataRow("get native wei balance 0x1234, write to %balance%")]
		[DataRow("get native balance in wei 0x1234, output to %balance%")]
		[DataRow("get native balance on 0x1234, write to %balance%")]
		public async Task GetBalanceInWei_Test(string text)
		{

			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("GetNativeBalanceOfAddressInWei", gf.Name);
			Assert.AreEqual("address", gf.Parameters[0].Name);
			Assert.AreEqual("0x1234", gf.Parameters[0].Value);
			Assert.AreEqual("balance", gf.ReturnValues[0].VariableName);
		}


		[DataTestMethod]
		[DataRow("retrieve decimal balance for 0x1234, write to %balance%")]
		[DataRow("retrieve balance for 0x1234, decimal count is 8, write to %balance%")]
		public async Task GetBalanceToDecimalPoint_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse); 
			
			Assert.AreEqual("GetNativeBalanceOfAddressToDecimalPoint", gf.Name);
			Assert.AreEqual("address", gf.Parameters[0].Name);
			Assert.AreEqual("0x1234", gf.Parameters[0].Value);
			if (text.Contains("decimal count is 8"))
			{
				Assert.AreEqual("decimalPlacesToUnit", gf.Parameters[1].Name);
				Assert.AreEqual((long)8, gf.Parameters[1].Value);
			}
			AssertVar.AreEqual("balance", gf.ReturnValues[0].VariableName);
		}


		[DataTestMethod]
		[DataRow("get decimal for 0x1234, write to %decimal%")]
		[DataRow("retrieve decimal point for smart contract 0x1234, write to %decimal%")]
		public async Task GetDecimal_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("GetDecimal", gf.Name);
			Assert.AreEqual("contractAddress", gf.Parameters[0].Name);
			Assert.AreEqual("0x1234", gf.Parameters[0].Value);
			Assert.AreEqual("decimal", gf.ReturnValues[0].VariableName);
		}




		[DataTestMethod]
		[DataRow("transfer %amount% to 0x123, write to %txHash%")]
		public async Task Transfer_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);
			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("Transfer", gf.Name);
			Assert.AreEqual("to", gf.Parameters[0].Name);
			Assert.AreEqual("0x123", gf.Parameters[0].Value);
			Assert.AreEqual("etherAmount", gf.Parameters[1].Name);
			Assert.AreEqual("%amount%", gf.Parameters[1].Value);
			Assert.AreEqual("%txHash%", gf.ReturnValues[0].VariableName);
		}


		[DataTestMethod]
		[DataRow("transfer usdc %amount% from 0x2333 to 0x123, write to %txHash%")]
		public async Task TransferErc20_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("TransferFromSmartContract", gf.Name);
			Assert.AreEqual("contractAddressOrSymbol", gf.Parameters[0].Name);
			Assert.AreEqual("USDC", gf.Parameters[0].Value);
			Assert.AreEqual("from", gf.Parameters[1].Name);
			Assert.AreEqual("0x2333", gf.Parameters[1].Value);
			Assert.AreEqual("to", gf.Parameters[2].Name);
			Assert.AreEqual("0x123", gf.Parameters[2].Value);
			Assert.AreEqual("value", gf.Parameters[3].Name);
			Assert.AreEqual("%amount%", gf.Parameters[3].Value);
			AssertVar.AreEqual("%txHash%", gf.ReturnValues[0].VariableName);
		}



		[DataTestMethod]
		[DataRow($@"send transaction to 0x123555, [{{""name"":""value""}}], params %amount%, %date%, %voted%, write to %txHash%")]
		public async Task SendTransaction_Test(string text)
		{
			SetupResponse(text);

			LoadStep(text);

			(var instruction, var error) = await builder.Build(step);
			var gf = instruction.Function as GenericFunction;

			Store(text, instruction.LlmRequest[0].RawResponse);
			
			Assert.AreEqual("SendTransaction", gf.Name);
			Assert.AreEqual("contractAddress", gf.Parameters[0].Name);
			Assert.AreEqual("0x123555", gf.Parameters[0].Value);
			Assert.AreEqual("abi", gf.Parameters[1].Name);
			Assert.AreEqual(@$"[{{""name"":""value""}}]", gf.Parameters[1].Value);
			Assert.AreEqual("args", gf.Parameters[2].Name);
			Assert.AreEqual("[\"%amount%\",\"%date%\",\"%voted%\"]", gf.Parameters[2].Value.ToString().Replace("\n", "").Replace("\r", "").Replace(" ", ""));
			AssertVar.AreEqual("%txHash%", gf.ReturnValues[0].VariableName);
		}

	}
}