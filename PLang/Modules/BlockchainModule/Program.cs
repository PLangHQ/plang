using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using System.ComponentModel;
using System.Numerics;
using System.Text;
using static PLang.Modules.BlockchainModule.ModuleSettings;

namespace PLang.Modules.BlockchainModule
{   
	
	//todo: should cleanup any usage of keys, set them to null after usage
	// this is so they dont stay in memory until garbage collection
	// this needs to happen down the call stack and figure out how settings is handled

	[Description("Use blockchain, create wallet, account info, transfer money")]
	public class Program : BaseProgram, IDisposable
	{
		private readonly IWeb3 web3;
		private readonly ISettings settings;
		private readonly IPseudoRuntime pseudoRuntime;
		private readonly IEngine engine;
		private readonly ILogger logger;
		private readonly ModuleSettings moduleSettings;

		private StreamingWebSocketClient? client = null;

		public static readonly string CurrentWalletContextKey = "PLang.Modules.BlockchainModule.ModuleSettings.CurrentWallet";
		public static readonly string CurrentAddressContextKey = "PLang.Modules.BlockchainModule.ModuleSettings.CurrentAddress";
		public static readonly string CurrentRpcServerContextKey = "PLang.Modules.BlockchainModule.ModuleSettings.CurrentRpcServer";

		public Program(ISettings settings, ILlmService aiService, 
			IPseudoRuntime pseudoRuntime, IEngine engine, ILogger logger) : base()
		{
			this.settings = settings;
			this.pseudoRuntime = pseudoRuntime;
			this.engine = engine;
			this.logger = logger;
			this.moduleSettings = new ModuleSettings(settings, aiService);

			var rpcServer = _GetCurrentRpcServer();
			var wallet = GetCurrentWallet();
			this.web3 = new Web3(getAccount(wallet, rpcServer.ChainId), new WebSocketClient(rpcServer.Url));
		}

		private IAccount getAccount(Wallet wallet, int chainId)
		{
			if (!string.IsNullOrEmpty(wallet.PrivateKey))
			{
				return new Account(wallet.PrivateKey, chainId);
			}

			var seed = Encoding.UTF8.GetBytes(wallet.Seed);
			var hdWallet = new Nethereum.HdWallet.Wallet(seed);
			return new Account(hdWallet.GetPrivateKey(GetCurrentAddressIndex()), chainId);
		}

		public async Task ListenToApprovalEventOnSmartContract(string contractAddressOrSymbol, string goalToCall, string subscriptIdVariableName = "subscriptionId")
		{
			string abi = @"{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""owner"",""type"":""address""},{""indexed"":true,""name"":""spender"",""type"":""address""},{""indexed"":false,""name"":""value"",""type"":""uint256""}],""name"":""Approval"",""type"":""event""}";
			await ListenToEventOnSmartContract(contractAddressOrSymbol, abi, goalToCall, subscriptIdVariableName);
		}

