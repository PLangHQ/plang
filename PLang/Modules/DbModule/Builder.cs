using CsvHelper;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.EventSourceService;
using PLang.Services.LlmService;
using PLang.Utils;
using System.ComponentModel;
using System.Data;
using static PLang.Modules.DbModule.Builder;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Modules.DbModule.Program;

namespace PLang.Modules.DbModule
{
	public class Builder : BaseBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly IDbConnection db;
		private readonly ISettings settings;
		private readonly PLangAppContext context;
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly ITypeHelper typeHelper;
		private readonly ILogger logger;
		private readonly MemoryStack memoryStack;
		private readonly VariableHelper variableHelper;
		private ModuleSettings moduleSettings;

		public Builder(IPLangFileSystem fileSystem, IDbConnection db, ISettings settings, PLangAppContext context,
			ILlmServiceFactory llmServiceFactory, ITypeHelper typeHelper, ILogger logger, MemoryStack memoryStack, VariableHelper variableHelper) : base()
		{
			this.fileSystem = fileSystem;
			this.db = db;
			this.settings = settings;
			this.context = context;
			this.llmServiceFactory = llmServiceFactory;
			this.typeHelper = typeHelper;
			this.logger = logger;
			this.memoryStack = memoryStack;
			this.variableHelper = variableHelper;
		}

