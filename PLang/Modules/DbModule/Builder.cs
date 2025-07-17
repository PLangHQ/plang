using AngleSharp.Html.Dom;
using LightInject;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using OpenAI.Assistants;
using Org.BouncyCastle.Crypto.Prng;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.DbService;
using PLang.Services.EventSourceService;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using System.Xml.Linq;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Modules.DbModule.Program;
using Instruction = PLang.Building.Model.Instruction;

namespace PLang.Modules.DbModule
{
	public class Builder : BaseBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly IDbServiceFactory dbFactory;
		private readonly ISettings settings;
		private readonly PLangAppContext context;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly ITypeHelper typeHelper;
		private readonly ILogger logger;
		private readonly MemoryStack memoryStack;
		private readonly VariableHelper variableHelper;
		private ModuleSettings dbSettings;
		private readonly PrParser prParser;
		private readonly ProgramFactory programFactory;

		public Builder(IPLangFileSystem fileSystem, IDbServiceFactory dbFactory, ISettings settings, PLangAppContext context,
			ILlmServiceFactory llmServiceFactory, ITypeHelper typeHelper, ILogger logger, MemoryStack memoryStack,
			VariableHelper variableHelper, ModuleSettings dbSettings, PrParser prParser, ProgramFactory programFactory) : base()
		{
			this.fileSystem = fileSystem;
			this.dbFactory = dbFactory;
			this.settings = settings;
			this.context = context;
			this.llmServiceFactory = llmServiceFactory;
			this.typeHelper = typeHelper;
			this.logger = logger;
			this.memoryStack = memoryStack;
			this.variableHelper = variableHelper;
			this.dbSettings = dbSettings;
			this.prParser = prParser;
			this.programFactory = programFactory;

			this.dbSettings.UseInMemoryDataSource = true;
		}

