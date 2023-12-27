using CsvHelper.Configuration;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Utils;
using System.Data;
using System.Data.SQLite;
using System.Drawing.Text;
using System.Reflection;
using static PLang.Runtime.Startup.ModuleLoader;

namespace PLang.Exceptions.AskUser
{
    public abstract class AskUserException : Exception
	{
		protected Func<object[], Task>? Callback { get; set; }
		public abstract Task InvokeCallback(object value);
		public AskUserException(string question, Func<object[], Task>? callback = null) : base(question)
		{
			this.Callback = callback;
		}

		protected static Func<object[], Task> CreateAdapter(Delegate? callback)
		{
			if (callback == null) { return null; }
			return async args =>
			{
				var result = callback.DynamicInvoke(args) as Task;
				if (result != null)
				{
					await result;
				}
			};
		}

	}

	public class AskUserWebserver : AskUserException
	{
		public int StatusCode { get; private set; }
		public AskUserWebserver(string question, int statusCode = 500, Func<object?, Task>? callback = null) : base(question, callback)
		{
			StatusCode = statusCode;
		}

		public override async Task InvokeCallback(object value)
		{
			return;
		}
	}

	public class AskUserConsole : AskUserException
	{
		public AskUserConsole(string question, Func<object?, Task>? callback = null) : base(question, callback)
		{
		}
		public override async Task InvokeCallback(object value)
		{
			await Callback.Invoke(new object[] { value });
		}
	}

	public class AskUserDbConnectionString : AskUserException
	{
		private readonly string typeFullName;
		private readonly string dataSourceName;
		string regexToExtractDatabaseNameFromConnectionString;
		private readonly bool keepHistory;
		private readonly bool isDefault;

		public AskUserDbConnectionString(string dataSourceName, string typeFullName,
			string regexToExtractDatabaseNameFromConnectionString, bool keepHistory, bool isDefault, string question, Func<string, string, string, string, bool, bool, Task> callback) : base(question, CreateAdapter(callback))
		{
			this.dataSourceName = dataSourceName;
			this.regexToExtractDatabaseNameFromConnectionString = regexToExtractDatabaseNameFromConnectionString;
			this.keepHistory = keepHistory;
			this.isDefault = isDefault;
			this.typeFullName = typeFullName;

		}

		public override async Task InvokeCallback(object answer)
		{
			await Callback.Invoke(new object[] { dataSourceName, typeFullName, regexToExtractDatabaseNameFromConnectionString, answer, keepHistory, isDefault });

		}
	}

	public class AskUserDatabaseType : AskUserException
	{
		private readonly ILlmService aiService;
		private readonly string supportedDbTypes;

		public AskUserDatabaseType(ILlmService aiService, string supportedDbTypes, string question, Func<string, string, string, string, string, bool, bool, Task> callback) : base(question, CreateAdapter(callback))
		{
			this.aiService = aiService;
			this.supportedDbTypes = supportedDbTypes;
		}

		private record MethodResponse(string typeFullName, string dataSourceName, string nugetCommand, 
				string regexToExtractDatabaseNameFromConnectionString, string dataSourceConnectionStringExample, 
				bool keepHistoryEventSourcing, bool isDefault = false);

		public override async Task InvokeCallback(object answer)
		{
			var system = @$"Map user request

If user provides a full data source connection, return {{error:explainWhyConnectionStringShouldNotBeInCodeMax100Characters}}.

typeFullName: from database types provided, is the type.FullName for IDbConnection in c# for this database type for .net 7
dataSourceName: give name to the datasource if not defined by user 
nugetCommand: nuget package name, for running ""nuget install ...""
dataSourceConnectionStringExample: create an example of a connection string for this type of databaseType
regexToExtractDatabaseNameFromConnectionString: generate regex to extract the database name from a connection string from user selected databaseType
keepHistoryEventSourcing: true when typeFullName is SQLite and not defined by user, else false
isDefault: true if user asks to make it default, else it is false


You must return JSON scheme:
{TypeHelper.GetJsonSchema(typeof(MethodResponse))}
";
			string assistant = $"## database types ##\r\n{supportedDbTypes}\r\n## database types ##";

			var llmQuestion = new LlmQuestion("AskUserDatabaseType", system, answer.ToString(), assistant);
			var result = await aiService.Query<MethodResponse>(llmQuestion);

			await Callback.Invoke(new object[] {
				 result.typeFullName, result.dataSourceName, result.nugetCommand, result.dataSourceConnectionStringExample,
				result.regexToExtractDatabaseNameFromConnectionString, result.keepHistoryEventSourcing, result.isDefault});	
		}
	}	
	public class AskUserSqliteName : AskUserException
	{
		private readonly string rootPath;

		public AskUserSqliteName(string rootPath, string question, Func<string, string, string, string, bool, bool, Task> callback) : base(question, CreateAdapter(callback))
		{
			this.rootPath = rootPath;
		}

		public override async Task InvokeCallback(object answer)
		{
			var dbName = answer.ToString().Replace(" ", "_").Replace(".sqlite", "");
			string dbPath = "." + Path.DirectorySeparatorChar + ".db" + Path.DirectorySeparatorChar + dbName + ".sqlite";
			string dbAbsolutePath = Path.Join(rootPath, dbPath);

			await Callback.Invoke(new object[] {
				dbName.ToString(), typeof(SQLiteConnection).FullName, dbName.ToString() + ".sqlite", $"Data Source={dbAbsolutePath};Version=3;", true, false});	

		}
	}		
	public class AskUserDataSourceNameExists : AskUserException
	{
		private readonly ILlmService aiService;
		private readonly string typeFullName;
		private readonly string dataSourceName;
		private readonly string nugetCommand;
		private readonly string dataSourceConnectionStringExample;
		private readonly string regexToExtractDatabaseNameFromConnectionString;
		private readonly bool keepHistoryEventSourcing;
		private readonly bool isDefault;

		public AskUserDataSourceNameExists(ILlmService aiService, string typeFullName, string dataSourceName, string nugetCommand,
			string dataSourceConnectionStringExample, string regexToExtractDatabaseNameFromConnectionString,
			bool keepHistoryEventSourcing, bool isDefault, string message, Func<string, string, string, string, string, bool, bool, Task> callback) : base(message, CreateAdapter(callback))
		{
			this.aiService = aiService;
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

			var llmQuestion = new LlmQuestion("AskUserDatabaseType", $"Map user request", answer.ToString(), "");
			var result = await aiService.Query<MethodResponse>(llmQuestion);

			await Callback.Invoke(new object[] {
				 result.typeFullName, result.dataSourceName, result.nugetCommand, result.dataSourceConnectionStringExample,
				result.regexToExtractDatabaseNameFromConnectionString, result.keepHistoryEventSourcing, result.isDefault});	

		}
	}
}