		public record FunctionInfo(string DatabaseType, string FunctionName, string[]? TableNames = null);
		public record DbGenericFunction(string FunctionName, List<Parameter> Parameters, List<ReturnValue>? ReturnValue = null, string? Warning = null) : GenericFunction(FunctionName, Parameters, ReturnValue);
		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep goalStep)
		{
			moduleSettings = new ModuleSettings(fileSystem, settings, context, llmServiceFactory, logger);
			(var buildInstruction, var buildError) = await base.Build(goalStep);
			if (buildError != null) return (null, buildError);

			var gf = buildInstruction.Action as GenericFunction;
			if (gf != null && gf.FunctionName == "CreateDataSource")
			{
				await CreateDataSource(gf);
				return (buildInstruction, null);
			}
			if (gf != null && gf.FunctionName == "SetDataSourceName")
			{
				await SetDataSourceName(gf);
				return (buildInstruction, null);
			}
			var dataSource = await moduleSettings.GetCurrentDataSource();


			SetSystem(@$"Parse user command.

variable is defined with starting and ending %, e.g. %filePath%

FunctionName: Select the correct function from list of available functions based on user command
TableNames: Table names in sql statement, leave variables as is
DatabaseType: Define the database type. The .net library being used is {dataSource.TypeFullName}, determine the database type from the library");


			SetAssistant($@"## functions available defined in csharp ##
{typeHelper.GetMethodsAsString(typeof(Program))}
## functions available ends ##
");
			var program = new Program(db, fileSystem, settings, llmServiceFactory, new DisableEventSourceRepository(), context, logger);
			if (!string.IsNullOrEmpty(dataSource.SelectTablesAndViews))
			{

				var result = await program.Select(dataSource.SelectTablesAndViews, new List<object>() { new ParameterInfo("Database", dataSource.DbName, "System.String") });

				if (result.rows != null)
				{
					AppendToAssistantCommand($@"## table & views in db start ##
{JsonConvert.SerializeObject(result.rows)}
## table & view in db end ##");
				}
			}

			(var instruction, buildError) = await base.Build<FunctionInfo>(goalStep);
			if (buildError != null || instruction == null)
			{
				return (null, buildError ?? new StepBuilderError("Could not build Sql statement", goalStep));
			}
			var functionInfo = instruction.Action as FunctionInfo;

			if (functionInfo.FunctionName == "Insert")
			{
				return await CreateInsert(goalStep, program, functionInfo, dataSource);
			}
			else if (functionInfo.FunctionName == "InsertAndSelectIdOfInsertedRow")
			{
				return await CreateInsertAndSelectIdOfInsertedRow(goalStep, program, functionInfo, dataSource);
			}
			else if (functionInfo.FunctionName == "Update")
			{
				return await CreateUpdate(goalStep, program, functionInfo, dataSource);
			}
			else if (functionInfo.FunctionName == "Delete")
			{
				return await CreateDelete(goalStep, program, functionInfo, dataSource);
			}
			else if (functionInfo.FunctionName == "CreateTable")
			{
				return await CreateTable(goalStep, program, functionInfo, dataSource);
			}
			else if (functionInfo.FunctionName == "Select" || functionInfo.FunctionName == "SelectOneRow")
			{
				return await CreateSelect(goalStep, program, functionInfo, dataSource);
			}

			SetSystem($@"Generate the SQL statement from user command. 
The SQL statement MUST be a valid SQL statement for {functionInfo.DatabaseType}. 
Make sure to use correct data types that match {functionInfo.DatabaseType}
You MUST provide Parameters if SQL has @parameter.
");
			SetAssistant($@"## functions available defined in csharp ##
{typeHelper.GetMethodsAsString(typeof(Program))}
## functions available ends ##
");

			await AppendTableInfo(dataSource, program, functionInfo.TableNames);

			return await base.Build(goalStep);

		}

		private async Task SetDataSourceName(GenericFunction gf)
		{
			var parameter = gf.Parameters.FirstOrDefault(p => p.Name == "name");
			if (parameter == null) return;

			var dataSourceName = parameter.Value.ToString();
			var datasources = settings.GetValues<DataSource>(typeof(PLang.Modules.DbModule.ModuleSettings)).ToList();
			var datasource = datasources.FirstOrDefault(p => p.Name == dataSourceName);
			if (datasource == null) return;

			context.AddOrReplace(ReservedKeywords.CurrentDataSource, datasource);

		}

		private async Task CreateDataSource(GenericFunction gf)
		{
			var dataSourceName = gf.Parameters.FirstOrDefault(p => p.Name == "name");
			if (dataSourceName == null || dataSourceName.Value == null || string.IsNullOrEmpty(dataSourceName.Value.ToString()))
			{
				throw new BuilderStepException("Name for the data source is missing. Please define it. Example: \"- Create sqlite data source 'myDatabase'\"");
			}

			var supportedDbTypesAsString = moduleSettings.GetSupportedDbTypesAsString();
			var dbTypeParam = gf.Parameters.FirstOrDefault(p => p.Name == "databaseType");
			if (dbTypeParam == null || dbTypeParam.Value == null || string.IsNullOrEmpty(dbTypeParam.Value.ToString()))
			{
				var supportedDbTypes = moduleSettings.GetSupportedDbTypes();
				if (supportedDbTypes.Count > 1)
				{
					throw new BuilderStepException(@$"Database type is missing. Add the database type into your step, Example: ""- Create postgres data source 'myDatabase'"". 
These are the supported databases (you dont need to be precise)
{supportedDbTypesAsString}
");
				}
			}
			var setAsDefaultForApp = gf.Parameters.FirstOrDefault(p => p.Name == "setAsDefaultForApp");
			var keepHistoryEventSourcing = gf.Parameters.FirstOrDefault(p => p.Name == "keepHistoryEventSourcing");

			bool isDefault = (setAsDefaultForApp != null && setAsDefaultForApp.Value != null) ? (bool)setAsDefaultForApp.Value : false;
			bool keepHistory = (keepHistoryEventSourcing != null && keepHistoryEventSourcing.Value != null) ? (bool)keepHistoryEventSourcing.Value : false;

			await moduleSettings.CreateDataSource((string)dataSourceName.Value, null, (string)dbTypeParam.Value, isDefault, keepHistory);
		}


		private async Task<(Instruction?, IBuilderError?)> CreateSelect(GoalStep goalStep, Program program, FunctionInfo functionInfo, DataSource dataSource)
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
			string functionDesc = "List<object> Select(String sql, List<object>()? SqlParameters = null) // always writes to one object";
			string functionHelp = $"Select function return multiple rows so user MUST define 'write to %variable%' and returns 1 value, a List<object>";
			if (functionInfo.FunctionName == "SelectOneRow")
			{
				functionDesc = "object? SelectOneRow(String sql, List<object>()? SqlParameters = null)";
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
%Now% variable is type of DateTime. %Now% variable should be left as is.
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
- select from tableB where id=%id%, into %table% => sql: SELECT * FROM tableB WHERE id=@id, ReturnValue: %table%
- select * from %table% WHERE %name%, write to %result% => sql: SELECT * FROM %table% WHERE name=@name, ReturnValue: %result%
- select id, name from users, write to %user% => then ReturnValue is %user% object
- select id, name from users => then ReturnValue is %id%, %name% objects
# examples #
# ParameterInfo scheme #
{TypeHelper.GetJsonSchemaForRecord(typeof(ParameterInfo))}
# ParameterInfo scheme #
");


			await AppendTableInfo(dataSource, program, functionInfo.TableNames);
			return await base.Build(goalStep);
		}

		private Task<(Instruction?, IBuilderError?)> CreateTable(GoalStep goalStep, Program program, FunctionInfo functionInfo, DataSource dataSource)
		{
			string databaseType = dataSource.TypeFullName.Substring(dataSource.TypeFullName.LastIndexOf(".") + 1);
			string keepHistoryCommand = "";
			if (dataSource.KeepHistory)
			{
				keepHistoryCommand = @$"You MUST add id to create statement.
If id is not defined then add id to the create statement. 
The id MUST NOT be auto incremental, but is primary key.
The id should be datatype long/bigint/.. which fits {functionInfo.DatabaseType}.";
			}
			else
			{
				keepHistoryCommand = @"If user does not define a primary key, add it to the create statement as id as auto increment";
			}

			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
void CreateTable(String sql)  
## csharp function ##

If table name is a variable, keep the variable in the sql statement
variable is defined with starting and ending %, e.g. %filePath%.
You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
{keepHistoryCommand}
");
			SetAssistant("");
			return base.Build(goalStep);
		}

		private async Task<(Instruction?, IBuilderError?)> CreateDelete(GoalStep goalStep, Program program, FunctionInfo functionInfo, DataSource dataSource)
		{
			string databaseType = dataSource.TypeFullName.Substring(dataSource.TypeFullName.LastIndexOf(".") + 1);
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "Parameter @id MUST be type System.Int64";
			}
			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
Int32 Delete(String sql, List<object>()? SqlParameters = null)
## csharp function ##

Variable is defined with starting and ending %, e.g. %filePath%.
SqlParameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, etc.
%Now% variable is type of DateTime. %Now% variable should be left as is.
{appendToSystem}
String sql IS required

If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
You MUST provide SqlParameters if SQL has @parameter.

# examples #
""delete from tableX"" => sql: ""DELETE FROM tableX"", warning: Missing WHERE statement can affect rows that should not
""delete tableB where id=%id%"" => sql: ""DELETE FROM tableB WHERE id=@id"", warning: null
""delete * from %table% WHERE %name% => sql: ""DELETE FROM %table% WHERE name=@name""
# examples #");

			return await BuildCustomStatementsWithWarning(goalStep, dataSource, program, functionInfo);
		}
		private async Task<(Instruction?, IBuilderError?)> CreateUpdate(GoalStep goalStep, Program program, FunctionInfo functionInfo, DataSource dataSource)
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
Int32 Update(String sql, List<object>()? SqlParameters = null)
## csharp function ##

variable is defined with starting and ending %, e.g. %filePath%. Do not remove %
String sql is the SQL statement that should be executed. 
String sql can be defined as variable, e.g. %sql%
Sql MAY NOT contain a variable(except table name), it MUST be injected using SqlParameters to prevent SQL injection
SqlParameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, System.DateTime, System.Int64, etc.
All integers are type of System.Int64.
%Now% variable is type of DateTime. %Now% variable should be left as is.

{appendToSystem}
If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
You MUST provide SqlParameters if SQL has @parameter.
");

			SetAssistant(@"# examples #
""update table myTable, street=%full_street%, %zip%"" => sql: ""UPDATE myTable SET street = @full_street, zip = @zip"", parameters:[{full_street:%full_street%, zip:%zip%}], Warning: Missing WHERE statement can affect rows that should not
""update tableB, %name%, %phone% where id=%id%"" => sql: ""UPDATE tableB SET name=@name, phone=@phone WHERE id=@id"", parameters:[{name:%name%, phone:%phone%, id=%id%}] 
""update %table% WHERE %name%, set zip=@zip => sql: ""UPDATE %table% SET zip=@zip WHERE name=@name"", parameters:[{name:%name%, zip:%zip%, id=%id%}] 
# examples #");


			var instruction = await BuildCustomStatementsWithWarning(goalStep, dataSource, program, functionInfo);

			return instruction;


		}

		private async Task<(Instruction?, IBuilderError?)> BuildCustomStatementsWithWarning(GoalStep goalStep, DataSource dataSource, Program program, FunctionInfo functionInfo, string? errorMessage = null, int errorCount = 0)
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
			return (instruction, null);
		}

		private async Task<(Instruction?, IBuilderError?)> CreateInsert(GoalStep goalStep, Program program, FunctionInfo functionInfo, ModuleSettings.DataSource dataSource)
		{
			string eventSourcing = (dataSource.KeepHistory) ? "You MUST modify the user command by adding id to the sql statement and parameter %id%." : "";
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "SqlParameters @id MUST be type System.Int64";
			}
			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
Int32 Insert(String sql, List<object>()? SqlParameters = null)  //returns number of rows affected
## csharp function ##

variable is defined with starting and ending %, e.g. %filePath%.
SqlParameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, System.DateTime, System.Int64, etc.
%Now% variable is type of DateTime. %Now% variable should be left as is.
{appendToSystem}
{eventSourcing}
If table name is a variable, keep the variable in the sql statement
Make sure sql statement matches columns provided for the table.

You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
You MUST provide SqlParameters if SQL has @parameter.
");
			if (dataSource.KeepHistory)
			{
				SetAssistant(@"# examples #
""insert into users, name=%name%"" => sql: ""insert into users (id, name) values (@id, @name)""
""insert into tableX, %phone%, write to %rows%"" => sql: ""insert into tableX (id, phone) values (@id, @phone)""
""insert into %table%, %phone%, write to %rows%"" => sql: ""insert into %table% (id, phone) values (@id, @phone)""

ParameterInfo has the scheme: {""ParameterName"": string, ""VariableNameOrValue"": string, ""TypeFullName"": string}
        },
# examples #");
			}
			else
			{
				SetAssistant(@"# examples #
""insert into users, name=%name%"" => sql: ""insert into users (name) values (@name)""
""insert into tableX, %phone%, write to %rows%"" => sql: ""insert into tableX (phone) values (@phone)""
""insert into %table%, %phone%, write to %rows%"" => sql: ""insert into %table% (phone) values (@phone)""
# examples #");
			}
			await AppendTableInfo(dataSource, program, functionInfo.TableNames);

			return await base.Build(goalStep);

		}

		private async Task<(Instruction?, IBuilderError?)> CreateInsertAndSelectIdOfInsertedRow(GoalStep goalStep, Program program, FunctionInfo functionInfo, ModuleSettings.DataSource dataSource)
		{
			string eventSourcing = (dataSource.KeepHistory) ? "You MUST modify the user command by adding id to the sql statement and parameter %id%." : "";
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "SqlParameters @id MUST be type System.Int64";
			}
			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
Object InsertAndSelectIdOfInsertedRow(String sql, List<object>()? SqlParameters = null)
## csharp function ##

variable is defined with starting and ending %, e.g. %filePath%.
SqlParameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, System.DateTime, System.Int64, etc.
InsertAndSelectIdOfInsertedRow returns a value that should be written into %variable% defined by user.
%Now% variable is type of DateTime. %Now% variable should be left as is.
{appendToSystem}
{eventSourcing}
If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {functionInfo.DatabaseType}.
You MUST provide SqlParameters if SQL has @parameter.
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

			return await base.Build(goalStep);
		}

		private async Task AppendTableInfo(DataSource dataSource, Program program, string[]? tableNames)
		{
			if (tableNames == null) return;

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

				string selectColumns = await moduleSettings.FormatSelectColumnsStatement(tableName);
				var result = await program.Select(selectColumns);
				var columnInfo = result.rows;
				if (columnInfo != null && ((dynamic)columnInfo).Count > 0)
				{
					AppendToSystemCommand($"Table name is: {tableName}. Fix sql if it includes type.");
					AppendToAssistantCommand($"### {tableName} columns ###\n{JsonConvert.SerializeObject(columnInfo)}\n### {tableName} columns ###");
				}
				else
				{
					logger.LogWarning($@"Could not find information about table {tableName}. 
I will not build this step. You need to run the setup file to create tables in database. This is the command: plang run Setup");
					throw new BuilderException("You need to run: 'plang run Setup.goal' to create or modify your database before you continue with your build");
				}
			}


		}
	}
}

