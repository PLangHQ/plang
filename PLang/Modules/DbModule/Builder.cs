using AngleSharp.Html.Dom;
using LightInject;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using OpenAI.Assistants;
using Org.BouncyCastle.Crypto.Prng;
using PLang.Attributes;
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
using System.Linq;
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
			this.dbSettings.IsBuilder = true;
		}

		public record DbGenericFunction(string Reasoning, string Name,
			List<Parameter>? Parameters = null, List<ReturnValue>? ReturnValues = null,
			List<string>? TableNames = null, Dictionary<string, string>? AffectedColumns = null, string? Warning = null) : GenericFunction(Reasoning, Name, Parameters, ReturnValues);



		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep goalStep, IBuilderError? previousBuildError = null)
		{
			(var methodsAndTables, var error) = await GetMethodsAndTables(goalStep, previousBuildError);
			if (error != null) return (null, error);

			var dataSource = methodsAndTables!.DataSource;

			if (dataSource == null && goalStep.Goal.IsSetup && goalStep.Goal.GoalName.Equals("Setup"))
			{
				dataSource = (await dbSettings.GetAllDataSources()).FirstOrDefault(p => p.IsDefault);
			}
			var dataSources = goalStep.Goal.GetVariable<List<DataSource>>();
			if (dataSource != null && dataSources != null)
			{
				var activeDs = dataSources.FirstOrDefault(p => p.Name == dataSource.Name);
				if (activeDs != null)
				{
					dataSource = dataSource with { NameInStep = activeDs.NameInStep };
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

				// todo: this will fail with different type of dbs

			}
			if (methodsAndTables.DataSourceWithTableInfos.Count > 0)
			{
				system += @$"
<dataSourceAndTableInfos>
{JsonConvert.SerializeObject(methodsAndTables.DataSourceWithTableInfos)}
<dataSourceAndTableInfos>";
			}
			string dataSourceName = (dataSource != null) ? $"(\"{dataSource.NameInStep ?? dataSource.Name}\")" : "";

			system += @$"

Additional json Response explaination:
- DataSource: Datasource to use {dataSourceName}
- TableNames: List of tables defined in sql statement
- AffectedColumns: Dictionary of affected columns with type(primary_key|select|insert|update|delete|create|alter|index|drop|where|order|other), e.g. select name from users where id=1 => 'name':'select', 'id':'where'

Rules:
- You MUST generate valid sql statement for {sqlType}
- Number(int => long) MUST be type System.Int64 unless defined by user.
- string in user sql statement should be replaced with @ parameter in sql statement and added as parameters in ParamterInfo but as strings. Strings are as is in the parameter list.";

			if (methodsAndTables.ContainsMethod("insert"))
			{
				system += "\n- when user defines to write into come sort of %id%, then choose the method which select id of row back";
				if (dataSource.KeepHistory)
				{
					system += "\n- For any type of Insert/Upsert statement, you MUST include ParameterInfo(\"@id\", \"auto\", \"System.Int64\") in your response";
					system += "\n- Make sure to include @id in sql statement and sqlParameters. Missing @id will cause invalid result.";
					system += "\n- Plang will handle retrieving the inserted id, only create the insert statement, nothing regarding retrieving the last id inserted";
					system += "\n- When user is doing upsert(on conflict update), make sure to return the id of the table on the update sql statement";
				}
			}
			else
			{
				if (!methodsAndTables.ContainsMethod("CreateTable"))
				{
					system += @"
- When generating SQL statements, only include columns that are explicitly specified by the user in their intent or provided variable structure. 
- Do not assume or include all columns from the table schema unless the user requests it. ";
				}
			}
			if (methodsAndTables.ContainsMethod("CreateTable"))
			{
				system += $"\n- On CreateTable, include primary key, id, not null, {autoIncremental} when not defined by user. It must be long";
			}
			if (methodsAndTables.ContainsMethod("update") || methodsAndTables.ContainsMethod("delete"))
			{
				system += $"\n- When sql statement is missing WHERE statement, give a Warning";
			}
			if (methodsAndTables.ContainsMethod("select"))
			{
				system += @$"
- You MUST generate ReturnValues for the select statement, see <select_example>
- select statement that retrieves columns and does not write the result into a variable, then each column selected MUST be in ReturnValues where the name of the column is the name of the variable. e.g. `select id from products` => ReturnValues: 'id'
- user might define his variable in the select statement, e.g. `select id as %articleId% from article where id=%id%`, the intent is to write into %articleId%, make sure to adjust the sql to be valid
- when user defines to write the result into a %variable%, then ReturnValues is only 1 item.
- Returning 1 mean user want only one row to be returned (limit 1)

<select_example>
`select id from users where id=%id%` => ReturnValues => VariableName:  id
`select price as selectedPrice from products where id=%id%` => ReturnValues => VariableName: selectedPrice
`select postcode as %zip% from address where id=%id%` => ReturnValues => VariableName: zip
`select * from address where id=%id%, write to %address%` => ReturnValues => VariableName: address
`select name, address, zip from users where id=%id%, write to %user%` => ReturnValues => VariableName: user

<select_example>
";
			}
			if (methodsAndTables.DataSourceWithTableInfos.Count > 0)
			{
				system += $@"
- Definition for List<ParameterInfo> => ParameterInfo(string ParameterName, object? VariableNameOrValue, string TypeFullName)
- use <dataSourceAndTableInfos> to build a valid sql for {sqlType}
";
			}
			if (dataSource != null)
			{
				system += $@"
- The dataSourceName for all database operations is: ""{dataSource.NameInStep ?? dataSource.Name}"". The dataSourceName is provided by external and MUST NOT be modified. Any variable in datasource name will be provided at later time.
- Id columns: {autoIncremental}

The dataSourceName is provided by external and MUST NOT be changed in any way. Any variable in dataSourceName will be provided at later time and MUST stay as is.
";
			}

			if (dataSources != null && dataSources.Count > 1)
			{
				List<string> aliases = ["main"];
				aliases.AddRange(dataSources.Skip(1).Select(p =>
				{
					if (p.Name.Contains("/"))
					{
						return p.Name.Substring(0, p.Name.IndexOf('/'));
					}
					return p.Name;
				}));

				string strDataSources = string.Join("\", \"", aliases);
				system += $@"
These databases use ATTACH in sqlite: ""{strDataSources}"". 
That means sql statements MUST be prefixed, e.g. `select * from data.orders`, keep the prefix in sql statement.
<prefix_examples>";
				foreach (var alias in aliases)
				{
					system += $@"
`select * from {alias}.products` => sql = ""select * from {alias}.products""
`update {alias}.variants set title=%title%` => sql = ""update {alias}.variants set title=@title""
`delete {alias}.users where %id%` => sql = ""delete {alias}.users where id=@id""
";

				}
				system += $@"
<prefix_example>
";

			}



			AppendToSystemCommand(system);
			var buildResult = await base.BuildWithClassDescription<DbGenericFunction>(goalStep, methodsAndTables.ClassDescription, previousBuildError);
			if (buildResult.BuilderError != null) return buildResult;

			var gf = buildResult.Instruction.Function as DbGenericFunction;

			if (gf == null || gf.Name == "N/A")
			{
				return (null, new StepBuilderError("Could not mappe step to a function", goalStep, Retry: false));
			}

			var dataSourceNameParam = gf.GetParameter<string>("dataSourceName");
			if (dataSourceNameParam?.Contains("%") == true)
			{
				var dynamicDataSourceName = ConvertVariableNamesInDataSourceName(variableHelper, dataSourceNameParam);
				var dsResult = await dbSettings.GetDataSource(dynamicDataSourceName);
				if (dsResult.Error != null) return (null, new BuilderError(dsResult.Error));
				/*
				int idx = gf.Parameters.FindIndex(p => p.Name == "dataSourceName");
				gf.Parameters[idx] = gf.Parameters[idx] with { Value = dynamicDataSourceName };
				*/
				//buildResult.Instruction = buildResult.Instruction with { Function = gf };
			}
			else if (string.IsNullOrEmpty(dataSourceNameParam))
			{
				var hasDataSourceName = methodsAndTables.ClassDescription.Methods[0].Parameters.FirstOrDefault(p => p.Name == "dataSourceName");
				if (hasDataSourceName == null)
				{
					return buildResult;
				}

				if (dataSource == null)
				{
					return (null, new StepBuilderError("Missing dataSourceName. Please include it", goalStep));
				}


				int idx = gf.Parameters.FindIndex(p => p.Name == "dataSourceName");
				if (idx == -1)
				{
					return (null, new StepBuilderError("Missing dataSourceName. Please include it", goalStep));
				}
				gf.Parameters[idx] = gf.Parameters[idx] with { Value = dataSource.NameInStep ?? dataSource.Name };

				buildResult.Instruction = buildResult.Instruction with { Function = gf };
			}
			/*
			var parameters = gf.GetParameter<List<ParameterInfo>>("sqlParameters");

			if (parameters != null && parameters.Count > 0)
			{
				return await ValidateSqlAndParameters(goalStep, insertAppend, buildResult.Instruction);
			}
			*/

			return buildResult;
		}

		[Description("Methods is dictionary(key:value). Key is method name, Value is confidents level(low|medium|high). DataSourceName can contain variables, e.g. /user/%user.id%")]
		public record MethodsAndTables(string Reasoning, Dictionary<string, string> Methods, List<string> TableNames, string? DataSourceName = null)
		{
			[LlmIgnore]
			public DataSource? DataSource { get; set; }

			[LlmIgnore]
			public ClassDescription ClassDescription { get; set; }

			[LlmIgnore]
			public Dictionary<string, List<TableInfo>> DataSourceWithTableInfos { get; set; } = new(StringComparer.OrdinalIgnoreCase);

			public bool ContainsMethod(string name)
			{
				return Methods.FirstOrDefault(p => p.Key.Contains(name, StringComparison.OrdinalIgnoreCase)).Key != null;
			}
		};

		public async Task<(MethodsAndTables?, IBuilderError?)> GetMethodsAndTables(GoalStep step, IBuilderError? previousBuildError = null)
		{
			string system = @"Determine which <methods> fit best with the user intent for this DbModule. 
This is pre-processing to choose selection of possible <methods>, so you can suggest multiple methods. 
For Select, Insert, Update, Delete, CreateTable and Execute methods, list out the table names that are affected
When a direct method is not provided for the user intentented sql statement, use Execute or ExecuteDynamicSql

## Scheme explained: 
- Reasoning: explain why you chose method(s)
- Methods: dictionary(key:value). Key is method name, Value is confidents level(low|medium|high)
- TableNames: List of tables that are used in user sql pseudo code
";

			var programType = typeHelper.GetRuntimeType(step.ModuleType);
			if (programType == null) return (null, new StepBuilderError($"Could not load type {step.ModuleType}", step));

			var classDescriptionHelper = new ClassDescriptionHelper();
			var (classDescription, error) = classDescriptionHelper.GetClassDescription(programType);
			if (error != null) return (null, error);

			List<object> methodList = new();
			foreach (var method in classDescription.Methods)
			{
				methodList.Add(new
				{
					Method = method.MethodName,
					Description = method.Description,
				});
			}

			system += $"\n<methods>\n{JsonConvert.SerializeObject(methodList)}\n</methods>";

			(var methodsAndTables, error) = await LlmRequest<MethodsAndTables>(system, step);
			if (error != null) return (null, error);

			// lets construction a new class description with only data that is needed
			var classDescResult = GetNewClassDescription(step, classDescription, methodsAndTables);
			if (classDescResult.Error != null) return (null, classDescResult.Error);

			bool methodHasDataSourceName = classDescResult.HasDataSourceName;
			methodsAndTables.ClassDescription = classDescResult.ClassDescription;

			var stepDataSource = step.GetVariable<DataSource>() ?? step.Goal.GetVariable<DataSource>();
			if (stepDataSource == null && step.Goal.IsSetup && step.Goal.GoalName.Equals("setup", StringComparison.OrdinalIgnoreCase))
			{
				var dataSourceResult = await dbSettings.GetDataSource("data");
				if (dataSourceResult.Error != null) return (null, new BuilderError(dataSourceResult.Error));
				if (dataSourceResult.DataSource == null)
				{
					var createDataSourceResult = await dbSettings.CreateDataSource("data", "sqlite", true, true);
					if (createDataSourceResult.Error != null) return (null, new BuilderError(createDataSourceResult.Error));

					stepDataSource = createDataSourceResult.DataSource;
				}
			}

			// todo: hack with execute sql file
			if (methodsAndTables.ContainsMethod("ExecuteSqlFile")
				|| methodsAndTables.TableNames[0] == "ExecuteDynamicSql")
			{
				return (methodsAndTables, null);
			}



			if (!methodHasDataSourceName)
			{
				if (stepDataSource != null) methodsAndTables.DataSource = stepDataSource;
				return (methodsAndTables, null);
			}



			// tables names are only needed for select, update, delete, insert, etc.
			if (methodsAndTables.TableNames == null || methodsAndTables.TableNames.Count == 0)
			{
				return (null, new StepBuilderError("You must provide TableNames", step));
			}

			var program = GetProgram(step);
			if (!string.IsNullOrEmpty(methodsAndTables.DataSourceName))
			{
				(var dataSource, var dsError) = await dbSettings.GetDataSource(methodsAndTables.DataSourceName);
				if (dsError != null) return (null, new BuilderError(dsError));

				(var tableInfos, dsError) = await program.GetDatabaseStructure(dataSource, methodsAndTables.TableNames);
				if (dsError != null && dsError.StatusCode != 404) return (null, new BuilderError(dsError));


				methodsAndTables.DataSource = dataSource;
				methodsAndTables.DataSourceWithTableInfos.Add("", tableInfos);
				return (methodsAndTables, null);
			}


			var dataSources = await dbSettings.GetAllDataSources();
			if (dataSources == null || dataSources.Count == 0)
			{
				return (null, new StepBuilderError("Data source has not been created.", step, Retry: false,
						FixSuggestion: @"Create a Setup.goal file and add steps creating tables, e.g. `Setup
- create table users, columns: 
	name(string, not null), created(datetime, default now)"));
			}

			HashSet<string> stepDataSourcesNames = new();
			if (stepDataSource != null)
			{
				stepDataSourcesNames.Add(stepDataSource.Name);
				dataSources = dataSources.OrderBy(ds => ds.Name != stepDataSource.Name).ToList();
			}

			DataSource? selectedDataSource = null;

			List<string> preferedNames = new();
			var prefixedTables = methodsAndTables.TableNames.Where(p => p.Contains("."));
			if (prefixedTables.Count() > 0)
			{
				foreach (var prefix in prefixedTables)
				{
					string prefixName = prefix.Substring(0, prefix.IndexOf("."));
					if (prefixName == "main")
					{
						var goalDataSources = step.Goal.GetVariable<List<DataSource>>();
						if (goalDataSources != null && goalDataSources.Count > 0)
						{
							preferedNames.Add(goalDataSources[0].Name);
						}
					}
					else
					{
						preferedNames.Add(prefixName);
					}
				}
			}
			IError? rError;

			foreach (var dataSource in dataSources)
			{

				(var tableInfos, rError) = await program.GetDatabaseStructure(dataSource, methodsAndTables.TableNames);
				if (rError != null && rError.StatusCode != 404) return (null, new BuilderError(rError));

				if (tableInfos == null || tableInfos.Count == 0) continue;

				if (selectedDataSource != null)
				{
					if (preferedNames.Count > 0 && preferedNames.Any(p => dataSource.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
					{
						selectedDataSource = dataSource;

					}

					if (preferedNames.Count == 0)
					{

						if (stepDataSourcesNames == null || stepDataSourcesNames.Contains(dataSource.Name))
						{
							return (null, new StepBuilderError($"Multiple datasource detected with table(s) {string.Join(",", methodsAndTables.TableNames)}. You must defined datasource in statement", step, Retry: false));
						}
					}

				}
				else
				{
					selectedDataSource = dataSource;
					if (methodsAndTables.DataSourceWithTableInfos.TryGetValue(dataSource.Name, out var tableInfos1))
					{
						tableInfos1.AddRange(tableInfos);
					}
					else
					{
						methodsAndTables.DataSourceWithTableInfos.Add(dataSource.Name, tableInfos);
					}
				}

			}



			if (selectedDataSource == null)
			{
				return (null, new StepBuilderError($"Table {string.Join(",", methodsAndTables.TableNames.Select(p => p))} couldn't be found in any data source", step, Retry: false,
					FixSuggestion: "Either create the table in you Setup file or remove the Step"
					));

			}

			methodsAndTables.DataSource = selectedDataSource;
			return (methodsAndTables, null);
		}

		private (ClassDescription? ClassDescription, bool HasDataSourceName, IBuilderError? Error) GetNewClassDescription(GoalStep step, ClassDescription classDescription, MethodsAndTables? methodsAndTables)
		{
			if (methodsAndTables == null) return (null, false, new StepBuilderError($"No methods to choose from", step));

			bool methodHasDataSourceName = false;
			var newClassDescription = new ClassDescription();
			foreach (var methodSelection in methodsAndTables.Methods)
			{
				var method = methodSelection.Key;

				var methodInfo = classDescription.Methods.FirstOrDefault(p => p.MethodName.Equals(method));
				if (methodInfo == null) return (null, false, new StepBuilderError($"Method '{method}' does not exist in class", step));

				var so = classDescription.SupportingObjects.FirstOrDefault(p => p.MethodNames.Contains(method));
				if (so != null && newClassDescription.SupportingObjects.FirstOrDefault(p => p == so) == null)
				{
					newClassDescription.SupportingObjects.Add(so);
				}

				newClassDescription.Methods.Add(methodInfo);

				if (methodInfo.Parameters == null) continue;

				var dataSourceName = methodInfo.Parameters.FirstOrDefault(p => p.Name.Equals("dataSourceName"));
				if (dataSourceName == null) continue;
				methodHasDataSourceName = true;
			}

			return (newClassDescription, methodHasDataSourceName, null);
		}

		private (string? DataSourceName, IBuilderError? Error) GetDataSourceName(GoalStep step, DbGenericFunction gf)
		{
			var dataSourceName = GenericFunctionHelper.GetParameterValueAsString(gf, "dataSourceName");

			if (string.IsNullOrEmpty(dataSourceName))
			{
				var dataSource = step.GetVariable<DataSource>();
				if (dataSource == null)
				{
					return (null, new StepBuilderError("Could not find datasource to use", step));
				}
				dataSourceName = dataSource.Name;
			}

			var convertedDataSourceName = ConvertVariableNamesInDataSourceName(variableHelper, dataSourceName);


			return (convertedDataSourceName, null);
		}

		public async Task<IBuilderError?> BuilderExecuteSqlFile(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var dataSourceResult = GetDataSourceName(step, gf);
			if (dataSourceResult.Error != null) return dataSourceResult.Error;


			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceResult.DataSourceName);
			if (error != null) return new BuilderError(error);

			var fileName = GenericFunctionHelper.GetParameterValueAsString(gf, "fileName");

			var file = programFactory.GetProgram<Modules.FileModule.Program>(step);
			var readResult = await file.ReadTextFile(fileName);
			if (readResult.Error != null) return new BuilderError(readResult.Error);

			var tableAllowList = GenericFunctionHelper.GetParameterValueAsList(gf, "tableAllowList");
			using var program = GetProgram(step);
			var result = await program.Execute(dataSource, readResult.Content.ToString(), tableAllowList);
			if (result.Error != null)
			{
				logger.LogWarning("  - ❌ Sql statement got error - " + result.Error.Message);
				return null;
			}
			logger.LogInformation($"  - ✅ Sql statement validated - {readResult.Content.ToString().MaxLength(30, "...")} - {step.Goal.RelativeGoalPath}:{step.LineNumber}");
			return null;

		}
		public async Task<IBuilderError?> BuilderExecute(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var dataSourceResult = GetDataSourceName(step, gf);
			if (dataSourceResult.Error != null) return dataSourceResult.Error;

			(var dataSource, var error) = await dbSettings.GetDataSource(dataSourceResult.DataSourceName);
			if (error != null) return new BuilderError(error);

			var sql = GenericFunctionHelper.GetParameterValueAsString(gf, "sql");
			if (VariableHelper.IsVariable(sql))
			{
				return new StepBuilderError("Do not use the Execute method when the sql is a %variable%. Use ExecuteDynamicSql method.", step);
			}

			var tableAllowList = GenericFunctionHelper.GetParameterValueAsList(gf, "tableAllowList");
			using var program = GetProgram(step);
			var result = await program.Execute(dataSource, sql, tableAllowList);
			if (result.Error != null)
			{
				return new BuilderError(result.Error) { Retry = false };
			}
			logger.LogInformation($"  - ✅ Sql statement validated - {sql.MaxLength(30, "...")} - {step.Goal.RelativeGoalPath}:{step.LineNumber}");
			return null;
		}

		public async Task<(Instruction, IBuilderError?)> BuilderValidate(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var dataSourceNameInstruction = GenericFunctionHelper.GetParameterValueAsString(gf, "dataSourceName", "-1");
			if (dataSourceNameInstruction == "-1") return (instruction, null);


			if (string.IsNullOrEmpty(dataSourceNameInstruction))
			{
				return (instruction, new StepBuilderError("Missing DataSource from instruction file. Not legal pr file", step,
					Key: "InvalidInstructionFile", FixSuggestion: $"Try rebuilding the .pr file: {step.RelativePrPath}"));
			}
			var dataSourceName = ConvertVariableNamesInDataSourceName(variableHelper, dataSourceNameInstruction);

			List<string> MethodsToValidate = ["Select", "SelectOneRow", "Update", "InsertOrUpdate", "InsertOrUpdateAndSelectIdOfRow", "Insert", "InsertAndSelectIdOfInsertedRow", "Delete"];

			(var dataSource, var dataSourceError) = await dbSettings.GetDataSource(dataSourceName);
			if (dataSourceError != null) return (instruction, new BuilderError(dataSourceError));

			var sql = GenericFunctionHelper.GetParameterValueAsString(gf, "sql");
			if (string.IsNullOrEmpty(sql)) return (instruction, null);

			if (!MethodsToValidate.Contains(gf.Name)) return (instruction, null);

			if (gf.Name.Contains("select", StringComparison.OrdinalIgnoreCase) && (gf.ReturnValues == null || gf.ReturnValues.Count == 0))
			{
				if (gf.Name.Contains("insert", StringComparison.OrdinalIgnoreCase))
				{
					return (instruction, new StepBuilderError("When selecting id back after insert/update statement you MUST have ReturnValues. According to user intent, is it enough to just do insert/update without selecting id?", step));
				}
				return (instruction, new StepBuilderError("Select statement MUST have ReturnValues", step));
			}

			if (gf.TableNames?.Any(p => p.Contains(".")) == true)
			{
				// when database is attached, the table name are prefixed. 
				// to validate, we need remove the prefix.
				foreach (var tableName in gf.TableNames)
				{
					if (tableName.Contains("."))
					{
						var newName = tableName.Substring(tableName.IndexOf(".") + 1);
						sql = sql.Replace(tableName, newName);
					}
				}
			}


			(var validSql, dataSourceName, var error) = await IsValidSql(sql, dataSource, step);
			if (error == null)
			{
				var updatedParams = gf.Parameters
					.Select(p => p.Name == "dataSourceName" ? p with { Value = dataSourceNameInstruction } : p)
					.ToList();

				gf = gf with { Parameters = updatedParams };
				instruction = instruction with { Function = gf };

				logger.LogInformation($"  - ✅ Sql statement validated - {sql.MaxLength(30, "...")} - {step.Goal.RelativeGoalPath}:{step.LineNumber}");

				return (instruction, null);
			}


			using var program = GetProgram(step);
			var tableStructure = await program.GetDatabaseStructure(dataSource, gf.TableNames);

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

		public async Task<IBuilderError?> BuilderBeginTransaction(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var dataSourceNames = gf.GetParameter<List<string>>("dataSourceNames");
			if (dataSourceNames == null || dataSourceNames.Count == 0) return null;

			List<DataSource> dataSources = new();
			foreach (var dataSourceName in dataSourceNames)
			{
				var convertedDataSourceName = ConvertVariableNamesInDataSourceName(variableHelper, dataSourceName);
				(var dataSource, var error) = await dbSettings.GetDataSource(convertedDataSourceName);
				if (error != null) return new StepBuilderError(error, step);

				dataSource = dataSource with { NameInStep = dataSourceName };
				dataSources.Add(dataSource);
			}
			step.Goal.AddVariable(dataSources);

			return null;
		}
		public async Task<IBuilderError?> BuilderCreateTable(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			if (!step.Goal.IsSetup) return new StepBuilderError("Create table can only be in a setup file", step,
				FixSuggestion: @"Move the create statment into a setup file");

			var sql = GenericFunctionHelper.GetParameterValueAsString(gf, "sql");
			if (string.IsNullOrEmpty(sql)) return new StepBuilderError("sql is empty, cannot create table", step);

			var convertedDataSourceName = ConvertVariableNamesInDataSourceName(variableHelper, step.Goal.DataSourceName ?? "data");

			(var dataSource, var error) = await dbSettings.GetDataSource(convertedDataSourceName);
			if (error != null && error.Key == "DataSourceNotFound")
			{
				var dataSources = await dbSettings.GetAllDataSources();
				if (dataSources.Count == 0)
				{
					(dataSource, error) = await dbSettings.CreateDataSource("data", setAsDefaultForApp: true, keepHistoryEventSourcing: true);
				}
			}

			if (error != null) return new StepBuilderError(error, step);

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
			var nameInStep = GenericFunctionHelper.GetParameterValueAsString(gf, "name");
			if (nameInStep == null) return new InstructionBuilderError("Could not find 'name' property in instructions", step, step.Instruction);

			var name = ConvertVariableNamesInDataSourceName(variableHelper, nameInStep);

			var result = await dbSettings.GetDataSource(name);
			if (result.Error != null) return new StepBuilderError(result.Error, step);

			result.DataSource.NameInStep = nameInStep;

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
			var dataSourceName = gf.GetParameter<string>("dataSourceName");
			if (string.IsNullOrEmpty(dataSourceName))
			{
				return (instruction, new StepBuilderError("dataSourceName is not provided. Please provide dataSourceName", step));
			}

			var dsName = ConvertVariableNamesInDataSourceName(variableHelper, dataSourceName);

			var dataSourceResult = await dbSettings.GetDataSource(dsName);
			if (dataSourceResult.Error != null) return (instruction, new StepBuilderError(dataSourceResult.Error, step));

			if (dataSourceResult.DataSource!.KeepHistory == false) return (instruction, null);


			var parameterInfos = gf.GetParameter<List<ParameterInfo>>("sqlParameters");
			if (parameterInfos == null)
			{
				return (instruction, new StepBuilderError("No parameters included. It needs at least to have @id", step));
			}
			var sql = gf.GetParameter<string>("sql");

			var hasId = parameterInfos.FirstOrDefault(p => p.ParameterName == "@id") != null && sql.Contains("@id");
			if (hasId || parameterInfos.Count == 0) return (instruction, null);

			return (instruction, new StepBuilderError($"No @id provided in either sqlParameters or sql statement. @id MUST be provided in both. This is required for this datasource: {dataSourceResult.DataSource!.Name}", step));


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

			var dataSourceNameInStep = GenericFunctionHelper.GetParameterValueAsString(gf, "name");
			if (string.IsNullOrEmpty(dataSourceNameInStep))
			{
				return (instruction, new StepBuilderError("Name for the data source is missing. Please define it.", step, FixSuggestion: $"Example: \"- Create sqlite data source 'myDatabase'\""));
			}

			var dataSourceName = ConvertVariableNamesInDataSourceName(variableHelper, dataSourceNameInStep);

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

			gf = gf with { Parameters = parameters };
			instruction = instruction with { Function = gf };

			var (datasource, error) = await dbSettings.CreateOrUpdateDataSource(dataSourceName, dbTypeParam, setAsDefaultForApp.Value, keepHistoryEventSourcing.Value);
			if (error != null) return (instruction, new StepBuilderError(error, step, false));

			step.Goal.DataSourceName = dataSourceName;

			datasource.NameInStep = dataSourceNameInStep;

			step.RunOnce = GoalHelper.RunOnce(step.Goal);
			step.Goal.AddVariable(datasource);

			return (instruction, null);

		}

		public bool HasNoSqlValidation()
		{
			var obj = AppContext.GetData(ReservedKeywords.ParametersAtAppStart);
			if (obj != null && obj is string[] args)
			{
				if (args.Any(p => p.Equals("--nosql"))) return true;
			}
			return false;
		}

		private async Task<(bool IsValid, string DataSourceName, IBuilderError? Error)> IsValidSql(string sql, DataSource dataSource, GoalStep step)
		{
			

			var anchors = context.GetOrDefault<Dictionary<string, IDbConnection>>("AnchorMemoryDb", new(StringComparer.OrdinalIgnoreCase)) ?? new(StringComparer.OrdinalIgnoreCase);
			if (!anchors.ContainsKey(dataSource.Name))
			{
				if (!anchors.ContainsKey(dataSource.Name))
				{
					return (false, dataSource.Name, new StepBuilderError($"Data source name '{dataSource.Name}' does not exists.", step,
					 FixSuggestion: $@"Choose datasource name from one of there: {string.Join(", ", anchors.Select(p => p.Key))}"));
				}
			}


			var variables = variableHelper.GetVariables(sql);
			foreach (var variable in variables)
			{
				sql = sql.Replace(variable.PathAsVariable, "?");
			}


			var anchor = anchors
				.FirstOrDefault(kvp => kvp.Key.Equals(dataSource.Name, StringComparison.OrdinalIgnoreCase));
			if (anchor.Key == null)
			{
				return (false, dataSource.Name, new StepBuilderError($"Could not find data source {dataSource.Name} to validate sql", step));
			}

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

				return (false, dataSource.Name, new StepBuilderError(errorInfo, Step: step, Retry: false));
			}

		}











		/*
		private async Task<(Instruction? Instruction, IBuilderError? Error)> ValidateSqlAndParameters(GoalStep goalStep, string insertAppend, Instruction instruction)
		{
			var gf = instruction.Function as DbGenericFunction;
			if (gf.TableNames == null || gf.TableNames.Count == 0)
			{
				return (null, new StepBuilderError("You must provide TableNames", goalStep));
			}

			var dataSourceName = ConvertVariableNamesInDataSourceName(variableHelper, gf.DataSource);
			using var program = GetProgram(GoalStep);
			var dbStructure = await program.GetDatabaseStructure(dataSourceName, gf.TableNames);
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
		*/
		/*
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
		}*/




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

