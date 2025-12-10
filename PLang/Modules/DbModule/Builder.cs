using AngleSharp.Html.Dom;
using IdGen;
using LightInject;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
using System.Data.Common;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using static PLang.Modules.DbModule.Builder;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Modules.DbModule.Program;
using static PLang.Modules.UiModule.Program;
using Instruction = PLang.Building.Model.Instruction;

namespace PLang.Modules.DbModule
{
	public class Builder : BaseBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly IDbServiceFactory dbFactory;
		private readonly ISettings settings;
		private readonly PLangAppContext appContext;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly ITypeHelper typeHelper;
		private readonly ILogger logger;
		private readonly VariableHelper variableHelper;
		private ModuleSettings dbSettings;
		private readonly IGoalParser goalParser;
		private readonly ProgramFactory programFactory;

		public Builder(IPLangFileSystem fileSystem, IDbServiceFactory dbFactory, ISettings settings, PLangAppContext appContext,
			ILlmServiceFactory llmServiceFactory, ITypeHelper typeHelper, ILogger logger,
			VariableHelper variableHelper, ModuleSettings dbSettings, IGoalParser goalParser, ProgramFactory programFactory) : base()
		{
			this.fileSystem = fileSystem;
			this.dbFactory = dbFactory;
			this.settings = settings;
			this.appContext = appContext;
			this.llmServiceFactory = llmServiceFactory;
			this.typeHelper = typeHelper;
			this.logger = logger;
			this.variableHelper = variableHelper;
			this.dbSettings = dbSettings;
			this.goalParser = goalParser;
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
				goalStep.Goal.DataSourceName = dataSource.Name;
			}
			/*
			List<DataSource> dataSources = new();
			if (methodsAndTables.DataSourceWithTableInfos.Count > 0)
			{
				foreach (var dsName in methodsAndTables.DataSourceWithTableInfos.Keys)
				{
					if (!string.IsNullOrEmpty(dsName.Name))
					{
						(var ds, var error2) = await dbSettings.GetDataSource(dsName.Name, goalStep, false);
						if (error2 != null) return (null, new BuilderError(error2));
						if (dataSource != null && dataSource.NameInStep == null && ds?.Name == dataSource.Name)
						{
							dataSource = dataSource with { NameInStep = ds.NameInStep };
						}
						dataSources.Add(ds);
					}
				}
			}
			*/

			if (dataSource == null)
			{
				dataSource = context.DataSource;

			}

			string sqlType = "sqlite";
			string system = "";
			if (dataSource != null)
			{
				sqlType = dataSource.TypeFullName;
			}

			if (methodsAndTables.Tables.Count > 0)
			{
				system += @$"
<dataSourceAndTableInfos>";
				foreach (var table in methodsAndTables.Tables)
				{
					if (table.DataSource == null) table.DataSource = dataSource;

					system += $@"
DataSource name: {table.DataSource.NameInStep}
Table name: {table.Name}
Columns: {JsonConvert.SerializeObject(table.Columns)}
----
";
				}
				system += "</dataSourceAndTableInfos>";
			}

			system += @$"

Additional json Response explaination:
- Datasource(s) to available: {dataSource.NameInStep}
- TableNames: List of tables defined in sql statement
- AffectedColumns: Dictionary of affected columns with type(primary_key|select|insert|update|delete|create|alter|index|drop|where|order|other), e.g. select name from users where id=1 => 'name':'select', 'id':'where'
- for `IN (@parameter)` statements => The parameter type should be System.Array

Rules:
- You MUST generate valid sql statement for {sqlType}
- Number(int => long) MUST be type System.Int64 unless defined by user.
- Keep ParameterInfo TypeFullName as sql type, e.g. column that is date/time map it to appropriate c# type, e.g. System.DateTime
- string in user sql statement should be replaced with @ parameter in sql statement and added as parameters in ParamterInfo but as strings. Strings are as is in the parameter list.";

			system += AppendSystemDependingOnMethod(methodsAndTables, dataSource);
			
			if (methodsAndTables.Tables.Count > 0)
			{
				system += $@"
- Definition for List<ParameterInfo> => ParameterInfo(string ParameterName, object? VariableNameOrValue, string TypeFullName)
- use <dataSourceAndTableInfos> to build a valid sql for {sqlType}
";

			}
			if (dataSource != null)
			{
				system += $@"
- The main dataSourceName for database operations is: ""{dataSource.NameInStep}"". The dataSourceName is provided by external and MUST NOT be modified. Any variable in datasource name will be provided at later time.

The dataSourceName is provided by external and MUST NOT be changed in any way. Any variable in dataSourceName will be provided at later time and MUST stay as is.
";

				if (dataSource.AttachedDbs.Count > 0)
				{
					system += @"
- Make sure to sort the dataSourceNames in your response so that the table marked as main comes first. For example:
	let say there are 2 data sources, 'marketing' and 'data'
	`select * from main.products p join marketing.hits h on p.id=h.productId`
	then use <dataSourceAndTableInfos> to determine what dataSourceName the products table belongs and sort it as first in dataSourceNames OR as user intends it to be";

					List<string> aliases = ["main"];
					aliases.AddRange(dataSource.AttachedDbs.Select(p =>
					{
						return p.Alias;
					}));

					string strDataSources = string.Join("\", \"", aliases);
					system += $@"
This sql statement will ATTACH in sqlite: ""{strDataSources}"". That means tables in sql statements MUST be prefixed.
'main.' data source should always be listed first in dataSourceNames Parameters (in this case it is '{dataSource.NameInStep}')

<prefix_examples>
`select * from products p join analytics a on p.id=a.productId` => sql = ""SELECT * FROM {aliases[0]}.products p JOIN {aliases[1]}.analytics a ON p.id=a.productId""
</prefix_example>
In this example above we assume 'products' table belongs to the dataSource '{dataSource.NameInStep}' which is referenced as '{aliases[0]}.', and 'analytics.' belongs to the datasource {aliases[1]}.
Use <dataSourceAndTableInfos> to construct the valid sql
";

				}
			}