		public async Task ListenToApprovalForAllEventOnSmartContract(string contractAddressOrSymbol, string goalToCall, string subscriptIdVariableName = "subscriptionId")
		{
			string abi = @"{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""account"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""operator"",""type"":""address""},{""indexed"":false,""internalType"":""bool"",""name"":""approved"",""type"":""bool""}],""name"":""ApprovalForAll"",""type"":""event""}";
			await ListenToEventOnSmartContract(contractAddressOrSymbol, abi, goalToCall, subscriptIdVariableName);
		}

		public async Task ListenToTransferBatchEventOnSmartContract(string contractAddressOrSymbol, string goalToCall, string subscriptIdVariableName = "subscriptionId")
		{
			string abi = @"{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""operator"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""from"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""to"",""type"":""address""},{""indexed"":false,""internalType"":""uint256[]"",""name"":""ids"",""type"":""uint256[]""},{""indexed"":false,""internalType"":""uint256[]"",""name"":""values"",""type"":""uint256[]""}],""name"":""TransferBatch"",""type"":""event""}";
			await ListenToEventOnSmartContract(contractAddressOrSymbol, abi, goalToCall, subscriptIdVariableName);
		}

		public async Task ListenToUriEventOnSmartContract(string contractAddressOrSymbol, string goalToCall, string subscriptIdVariableName = "subscriptionId")
		{
			string abi = @"{""anonymous"":false,""inputs"":[{""indexed"":false,""internalType"":""string"",""name"":""value"",""type"":""string""},{""indexed"":true,""internalType"":""uint256"",""name"":""id"",""type"":""uint256""}],""name"":""URI"",""type"":""event""}";
			await ListenToEventOnSmartContract(contractAddressOrSymbol, abi, goalToCall, subscriptIdVariableName);
		}

		public async Task ListenToTransferSingleEventOnSmartContract(string contractAddressOrSymbol, string goalToCall, string subscriptIdVariableName = "subscriptionId")
		{
			string abi = @"{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""address"",""name"":""operator"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""from"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""to"",""type"":""address""},{""indexed"":false,""internalType"":""uint256"",""name"":""id"",""type"":""uint256""},{""indexed"":false,""internalType"":""uint256"",""name"":""value"",""type"":""uint256""}],""name"":""TransferSingle"",""type"":""event""}";
			await ListenToEventOnSmartContract(contractAddressOrSymbol, abi, goalToCall, subscriptIdVariableName);
		}
		public async Task ListenToTransferEventOnSmartContract(string contractAddressOrSymbol, string goalToCall, string subscriptIdVariableName = "subscriptionId")
		{
			string abi = @"{""anonymous"":false,""inputs"":[{""indexed"":true,""name"":""from"",""type"":""address""},{""indexed"":true,""name"":""to"",""type"":""address""},{""indexed"":false,""name"":""value"",""type"":""uint256""}],""name"":""Transfer"",""type"":""event""}";
			await ListenToEventOnSmartContract(contractAddressOrSymbol, abi, goalToCall, subscriptIdVariableName);
		}

		private static readonly object _lock = new();
		public async Task ListenToEventOnSmartContract(string contractAddressOrSymbol, string abi, string goalToCall, string subscriptIdVariableName = "subscriptionId")
		{
			var rpcServer = await GetCurrentRpcServer();
			if (contractAddressOrSymbol.Length != 42)
			{
				var token = moduleSettings.GetTokens().FirstOrDefault(p => p.symbol.ToLower() == contractAddressOrSymbol.ToLower() && p.chainId == rpcServer.ChainId);
				if (token == null)
				{
					throw new RuntimeStepException($"Could not find token {contractAddressOrSymbol}", goalStep);
				}
				contractAddressOrSymbol = token.contractAddress;
			}

			var wallet = GetCurrentWallet();
			var web3 = new Web3(getAccount(wallet, rpcServer.ChainId), new WebSocketClient(rpcServer.Url));
			if (!abi.Trim().StartsWith("[")) abi = "[" + abi + "]";

			var contract = web3.Eth.GetContract(abi, contractAddressOrSymbol);
			var json = JsonConvert.DeserializeObject<dynamic>(abi);
			string eventName = json[0].name;
			var myEvent = contract.GetEvent(eventName);
			var filterInput = myEvent.CreateFilterInput();// new[] { null, "" });


			if (client == null)
			{
				client = new StreamingWebSocketClient(rpcServer.Url);
			}
		
			var subscription = new EthLogsObservableSubscription(client);
			

			subscription.GetSubscribeResponseAsObservable().Subscribe(id =>
			{
				memoryStack.Put(subscriptIdVariableName, id);
			});

			subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(async log =>
			{
				//some rase condition I cant find, so putting lock
				lock (_lock)
				{
					var eventLogs = myEvent.DecodeAllEventsDefaultForEvent(new FilterLog[1] { log });
					if (eventLogs == null || eventLogs.Count == 0) return;


					foreach (var eventLog in eventLogs)
					{
						var parameters = new Dictionary<string, object>();
						foreach (var eve in eventLog.Event)
						{
							parameters.Add(eve.Parameter.Name, eve.Result);
							if (eve.Parameter.Name == "from" && eve.Parameter.Name == "to")
							{
								parameters.Add(eve.Parameter.Name + "Hashed", eve.Result.ToString().ComputeHash());
							}
						}
						parameters.Add("__TxLog__", eventLog.Log);
						var task = pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, goalToCall, parameters, goal);
						task.Wait();
					}
				}

			});

			subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
			{
				Console.WriteLine("DISCONNECTED");
				memoryStack.Remove(subscriptIdVariableName);
			});

			client.Error += async (object sender, Exception ex) =>
			{
				logger.LogError(goalStep.Text, ex);
				logger.LogInformation("Waiting 5 seconds and trying to reconnect");

				await Task.Delay(1000 * 5);			
				await ListenToEventOnSmartContract(contractAddressOrSymbol, abi, goalToCall, subscriptIdVariableName);
			};



			var task = client.StartAsync().ContinueWith(task =>
			{
				if (task.IsFaulted)
				{
					throw task.Exception;
				}
			});

			task.Wait();

			if (subscription.SubscriptionState != Nethereum.JsonRpc.Client.Streaming.SubscriptionState.Subscribing)
			{
				await subscription.SubscribeAsync(filterInput);

			}
			KeepAlive(this, "ListenToEventOnSmartContract");

		}

		

		public async Task StopListening(string subscriptionId)
		{
			if (client != null)
			{
				client.RemoveSubscription(subscriptionId);
			}
		}


		public async Task ListenToBlock(string callGoal, string subcriptionId = "subscriptionId", string? callGoalOnUnsubscribe = null)
		{

			var rpcServer = await GetCurrentRpcServer();
			var client = new StreamingWebSocketClient(rpcServer.Url);

			var subscription = new EthNewBlockHeadersObservableSubscription(client);
			subscription.GetSubscribeResponseAsObservable().Subscribe(subscriptionId =>
				memoryStack.Put(subcriptionId, subscriptionId));

			DateTime? lastBlockNotification = null;
			double secondsSinceLastBlock = 0;

			subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(block =>
			{
				secondsSinceLastBlock = (lastBlockNotification == null) ? 0 : (int)DateTime.Now.Subtract(lastBlockNotification.Value).TotalSeconds;
				lastBlockNotification = DateTime.Now;
				var utcTimestamp = DateTimeOffset.FromUnixTimeSeconds((long)block.Timestamp.Value);
				var parameters = new Dictionary<string, object>();
				parameters.Add("block", block);
				parameters.Add("timestamp", utcTimestamp);
				parameters.Add("lastBlockNotification", lastBlockNotification);

				pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, callGoal, parameters, Goal);
			});

			subscription.GetUnsubscribeResponseAsObservable().Subscribe(response =>
			{
				if (callGoalOnUnsubscribe != null)
				{
					var parameters = new Dictionary<string, object>();
					pseudoRuntime.RunGoal(engine, context, Goal.RelativeAppStartupFolderPath, callGoalOnUnsubscribe, parameters, Goal);
				}
			});

			var task = client.StartAsync().ContinueWith(task =>
			{
				if (task.IsFaulted)
				{
					throw task.Exception; // You might want to throw the inner exception instead.
				}
			});

			task.Wait();

			if (subscription.SubscriptionState != Nethereum.JsonRpc.Client.Streaming.SubscriptionState.Subscribing)
			{
				await subscription.SubscribeAsync();
			}

			KeepAlive(client, "ListenToBlock");
		}


		public async Task<object> NameOfSmartContract(string contractAddressOrSymbol)
		{
			string abi = @"{""constant"":true,""inputs"":[],""name"":""name"",""outputs"":[{""name"":"""",""type"":""string""}],""payable"":false,""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { });
		}
		public async Task<object> TotalSupplyOnSmartContract(string contractAddressOrSymbol)
		{
			string abi = @"{""constant"":true,""inputs"":[],""name"":""totalSupply"",""outputs"":[{""name"":"""",""type"":""uint256""}],""payable"":false,""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { });
		}
		public async Task<object> GetMyBalanceOnSmartContract(string contractAddressOrSymbol)
		{
			string address = await GetCurrentAddress();
			string abi = @"{""constant"":true,""inputs"":[{""name"":""_owner"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":""balance"",""type"":""uint256""}],""payable"":false,""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { address });
		}

		public async Task<object> BalanceOfOnSmartContract(string contractAddressOrSymbol, string addressToCheckBalanceOf)
		{
			string abi = @"{""constant"":true,""inputs"":[{""name"":""_owner"",""type"":""address""}],""name"":""balanceOf"",""outputs"":[{""name"":""balance"",""type"":""uint256""}],""payable"":false,""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { addressToCheckBalanceOf });
		}
		public async Task<object> BalanceOfBatchOnSmartContract(string contractAddressOrSymbol, string[] addresses, BigInteger[] ids)
		{
			string abi = @"{""inputs"":[{""internalType"":""address[]"",""name"":""accounts"",""type"":""address[]""},{""internalType"":""uint256[]"",""name"":""ids"",""type"":""uint256[]""}],""name"":""balanceOfBatch"",""outputs"":[{""internalType"":""uint256[]"",""name"":"""",""type"":""uint256[]""}],""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { addresses, ids });
		}
		public async Task<object> IsApprovedForAllOnSmartContract(string contractAddressOrSymbol, string accountAddress, string operatorAddress)
		{
			string abi = @"{""inputs"":[{""internalType"":""address"",""name"":""account"",""type"":""address""},{""internalType"":""address"",""name"":""operator"",""type"":""address""}],""name"":""isApprovedForAll"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { accountAddress, operatorAddress });
		}
		public async Task<object> CecimalsOnSmartContract(string contractAddressOrSymbol)
		{
			string abi = @"{""constant"":true,""inputs"":[],""name"":""decimals"",""outputs"":[{""name"":"""",""type"":""uint8""}],""payable"":false,""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { });
		}
		public async Task<object> AllowanceFromSmartContract(string contractAddressOrSymbol, string from, string to, BigInteger value)
		{
			string abi = @"{""constant"":true,""inputs"":[{""name"":""_owner"",""type"":""address""},{""name"":""_spender"",""type"":""address""}],""name"":""allowance"",""outputs"":[{""name"":"""",""type"":""uint256""}],""payable"":false,""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { from, to, value });
		}

		public async Task<object> SymbolOnSmartContract(string contractAddressOrSymbol)
		{
			string abi = @"{""constant"":true,""inputs"":[],""name"":""symbol"",""outputs"":[{""name"":"""",""type"":""string""}],""payable"":false,""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { });
		}

		public async Task<object> SupportsInterfaceOnSmartContract(string contractAddressOrSymbol, string interfaceId)
		{
			string abi = @"{""inputs"":[{""internalType"":""bytes4"",""name"":""interfaceId"",""type"":""bytes4""}],""name"":""supportsInterface"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { interfaceId });
		}

		public async Task<object> GetUriOnSmartContract(string contractAddressOrSymbol, BigInteger id)
		{
			string abi = @"{""inputs"":[{""internalType"":""uint256"",""name"":""id"",""type"":""uint256""}],""name"":""uri"",""outputs"":[{""internalType"":""string"",""name"":"""",""type"":""string""}],""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { id });
		}
		public async Task<object> GetApprovedOnSmartContract(string contractAddressOrSymbol)
		{
			string abi = @"{""constant"":true,""inputs"":[{""internalType"":""uint256"",""name"":""tokenId"",""type"":""uint256""}],""name"":""getApproved"",""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],""payable"":false,""stateMutability"":""view"",""type"":""function""}";
			return await CallFunction(contractAddressOrSymbol, abi, new object[] { });
		}




		public async Task<object> ApproveSmartContract(string contractAddressOrSymbol, string spender, BigInteger value, bool waitForReceipt = false)
		{
			string abi = @"{""constant"":false,""inputs"":[{""name"":""_spender"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""approve"",""outputs"":[{""name"":"""",""type"":""bool""}],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""}";
			return await CallAndSignFunction(contractAddressOrSymbol, abi, new object[] { spender, value }, waitForReceipt);
		}
		public async Task<object> MintSmartContract(string contractAddressOrSymbol, string to, BigInteger amount, bool waitForReceipt = false)
		{
			string abi = @"{""constant"":false,""inputs"":[{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""mint"",""outputs"":[],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""}";
			return await CallAndSignFunction(contractAddressOrSymbol, abi, new object[] { to, amount }, waitForReceipt);
		}
		public async Task<object> BurnSmartContract(string contractAddressOrSymbol, string account, BigInteger amount, bool waitForReceipt = false)
		{
			string abi = @"{""constant"":false,""inputs"":[{""internalType"":""address"",""name"":""account"",""type"":""address""},{""internalType"":""uint256"",""name"":""amount"",""type"":""uint256""}],""name"":""burn"",""outputs"":[],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""}";
			return await CallAndSignFunction(contractAddressOrSymbol, abi, new object[] { account, amount }, waitForReceipt);
		}
		public async Task<object> TransferSmartContract(string contractAddressOrSymbol, string to, BigInteger value, bool waitForReceipt = false)
		{

			string abi = @"{""constant"":false,""inputs"":[{""name"":""_to"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""transfer"",""outputs"":[{""name"":"""",""type"":""bool""}],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""}";
			return await CallAndSignFunction(contractAddressOrSymbol, abi, new object[] { to, value }, waitForReceipt);
		}
		public async Task<object> TransferFromSmartContract(string contractAddressOrSymbol, string from, string to, BigInteger value, bool waitForReceipt = false)
		{
			string abi = @"{""constant"":false,""inputs"":[{""name"":""_from"",""type"":""address""},{""name"":""_to"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""transferFrom"",""outputs"":[{""name"":"""",""type"":""bool""}],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""}";
			return await CallAndSignFunction(contractAddressOrSymbol, abi, new object[] { from, to, value }, waitForReceipt);
		}
		public async Task<object> SafeTransferFromErc721SmartContract(string contractAddressOrSymbol, string from, string to, BigInteger id, bool waitForReceipt = false)
		{
			string abi = @"{""constant"":false,""inputs"":[{""internalType"":""address"",""name"":""from"",""type"":""address""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256"",""name"":""tokenId"",""type"":""uint256""}],""name"":""safeTransferFrom"",""outputs"":[],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""}";
			return await CallAndSignFunction(contractAddressOrSymbol, abi, new object[] { from, to, id }, waitForReceipt);
		}
		public async Task<object> SafeTransferFromErc1155SmartContract(string contractAddressOrSymbol, string from, string to, BigInteger[] ids, BigInteger[] amounts, bool waitForReceipt = false)
		{
			string abi = @"{""constant"":false,""inputs"":[{""name"":""_from"",""type"":""address""},{""name"":""_to"",""type"":""address""},{""name"":""_value"",""type"":""uint256""}],""name"":""safeTransferFrom"",""outputs"":[{""name"":"""",""type"":""bool""}],""payable"":false,""stateMutability"":""nonpayable"",""type"":""function""}";
			return await CallAndSignFunction(contractAddressOrSymbol, abi, new object[] { from, to, ids, amounts }, waitForReceipt);
		}

		public async Task<object> SafeBatchTransferFromSmartContract(string contractAddressOrSymbol, string from, string to, BigInteger value, bool waitForReceipt = false)
		{
			string abi = @"{""inputs"":[{""internalType"":""address"",""name"":""from"",""type"":""address""},{""internalType"":""address"",""name"":""to"",""type"":""address""},{""internalType"":""uint256[]"",""name"":""ids"",""type"":""uint256[]""},{""internalType"":""uint256[]"",""name"":""amounts"",""type"":""uint256[]""},{""internalType"":""bytes"",""name"":""data"",""type"":""bytes""}],""name"":""safeBatchTransferFrom"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}";
			return await CallAndSignFunction(contractAddressOrSymbol, abi, new object[] { from, to, value }, waitForReceipt);
		}

		public async Task<object> SetApprovalForAllOnSmartContract(string contractAddressOrSymbol, string operatorAddress, bool approved, bool waitForReceipt = false)
		{
			string abi = @"{""inputs"":[{""internalType"":""address"",""name"":""operator"",""type"":""address""},{""internalType"":""bool"",""name"":""approved"",""type"":""bool""}],""name"":""setApprovalForAll"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}";
			return await CallAndSignFunction(contractAddressOrSymbol, abi, new object[] { operatorAddress, approved }, waitForReceipt);
		}




		public async Task<object> CallFunction(string contractAddressOrSymbol, string abi, object[]? functionInputs = null)
		{
			if (!contractAddressOrSymbol.StartsWith("0x"))
			{
				var currentRpcServer = await GetCurrentRpcServer();
				var token = moduleSettings.GetTokens().FirstOrDefault(p => p.symbol == contractAddressOrSymbol && p.chainId == currentRpcServer.ChainId);
				if (token == null)
				{
					throw new RuntimeStepException($"Could not find token {contractAddressOrSymbol}", goalStep);
				}
				contractAddressOrSymbol = token.contractAddress;
			}

			if (!abi.Trim().StartsWith("[")) abi = "[" + abi + "]";

			var contract = web3.Eth.GetContract(abi, contractAddressOrSymbol);

			var json = JsonConvert.DeserializeObject<dynamic>(abi);
			string functionName = json[0].name;

			var function = contract.GetFunction(functionName);

			var result = await function.CallAsync<object>(functionInputs);
			return result;

		}


		[Description("Generate abi from functionName and functionInputs if not provided by user.")]
		public async Task<object> CallAndSignFunction(string contractAddressOrSymbol, string abi, object[]? functionInputs = null, bool waitForReceipt = false)
		{
			if (!contractAddressOrSymbol.StartsWith("0x"))
			{
				var currentRpcServer = await GetCurrentRpcServer();
				var token = moduleSettings.GetTokens().FirstOrDefault(p => p.symbol == contractAddressOrSymbol && p.chainId == currentRpcServer.ChainId);
				if (token == null)
				{
					throw new RuntimeStepException($"Could not find token {contractAddressOrSymbol}", goalStep);
				}
				contractAddressOrSymbol = token.contractAddress;
			}

			if (!abi.Trim().StartsWith("[")) abi = "[" + abi + "]";
			var contract = web3.Eth.GetContract(abi, contractAddressOrSymbol);

			var json = JsonConvert.DeserializeObject<dynamic>(abi);
			string functionName = json[0].name;

			var function = contract.GetFunction(functionName);

			var rpcServer = await GetCurrentRpcServer();
			var wallet = GetCurrentWallet();
			var currentAddress = await GetCurrentAddress();
			var txInput = function.CreateTransactionInput(currentAddress, functionInputs);
			//txInput.Type = TransactionType.Legacy.AsHexBigInteger();// TransactionType.EIP1559.AsHexBigInteger();
			var account = getAccount(wallet, rpcServer.ChainId);

			var nounce = await web3.TransactionManager.Account.NonceService.GetNextNonceAsync();
			txInput.Nonce = nounce;
			try
			{
				var estimatedGas = await web3.TransactionManager.EstimateGasAsync(txInput);
				txInput.Gas = estimatedGas;
			}
			catch (RpcResponseException ex)
			{
				throw new Exception(ex.Message.Replace("eth_estimateGas", ""));
			}
			var accountSigner = new AccountSignerTransactionManager(web3.Client, (Account)account);
			//accountSigner.UseLegacyAsDefault = true;
			var result = await accountSigner.SignTransactionAsync(txInput);

			if (!waitForReceipt)
			{
				result = await accountSigner.SendTransactionAsync(txInput);
				return result;
			}
			else
			{
				var receipt = await accountSigner.SendTransactionAndWaitForReceiptAsync(txInput);
				return receipt;
			}
		}

		public async Task<List<RpcServer>> GetRpcServers()
		{
			return settings.GetValues<RpcServer>(typeof(ModuleSettings)) ?? new List<RpcServer>();
		}

		[RunOnBuild]
		public async Task SetCurrentRpcServer(string nameOrUrl)
		{
			var rpcServers = await GetRpcServers();
			var rpcServer = rpcServers.FirstOrDefault(p => p.Name.ToLower() == nameOrUrl.ToLower() || p.Url.ToLower() == nameOrUrl.ToLower());
			if (rpcServer != null)
			{
				context.AddOrReplace(Program.CurrentRpcServerContextKey, rpcServer);
			}

		}
		private RpcServer _GetCurrentRpcServer()
		{
			var rpcServers = settings.GetValues<RpcServer>(typeof(ModuleSettings)) ?? new List<RpcServer>();
			if (context.TryGetValue(CurrentRpcServerContextKey, out object? rpcServer))
			{
				var currentRpcServer = rpcServers.FirstOrDefault(p => p.Url == ((RpcServer)rpcServer).Url);
				if (currentRpcServer != null) return currentRpcServer;
			}

			var server = rpcServers.FirstOrDefault(p => p.IsDefault);
			if (server != null)
			{
				return server;
			}
			return moduleSettings.GetRpcServers()[0];
		}
		public async Task<RpcServer> GetCurrentRpcServer()
		{
			return _GetCurrentRpcServer();
		}

		public async Task<List<Wallet>> GetWallets()
		{
			return moduleSettings.GetWallets();
		}
		[RunOnBuild]
		public async Task SetCurrentWallet(string walletName)
		{
			var wallets = await GetWallets();
			var wallet = wallets.FirstOrDefault(p => p.Name.ToLower() == walletName.ToLower());
			if (wallet != null)
			{
				context.AddOrReplace(CurrentWalletContextKey, wallet.Name);
			}
			else
			{
				throw new RuntimeException($"Could not find wallet name {walletName}");
			}
		}



		public async Task<Nethereum.HdWallet.Wallet> GetOrCreateWallet()
		{
			var wallet = GetCurrentWallet();

			var seed = Encoding.UTF8.GetBytes(wallet.Seed);
			var hdWallet = new Nethereum.HdWallet.Wallet(seed);

			return hdWallet;
		}
		[RunOnBuild]
		public async Task SetCurrentAddress(string address)
		{
			if (address == null) return;
			var wallets = await GetWallets();

			foreach (var wallet in wallets)
			{
				int idx = wallet.Addresses.FindIndex(p => p == address);
				if (idx != -1)
				{
					context.AddOrReplace(CurrentAddressContextKey, idx);
					context.AddOrReplace(CurrentWalletContextKey, wallet);
					return;
				}
			}
		}

		private int GetCurrentAddressIndex()
		{
			var idx = 0;
			if (context.ContainsKey(CurrentAddressContextKey))
			{
				idx = (int)context[CurrentAddressContextKey];
			}
			return idx;
		}

		public async Task<string> GetCurrentAddress()
		{
			var wallet = await GetOrCreateWallet();
			var idx = GetCurrentAddressIndex();


			var addresses = wallet.GetAddresses();
			if (addresses.Length - 1 < idx) idx = 0;
			return wallet.GetEthereumKey(idx).GetPublicAddress();
			//return addresses[idx];

		}

		public async Task<BigInteger> GetBalanceInWei(string address)
		{
			var balance = await web3.Eth.GetBalance.SendRequestAsync(address);
			return balance.Value;
		}

		[Description("Get the balance in ETH, converts from Wei to Eth")]
		public async Task<decimal> GetBalanceToDecimalPoint(string address, int decimalPlacesToUnit = 18)
		{
			var balance = await web3.Eth.GetBalance.SendRequestAsync(address);
			var balanceInEther = Web3.Convert.FromWei(balance.Value, decimalPlacesToUnit);
			return balanceInEther;
		}


		public async Task<uint> GetDecimal(string contractAddress)
		{
			string abi = @"[{""constant"":true,""inputs"":[],""name"":""decimals"",""outputs"":[{""name"":"""",""type"":""uint8""}],""type"":""function""}]";

			var contract = web3.Eth.GetContract(abi, contractAddress);
			var decimalsFunction = contract.GetFunction("decimals");
			var decimals = await decimalsFunction.CallAsync<uint>();
			return decimals;
		}
		public async Task<string> Transfer(string to, decimal etherAmount, decimal? gasPriceWei = null, BigInteger? gas = null, BigInteger? nonce = null)
		{
			var transaction = await web3.Eth.GetEtherTransferService().TransferEtherAsync(to, etherAmount, gasPriceWei, gas, nonce);
			return transaction;
		}
		public async Task<TransactionReceipt> TransferWaitForReceipt(string to, decimal etherAmount, decimal? gasPriceWei = null, BigInteger? gas = null, BigInteger? nonce = null)
		{
			var transaction = await web3.Eth.GetEtherTransferService()
				.TransferEtherAndWaitForReceiptAsync(to, etherAmount, gasPriceWei, gas, nonce);
			return transaction;
		}
		
		public async Task<string> SendTransaction(string contractAddress, string abi, params object[] args)
		{
			var contract = web3.Eth.GetContract(abi, contractAddress);
			var function = contract.GetFunction(abi);
			var sender = await GetCurrentAddress();
			var transactionHash = await function.SendTransactionAsync(sender, args);
			return transactionHash;
		}

		public async Task<TransactionReceipt> SendTransactionAndWaitForReceipt(string contractAddress, string abi, params object[] args)
		{
			CancellationToken cancellationToken = new CancellationToken();

			var contract = web3.Eth.GetContract(abi, contractAddress);
			var function = contract.GetFunction(abi);
			var sender = await GetCurrentAddress();
			var transaction = await function.SendTransactionAndWaitForReceiptAsync(sender, cancellationToken, args);
			return transaction;
		}

		public void Dispose()
		{
			context.Remove(CurrentAddressContextKey);
			context.Remove(CurrentRpcServerContextKey);
			context.Remove(CurrentWalletContextKey);
		}


		private Wallet GetCurrentWallet()
		{
			var wallets = settings.GetValues<Wallet>(typeof(ModuleSettings)).Where(p => !p.IsArchived).ToList();
			if (context.ContainsKey(CurrentWalletContextKey))
			{
				var currentWallet = wallets.FirstOrDefault(p => p.Name.ToLower() == context[CurrentWalletContextKey].ToString().ToLower());
				if (currentWallet != null)
				{
					return currentWallet;
				}
			}

			var wallet = wallets.FirstOrDefault(p => p.IsDefault);
			if (wallet != null) return wallet;

			if (wallets.Count > 0) return wallets[0];

			moduleSettings.CreateWallet("Default", true);
			return GetWallets().Result[0];
		}

	}


}

