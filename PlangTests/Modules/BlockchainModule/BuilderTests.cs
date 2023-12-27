using Microsoft.VisualStudio.TestPlatform.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Utils;
using PLangTests;
using System.Reflection.Metadata;
using System.Xml.Linq;
using static PLang.Modules.BaseBuilder;
using static PLang.Modules.BlockchainModule.ModuleSettings;

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
			settings.Get(typeof(PLangLlmService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));
			var aiService = new PLangLlmService(cacheHelper, context);
			var moduleSettings = new ModuleSettings(settings, aiService);

			var fileSystem = new PLangFileSystem(Environment.CurrentDirectory, "./");
			typeHelper = new TypeHelper(fileSystem, settings);

			builder = new Builder(settings, context, aiService);
			builder.InitBaseBuilder("PLang.Modules.BlockchainModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);

			
		}

		private void SetupResponse(string response, Type type)
		{
			var aiService = Substitute.For<ILlmService>();
			aiService.Query(Arg.Any<LlmQuestion>(), type).Returns(p => { 
				return JsonConvert.DeserializeObject(response, type); 
			});

			var moduleSettings = new ModuleSettings(settings, aiService);

			builder = new Builder(settings, context, aiService);
			builder.InitBaseBuilder("PLang.Modules.BlockchainModule", fileSystem, aiService, typeHelper, memoryStack, context, variableHelper);
		}

		[DataTestMethod]
		[DataRow("get rpc servers, write to %servers%")]
		public async Task GetRpcServers_Test(string text)
		{

			string response = @"{""FunctionName"": ""GetRpcServers"",
""Parameters"": [],
""ReturnValue"": {""Type"": ""List<RpcServer>"",
""VariableName"": ""%servers%""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;			

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			Assert.AreEqual("GetRpcServers", gf.FunctionName);
			Assert.AreEqual("%servers%", gf.ReturnValue.VariableName);
		}

		[DataTestMethod]
		[DataRow("set mumbai as current rpc server")]
		public async Task SetCurrentRpcServer_Test(string text)
		{
			string response = @"{""FunctionName"": ""SetCurrentRpcServer"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""nameOrUrl"",
""Value"": ""Mumbai - Polygon testnet""}]}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;
			
			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SetCurrentRpcServer", gf.FunctionName);
			Assert.AreEqual("Mumbai - Polygon testnet", gf.Parameters[0].Value);
			Assert.AreEqual("nameOrUrl", gf.Parameters[0].Name);
			
		}

		public static string CallFunctionInstructions = @"{""FunctionName"": ""CallFunction"", 
""Parameters"": [
    {""Type"": ""String"", ""Name"": ""contractAddress"", ""Value"": ""0x05FaaFA42C7AD9d3b72EC823F6a6BA16F161f63E""},
    {""Type"": ""String"", ""Name"": ""abi"", ""Value"": ""{\""constant\"":true,\""inputs\"":[{\""name\"":\""_owner\"",\""type\"":\""address\""}],\""name\"":\""balanceOf\"",\""outputs\"":[{\""name\"":\""balance\"",\""type\"":\""uint256\""}],\""payable\"":false,\""stateMutability\"":\""view\"",\""type\"":\""function\""}""},
    {""Type"": ""String"", ""Name"": ""functionName"", ""Value"": ""balanceOf""},
    {""Type"": ""Object[]"", ""Name"": ""functionInputs"", ""Value"": [""0x39AdD0ff2cb924fe6f268305324f3cBD9873A323""]},
    {""Type"": ""String"", ""Name"": ""returnType"", ""Value"": ""BigInteger""}
],
""ReturnValue"": {""Type"": ""BigInteger"", ""VariableName"": ""balance""}}";
		
		[DataTestMethod]
		[DataRow("get balanceOf(0xe4ddb4233513498b5aa79b98bea473b01b101a67) on contract 0x326C977E6efc84E512bB9C30f76E30c160eD06FB, write to %balance%"
+ "abi:{\"constant\":true,\"inputs\":[{\"name\":\"_owner\",\"type\":\"address\"}],\"name\":\"balanceOf\",\"outputs\":[{\"name\":\"balance\",\"type\":\"uint256\"}],\"payable\":false,\"stateMutability\":\"view\",\"type\":\"function\"}"
		)]
		public async Task CallFunction(string text)
		{
			var wallets = new List<Wallet>();
			wallets.Add(new Wallet("Default", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));
			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);
			
			SetupResponse(CallFunctionInstructions, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("CallFunction", gf.FunctionName);
			Assert.AreEqual("contractAddress", gf.Parameters[0].Name);
			Assert.AreEqual("0x05FaaFA42C7AD9d3b72EC823F6a6BA16F161f63E", gf.Parameters[0].Value);
			Assert.AreEqual("abi", gf.Parameters[1].Name);

			Assert.AreEqual("functionName", gf.Parameters[2].Name);
			Assert.AreEqual("balanceOf", gf.Parameters[2].Value);
			Assert.AreEqual("functionInputs", gf.Parameters[3].Name);
			Assert.AreEqual("0x39AdD0ff2cb924fe6f268305324f3cBD9873A323", ((JArray)gf.Parameters[3].Value)[0].ToString());
		}





		[DataTestMethod]
		[DataRow("get currenct rpc server, write to %rpcServer%")]
		public async Task GetCurrentRpcServer_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetCurrentRpcServer"", 
