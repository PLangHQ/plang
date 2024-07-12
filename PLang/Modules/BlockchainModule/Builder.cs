using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Interfaces;
using PLang.Services.LlmService;
using PLang.Services.SettingsService;
using System.Dynamic;

namespace PLang.Modules.BlockchainModule
{
    public class Builder : BaseBuilder 
	{
		private readonly ISettings settings;
		private readonly PLangAppContext context;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly Program program;

		public Builder(ISettings settings, PLangAppContext context, ILlmServiceFactory llmServiceFactory)
		{
			this.settings = settings;
			this.context = context;
			this.llmServiceFactory = llmServiceFactory;
		}

		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step)
		{
			
			var moduleSettings = new ModuleSettings(settings, llmServiceFactory);
			var rpcServers = moduleSettings.GetRpcServers();
	
			var wallets = moduleSettings.GetWallets();
			var tokens = moduleSettings.GetTokens().ToList();
			AppendToAssistantCommand(@$"# RPC servers available #
{JsonConvert.SerializeObject(rpcServers)}
# RPC servers available #
# wallet addresses #
{JsonConvert.SerializeObject(wallets)}
# wallet addresses #");
			(var instruction, var buildError) = await base.Build(step);
			if (buildError != null) return (null, buildError);

			var gf = instruction.Action as GenericFunction;
			var abi = gf.Parameters.FirstOrDefault(p => p.Name.ToLower() == "abi");
			if (abi != null && abi.Value.ToString().Contains("\"inputs\"")) {
				var obj = JsonConvert.DeserializeObject<Dictionary<string, object>>(abi.Value.ToString());
				if (obj != null && obj.ContainsKey("inputs"))
				{
					var jarray = obj["inputs"] as JArray;
					int index = 0;
					List<Parameter> parameters = new List<Parameter>();
					foreach (var input in jarray)
					{
						parameters.Add(new Parameter(gf.Parameters[index++].Type, input["name"].ToString(), gf.Parameters[index++].Value));
					}
				}
			}

			if (gf.FunctionName == "SetCurrentWallet")
			{
				var wallet = wallets.FirstOrDefault(p => p.Name.ToLower() == gf.Parameters[0].Value.ToString().ToLower());
				context.AddOrReplace(Program.CurrentWalletContextKey, wallet);
			}

			if (gf.FunctionName == "SetCurrentRpcServer")
			{
				var currentRpcServer = rpcServers.FirstOrDefault(p => p.Name.ToLower() == gf.Parameters[0].Value.ToString().ToLower() || p.Url.ToLower() == gf.Parameters[0].Value.ToString().ToLower());
				context.AddOrReplace(Program.CurrentRpcServerContextKey, currentRpcServer);
			}

			if (gf.FunctionName == "SetCurrentAddress")
			{
				foreach (var wallet in wallets)
				{
					int idx = wallet.Addresses.FindIndex(p => p == gf.Parameters[0].Value);
					if (idx != -1)
					{
						context.AddOrReplace(Program.CurrentAddressContextKey, idx);
						context.AddOrReplace(Program.CurrentWalletContextKey, wallet);
					}
				}
			}
			return (instruction, null);
		}
	}
}
