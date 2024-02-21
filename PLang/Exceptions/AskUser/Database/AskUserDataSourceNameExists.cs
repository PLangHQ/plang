using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;

namespace PLang.Exceptions.AskUser.Database
{
	public class AskUserDataSourceNameExists : AskUserException
	{
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly string typeFullName;
		private readonly string dataSourceName;
		private readonly string nugetCommand;
		private readonly string dataSourceConnectionStringExample;
		private readonly string regexToExtractDatabaseNameFromConnectionString;
		private readonly bool keepHistoryEventSourcing;
		private readonly bool isDefault;

		public AskUserDataSourceNameExists(ILlmServiceFactory llmServiceFactory, string typeFullName, string dataSourceName, string nugetCommand,
			string dataSourceConnectionStringExample, string regexToExtractDatabaseNameFromConnectionString,
			bool keepHistoryEventSourcing, bool isDefault, string message, Func<string, string, string, string, string, bool, bool, Task> callback) : base(message, CreateAdapter(callback))
		{
			this.llmServiceFactory = llmServiceFactory;
			this.typeFullName = typeFullName;
			this.dataSourceName = dataSourceName;
			this.nugetCommand = nugetCommand;
			this.dataSourceConnectionStringExample = dataSourceConnectionStringExample;
			this.regexToExtractDatabaseNameFromConnectionString = regexToExtractDatabaseNameFromConnectionString;
			this.keepHistoryEventSourcing = keepHistoryEventSourcing;
			this.isDefault = isDefault;
		}

		private record MethodResponse(string typeFullName, string dataSourceName, string dataSourceConnectionStringExample, string nugetCommand, string regexToExtractDatabaseNameFromConnectionString, bool keepHistoryEventSourcing, bool isDefault = false);
		public override async Task InvokeCallback(object answer)
		{
			if (Callback == null) return;

			string assistant = @$"These are previously defined properties by the user, use them if not otherwise defined by user.
## previously defined ##
typeFullName: {typeFullName}
dataSourceName: {dataSourceName}
nugetCommand: {nugetCommand}
dataSourceConnectionStringExample: {dataSourceConnectionStringExample}
regexToExtractDatabaseNameFromConnectionString: {regexToExtractDatabaseNameFromConnectionString}
isDefault: {isDefault}
keepHistoryEventSourcing: {keepHistoryEventSourcing}
## previously defined ##
";

			var promptMessage = new List<LlmMessage>();
			promptMessage.Add(new LlmMessage("system", "Map user request"));
			promptMessage.Add(new LlmMessage("assistant", assistant));
			promptMessage.Add(new LlmMessage("user", answer.ToString()!));


			var llmRequest = new LlmRequest("AskUserDatabaseType", promptMessage);
			var result = await llmServiceFactory.CreateHandler().Query<MethodResponse>(llmRequest);
			if (result == null) return;

			await Callback.Invoke(new object[] {
				 result.typeFullName, result.dataSourceName, result.nugetCommand, result.dataSourceConnectionStringExample,
				result.regexToExtractDatabaseNameFromConnectionString, result.keepHistoryEventSourcing, result.isDefault});

		}
	}
}