""Parameters"": [], 
""ReturnValue"": {""Type"": ""RpcServer"", 
""VariableName"": ""rpcServer""}}"
			;
			SetupResponse(response, typeof(GenericFunction));
			var step = new Building.Model.GoalStep();
			step.Text = text;
			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetCurrentRpcServer", gf.FunctionName);
			Assert.AreEqual("rpcServer", gf.ReturnValue.VariableName);
		}


		[DataTestMethod]
		[DataRow("get wallets, write to %wallets%")]
		public async Task GetWallets_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetWallets"",
""Parameters"": [],
""ReturnValue"": {""Type"": ""List`1"", ""VariableName"": ""wallets""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetWallets", gf.FunctionName);
			Assert.AreEqual("wallets", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("set business wallet as current wallet")]
		public async Task SetCurrentWallet_Test(string text)
		{
			var wallets = new List<Wallet>();
			wallets.Add(new Wallet("My wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));
			wallets.Add(new Wallet("My business wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));

			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);



			string response = @"{""FunctionName"": ""SetCurrentWallet"", ""Parameters"": [{""Type"": ""String"", ""Name"": ""walletName"", ""Value"": ""My business wallet""}], ""ReturnValue"": null}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SetCurrentWallet", gf.FunctionName);
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

			string response = @"{""FunctionName"": ""GetOrCreateWallet"", ""Parameters"": [], ""ReturnValue"": {""Type"": ""Wallet"", ""VariableName"": ""wallet""}}";

			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetOrCreateWallet", gf.FunctionName);
			Assert.AreEqual("wallet", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("set current address as 0xeC146d8c6D51d547D09548A9D9B4451AeC3df285")]
		public async Task SetAddress_Test(string text)
		{
			var wallets = new List<Wallet>();
			wallets.Add(new Wallet("My wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));
			wallets.Add(new Wallet("My business wallet", "a26c463040c1ea9ed3a11da2a1619ab81a3937b7ab4a535a33456ebff682ed36583a5f11ed359a230cc20790284bbf7198e06091d315d02ee50cc4f351cb4f40", ""));

			settings.GetValues<Wallet>(typeof(ModuleSettings)).Returns(wallets);

			string response = @"{""FunctionName"": ""SetAddress"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""address"",
""Value"": ""0xeC146d8c6D51d547D09548A9D9B4451AeC3df285""}],
""ReturnValue"": null}";

			
			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SetAddress", gf.FunctionName);
			Assert.AreEqual("address", gf.Parameters[0].Name);
			Assert.AreEqual("0xeC146d8c6D51d547D09548A9D9B4451AeC3df285", gf.Parameters[0].Value);
		}


		[DataTestMethod]
		[DataRow("sign 'Hello world', write to %signature%")]
		[DataRow("sign using my wallet 'Hello world', write to %signature%")]
		[DataRow("sign a message 'Hello world', write to %signature%")]
		public async Task SignMessage_Test(string text)
		{
			string response = @"{""FunctionName"": ""SignMessage"",
""Parameters"": [{""Type"": ""String"",
""Name"": ""message"",
""Value"": ""Hello world""}],
""ReturnValue"": {""Type"": ""String"",
""VariableName"": ""signature""}}";


			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SignMessage", gf.FunctionName);
			Assert.AreEqual("message", gf.Parameters[0].Name);
			Assert.AreEqual("Hello world", gf.Parameters[0].Value);

		}

		[DataTestMethod]
		[DataRow("verify that %message% matches %signature% for address 0x1234, write to %isVerified%")]
		public async Task VerifySignature_Test(string text)
		{
			string response = @"{""FunctionName"": ""VerifySignature"",
""Parameters"": [{""Type"": ""String"", ""Name"": ""message"", ""Value"": ""%message%""},
               {""Type"": ""String"", ""Name"": ""signature"", ""Value"": ""%signature%""},
               {""Type"": ""String"", ""Name"": ""expectedAddress"", ""Value"": ""0x1234""}],
""ReturnValue"": {""Type"": ""Boolean"", ""VariableName"": ""isVerified""}}";


			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("VerifySignature", gf.FunctionName);
			Assert.AreEqual("message", gf.Parameters[0].Name);
			Assert.AreEqual("signature", gf.Parameters[1].Name);
			Assert.AreEqual("expectedAddress", gf.Parameters[2].Name);
			Assert.AreEqual("%message%", gf.Parameters[0].Value);
			Assert.AreEqual("%signature%", gf.Parameters[1].Value);
			Assert.AreEqual("0x1234", gf.Parameters[2].Value);
			Assert.AreEqual("isVerified", gf.ReturnValue.VariableName);

		}

		[DataTestMethod]
		[DataRow("get wei balance 0x1234, write to %balance%")]
		[DataRow("get balance in wei 0x1234, output to %balance%")]
		[DataRow("get balance on 0x1234, write to %balance%")]
		public async Task GetBalanceInWei_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetBalanceInWei"", 
""Parameters"": [{""Type"": ""String"", 
""Name"": ""address"", 
""Value"": ""0x1234""}], 
""ReturnValue"": {""Type"": ""BigInteger"", 
""VariableName"": ""balance""}}";


			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetBalanceInWei", gf.FunctionName);
			Assert.AreEqual("address", gf.Parameters[0].Name);
			Assert.AreEqual("0x1234", gf.Parameters[0].Value);
			Assert.AreEqual("balance", gf.ReturnValue.VariableName);
		}


		[DataTestMethod]
		[DataRow("retrieve decimal balance for 0x1234, write to %balance%")]
		[DataRow("retrieve balance for 0x1234, decimal count is 8, write to %balance%")]
		public async Task GetBalanceToDecimalPoint_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetBalanceToDecimalPoint"", 
""Parameters"": [{""Type"": ""String"", 
""Name"": ""address"", 
""Value"": ""0x1234""}, 
{""Type"": ""Int32"", 
""Name"": ""decimalPlacesToUnit"", 
""Value"": ""8""}], 
""ReturnValue"": {""Type"": ""Decimal"", 
""VariableName"": ""balance""}}";


			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetBalanceToDecimalPoint", gf.FunctionName);
			Assert.AreEqual("address", gf.Parameters[0].Name);
			Assert.AreEqual("decimalPlacesToUnit", gf.Parameters[1].Name);
			Assert.AreEqual("0x1234", gf.Parameters[0].Value);
			Assert.AreEqual("8", gf.Parameters[1].Value);
			Assert.AreEqual("balance", gf.ReturnValue.VariableName);
		}


		[DataTestMethod]
		[DataRow("get decimal for 0x1234, write to %decimal%")]
		[DataRow("retrieve decimal point for smart contract 0x1234, write to %decimal%")]
		public async Task GetDecimal_Test(string text)
		{
			string response = @"{""FunctionName"": ""GetDecimal"", 
""Parameters"": [{""Type"": ""String"", 
""Name"": ""contractAddress"", 
""Value"": ""0x1234""}], 
""ReturnValue"": {""Type"": ""UInt32"", 
""VariableName"": ""decimal""}}";


			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("GetDecimal", gf.FunctionName);
			Assert.AreEqual("contractAddress", gf.Parameters[0].Name);
			Assert.AreEqual("0x1234", gf.Parameters[0].Value);
			Assert.AreEqual("decimal", gf.ReturnValue.VariableName);
		}




		[DataTestMethod]
		[DataRow("transfer %amount% to 0x123, write to %txHash%")]
		public async Task Transfer_Test(string text)
		{
			string response = @"{""FunctionName"": ""Transfer"", 
""Parameters"": [{""Type"": ""String"", 
""Name"": ""to"", 
""Value"": ""0x123""}, 
{""Type"": ""Decimal"", 
""Name"": ""etherAmount"", 
""Value"": ""%amount%""}, 
{""Type"": ""Nullable`1"", 
""Name"": ""gasPriceWei"", 
""Value"": null}, 
{""Type"": ""Nullable`1"", 
""Name"": ""gas"", 
""Value"": null}, 
{""Type"": ""Nullable`1"", 
""Name"": ""nonce"", 
""Value"": null}], 
""ReturnValue"": {""Type"": ""String"", 
""VariableName"": ""%txHash%""}}";


			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("Transfer", gf.FunctionName);
			Assert.AreEqual("to", gf.Parameters[0].Name);
			Assert.AreEqual("0x123", gf.Parameters[0].Value);
			Assert.AreEqual("etherAmount", gf.Parameters[1].Name);
			Assert.AreEqual("%amount%", gf.Parameters[1].Value);
			Assert.AreEqual("%txHash%", gf.ReturnValue.VariableName);
		}


		[DataTestMethod]
		[DataRow("transfer erc20 %amount% from 0x2333 to 0x123, write to %txHash%")]
		public async Task TransferErc20_Test(string text)
		{
			string response = @"{""FunctionName"": ""TransferErc20"", 
""Parameters"": [{""Type"": ""String"", 
""Name"": ""contractAddress"", 
""Value"": ""0x2333""}, 
{""Type"": ""String"", 
""Name"": ""to"", 
""Value"": ""0x123""}, 
{""Type"": ""Decimal"", 
""Name"": ""amount"", 
""Value"": ""%amount%""}, 
{""Type"": ""Nullable`1"", 
""Name"": ""gasPriceWei"", 
""Value"": null}, 
{""Type"": ""Nullable`1"", 
""Name"": ""gas"", 
""Value"": null}, 
{""Type"": ""Nullable`1"", 
""Name"": ""nonce"", 
""Value"": null}], 
""ReturnValue"": {""Type"": ""String"", 
""VariableName"": ""%txHash%""}}";


			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("TransferErc20", gf.FunctionName);
			Assert.AreEqual("contractAddress", gf.Parameters[0].Name);
			Assert.AreEqual("0x2333", gf.Parameters[0].Value);
			Assert.AreEqual("to", gf.Parameters[1].Name);
			Assert.AreEqual("0x123", gf.Parameters[1].Value);
			Assert.AreEqual("amount", gf.Parameters[2].Name);
			Assert.AreEqual("%amount%", gf.Parameters[2].Value);
			Assert.AreEqual("%txHash%", gf.ReturnValue.VariableName);
		}



		[DataTestMethod]
		[DataRow($@"send transaction to 0x123555, [{{""name"":""value""}}], params %amount%, %date%, %voted%, write to %txHash%")]
		public async Task SendTransaction_Test(string text)
		{
			string response = @"{""FunctionName"": ""SendTransaction"",
""Parameters"": [
    {""Type"": ""String"", ""Name"": ""contractAddress"", ""Value"": ""0x123555""},
    {""Type"": ""String"", ""Name"": ""abi"", ""Value"": ""[{\""name\"":\""value\""}]""},
    {""Type"": ""Object[]"", ""Name"": ""args"", ""Value"": ""[%amount%, %date%, %voted%]""}
],
""ReturnValue"": {""Type"": ""String"", ""VariableName"": ""%txHash%""}}";


			SetupResponse(response, typeof(GenericFunction));

			var step = new Building.Model.GoalStep();
			step.Text = text;

			var instruction = await builder.Build(step);
			var gf = instruction.Action as GenericFunction;

			//Assert.AreEqual("1", instruction.LlmQuestion.RawResponse);
			Assert.AreEqual("SendTransaction", gf.FunctionName);
			Assert.AreEqual("contractAddress", gf.Parameters[0].Name);
			Assert.AreEqual("0x123555", gf.Parameters[0].Value);
			Assert.AreEqual("abi", gf.Parameters[1].Name);
			Assert.AreEqual(@$"[{{""name"":""value""}}]", gf.Parameters[1].Value);
			Assert.AreEqual("args", gf.Parameters[2].Name);
			Assert.AreEqual("[%amount%, %date%, %voted%]", gf.Parameters[2].Value);
			Assert.AreEqual("%txHash%", gf.ReturnValue.VariableName);
		}

	}
}