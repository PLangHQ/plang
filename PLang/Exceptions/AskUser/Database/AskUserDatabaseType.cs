using PLang.Errors.Handlers;
using PLang.Models;
using PLang.Services.LlmService;
using PLang.Utils;

namespace PLang.Exceptions.AskUser.Database;

public class AskUserDatabaseType : AskUserError
{
    private readonly string dataSourceName;
    private readonly bool keepHistoryEventSourcing;
    private readonly ILlmServiceFactory llmServiceFactory;
    private readonly bool setAsDefaultForApp;
    private readonly string supportedDbTypes;

    public AskUserDatabaseType(ILlmServiceFactory llmServiceFactory, bool setAsDefaultForApp,
        bool keepHistoryEventSourcing,
        string supportedDbTypes, string dataSourceName, string question,
        Func<string, string, string, string, string, bool, bool, Task> callback) : base(question,
        CreateAdapter(callback))
    {
        this.llmServiceFactory = llmServiceFactory;
        this.setAsDefaultForApp = setAsDefaultForApp;
        this.keepHistoryEventSourcing = keepHistoryEventSourcing;
        this.supportedDbTypes = supportedDbTypes;
        this.dataSourceName = dataSourceName;
    }

    public override async Task InvokeCallback(object answer)
    {
        var system = @"Map user request

If user provides a full data source connection, return {error:explainWhyConnectionStringShouldNotBeInCodeMax100Characters}.

typeFullName: from database types provided, is the type.FullName for IDbConnection in c# for this database type for .net 7
nugetCommand: nuget package name, for running ""nuget install ...""
dataSourceConnectionStringExample: create an example of a connection string for this type of databaseType
regexToExtractDatabaseNameFromConnectionString: generate regex to extract the database name from a connection string from user selected databaseType
";
        var assistant = $"## database types ##\r\n{supportedDbTypes}\r\n## database types ##";

        var promptMessage = new List<LlmMessage>();
        promptMessage.Add(new LlmMessage("system", system));
        promptMessage.Add(new LlmMessage("assistant", assistant));
        promptMessage.Add(new LlmMessage("user", answer.ToString()));

        var llmRequest = new LlmRequest("AskUserDatabaseType", promptMessage);
        llmRequest.scheme = TypeHelper.GetJsonSchema(typeof(DatabaseTypeResponse));

        var (result, queryError) = await llmServiceFactory.CreateHandler().Query<DatabaseTypeResponse>(llmRequest);

        if (result == null) throw new Exception("Could not use LLM to format your answer");
        if (Callback == null) return;

        await Callback.Invoke([
            result.typeFullName,
            dataSourceName,
            result.nugetCommand,
            result.dataSourceConnectionStringExample,
            result.regexToExtractDatabaseNameFromConnectionString,
            keepHistoryEventSourcing,
            setAsDefaultForApp
        ]);
    }

    private record DatabaseTypeResponse(
        string typeFullName,
        string nugetCommand,
        string regexToExtractDatabaseNameFromConnectionString,
        string dataSourceConnectionStringExample);
}