			AppendToSystemCommand(system);
			var buildResult = await base.BuildWithClassDescription<DbGenericFunction>(goalStep, methodsAndTables.ClassDescription, previousBuildError);
			if (buildResult.BuilderError != null) return buildResult;

			var gf = buildResult.Instruction.Function as DbGenericFunction;

			if (gf == null || gf.Name == "N/A")
			{
				return (null, new StepBuilderError("Could not map step to a function", goalStep, Retry: false));
			}

			if (gf.Name == "ExecutePlang")
			{
				return buildResult;
			}

			var dataSourceNameParam = gf.GetParameter<string>("dataSourceName");
			if (dataSourceNameParam?.Contains("%variable0%") == true)
			{
				var ds = context.DataSource;
				if (ds.NameInStep != null && !ds.NameInStep.Contains("%variable0%"))
				{
					var updatedParams = gf.Parameters
						.Select(p => p.Name == "dataSourceName" ? p with { Value = ds.NameInStep } : p)
						.ToList();

					gf = gf with { Parameters = updatedParams };
					buildResult.Instruction = buildResult.Instruction with { Function = gf };
					dataSourceNameParam = ds.NameInStep;
				}
				else
				{
					return (null, new StepBuilderError("dataSourceName cannot contain %variable0%", goalStep, Retry: ds.NameInStep != null));
				}
			}
			if (dataSourceNameParam?.Contains("%") == true && !VariableHelper.IsVariable(dataSourceNameParam))
			{
				var dsResult = await dbSettings.GetDataSource(dataSourceNameParam, goalStep, false);
				if (dsResult.Error != null) return (null, new BuilderError(dsResult.Error));
			}
			else if (string.IsNullOrEmpty(dataSourceNameParam))
			{
				var hasDataSourceName = methodsAndTables.ClassDescription.Methods[0].Parameters.FirstOrDefault(p => p.Name == "dataSourceName");
				if (hasDataSourceName == null)
				{
					return buildResult;
				}

				List<string>? dsNames = gf.GetParameter<List<string>>("dataSourceNames");

				if (dataSource == null && (dsNames == null || dsNames.Count == 0))
				{
					return (null, new StepBuilderError("Missing dataSourceName. Please include it", goalStep));
				}

				if (dsNames?.Count > 0)
				{
					foreach (var dsName in dsNames)
					{
						(_, var error2) = await dbSettings.GetDataSource(dsName, goalStep, false);
						if (error2 != null) return (null, new BuilderError($"Datasource '{dsName}' does not exist. Use the original names of the data sources provided. The dataSourceName that belonds to '{dsName}' should come first in your response"));
					}


					dsNames = dsNames.OrderBy(s => s.Equals("main", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
					int dsIdx = gf.Parameters.FindIndex(p => p.Name == "dataSourceNames");
					if (dsIdx != -1)
					{
						gf.Parameters[dsIdx] = gf.Parameters[dsIdx] with { Value = dsNames };

						buildResult.Instruction = buildResult.Instruction with { Function = gf };
					}

					int sqlIdx = gf.Parameters.FindIndex(p => p.Name == "sql");
					if (sqlIdx != -1)
					{

						var sql = gf.GetParameter<string>("sql");
						// todo: fix this should be done in the builder
						var mainDataSource = dsNames.FirstOrDefault();
						sql = sql.Replace(mainDataSource + ".", "main.");

						gf.Parameters[sqlIdx] = gf.Parameters[sqlIdx] with { Value = sql };

						buildResult.Instruction = buildResult.Instruction with { Function = gf };
					}

				}
				else
				{

					int idx = gf.Parameters.FindIndex(p => p.Name == "dataSourceName");
					if (idx != -1)
					{

						gf.Parameters[idx] = gf.Parameters[idx] with { Value = dataSource.NameInStep ?? dataSource.Name };

						buildResult.Instruction = buildResult.Instruction with { Function = gf };
					}
				}
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

		private string AppendSystemDependingOnMethod(MethodsAndTables methodsAndTables, DataSource dataSource)
		{
			string system = "";
			if (methodsAndTables.ContainsMethod("insert"))
			{
				system += @"\n- when user defines to write into come sort of %id%, then choose the method which select id of row back
- ignore on duplicate should set validateAffectedRows=false";
				if (dataSource?.KeepHistory == true)
				{
					system += "\n- YOU MUST include ParameterInfo(\"@id\", \"auto\", \"System.Int64\") in your response AND modify the sql to include that column and parameter";
					system += "\n- Make sure to include @id in sql statement and sqlParameters. Missing @id will cause invalid result.";
					system += "\n- Plang will handle retrieving the inserted id, only create the insert statement, nothing regarding retrieving the last id inserted";
					system += "\n- When user is doing upsert(on conflict update), make sure to return the id of the table on the update sql statement";
				}
				else
				{
					system += "\n- When user want to do InsertAndSelectIdOfInsertedRow, include the select statment to get the id of inserted row";
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
				string autoIncremental = (dataSource.KeepHistory) ? "(NO auto increment)" : "auto incremental";
				system += $"\n- On CreateTable, include primary key, id, not null, {autoIncremental} when not defined by user. It must be long";
			}
			if (methodsAndTables.ContainsMethod("update") || methodsAndTables.ContainsMethod("delete"))
			{
				system += $"\n- When sql statement is missing WHERE statement, give a Warning";
			}
			if (methodsAndTables.ContainsMethod("select"))
			{
				system += @$"
- when user defines select * from XXX, keep the * in sql
- You MUST generate ReturnValues for the select statement, see <select_example>
- when user defines to write the result into a %variable%, then ReturnValues is only 1 item.
- when user defines, write to %variable%, then result is an object

<select_example>
`select * from address where id=%id%, write to %address%` => ReturnValues => VariableName: address
`select name, address, zip from users where id=%id%, write to %user%` => ReturnValues => VariableName: user
`select count(*) as totalCount, sum(amount) as totalAmount, zip from orders where id=%id%, write to %orders%` => ReturnValues => VariableName: orders
`select id as %productId% from products` => sql=""select id as productId from products"", ReturnValues => VariableName: productId
<select_example>
";


			}

			if (methodsAndTables.DataSource?.AttachedDbs.Count > 0)
			{
				system += $@"
sql statement contains multiple datasource. The first datasource is referenced as 'main' in the sql statement and MUST contain the original name({methodsAndTables.DataSource.NameInStep}) in dataSourceNames
<select_example>
`select main.title, analytics.hits from main.products p join analytics.hits h on p.id=h.productId` => main MUST contain the original datasource name in dataSourceNames but MUST keep main in sql statement 
</select_example>

DO NOT CHANGE main. prefix
";
			}

			if (methodsAndTables.ContainsMethod("ExecuteDynamicSql"))
			{
				system += @"
- for dynamic sql, keep dynamic table and columns names in sql statement, e.g. select * from %type%Options, then keep %type% for in the sql statement
";
			}

			if (methodsAndTables.ContainsMethod("SelectOneRow"))
			{
				system += $@"
- when user defines select * from XXX, keep the * in sql
- select statement that retrieves columns and does not write the result into a variable, then each column selected MUST be in ReturnValues where the name of the column is the name of the variable. e.g. `select id from products` => ReturnValues: 'id'
- user might define his variable in the select statement, e.g. `select id as %articleId% from article where id=%id%`, the intent is to write into %articleId%, make sure to adjust the sql to be valid
- Returning 1 mean user want only one row to be returned (limit 1)

<select_example>
`select id from users where id=%id%` => ReturnValues => VariableName:  id
`select price as selectedPrice from products where id=%id%` => ReturnValues => VariableName: selectedPrice
`select postcode as %zip% from address where id=%id%` => ReturnValues => VariableName: zip
<select_example>
";
			}
			return system;
		}

		[Description("DataSource Name can contain variables, e.g. /user/%user.id%, then IsDynamic=true or it can be something string such as 'data', 'analytics', etc. then IsDynamic=false")]
		public record DataSourceName(string Name, bool IsDynamic);

		public record Table(string Name)
		{
			[LlmIgnore]
			public DataSource DataSource { get; set; }
			public string DataSourceName { get; set; }
			public List<ColumnInfo> Columns { get; internal set; }
		}
		[Description("Methods is dictionary(key:value). Key is method name, Value is confidents level(low|medium|high).  Tables that are in the sql statement. DataSourceNames defined by the user.")]
		public record MethodsAndTables(string Reasoning, Dictionary<string, string> Methods, List<Table> Tables, List<DataSourceName>? DataSourceNames = null)
		{
			[LlmIgnore]
			public DataSource? DataSource { get; set; }

			[LlmIgnore]
			public ClassDescription ClassDescription { get; set; }
			/*
						[LlmIgnore]
						public OrderedDictionary<DataSourceName, List<TableInfo>> DataSourceWithTableInfos { get; set; } = new();
			*/
			public bool ContainsMethod(string name)
			{
				return Methods.FirstOrDefault(p => p.Key.Contains(name, StringComparison.OrdinalIgnoreCase) && p.Value == "high").Key != null;
			}
		};

		public async Task<(MethodsAndTables?, IBuilderError?)> GetMethodsAndTables(GoalStep step, IBuilderError? previousBuildError = null)
		{
			string system = @"Determine which <methods> fit best with the user intent for this DbModule. 
This is pre-processing to choose selection of possible <methods>, so you can suggest multiple methods. 
For Select, Insert, Update, Delete, CreateTable and Execute methods, list out the table names that are affected
When a you cannot determine method for the user intentented sql statement, use Execute(when sql is know) or ExecuteDynamicSql(when sql cannot be determined)
When table name is unknown at built time because it is created with variable, use ExecuteDynamicSql, e.g. select * from %tableName%, or select * from %type%Options

## Scheme explained: 
- Reasoning: explain why you chose method(s)
- Methods: dictionary(key:value). Key is method name, Value is confidents level(low|medium|high)
- TableNames: List of tables that are used in user sql pseudo code
- DataSourceNames: list of datasource names defined by the user. Can be null
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

			var dss = await dbSettings.GetAllDataSources();

			if (error != null) return (null, new BuilderError(error));
			var strDataSources = string.Join(',', dss.Select(p => p.Name));
			if (String.IsNullOrEmpty(strDataSources)) strDataSources = "data";

			system += $"\n<methods>\n{JsonConvert.SerializeObject(methodList)}\n</methods>";
			system += $"\n<datasources>\n{strDataSources}\n</datasources>";

			if (previousBuildError != null)
			{
				system += $"\n\nAdjust your response as last llm request gave this error: {previousBuildError.Message}";
			}
			(var methodsAndTables, error) = await LlmRequest<MethodsAndTables>(system, step);
			if (error != null) return (null, error);

			// lets construction a new class description with only data that is needed
			var classDescResult = GetNewClassDescription(step, classDescription, methodsAndTables);
			if (classDescResult.Error != null) return (null, classDescResult.Error);

			bool methodHasDataSourceName = classDescResult.HasDataSourceName;
			methodsAndTables.ClassDescription = classDescResult.ClassDescription;

			var stepDataSource = context.DataSource;
			if (stepDataSource == null && step.Goal.IsSetup && step.Goal.GoalName.Equals("setup", StringComparison.OrdinalIgnoreCase))
			{
				var dataSourceResult = await dbSettings.GetDataSource("data", step, false);
				if (dataSourceResult.Error != null && dataSourceResult.Error.StatusCode != 404) return (null, new BuilderError(dataSourceResult.Error));
				if (dataSourceResult.DataSource == null)
				{
					var createDataSourceResult = await dbSettings.CreateDataSource("data", "sqlite", true, true);
					if (createDataSourceResult.Error != null) return (null, new BuilderError(createDataSourceResult.Error));

					stepDataSource = createDataSourceResult.DataSource;
					context.DataSource = stepDataSource;
					step.Goal.DataSourceName = stepDataSource.Name;
				}
			}

			// todo: hack with execute sql file
			if (methodsAndTables.ContainsMethod("ExecuteSqlFile") ||
				methodsAndTables.ContainsMethod("ExecuteDynamicSql") ||
				methodsAndTables.ContainsMethod("QueryDynamicSql") ||
				methodsAndTables.ContainsMethod("ExecutePlang")
				|| (methodsAndTables.Tables.Count > 0 && methodsAndTables.Tables[0].Name == "ExecuteDynamicSql"))
			{
				if (stepDataSource != null)
				{
					methodsAndTables.DataSource = stepDataSource;
					methodsAndTables.Tables.ForEach(p => p.DataSource = stepDataSource);
				}
					return (methodsAndTables, null);
			}



			if (!methodHasDataSourceName)
			{
				if (stepDataSource != null)
				{
					methodsAndTables.DataSource = stepDataSource;
					methodsAndTables.Tables.ForEach(p => p.DataSource = stepDataSource);
				}
				return (methodsAndTables, null);
			}



			// tables names are only needed for select, update, delete, insert, etc.
			if (methodsAndTables.Tables == null || methodsAndTables.Tables.Count == 0)
			{
				if (!methodsAndTables.ContainsMethod("Execute"))
				{
					return (null, new StepBuilderError("You must provide TableNames", step));
				}
			}


			List<DataSource> dataSources = new();
			var program = GetProgram(step);
			if (methodsAndTables.DataSourceNames?.Count > 0)
			{
				foreach (var dsName in methodsAndTables.DataSourceNames)
				{
					(var dataSource, var dsError) = await dbSettings.GetDataSource(dsName.Name);
					if (dsError != null) return (null, new StepBuilderError(dsError, step));

					dataSources.Add(dataSource);
				}
			}
			else
			{
				dataSources = await dbSettings.GetAllDataSources();
			}

			if (methodsAndTables.Tables != null)
			{
				foreach (var dataSource in dataSources)
				{
					(var tableInfos, var dsError) = await program.GetDatabaseStructure(dataSource, methodsAndTables.Tables.Select(p => p.Name).ToList());
					if (dsError?.StatusCode == 404) continue;
					if (dsError != null) return (null, new StepBuilderError(dsError, step));

					foreach (var tableInfo in tableInfos)
					{
						var table = methodsAndTables.Tables.FirstOrDefault(p => p.Name == tableInfo.Name);
						if (table != null)
						{
							if (table.DataSource != null)
							{
								return (null, new StepBuilderError($"At least two datasources have the table '{table.Name}'. They are '{table.DataSource.Name}' and '{dataSource.Name}'", step,
									FixSuggestion: $"Define the datasource you want to use in your step, e.g. {step.Text}, datasource: {dataSource.Name}", Retry: false));
							}
							table.DataSource = dataSource;
							table.DataSourceName = dataSource.NameInStep;
							table.Columns = tableInfo.Columns;
							if (methodsAndTables.DataSource == null)
							{
								methodsAndTables.DataSource = dataSource;
							}
							else if (methodsAndTables.DataSource.LocalPath != dataSource.LocalPath)
							{
								methodsAndTables.DataSource.Attach(dataSource.NameInStep, dataSource.LocalPath);
							}
						}
					}
				}

				var tablesWithMissingDataSource = methodsAndTables.Tables.Where(p => p.DataSource == null).ToList();
				if (tablesWithMissingDataSource.Count > 0)
				{
					string tableSuggestions = await GetTableSuggestions(tablesWithMissingDataSource);
					return (null, new StepBuilderError($"Could not find datasource for table(s): {string.Join(",", tablesWithMissingDataSource.Select(p => p.Name))}. Is there a typo in the sql?"
						, step, FixSuggestion: tableSuggestions, Retry: false));
				}
			}
			return (methodsAndTables, null);
		}

		private async Task<string> GetTableSuggestions(List<Table> tablesWithMissingDataSource)
		{
			var program = GetProgram(GoalStep);
			var dataSources = await dbSettings.GetAllDataSources();
			string suggestions = "";
			foreach (var table in tablesWithMissingDataSource)
			{
				foreach (var dataSource in dataSources)
				{
					var schemas = await program.GetDbScheme(dataSource);
					if (schemas.Scheme == null) continue;

					var tables = schemas.Scheme.Where(p => p.StartsWith(table.Name, StringComparison.OrdinalIgnoreCase));
					suggestions += string.Join("\n", tables.Select(p => $" - {p} (DataSource: {dataSource.NameInStep})\n"));
				}
			}

			if (suggestions != "")
			{
				suggestions = "Here are table suggestions:\n" + suggestions;
			}
			return suggestions;
		}

		private (ClassDescription? ClassDescription, bool HasDataSourceName, IBuilderError? Error) GetNewClassDescription(GoalStep step, ClassDescription classDescription, MethodsAndTables? methodsAndTables)
		{
			if (methodsAndTables == null) return (null, false, new StepBuilderError($"No methods to choose from", step));

			bool methodHasDataSourceName = false;
			var newClassDescription = new ClassDescription();

			var selectMethod = methodsAndTables.Methods.FirstOrDefault(p => p.Key == "Select");
			if (selectMethod.Key != null && !methodsAndTables.Methods.ContainsKey("SelectWithMultipleDataSources"))
			{
				methodsAndTables.Methods.Add("SelectWithMultipleDataSources", selectMethod.Value);
			}
			selectMethod = methodsAndTables.Methods.FirstOrDefault(p => p.Key == "SelectOneRow");
			if (selectMethod.Key != null && !methodsAndTables.Methods.ContainsKey("SelectOneRowWithMultipleDataSources"))
			{
				methodsAndTables.Methods.Add("SelectOneRowWithMultipleDataSources", selectMethod.Value);
			}

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
				if (dataSourceName == null)
				{
					dataSourceName = methodInfo.Parameters.FirstOrDefault(p => p.Name.Equals("dataSourceNames"));
					if (dataSourceName == null)
					{
						continue;
					}
				}
				methodHasDataSourceName = true;
			}

			return (newClassDescription, methodHasDataSourceName, null);
		}

		private async Task<(DataSource? DataSource, IBuilderError? Error)> GetDataSource(GoalStep step, DbGenericFunction gf, string keyName = "dataSourceName")
		{
			var dataSourceNames = gf.GetParameter<List<string>>(keyName);
			if (dataSourceNames == null)
			{
				dataSourceNames = gf.GetParameter<List<string>>(keyName + "s");
			}
			List<AttachedDb> attachedDbs = new();
			string? dataSourceName = null;
			if (dataSourceNames != null)
			{
				dataSourceName = dataSourceNames[0];
				if (dataSourceNames.Count > 1)
				{
					foreach (var name in dataSourceNames[1..])
					{
						var result = await dbSettings.GetDataSource(name, step, false);
						if (result.Error != null) return (null, new BuilderError(result.Error));

						if (result.DataSource != null && !attachedDbs.Any(p => p.Path == result.DataSource.LocalPath))
						{
							attachedDbs.AddRange(new AttachedDb(result.DataSource.Name, result.DataSource.LocalPath));
						}
					}
				}
			}
			else
			{

				dataSourceName = gf.GetParameter<string>(keyName);
			}

			if (string.IsNullOrEmpty(dataSourceName) && step.Goal.IsSetup)
			{
				dataSourceName = step.Goal.DataSourceName;
			}

			if (string.IsNullOrEmpty(dataSourceName))
			{
				return (null, new StepBuilderError("No datasource name defined", step, StatusCode: 406));


			}

			if (VariableHelper.IsVariable(dataSourceName))
			{
				return (null, new StepBuilderError($"Cannot validate datasource that is variable: {dataSourceName}", step, StatusCode: 401));
			}

			var (dataSource, error) = await dbSettings.GetDataSource(dataSourceName, step, false);
			if (error != null) return (null, new StepBuilderError(error, step));

			foreach (var attachDb in attachedDbs)
			{
				dataSource.Attach(attachDb.Alias, attachDb.Path);
			}
			
			return (dataSource, null);
		}

		public async Task<IBuilderError?> BuilderExecuteSqlFile(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var (dataSource, error) = await GetDataSource(step, gf);
			if (error != null) return error;

			var fileName = gf.GetParameter<string>("fileName");
			if (string.IsNullOrEmpty(fileName))
			{
				return new StepBuilderError("Filename is empty", step);
			}

			var file = programFactory.GetProgram<Modules.FileModule.Program>(step);
			var readResult = await file.ReadTextFile(fileName);
			if (readResult.Error != null) return new BuilderError(readResult.Error);

			var tableAllowList = GenericFunctionHelper.GetParameterValueAsList(gf, "tableAllowList");
			using var program = GetProgram(step);
			var result = await program.Execute(dataSource, readResult.Content.ToString(), tableAllowList);
			if (result.Error != null)
			{
				logger.LogWarning($"  - ❌ Sql statement got error - {result.Error.Message} - {step.Text} @ {step.RelativeGoalPath}:{step.LineNumber}");
				return null;
			}
			logger.LogInformation($"  - ✅ Sql statement validated - {readResult.Content.ToString().MaxLength(30, "...")} - {step.Goal.RelativeGoalPath}:{step.LineNumber}");
			return null;

		}

		public async Task<IBuilderError?> BuilderExecuteDynamicSql(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			string sql = gf.GetParameter<string>("sql");
			var variables = variableHelper.GetVariables(sql, memoryStack);
			if (variables.Count > 0)
			{
				logger.LogWarning($"  - ⚠️ Dynamic sql will not be validated - {sql}");
				return null;
			}
			else
			{
				return new BuilderError("Sql statement does not contain dynamic variable. DO NOT select ExecuteDynamicSql, u se 'Execute' method");
			}
		}
		public async Task<IBuilderError?> BuilderExecute(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var (dataSource, error) = await GetDataSource(step, gf);
			if (error != null)
			{
				if (error.StatusCode == 401)
				{
					logger.LogWarning("  - ⚠️ Cannot validate sql, dont know datasource as it is a variable.");
					return null;
				}
				return error;
			}


			var sql = gf.GetParameter<string>("sql");
			if (VariableHelper.IsVariable(sql))
			{
				return new StepBuilderError("Do not use the Execute method when the sql is a %variable%. Use ExecuteDynamicSql method.", step);
			}

			var tableAllowList = gf.GetParameter<List<string>>("tableAllowList");
			var parameters = gf.GetParameter<List<ParameterInfo>?>("parameters");

			using var program = GetProgram(step);
			var result = await program.Execute(dataSource, sql, tableAllowList, parameters);
			if (result.Error != null)
			{
				return new BuilderError(result.Error) { Retry = false };
			}
			logger.LogInformation($"  - ✅ Sql statement validated - {sql.MaxLength(30, "...")} - {step.Goal.RelativeGoalPath}:{step.LineNumber}");
			return null;
		}

		public bool IsValidated(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			if (step.Goal.IsSetup || (AppContext.TryGetSwitch("Validate", out bool validate) && validate)) return false;
			if (gf.GetParameter<string>("sql") == null && gf.GetParameter<string>("fileName") == null) return false;

			if (instruction.Properties.TryGetValue("IsValidSql", out object? prop))
			{
				if (prop is JObject jObj)
				{
					var isValidSql = jObj.ToObject<ValidSql>();
					if (isValidSql == null) return false;

					var setupGoal = goalParser.GetGoals().FirstOrDefault(p => p.IsSetup && p.RelativePrPath == isValidSql.SetupRelativePrPath);
					if (setupGoal != null)
					{
						if (setupGoal.Hash == isValidSql.SetupHash) return true;
					}
				}
				else if (prop is JArray jArray)
				{
					var validSqls = jArray.ToObject<List<ValidSql>>();
					if (validSqls == null || validSqls.Count == 0) return false;

					foreach (var validSql in validSqls)
					{
						var setupGoal = goalParser.GetGoals().FirstOrDefault(p => p.IsSetup && p.RelativePrPath == validSql.SetupRelativePrPath);
						if (setupGoal != null)
						{
							if (setupGoal.Hash != validSql.SetupHash) return false;
						}
					}
					return true;
				}
			}
			return false;
		}

		public async Task<(Instruction?, IBuilderError?)> SetAsValidated(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			if (step.Goal.IsSetup)
			{
				return (instruction, null);
			}
			if (gf.GetParameter<string>("sql") == null && gf.GetParameter<string>("fileName") == null) return (instruction, null);

			var dataSourceName = gf.GetParameter<string>("dataSourceName");
			dataSourceName = ModuleSettings.ConvertDataSourceNameInStep(dataSourceName);
			var setupGoal = goalParser.GetGoals().FirstOrDefault(p => p.IsSetup && p.DataSourceName.Equals(dataSourceName, StringComparison.OrdinalIgnoreCase));

			if (!string.IsNullOrEmpty(dataSourceName))
			{

				if (setupGoal == null)
				{
					logger.LogWarning($"  - ℹ️ Could not find setup file for data source '{dataSourceName}' - {step.RelativeGoalPath}:{step.LineNumber}", step);
					return (instruction, null);
				}

				var isValidSql = new ValidSql(setupGoal.Hash, setupGoal.RelativePrPath, dataSourceName, DateTime.Now);
				bool writeInstruction = !instruction.Properties.ContainsKey("IsValidSql");

				instruction.Properties.AddOrReplace("IsValidSql", isValidSql);
			}
			else
			{
				List<ValidSql> validSqls = new();
				var dataSourceNames = gf.GetParameter<List<string>>("dataSourceNames");
				if (dataSourceNames == null || dataSourceNames.Count == 0) return (instruction, null);

				foreach (var dsNameVar in dataSourceNames)
				{
					var dsName = ModuleSettings.ConvertDataSourceNameInStep(dsNameVar);

					setupGoal = goalParser.GetGoals().FirstOrDefault(p => p.IsSetup && p.DataSourceName.Equals(dsName, StringComparison.OrdinalIgnoreCase));
					if (setupGoal == null)
					{
						logger.LogWarning($"  - ℹ️ Could not find setup file for data source '{dsName}' - {step.RelativeGoalPath}:{step.LineNumber}", step);
						return (instruction, null);
					}

					validSqls.Add(new ValidSql(setupGoal.Hash, setupGoal.RelativePrPath, dsName, DateTime.Now));

				}

				instruction.Properties.AddOrReplace("IsValidSql", validSqls);
			}
			return (instruction, null);
		}

		public record ValidSql(string SetupHash, string SetupRelativePrPath, string DataSourceName, DateTime Updated);
		public async Task<(Instruction, IBuilderError?)> BuilderValidate(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var (dataSource, error) = await GetDataSource(step, gf);
			if (error != null)
			{
				if (error.StatusCode is 406 or 401) return (instruction, null);
				return (instruction, error);
			}

			if (VariableHelper.IsVariable(dataSource!.Name))
			{
				logger.LogWarning($"  - ⚠️ Cannot validate sql, dont know datasource as it is a variable: {dataSource.Name}");
				return (instruction, null);
			}

			List<string> MethodsToValidate = ["Select", "SelectOneRow", "SelectWithMultipleDataSources", "SelectOneRowWithMultipleDataSources", "Update", "InsertOrUpdate", "InsertOrUpdateAndSelectIdOfRow", "Insert", "InsertAndSelectIdOfInsertedRow", "Delete"];
			if (!MethodsToValidate.Contains(gf.Name)) return (instruction, null);

			var dataSourceName = gf.GetParameter<string?>("dataSourceName");
			if (!string.IsNullOrEmpty(dataSourceName) && dataSourceName.Contains("%variable0%"))
			{
				return (instruction, new StepBuilderError("dataSourceName cannot contain %variable0%", step));
			}

			var sql = GenericFunctionHelper.GetParameterValueAsString(gf, "sql");
			if (string.IsNullOrEmpty(sql)) return (instruction, new StepBuilderError("sql statement is missing", step));



			if (gf.Name.Contains("select", StringComparison.OrdinalIgnoreCase) && (gf.ReturnValues == null || gf.ReturnValues.Count == 0))
			{
				if (gf.Name.Contains("insert", StringComparison.OrdinalIgnoreCase))
				{
					return (instruction, new StepBuilderError("When selecting id back after insert/update statement you MUST have ReturnValues. According to user intent, is it enough to just do insert/update without selecting id?", step));
				}
				return (instruction, new StepBuilderError("Select statement MUST have ReturnValues", step));
			}

			if (gf.TableNames?.FirstOrDefault(p => p.Contains("%")) != null)
			{
				return (instruction, null);
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


			(var validSql, dataSourceName, error) = await IsValidSql(sql, dataSource, step);
			if (error == null)
			{
				var dataSourceNameToSet = (dataSource.Name == dataSourceName) ? dataSource.NameInStep : dataSourceName;
				if (dataSourceNameToSet?.Contains("%variable0%") == true) return (instruction, new StepBuilderError("datasource cannot be users/%variable0%", step));

				var updatedParams = gf.Parameters
					.Select(p => p.Name == "dataSourceName" ? p with { Value = dataSourceNameToSet } : p)
					.ToList();

				gf = gf with { Parameters = updatedParams };
				instruction = instruction with { Function = gf };

				logger.LogInformation($"  - ✅ Sql statement validated - {sql.MaxLength(30, "...")} - {step.Goal.RelativeGoalPath}:{step.LineNumber}");

				var setupFile = goalParser.GetGoals().FirstOrDefault(p => p.IsSetup && p.DataSourceName == dataSource.Name);
				if (setupFile == null)
				{
					return (instruction, new StepBuilderError($"Could not find setup file matching data source: '{dataSource.Name}'", step));
				}

				return (instruction, null);
			}

			var dataSourceNameForTable = gf.GetParameter<string>("dataSourceName");
			if (dataSourceNameForTable?.Contains("%variable0%") == true) return (instruction, new StepBuilderError("datasource cannot be users/%variable0%", step));

			var (dataSourceForTable, rError) = await dbSettings.GetDataSource(dataSourceNameForTable, step, false);
			if (rError != null) return (instruction, new StepBuilderError(rError, step));

			using var program = GetProgram(step);
			var tableStructure = await program.GetDatabaseStructure(dataSourceForTable, gf.TableNames);

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
		public async Task<IBuilderError?> BuilderEndTransaction(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var program = GetProgram(step);
			var error = await program.EndTransaction();
			if (error != null) return new StepBuilderError(error, step);

			return null;
		}
		public async Task<IBuilderError?> BuilderBeginTransaction(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var dataSourceNames = gf.GetParameter<List<string>>("dataSourceNames");
			if (dataSourceNames == null || dataSourceNames.Count == 0) return null;

			var program = GetProgram(step);
			var error = await program.BeginTransaction(dataSourceNames);
			if (error != null)
			{
				if (error.StatusCode == 409)
				{
					string atGoal = "";
					var goal = goalParser.GetGoals().FirstOrDefault(p => p.RelativePrPath == context.DataSource.TransactionStartGoal);
					if (goal != null)
					{
						atGoal = " " + goal.RelativeGoalPath;
					}

					logger.LogWarning($"  - ⚠️ Transaction not commited in code{atGoal}. It will be automatically commited. Good practice to add `- end transaction` step to you code.");

					error = await program.EndTransaction(true);
					if (error != null) return new StepBuilderError(error, step);

					return await BuilderBeginTransaction(step, instruction, gf);
				}
				else
				{
					return new StepBuilderError(error, step);
				}
			}

			return null;
		}
		public async Task<IBuilderError?> BuilderRollback(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var program = GetProgram(step);
			await program.Rollback();
			return null;
		}
		public async Task<IBuilderError?> BuilderCreateTable(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			if (!step.Goal.IsSetup) return new StepBuilderError("Create table can only be in a setup file", step,
				FixSuggestion: @"Move the create statement into a setup file");

			var sql = gf.GetParameter<string>("sql");
			if (string.IsNullOrEmpty(sql)) return new StepBuilderError("sql is empty, cannot create table", step);

			var (dataSource, error) = await GetDataSource(step, gf);
			if (error != null)
			{
				if (error.StatusCode != 404) return error;

				var dataSources = await dbSettings.GetAllDataSources();
				if (dataSources.Count > 0) return error;

				(dataSource, var rError) = await dbSettings.CreateDataSource("data", setAsDefaultForApp: true, keepHistoryEventSourcing: true);
				if (rError != null) return new StepBuilderError(rError, step);
			}

			if (dataSource.ConnectionString.Contains("Mode=Memory;"))
			{
				using var program = GetProgram(step);
				(_, var rError) = await program.CreateTable(sql);

				if (rError != null) return new StepBuilderError(rError, step);
				logger.LogInformation($"  - ✅ Sql statement validated - {sql.MaxLength(30, "...")} - {step.Goal.RelativeGoalPath}:{step.LineNumber}");
			}

			return AddDataSourceToContext(dataSource, step);
		}

		private IBuilderError? AddDataSourceToContext(DataSource main, GoalStep step)
		{
			var dataSource = context.DataSource;
			if (dataSource != null)
			{
				if (dataSource.Transaction != null)
				{
					dataSource.Transaction.Commit();
					dataSource.Transaction.Connection?.Close();
					dataSource.Transaction = null;
					dataSource.AttachedDbs.Clear();
				}
			}
			context.DataSource = main;

			return null;
		}

		private Program GetProgram(GoalStep step)
		{
			var program = programFactory.GetProgram<Program>(step);

			return program;
		}

		public async Task<IBuilderError?> BuilderSetDataSourceNames(GoalStep step, Instruction instruction, DbGenericFunction gf)
		{
			var (dataSource, error) = await GetDataSource(step, gf, "dataSourceNames");
			if (dataSource != null)
			{
				return AddDataSourceToContext(dataSource, step);
			}

			if (error?.StatusCode == 401)
			{
				var name = gf.GetParameter<List<string>>("dataSourceNames")[0];
				logger.LogWarning($"  - ℹ️ {name} is a variable, cannot validate datasource. This just means that I can only validate when you run your app");
				return null;
			}
			return error;
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
			var (dataSource, error) = await GetDataSource(step, gf);
			if (error != null) return (instruction, error);

			if (dataSource!.KeepHistory == false) return (instruction, null);

			var parameterInfos = gf.GetParameter<List<ParameterInfo>>("sqlParameters");
			if (parameterInfos == null)
			{
				return (instruction, new StepBuilderError("No parameters included. It needs at least to have @id", step));
			}
			var sql = gf.GetParameter<string>("sql");

			var hasId = parameterInfos.FirstOrDefault(p => p.ParameterName == "@id") != null && Regex.IsMatch(sql, @"@id\b");
			if (hasId || parameterInfos.Count == 0) return (instruction, null);

			return (instruction, new StepBuilderError($"No @id provided in either sqlParameters or sql statement. @id MUST be provided in both. This is required for this datasource: {dataSource.Name}", step,
				FixSuggestion: @"Examples:
`- insert into users, write to %id%` => sql = ""insert into users (id) values (@id)"", sqlParameter must contain,  ParameterInfo(""@id"", ""auto"", ""System.Int64"")


"));


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

			var (dataSource, error) = await GetDataSource(step, gf, "name");
			if (error != null && error.StatusCode != 404) return (instruction, error);

			var dataSources = await dbSettings.GetAllDataSources();
			var dbTypeParam = gf.GetParameter<string>("databaseType", "sqlite");
			var dataSourceName = gf.GetParameter<string>("name", "data");

			var setAsDefaultForApp = gf.GetParameter<bool?>("setAsDefaultForApp", dataSources.Count == 0);


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

			var keepHistoryEventSourcing = gf.GetParameter<bool?>("keepHistoryEventSourcing", true);
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

			(var createdDataSource, var rError) = await dbSettings.CreateOrUpdateDataSource(dataSourceName, dbTypeParam, setAsDefaultForApp.Value, keepHistoryEventSourcing.Value);
			if (rError != null) return (instruction, new StepBuilderError(rError, step, false));

			createdDataSource = createdDataSource with { ConnectionString = dataSource.ConnectionString };
			createdDataSource.NameInStep = dataSourceName;
			step.Goal.DataSourceName = createdDataSource.Name;

			step.RunOnce = GoalHelper.RunOnce(step.Goal);

			error = AddDataSourceToContext(createdDataSource, step);
			return (instruction, error);

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



			var anchors = appContext.GetOrDefault<Dictionary<string, IDbConnection>>("AnchorMemoryDb", new(StringComparer.OrdinalIgnoreCase)) ?? new(StringComparer.OrdinalIgnoreCase);
			if (!anchors.ContainsKey(dataSource.Name))
			{
				return (false, dataSource.Name, new StepBuilderError($"Data source name '{dataSource.Name}' does not exists.", step,
				 FixSuggestion: $@"Choose datasource name from one of there: {string.Join(", ", anchors.Select(p => p.Key))}"));

			}


			var variables = variableHelper.GetVariables(sql, memoryStack);
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


			IDbConnection connection = (dataSource.Transaction != null) ?
					dataSource.Transaction.Connection :
					anchor.Value;
			

			try
			{
				

				AttachDb(connection, dataSource, step);

				var cmd = connection.CreateCommand();

				cmd.CommandText = sql;
				cmd.Prepare();				

				return (true, anchor.Key, null);
			}
			catch (Exception ex)
			{
				string errorInfo = "Error(s) while trying to valid the sql.\n";
				errorInfo += $"\tDataSource: {anchor.Key} - Error Message:{ex.Message}\n";

				return (false, dataSource.Name, new StepBuilderError(errorInfo, Step: step));
			} finally
			{
				try
				{
					DetachDb(connection, dataSource, step);
				} catch (Exception ex)
				{
					int i = 0;
				}
			}

		}


		private void AttachDb(IDbConnection connection, DataSource dataSource, GoalStep step)
		{
			if (dataSource.AttachedDbs.Count == 0) return;
			if (dataSource.AttachedDbs.FirstOrDefault(p => !p.IsAttached) == null) return;

			string attach = "";
			for (int i = 0; i < dataSource.AttachedDbs.Count; i++)
			{
				if (dataSource.AttachedDbs[i].IsAttached) continue;

				var alias = dataSource.AttachedDbs[i].Alias;
				
				if (string.Equals(alias, "main", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(alias, "temp", StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException($"Invalid alias: {alias}");

			
				attach +=  $"ATTACH DATABASE '{dataSource.AttachedDbs[i].Path}' AS {alias};\n";
				dataSource.AttachedDbs[i].IsAttached = true;
			}
			var cmd = connection.CreateCommand();
			cmd.CommandText = attach;
			cmd.ExecuteNonQuery();

		}

		private void DetachDb(IDbConnection connection, DataSource dataSource, GoalStep step)
		{
			if (dataSource.AttachedDbs.Count == 0 || dataSource.IsInTransaction) return;

			string detach = "";
			for (int i = 0; i < dataSource.AttachedDbs.Count; i++)
			{
				var alias = dataSource.AttachedDbs[i].Alias;

				if (string.Equals(alias, "main", StringComparison.OrdinalIgnoreCase) ||
					string.Equals(alias, "temp", StringComparison.OrdinalIgnoreCase))
					throw new InvalidOperationException($"Invalid alias: {alias}");

				detach += $";\nDETACH DATABASE \"{alias}\";";
				dataSource.AttachedDbs[i].IsAttached = false;

			}
			var cmd = connection.CreateCommand();
			cmd.CommandText = detach;
			cmd.ExecuteNonQuery();

			dataSource.AttachedDbs.Clear();
			
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