		public record DbGenericFunction(string Reasoning, string Name,
			List<Parameter>? Parameters = null, List<ReturnValue>? ReturnValues = null,
			List<string>? TableNames = null, string? DataSource = null, Dictionary<string, string>? AffectedColumns = null, string? Warning = null) : GenericFunction(Reasoning, Name, Parameters, ReturnValues);



		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep goalStep, IBuilderError? previousBuildError = null)
		{
			var dataSource = goalStep.GetVariable<DataSource>();
			if (dataSource == null)
			{
				var dataSources = await dbSettings.GetAllDataSources();
				if (dataSources == null)
				{
					await dbSettings.CreateDataSource();
					dataSources = await dbSettings.GetAllDataSources();
				}
				else if (dataSources.Count > 1)
				{
					(dataSources, var error) = await FigureOutDataSource(goalStep);
					if (error != null) return (null, error);
				}

				dataSource = dataSources.FirstOrDefault();
				goalStep.AddVariable<DataSource>(dataSource);
			}

			if (!goalStep.Goal.IsSetup && dataSource == null)
			{
				return (null, new StepBuilderError("Could not find data source. Are you building in the correct directory?", goalStep, ContinueBuild: false));
			}
			else
			{
				if (goalStep.Goal.GoalName.Equals("Setup"))
				{
					dataSource = (await dbSettings.GetAllDataSources()).FirstOrDefault(p => p.IsDefault);
				}
			}

			string sqlType = "sqlite";
			string autoIncremental = "";
			string insertAppend = "";
			string system = "";
			if (dataSource != null)
			{
				sqlType = dataSource.TypeFullName;
				autoIncremental = (dataSource!.KeepHistory) ? "(NO auto increment)" : "auto incremental";

				system = @$"
Use dataSourceName from <dataSource> when parameter for method is dataSourceName

<dataSource name=""{dataSource.Name}"">
Database type:{dataSource.TypeFullName}
Database name:{dataSource.DbName}
Id columns: {autoIncremental}
</dataSource>
";
				// todo: this will fail with different type of dbs
				if (dataSource.KeepHistory)
				{
					insertAppend = "- On Insert, InsertOrUpdate, InsertOrUpdateAndSelectIdOfRow you must modify sql to include id(System.Int64) column and ParameterInfo @id=\"auto\"";
				}
			}

			system += @$"
Additional json Response explaination:
- DataSource: Datasource to use
- TableNames: List of tables defined in sql statement
- AffectedColumns: Dictionary of affected columns with type(primary_key|select|insert|update|delete|create|alter|index|drop|where|order|other), e.g. select name from users where id=1 => 'name':'select', 'id':'where'

Rules:
- You MUST generate valid sql statement for {sqlType}
- Returning 1 mean user want only one row to be returned (limit 1)
- On Select statement and result is not written to a variable, Set ReturnValues of the function as the columns that are being selected from the sql statement
- On CreateTable, include primary key, id, not null, {autoIncremental} when not defined by user. It must be long
{insertAppend}
- Number(int => long) MUST be type System.Int64 unless defined by user.
- string in user sql statement should be replaced with @ parameter in sql statement and added as parameters in ParamterInfo but as strings. Strings are as is in the parameter list.
";

			AppendToSystemCommand(system);
			var buildResult = await base.Build<DbGenericFunction>(goalStep, previousBuildError);
			if (buildResult.BuilderError != null) return buildResult;

			var gf = buildResult.Instruction.Function as DbGenericFunction;


			var dataSourceNameParam = gf.GetParameter<string>("dataSourceName");
			if (dataSourceNameParam?.Contains("%") == true)
			{
				var dynamicDataSourceName = ConvertVariableNamesInDataSourceName(variableHelper, dataSourceNameParam);
				var dsResult = await dbSettings.GetDataSource(dynamicDataSourceName);
				if (dsResult.Error != null) return (null, new BuilderError(dsResult.Error));

				int idx = gf.Parameters.FindIndex(p => p.Name == "dataSourceName");
				gf.Parameters[idx] = gf.Parameters[idx] with { Value = dynamicDataSourceName };

				//buildResult.Instruction = buildResult.Instruction with { Function = gf };
			}

			var parameters = gf.GetParameter<List<ParameterInfo>>("sqlParameters");

			if (parameters != null && parameters.Count > 0)
			{
				return await ValidateSqlAndParameters(goalStep, insertAppend, buildResult.Instruction);
			}


			return buildResult;
		}


		public record DataSourceSelection(string Reason, List<string> DataSources, string? NameResolutionAmbiguity = null);
		public async Task<(List<DataSource>?, IBuilderError?)> FigureOutDataSource(GoalStep step)
		{
			string system = @"
Your job is to figure out which data source(s) to use from the list of <datasources>.
You are provided with table names of each data source.
Understand the intent of the user to choose the right data source(s)
When it is not possible to know which datasource to use, set NameResolutionAmbiguity, e.g. :""table 'products' is in both datasources 'default' and 'userdata', I cannot determine which to user""
Give a short description of you Reason for the selection
";

			var program = GetProgram(step);
			var dataSources = await dbSettings.GetAllDataSources();
			List<object> dataSourceInfos = new();
			foreach (var dataSource in dataSources)
			{
				var tableResult = await program.Select(dataSource.SelectTablesAndViews, dataSourceName: dataSource.Name);

				dataSourceInfos.Add(new
				{
					DataSourceName = dataSource.Name,
					Tables = tableResult.Table
				});
			}

			system += @$"<datasources>\n{JsonConvert.SerializeObject(dataSourceInfos)}\n</datasources>";

			List<LlmMessage> messages = new();
			messages.Add(new LlmMessage("system", system));
			messages.Add(new LlmMessage("user", step.Text));
			LlmRequest llmRequest = new("SelectDatasource", messages);


			(var dataSourceSelection, var error) = await llmServiceFactory.CreateHandler().Query<DataSourceSelection>(llmRequest);
			if (error != null) return (null, new BuilderError(error));


			if (!string.IsNullOrEmpty(dataSourceSelection.NameResolutionAmbiguity))
			{
				return (null, new BuilderError(dataSourceSelection.NameResolutionAmbiguity,
					FixSuggestion: @$"Add data source to your step, e.g. 
`- {step.Text}
	data source: {dataSources[0].Name}", Retry: false));
			}

			List<DataSource> selectedDataSources = new List<DataSource>();
			foreach (var dataSource in dataSourceSelection.DataSources)
			{
				var selectedDataSource = dataSources.FirstOrDefault(p => p.Name.Equals(dataSource, StringComparison.OrdinalIgnoreCase));
				if (selectedDataSource == null)
				{
					return (null, new BuilderError($"Data source '{dataSource}' could not be found in list of data sources."));
				}
				selectedDataSources.Add(selectedDataSource);
			}

			return (selectedDataSources, null);

		}


		private async Task<(Instruction? Instruction, IBuilderError? Error)> ValidateSqlAndParameters(GoalStep goalStep, string insertAppend, Instruction instruction)
		{
			var gf = instruction.Function as DbGenericFunction;
			if (gf.TableNames == null || gf.TableNames.Count == 0)
			{
				return (null, new StepBuilderError("You must provide TableNames", goalStep));
			}
			if (string.IsNullOrEmpty(gf.DataSource))
			{
				return (null, new StepBuilderError("You must provide DataSource", goalStep));
			}

			using var program = GetProgram(GoalStep);
			var dbStructure = await program.GetDatabaseStructure(gf.TableNames, gf.DataSource);
			string? sql = gf.GetParameter<string>("sql");

			string? additionalInfo = null;
			if (gf.Name.StartsWith("Insert"))
			{
				additionalInfo = "- Validate that the insert statement is not missing a not null column. Give a warning.";
			}
			if (gf.Name.StartsWith("Select"))
			{
				additionalInfo = @"- User might escape % on LIKE statement, e.g. title=\\%%q%, then the VariableNameOrValue should be escaped as well, e.g. \\%%title%\\%, should map to VariableNameOrValue=\\%%title%\\%
- Respect join request by user";
			}

			List<LlmMessage> messages = new List<LlmMessage>();
			messages.Add(new LlmMessage("system", @$"
You are provided with a information for sql query executed in c#.
You job is to validate it with <table> information provided.

- User is likely writing a pseudo sql, use your best judgement to create and validate the sql with help of <table> information
- Adjust the Parameters to match the names and data types of the columns in the <table>. 
- Datatype are .net object, TEXT=System.String, INTEGER=System.Int64, REAL=System.Double, NUMERIC=System.Double, BOOLEAN=System.Boolean, BLOB=byte[], etc.
- Validate <sql> statement that it matches with columns, adjust the sql if needed.
{additionalInfo}
{insertAppend}
"));

			var parameters = gf.GetParameter<List<ParameterInfo>>("sqlParameters");

			messages.Add(new LlmMessage("user", $@"
User intent: {goalStep.Text}
<parameters>{JsonConvert.SerializeObject(parameters)}</parameters>
<sql>{sql}</sql>
<table>{JsonConvert.SerializeObject(dbStructure.TablesAndColumns)}</table>"));
			LlmRequest question = new LlmRequest("ParameterList", messages);
			question.llmResponseType = "json";

			var result = await llmServiceFactory.CreateHandler().Query<ValidateSqlParameters>(question);
			if (result.Error != null) return (null, new StepBuilderError(result.Error, goalStep));

			var sqlAndParams = result.Response;
			gf.Parameters[0] = gf.Parameters[0] with { Value = sqlAndParams.Sql };
			gf.Parameters[1] = gf.Parameters[1] with { Value = sqlAndParams.Parameters };
			gf = gf with { Warning = sqlAndParams.Warning };

			instruction = instruction with { Function = gf };
			instruction.LlmRequest.Add(question);
			return (instruction, null);
		}

		private record ValidateSqlParameters(string Sql, List<ParameterInfo> Parameters, string? Warning = null);

		public record DataSourceWithTableInfo(DataSource DataSource, List<TableInfo> TableInfos);

		public async Task<List<DataSourceWithTableInfo>> GetDataSources(List<string> TableNames)
		{
			List<DataSourceWithTableInfo> DataSources = new();

			using var program = GetProgram(GoalStep);
			var dataSources = await program.GetDataSources();
			foreach (var dataSource in dataSources)
			{

				var dbStructure = await program.GetDatabaseStructure(TableNames, dataSource.Name);
				if (dbStructure.TablesAndColumns is null) continue;
				if (dbStructure.Error != null)
				{
					logger.LogWarning(dbStructure.Error.Message);
					continue;
				}
				DataSources.Add(new DataSourceWithTableInfo(dataSource, dbStructure.TablesAndColumns));
			}

			return DataSources;
		}

		public async Task<IBuilderError?> BuilderExecute(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var dataSourceName = GenericFunctionHelper.GetParameterValueAsString(gf, "dataSourceName");
			var sql = GenericFunctionHelper.GetParameterValueAsString(gf, "sql");

			if (VariableHelper.IsVariable(sql))
			{
				return new StepBuilderError("Do not use the Execute method when the sql is a %variable%. Use ExecuteDynamicSql method.", step);
			}

			var tableAllowList = GenericFunctionHelper.GetParameterValueAsList(gf, "tableAllowList");
			using var program = GetProgram(step);
			var result = await program.Execute(sql, tableAllowList, dataSourceName);
			if (result.Error != null)
			{
				return new BuilderError(result.Error) { Retry = false };
			}
			logger.LogInformation($"  - ✅ Sql statement validated - {sql.MaxLength(30, "...")} - {step.Goal.RelativeGoalPath}:{step.LineNumber}");
			return null;
		}

		public async Task<(Instruction, IBuilderError?)> BuilderValidate(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var dataSourceName = GenericFunctionHelper.GetParameterValueAsString(gf, "dataSourceName", "-1");
			if (dataSourceName == "-1") return (instruction, null);

			if (string.IsNullOrEmpty(dataSourceName))
			{
				return (instruction, new StepBuilderError("Missing DataSource from instruction file. Not legal pr file", step,
					Key: "InvalidInstructionFile", FixSuggestion: $"Try rebuilding the .pr file: {step.RelativePrPath}"));
			}

			List<string> MethodsToValidate = ["Select", "SelectOneRow", "Update", "InsertOrUpdate", "InsertOrUpdateAndSelectIdOfRow", "Insert", "InsertAndSelectIdOfInsertedRow", "Delete"];

			var sql = GenericFunctionHelper.GetParameterValueAsString(gf, "sql");
			if (string.IsNullOrEmpty(sql)) return (instruction, null);

			if (!MethodsToValidate.Contains(gf.Name)) return (instruction, null);


			(var validSql, dataSourceName, var error) = await IsValidSql(sql, dataSourceName, step);
			if (error == null)
			{
				var updatedParams = gf.Parameters
					.Select(p => p.Name == "dataSourceName" ? p with { Value = dataSourceName } : p)
					.ToList();

				gf = gf with { DataSource = dataSourceName, Parameters = updatedParams };
				instruction = instruction with { Function = gf };

				logger.LogInformation($"  - ✅ Sql statement validated - {sql.MaxLength(30, "...")} - {step.Goal.RelativeGoalPath}:{step.LineNumber}");

				return (instruction, null);
			}


			using var program = GetProgram(step);
			var tableStructure = await program.GetDatabaseStructure(gf.TableNames, dataSourceName);

			bool retry = error.Retry;
			string info = "";
			if (tableStructure.TablesAndColumns != null && tableStructure.TablesAndColumns.Count > 0)
			{
				retry = true;
				info = $"Use <table_info> to rebuild a valid sql.\n<table_info>{JsonConvert.SerializeObject(tableStructure.TablesAndColumns)}<table_info>";
			}
			if (tableStructure.Error != null)
			{
				info = tableStructure.Error.Message;
			}

			return (instruction, new StepBuilderError($@"Could not validate sql: {sql}.
Reason:{error.Message}", step,
				FixSuggestion: info, LlmBuilderHelp: info, Retry: retry));



		}


		public async Task<IBuilderError?> BuilderCreateTable(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			if (!step.Goal.IsSetup) return new StepBuilderError("Create table can only be in a setup file", step,
				FixSuggestion: @"Move the create statment into a setup file");

			var sql = GenericFunctionHelper.GetParameterValueAsString(gf, "sql");
			if (string.IsNullOrEmpty(sql)) return new StepBuilderError("sql is empty, cannot create table", step);

			(var dataSource, var error) = await dbSettings.GetDataSource(step.Goal.DataSourceName ?? "data");
			if (error != null && error.Key == "DataSourceNotFound")
			{
				var dataSources = await dbSettings.GetAllDataSources();
				if (dataSources.Count == 0)
				{
					(dataSource, error) = await dbSettings.CreateDataSource("data", setAsDefaultForApp: true, keepHistoryEventSourcing: true);
				}
			}

			if (error != null) return new BuilderError(error);

			step.Goal.DataSourceName = dataSource!.Name;
			step.Goal.AddVariable(dataSource);

			if (dataSource.ConnectionString.Contains("Mode=Memory;"))
			{
				using var program = GetProgram(step);
				(_, error) = await program.CreateTable(sql);

				if (error != null) return new StepBuilderError(error, step);
				logger.LogInformation($"  - ✅ Sql statement validated - {sql.MaxLength(30, "...")} - {step.Goal.RelativeGoalPath}:{step.LineNumber}");
			}


			return null;
		}

		private Program GetProgram(GoalStep step)
		{
			var program = new Program(dbFactory, fileSystem, settings, llmServiceFactory, new DisableEventSourceRepository(), context, logger, typeHelper, dbSettings, prParser, null);
			program.SetStep(step);
			program.SetGoal(step.Goal);
			return program;
		}

		public async Task<IBuilderError?> BuilderSetDataSourceName(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var name = GenericFunctionHelper.GetParameterValueAsString(gf, "dataSourceName");
			if (name == null) return new InstructionBuilderError("Could not find 'dataSourceName' property in instructions", step, step.Instruction);

			name = ConvertVariableNamesInDataSourceName(variableHelper, name);

			var result = await dbSettings.GetDataSource(name);
			if (result.Error != null) return new StepBuilderError(result.Error, step);

			step.Goal.AddVariable(result.DataSource);

			return null;
		}

		public async Task<(Instruction, IBuilderError?)> BuilderInsertAndSelectIdOfInsertedRow(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			return await BuilderInsert(step, instruction, gf);
		}
		public async Task<(Instruction, IBuilderError?)> BuilderInsertOrUpdateAndSelectIdOfRow(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			return await BuilderInsert(step, instruction, gf);
		}
		public async Task<(Instruction, IBuilderError?)> BuilderInsertOrUpdate(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			return await BuilderInsert(step, instruction, gf);
		}
		public async Task<(Instruction, IBuilderError?)> BuilderInsert(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var dataSourceResult = await dbSettings.GetDataSource(gf.DataSource);
			if (dataSourceResult.Error != null) return (instruction, new StepBuilderError(dataSourceResult.Error, step));

			if (dataSourceResult.DataSource!.KeepHistory == false) return (instruction, null);

			var parameter = gf.Parameters?.FirstOrDefault(p => p.Name.Equals("sqlParameters"));
			if (parameter == null) return (instruction, new StepBuilderError("No parameters included. It needs at least to have @id", step));

			var parameterInfos = TypeHelper.ConvertToType<List<ParameterInfo>>(parameter.Value);
			if (parameterInfos == null)
			{
				return (instruction, new StepBuilderError("Instruction file is invalid. Could not deserialize ParameterInfo list", step));
			}

			var hasId = parameterInfos.FirstOrDefault(p => p.ParameterName == "@id");
			if (hasId != null) return (instruction, null);

			return (instruction, new StepBuilderError("No id provided in sqlParameters. This is required for this datasource", step));
		}

		public async Task<(Instruction, IBuilderError?)> BuilderCreateDataSource(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			if (!step.Goal.IsSetup) return (instruction, new StepBuilderError("Create data source can only be in a setup file", step,
				FixSuggestion: $"Move this step, {step.Text} to Setup.goal file or into a Setup file under a setup folder", Retry: false));

			var prevStep = step.PreviousStep;
			while (prevStep != null)
			{
				(var function, var instructionError) = prevStep.GetFunction(fileSystem);
				if (instructionError != null)
				{
					logger.LogError(instructionError.ToString());
				}
				else
				{
					if (function!.Name == gf.Name)
					{
						return (instruction, new StepBuilderError("Only one Create data source can exist in each setup file.", step, Retry: false,
							FixSuggestion: $"When having multiple data sources, create a folder called 'setup', then create a new .goal file for each data source. Each setup file should contain the create statements for that data source"));
					}
				}
				prevStep = prevStep.PreviousStep;
			}

			var dataSourceName = GenericFunctionHelper.GetParameterValueAsString(gf, "dataSourceName");
			if (string.IsNullOrEmpty(dataSourceName))
			{
				return (instruction, new StepBuilderError("Name for the data source is missing. Please define it.", step, FixSuggestion: $"Example: \"- Create sqlite data source 'myDatabase'\""));
			}

			dataSourceName = ConvertVariableNamesInDataSourceName(variableHelper, dataSourceName);

			var dataSources = await dbSettings.GetAllDataSources();
			var dataSource = dataSources.FirstOrDefault(p => p.Name == dataSourceName);

			var dbTypeParam = GenericFunctionHelper.GetParameterValueAsString(gf, "databaseType");
			if (string.IsNullOrEmpty(dbTypeParam)) dbTypeParam = "sqlite";

			var setAsDefaultForApp = GenericFunctionHelper.GetParameterValueAsBool(gf, "setAsDefaultForApp");

			if (setAsDefaultForApp == null)
			{
				if (dataSource != null)
				{
					setAsDefaultForApp = dataSource.IsDefault;
				}
				else
				{
					setAsDefaultForApp = dataSources.Count == 0;
				}
			}

			var keepHistoryEventSourcing = GenericFunctionHelper.GetParameterValueAsBool(gf, "keepHistoryEventSourcing");
			if (keepHistoryEventSourcing == null)
			{
				if (dataSource != null)
				{
					keepHistoryEventSourcing = dataSource.KeepHistory;
				}
				else
				{
					keepHistoryEventSourcing = true;
				}
			}

			var parameters = gf.Parameters;
			var setDefaultIdx = parameters.FindIndex(p => p.Name == "setAsDefaultForApp");
			if (setDefaultIdx == -1)
			{
				var pi = new Parameter(setAsDefaultForApp.GetType().FullName, "setAsDefaultForApp", setAsDefaultForApp);
				parameters.Add(pi);
			}
			else
			{
				var setDefaultParam = parameters[setDefaultIdx];

				setDefaultParam = setDefaultParam with { Value = setAsDefaultForApp };
				parameters[setDefaultIdx] = setDefaultParam;
			}
			var keepHistoryIdx = parameters.FindIndex(p => p.Name == "keepHistoryEventSourcing");
			if (keepHistoryIdx == -1)
			{
				var pi = new Parameter(setAsDefaultForApp.GetType().FullName, "keepHistoryEventSourcing", setAsDefaultForApp);
				parameters.Add(pi);
			}
			else
			{
				var keepHistoryParam = parameters[keepHistoryIdx];

				keepHistoryParam = keepHistoryParam with { Value = keepHistoryEventSourcing };
				parameters[keepHistoryIdx] = keepHistoryParam;
			}

			gf = gf with { DataSource = dataSourceName, Parameters = parameters };
			instruction = instruction with { Function = gf };

			var (datasource, error) = await dbSettings.CreateOrUpdateDataSource(dataSourceName, dbTypeParam, setAsDefaultForApp.Value, keepHistoryEventSourcing.Value);
			if (error != null) return (instruction, new StepBuilderError(error, step, false));

			step.Goal.DataSourceName = dataSourceName;
			step.RunOnce = GoalHelper.RunOnce(step.Goal);
			step.Goal.AddVariable(datasource);

			return (instruction, null);

		}


		private async Task<(bool IsValid, string DataSourceName, IBuilderError? Error)> IsValidSql(string sql, string dataSourceName, GoalStep step)
		{

			if (string.IsNullOrEmpty(dataSourceName))
			{
				var dataSource = step.GetVariable<DataSource>();
				if (dataSource != null)
				{
					dataSourceName = dataSource.Name;
				}
			}

			var anchors = context.GetOrDefault<Dictionary<string, IDbConnection>>("AnchorMemoryDb", new()) ?? new();
			if (!anchors.ContainsKey(dataSourceName))
			{
				(dataSourceName, _) = dbSettings.GetNameAndPathByVariable(dataSourceName, null);
				if (!anchors.ContainsKey(dataSourceName))
				{
					return (false, dataSourceName, new StepBuilderError($"Data source name '{dataSourceName}' does not exists.", step,
					 FixSuggestion: $@"Choose datasource name from one of there: {string.Join(", ", anchors.Select(p => p.Key))}"));
				}
			}


			var variables = variableHelper.GetVariables(sql);
			foreach (var variable in variables)
			{
				sql = sql.Replace(variable.PathAsVariable, "?");
			}


			var anchor = anchors
				.FirstOrDefault(kvp => kvp.Key == dataSourceName);

			try
			{
				using var cmd = anchor.Value.CreateCommand();
				cmd.CommandText = sql;
				cmd.Prepare();

				return (true, anchor.Key, null);
			}
			catch (Exception ex)
			{
				string errorInfo = "Error(s) while trying to valid the sql.\n";
				errorInfo += $"\tDataSource: {anchor.Key} - Error Message:{ex.Message}\n";

				return (false, dataSourceName, new StepBuilderError(errorInfo, Step: step, Retry: false));
			}

		}















		/*
		public async Task<(Instruction?, IBuilderError?)> Build2(GoalStep goalStep)
		{
			

			var (dataSource, error) = await GetDataSource(gf, goalStep);
			if (error != null) return (null, new StepBuilderError(error, goalStep));
			if (dataSource == null) return (null, new StepBuilderError("Datasource could not be found. This should not happen. You might need to remove datasource in system.sqlite", goalStep));


			SetSystem(@$"Parse user intent.

variable is defined with starting and ending %, e.g. %filePath%

ExplainUserIntent: Write short description of what user want to accomplish
FunctionName: Select the correct function from list of available functions based on user intent.
TableNames: Table names in sql statement, leave variables as is
DatabaseType: Define the database type. The .net library being used is {dataSource.TypeFullName}, determine the database type from the library");


			(var methodDescs, error) = typeHelper.GetMethodDescriptions(typeof(Program));
			var methodJson = JsonConvert.SerializeObject(methodDescs, new JsonSerializerSettings
			{
				NullValueHandling = NullValueHandling.Ignore
			});
			SetAssistant($@"## functions available defined in csharp ##
{methodJson}
## functions available ends ##
");
			using var program = GetProgram(goalStep);

			if (!string.IsNullOrEmpty(dataSource.SelectTablesAndViews))
			{
				var selectTablesAndViews = dataSource.SelectTablesAndViews.Replace("@Database", $"'{dataSource.DbName}'");
				var result = await program.Select(selectTablesAndViews, null, dataSource.Name);
				if (result.error != null) return (null, new BuilderError(result.error));

				if (result.rows != null && result.rows.Count > 0)
				{
					AppendToAssistantCommand($@"## table & views in db start ##
{JsonConvert.SerializeObject(result.rows)}
## table & view in db end ##");
				}
			}


			var buildResult = await base.Build<FunctionAndTables>(goalStep);

			buildError = buildResult.BuilderError;
			var instruction = buildResult.Instruction;

			if (buildError != null || instruction == null)
			{
				return (null, buildError ?? new StepBuilderError("Could not build Sql statement", goalStep));
			}
			var functionInfo = instruction.Action as FunctionAndTables;

			if (functionInfo.FunctionName == "Insert")
			{
				buildResult = await CreateInsert(goalStep, program, functionInfo, dataSource);
			}
			if (functionInfo.FunctionName == "InsertOrUpdate" || functionInfo.FunctionName == "InsertOrUpdateAndSelectIdOfRow")
			{
				buildResult = await CreateInsertOrUpdate(goalStep, program, functionInfo, dataSource);
			}
			else if (functionInfo.FunctionName == "InsertAndSelectIdOfInsertedRow")
			{
				buildResult = await CreateInsertAndSelectIdOfInsertedRow(goalStep, program, functionInfo, dataSource);
			}
			else if (functionInfo.FunctionName == "Update")
			{
				buildResult = await CreateUpdate(goalStep, program, functionInfo, dataSource);
			}
			else if (functionInfo.FunctionName == "Delete")
			{
				buildResult = await CreateDelete(goalStep, program, functionInfo, dataSource);
			}
			else if (functionInfo.FunctionName == "CreateTable")
			{
				buildResult = await CreateTable(goalStep, program, functionInfo, dataSource);
			}
			else if (functionInfo.FunctionName == "Select" || functionInfo.FunctionName == "SelectOneRow")
			{
				buildResult = await CreateSelect(goalStep, program, functionInfo, dataSource);
			}

			if (buildResult.Instruction != null && buildResult.Instruction.Action is GenericFunction)
			{
				buildResult.Instruction.Properties.AddOrReplace("TableNames", functionInfo.TableNames);
				buildResult.Instruction.Properties.AddOrReplace("DataSource", dataSource.Name);

				return buildResult;
			}

			string setupCommand = "";
			if (goalStep.Goal.GoalName == "Setup")
			{
				setupCommand = "Even if table, view or column exists in table, create the statement";
			}
			SetSystem($@"Generate the SQL statement from user command. 
The SQL statement MUST be a valid SQL statement for {functionInfo.DatabaseType}. 
Make sure to use correct data types that match {functionInfo.DatabaseType}
You MUST provide Parameters if SQL has @parameter.
{setupCommand}
");


			SetAssistant($@"## functions available defined in csharp ##
{typeHelper.GetMethodsAsString(typeof(Program))}
## functions available ends ##
");

			await AppendTableInfo(dataSource, program, functionInfo.TableNames);

			buildResult = await base.Build(goalStep);

			if (buildResult.Instruction != null && buildResult.Instruction.Action is GenericFunction)
			{
				buildResult.Instruction.Properties.AddOrReplace("TableNames", functionInfo.TableNames);
				buildResult.Instruction.Properties.AddOrReplace("DataSource", dataSource.Name);

				return buildResult;
			}

			return (null, new BuilderError("Did not get any instruction from LLM"));

		}

		private async Task<(DataSource? DataSource, IError? Error)> GetDataSource(GenericFunction gf, GoalStep step)
		{
			var dataSourceName = GeneralFunctionHelper.GetParameterValueAsString(gf, "dataSourceName");
			if (!string.IsNullOrEmpty(dataSourceName))
			{
				var result = await dbSettings.GetDataSource(dataSourceName);
				if (result.DataSource != null)
				{
					step.AddVariable<DataSource>(result.DataSource);
				}

				return result;
			}

			if (string.IsNullOrEmpty(dataSourceName))
			{
				var ds = step.GetVariable<DataSource>(ReservedKeywords.CurrentDataSource);
				if (ds != null)
				{
					return (ds, null);
				}
			}

			return await dbSettings.GetDataSource(dataSourceName);

		}

		private async Task<(Instruction?, IBuilderError?)> CreateSelect(GoalStep goalStep, Program program, FunctionAndTables functionInfo, DataSource dataSource)
		{
			string databaseType = dataSource.TypeFullName.Substring(dataSource.TypeFullName.LastIndexOf(".") + 1);
			string appendToSystem = "";
			string appendToSelectOneRow = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "SqlParameters @id MUST be type System.Int64";
				appendToSelectOneRow = "or when filtering by %id%";
			}

			string returnValueDesc = @"- If user defines variable to write into, e.g. 'write to %result%' or 'into %result%' then ReturnValue=%result%, 
- Select function always writes to one variable";
			string functionDesc = "List<object> Select(String sql, List<object>()? SqlParameters = null, string? dataSourceName = null) // always writes to one object";
			string functionHelp = $"Select function return multiple rows so user MUST define 'write to %variable%' and returns 1 value, a List<object>";
			if (functionInfo.FunctionName == "SelectOneRow")
			{
				functionDesc = "object? SelectOneRow(String sql, List<object>()? SqlParameters = null, string? dataSourceName = null)";
				returnValueDesc = "- If user does not define 'write to ..' statement then Columns being returned with type if defined by user.";
				functionHelp = $"SelectOneRow function is used when user defines to select only one row {appendToSelectOneRow}";
			}

			SetSystem(@$"Map user command to either of these c# functions: 

## csharp function ##
{functionDesc}
## csharp function ##

## Rules ##
{functionHelp}
Variable is defined with starting and ending %, e.g. %filePath%.
\% is escape from start of variable, would be used in LIKE statements, then VariableNameOrValue should keep the escaped character, e.g. the user input \%%title%\%, should map to VariableNameOrValue=\%%title%\%
SqlParameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName). {appendToSystem}
TypeFullName is Full name of the type in c#, System.String, System.Double, etc.
%Now% variable is type of DateTime. %Now% variable should be injected as SqlParameter 
ReturnValue rules: 
{returnValueDesc}
- integer/int should always be System.Int64. 

If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
You MUST provide SqlParameters if SQL has @parameter.
## Rules ##
");

			SetAssistant(@$"# examples #
- select everything from tableX, write to %table% => sql: SELECT * FROM tableX, ReturnValue: %table%
- select from tableB where id=%id%, into %table% => sql: SELECT * FROM tableB WHERE id=@id, ReturnValue: %table%, SqlParameters:[{{ParameterName:id, VariableNameOrValue:%id%, TypeFullName:int64}}],
- select * from %table% WHERE %name% and value_id=%valueId%, write to %result% => sql: SELECT * FROM %table% WHERE name=@name and value_id=@valueId, ReturnValue: %result%, SqlParameters:[{{ParameterName:name, VariableNameOrValue:%name%, TypeFullName:string}}, {{ParameterName:valueId, VariableNameOrValue:%valueId%, TypeFullName:int}}] 
- select id, name from users, write to %user% => then ReturnValue is %user% object
- select id, name from users => then ReturnValue is %id%, %name% objects
- select * from addresses WHERE address like %address%% => sql SELECT * from addresses WHERE address LIKE @address, SqlParameters:[{{""ParameterName"":""address"", ""VariableNameOrValue"":""%address%%"", ""TypeFullName"":""int64""}}]
# examples #
# ParameterInfo scheme #
{TypeHelper.GetJsonSchemaForRecord(typeof(ParameterInfo))}
# ParameterInfo scheme #
");


			await AppendTableInfo(dataSource, program, functionInfo.TableNames);
			var result = await base.Build(goalStep);
			result.Instruction?.Properties.AddOrReplace("TableNames", functionInfo.TableNames);
			result.Instruction?.Properties.AddOrReplace("DataSource", dataSource.Name);

			return result;

		}


		private async Task<(Instruction?, IBuilderError?)> CreateTable(GoalStep goalStep, Program program, FunctionAndTables functionInfo, DataSource dataSource)
		{
			string keepHistoryCommand = @"If user does not define a primary key, add it to the create statement as id as auto increment and not null";
			if (dataSource.KeepHistory)
			{
				keepHistoryCommand = @$"You MUST add id to create statement.
If id is not defined then add id to the create statement
The id MUST NOT be auto incremental, but is primary key and not null.
The id should be datatype long/bigint/.. which fits {functionInfo.DatabaseType}.
";
			}

			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
void CreateTable(String sql, string? dataSourceName = null)  
## csharp function ##

If table name is a variable, keep the variable in the sql statement
variable is defined with starting and ending %, e.g. %filePath%.
You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
{keepHistoryCommand}
");
			SetAssistant("");

			var result = await base.Build(goalStep);
			result.Instruction?.Properties.AddOrReplace("TableNames", functionInfo.TableNames);

			return result;

		}

		private async Task<(Instruction?, IBuilderError?)> CreateDelete(GoalStep goalStep, Program program, FunctionAndTables functionInfo, DataSource dataSource)
		{
			string databaseType = dataSource.TypeFullName.Substring(dataSource.TypeFullName.LastIndexOf(".") + 1);
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "Parameter @id MUST be type System.Int64";
			}
			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
Int32 Delete(String sql, List<object>()? SqlParameters = null, string? dataSourceName = null)
## csharp function ##

Variable is defined with starting and ending %, e.g. %filePath%.
SqlParameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, etc.
%Now% variable is type of DateTime. %Now% variable should be injected as SqlParameter 
{appendToSystem}
String sql IS required
integer/int should always be System.Int64. 
If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
You MUST provide SqlParameters if SQL has @parameter.

# examples #
""delete from tableX"" => sql: ""DELETE FROM tableX"", warning: Missing WHERE statement can affect rows that should not
""delete tableB where id=%id%"" => sql: ""DELETE FROM tableB WHERE id=@id"", warning: null, SqlParameters:[{{ParameterName:id, VariableNameOrValue:%id%, TypeFullName:int}}]
""delete * from %table% WHERE %name% => sql: ""DELETE FROM %table% WHERE name=@name"", SqlParameters:[{{ParameterName:name, VariableNameOrValue:%name%, TypeFullName:string}}]
# examples #");

			return await BuildCustomStatementsWithWarning(goalStep, dataSource, program, functionInfo);

		}
		private async Task<(Instruction?, IBuilderError?)> CreateUpdate(GoalStep goalStep, Program program, FunctionAndTables functionInfo, DataSource dataSource)
		{
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "Parameter @id MUST be type System.Int64";
			}
			SetSystem(@$"Your job is: 
1. Parse user intent
2. Map the intent to one of C# function provided to you
3. Return a valid JSON: 

## csharp function ##
Int32 Update(String sql, List<object>()? SqlParameters = null, string? dataSourceName = null)
## csharp function ##

variable is defined with starting and ending %, e.g. %filePath%. Do not remove %
String sql is the SQL statement that should be executed. 
String sql can be defined as variable, e.g. %sql%
Sql MAY NOT contain a variable(except table name), it MUST be injected using SqlParameters to prevent SQL injection
SqlParameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, System.DateTime, System.Int64, etc.
All integers are type of System.Int64.
%Now% variable is type of DateTime. %Now% variable should be injected as SqlParameter 
integer/int should always be System.Int64. 
{appendToSystem}
If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
You MUST provide SqlParameters if SQL has @parameter.
");

			SetAssistant(@"# examples #
""update table myTable, street=%full_street%, %zip%"" => sql: ""UPDATE myTable SET street = @full_street, zip = @zip"", SqlParameters:[{ParameterName:full_street, VariableNameOrValue:%full_street%, TypeFullName:string}, {ParameterName:zip, VariableNameOrValue:%zip%, TypeFullName:int}], Warning: Missing WHERE statement can affect rows that should not
""update tableB, %name%, %phone% where id=%id%"" => sql: ""UPDATE tableB SET name=@name, phone=@phone WHERE id=@id"", SqlParameters:[{ParameterName:name, VariableNameOrValue:%name%, TypeFullName:string}, {ParameterName:phone, VariableNameOrValue:%phone%, TypeFullName:string}, {ParameterName:id, VariableNameOrValue:%id%, TypeFullName:int}] 
""update %table% WHERE %name%, set zip=@zip => sql: ""UPDATE %table% SET zip=@zip WHERE name=@name"", SqlParameters:[{ParameterName:name, VariableNameOrValue:%name%, TypeFullName:string}, {ParameterName:zip, VariableNameOrValue%zip%, TypeFullName:int}, {VariableNameOrValue:id, VariableNameOrValue%id%, TypeFullName:int}] 
# examples #");


			var result = await BuildCustomStatementsWithWarning(goalStep, dataSource, program, functionInfo);

			return result;


		}

		private async Task<(Instruction?, IBuilderError?)> BuildCustomStatementsWithWarning(GoalStep goalStep, DataSource dataSource, Program program, FunctionAndTables functionInfo, string? errorMessage = null, int errorCount = 0)
		{
			await AppendTableInfo(dataSource, program, functionInfo.TableNames);
			(var instruction, var buildError) = await base.Build<DbGenericFunction>(goalStep);
			if (buildError != null || instruction == null)
			{
				return (null, buildError ?? new StepBuilderError($"Tried to build SQL statement for {functionInfo.FunctionName}", goalStep));
			}
			var gf = instruction.Action as DbGenericFunction;
			if (!string.IsNullOrWhiteSpace(gf.Warning))
			{
				logger.LogWarning(gf.Warning);
			}
			instruction.Properties.AddOrReplace("TableNames", functionInfo.TableNames);

			return (instruction, null);
		}

		private async Task<(Instruction?, IBuilderError?)> CreateInsert(GoalStep goalStep, Program program, FunctionAndTables functionInfo, ModuleSettings.DataSource dataSource)
		{
			string eventSourcing = (dataSource.KeepHistory) ? "You MUST modify the user command by adding id to the sql statement and parameter %id%." : "";
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "SqlParameters @id MUST be type System.Int64. VariableNameOrValue=\"auto\"";
			}
			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
Int32 Insert(String sql, List<object>()? SqlParameters = null, string? dataSourceName = null)  //returns number of rows affected
## csharp function ##

variable is defined with starting and ending %, e.g. %filePath%.
SqlParameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, System.DateTime, System.Int64, etc.
%Now% variable is type of DateTime. %Now% variable should be injected as SqlParameter 
{appendToSystem}
{eventSourcing}
If table name is a variable, keep the variable in the sql statement
Make sure sql statement matches columns provided for the table.
integer/int should always be System.Int64. 

You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
You MUST provide SqlParameters if SQL has @parameter.
");
			if (dataSource.KeepHistory)
			{
				SetAssistant(@"# examples #
""insert into users, name=%name%"" => sql: ""insert into users (id, name) values (@id, @name)"",  SqlParameters:[{ParameterName:id, VariableNameOrValue:""auto"", TypeFullName:int64}, {ParameterName:name, VariableNameOrValue:%name%, TypeFullName:string}]
""insert into tableX, %phone%, write to %rows%"" => sql: ""insert into tableX (id, phone) values (""auto"", @phone)""
""insert into %table%, %phone%, write to %rows%"" => sql: ""insert into %table% (id, phone) values (""auto"", @phone)""

ParameterInfo has the scheme: {""ParameterName"": string, ""VariableNameOrValue"": string, ""TypeFullName"": string}
        },
# examples #");
			}
			else
			{
				SetAssistant(@"# examples #
""insert into users, name=%name%"" => sql: ""insert into users (name) values (@name)""
""insert into tableX, %phone%, status='off', write to %rows%"" => sql: ""insert into tableX (phone, status) values (@phone, 'off')""
""insert into %table%, %phone%, write to %rows%"" => sql: ""insert into %table% (phone) values (@phone)""
# examples #");
			}
			await AppendTableInfo(dataSource, program, functionInfo.TableNames);

			var result = await base.Build(goalStep);
			result.Instruction?.Properties.AddOrReplace("TableNames", functionInfo.TableNames);

			return result;

		}
		private async Task<(Instruction?, IBuilderError?)> CreateInsertOrUpdate(GoalStep goalStep, Program program, FunctionAndTables functionInfo, ModuleSettings.DataSource dataSource)
		{
			string eventSourcing = (dataSource.KeepHistory) ? "You MUST modify the user command by adding id to the sql statement and parameter %id%." : "";
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "SqlParameters @id MUST be type System.Int64. VariableNameOrValue is \"auto\"";
			}

			string functionDesc = "int InsertOrUpdate(String sql, List<ParameterInfo>()? SqlParameters = null, string? dataSourceName = null) //return rows affected";
			if (functionInfo.FunctionName == "InsertOrUpdateAndSelectIdOfRow")
			{
				functionDesc = "object InsertOrUpdateAndSelectIdOfRow(String sql, List<ParameterInfo>()? SqlParameters = null, string? dataSourceName = null) //returns the primary key of the affected row";
			}

			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
{functionDesc}
## csharp function ##

variable is defined with starting and ending %, e.g. %filePath%. 
%variables% MUST be kept as is.
SqlParameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, System.DateTime, System.Int64, etc.
Do not escape string("" or ') when dealing with string for VariableNameOrValue
InsertOrUpdateAndSelectIdOfRow returns a value that should be written into %variable% defined by user.
%Now% variable is type of DateTime. %Now% variable should be injected as SqlParameter 
{appendToSystem}
{eventSourcing}
If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
You MUST provide SqlParameters if SQL has @parameter.
Use the table index information to handle ignores on conflicts
integer/int should always be System.Int64. 
");

			if (functionInfo.DatabaseType.ToLower().Contains("sqlite"))
			{
				string selectIdRow = "";
				if (functionInfo.FunctionName == "InsertOrUpdateAndSelectIdOfRow")
				{
					selectIdRow = " RETURNING id";
				}

				if (dataSource.KeepHistory)
				{
					SetAssistant($@"# examples for sqlite #
""insert or update users, name=%name%(unqiue), %type%, write to %id%"" => 
	sql: ""insert into users (id, name, type) values (@id, @name, @type) ON CONFLICT(name) DO UPDATE SET type = excluded.type {selectIdRow}""
	Keep variables as is, e.g. VariableNameOrValue=%name%, VariableNameOrValue=%type%, VariableNameOrValue=%id%
# examples #");

				}
				else
				{
					SetAssistant(@$"# examples #
""insert into users, name=%name%(unqiue), %type%, write to %id%""
	sql: ""insert into users (name, type) values (@name, @type) ON CONFLICT(name) DO UPDATE SET type = excluded.type {selectIdRow}""
	Keep variables as is, e.g. VariableNameOrValue=%name%, VariableNameOrValue=%type%, VariableNameOrValue=%id%
# examples #");
				}
			}

			await AppendTableInfo(dataSource, program, functionInfo.TableNames);

			var result = await base.Build(goalStep);
			result.Instruction?.Properties.AddOrReplace("TableNames", functionInfo.TableNames);
			

			return result;

		}
		private async Task<(Instruction?, IBuilderError?)> CreateInsertAndSelectIdOfInsertedRow(GoalStep goalStep, Program program, FunctionAndTables functionInfo, ModuleSettings.DataSource dataSource)
		{
			string eventSourcing = (dataSource.KeepHistory) ? "You MUST modify the user command by adding id to the sql statement and parameter %id%." : "";
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "SqlParameters @id MUST be type System.Int64 VariableNameOrValue is \"auto\"";
			}
			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
Object InsertAndSelectIdOfInsertedRow(String sql, List<object>()? SqlParameters = null, string? dataSourceName = null)
## csharp function ##

variable is defined with starting and ending %, e.g. %filePath%.
SqlParameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, System.DateTime, System.Int64, etc.
InsertAndSelectIdOfInsertedRow returns a value that should be written into %variable% defined by user.
%Now% variable is type of DateTime. %Now% variable should be injected as SqlParameter 
{appendToSystem}
{eventSourcing}
If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
You MUST provide SqlParameters if SQL has @parameter.
integer/int should always be System.Int64. 
");
			if (dataSource.KeepHistory)
			{
				SetAssistant(@"# examples #
""insert into users, name=%name%, write to %id%"" => sql: ""insert into users (id, name) values (@id, @name)""
""insert into tableX, %phone%, write to %id%"" => sql: ""insert into tableX (id, phone) values (@id, @phone)""
# examples #");
			}
			else
			{
				SetAssistant(@$"# examples #
""insert into users, name=%name%, write to %id%"" => sql: ""insert into users (name) values (@name);"", select inserted id ({functionInfo.DatabaseType})
""insert into tableX, %phone%, write to %id%"" => sql: ""insert into tableX (phone) values (@phone);"", select inserted id ({functionInfo.DatabaseType})
# examples #");
			}

			await AppendTableInfo(dataSource, program, functionInfo.TableNames);

			var result = await base.Build(goalStep);
			result.Instruction?.Properties.AddOrReplace("TableNames", functionInfo.TableNames);

			return result;
		}

		private async Task<IError?> AppendTableInfo(DataSource dataSource, Program program, string[]? tableNames)
		{
			if (tableNames == null) return null;

			foreach (var item in tableNames)
			{
				string tableName = item;
				if (VariableHelper.IsVariable(tableName))
				{
					var obj = memoryStack.GetObjectValue(tableName, false);
					if (!obj.Initiated) continue;

					if (obj.Value != null)
					{
						tableName = obj.Value.ToString();
					}
				}
				if (tableName == null || tableName.StartsWith("pragma_table_info")) continue;

				string selectColumns = await dbSettings.FormatSelectColumnsStatement(dataSource, tableName);
				var columnInfo = await GetColumnInfo(selectColumns, program, dataSource);
				if (columnInfo != null)
				{
					AppendToSystemCommand($"Table name is: {tableName}. Fix sql if it includes type.");
					AppendToAssistantCommand($"### {tableName} columns ###\n{JsonConvert.SerializeObject(columnInfo)}\n### {tableName} columns ###");

					if (typeof(SqliteConnection).FullName == dataSource.TypeFullName)
					{
						string indexInformation = string.Empty;
						var indexes = await program.Select($"SELECT name, [unique], [partial] FROM pragma_index_list('{tableName}') WHERE origin = 'u';");
						if (indexes.Error != null) return indexes.Error;

						foreach (dynamic index in indexes.Table!)
						{
							var columns = await program.Select($"SELECT group_concat(name) as columns FROM pragma_index_info('{index.name}')");
							if (columns.Table.Count > 0)
							{
								dynamic row = columns.Table[0];
								indexInformation += @$"- index name:{index.name} - is unique:{index.unique} - is partial:{index.partial} - columns:{row.columns}\n";
							}
						}

						if (indexInformation != string.Empty)
						{
							indexInformation = $"### Index information for {tableName} ###\n{indexInformation}\n### Index information for {tableName} ###";
							AppendToAssistantCommand(indexInformation);
						}
					}
				}
				else
				{
					logger.LogWarning($@"Could not find information about table '{tableName}'. 
I will not be able to validate the sql. To enable validation run the command: plang run Setup");
					//throw new BuilderException("You need to run: 'plang run Setup.goal' to create or modify your database before you continue with your build");
				}
			}

			return null;


		}
		
		public async Task<object?> GetColumnInfo(string selectColumns, Program program, DataSource dataSource)
		{
			var result = await program.Select(selectColumns, dataSourceName: dataSource.Name);
			var columnInfo = result.Table;
			if (columnInfo != null && ((dynamic)columnInfo).Count > 0)
			{
				return columnInfo;
			}

			return null;
		}
		*/
	}
}

