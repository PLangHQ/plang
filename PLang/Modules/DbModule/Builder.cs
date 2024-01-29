using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.EventSourceService;
using PLang.Utils;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
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
		private readonly ILlmService aiService;
		private readonly ITypeHelper typeHelper;
		private readonly ILogger logger;
		private readonly MemoryStack memoryStack;
		private readonly VariableHelper variableHelper;
		private ModuleSettings moduleSettings;

		public Builder(IPLangFileSystem fileSystem, IDbConnection db, ISettings settings, PLangAppContext context, 
			ILlmService aiService, ITypeHelper typeHelper, ILogger logger, MemoryStack memoryStack, VariableHelper variableHelper) : base()
		{
			this.fileSystem = fileSystem;
			this.db = db;
			this.settings = settings;
			this.context = context;
			this.aiService = aiService;
			this.typeHelper = typeHelper;
			this.logger = logger;
			this.memoryStack = memoryStack;
			this.variableHelper = variableHelper;
		}

		public record FunctionInfo(string FunctionName, string[]? TableNames = null);
		public record DbGenericFunction(string FunctionName, List<Parameter> Parameters, List<ReturnValue>? ReturnValue = null, string? Warning = null) : GenericFunction(FunctionName, Parameters, ReturnValue);
		public override async Task<Instruction> Build(GoalStep goalStep)
		{
			moduleSettings = new ModuleSettings(fileSystem, settings, context, aiService, db, logger);
			var buildInstruction = await base.Build(goalStep);
			var gf = buildInstruction.Action as GenericFunction;
			if (gf != null && gf.FunctionName == "CreateDataSource")
			{
				if (string.IsNullOrEmpty(gf.Parameters[0].Value.ToString()))
				{
					throw new BuilderStepException("Name of the datasource is missing. Please define it. Example: \"Create data source 'myDatabase'\"");
				}
				var dbSourceName = variableHelper.LoadVariables(gf.Parameters[0].Value.ToString());
				await moduleSettings.CreateDataSource(dbSourceName.ToString());
				return buildInstruction;
			}

			
			var dataSource = await moduleSettings.GetCurrentDatasource();

			

			SetSystem(@"Parse user command.

Select the correct function from list of available functions based on user command

variable is defined with starting and ending %, e.g. %filePath%

TableNames: table names in sql statement, leave variables as is");


			SetAssistant($@"## functions available defined in csharp ##
{typeHelper.GetMethodsAsString(typeof(Program))}
## functions available ends ##
");
			var program = new Program(db, fileSystem, settings, aiService, new DisableEventSourceRepository(), context, logger);
			if (!string.IsNullOrEmpty(dataSource.SelectTablesAndViews))
			{

				var result = await program.Select(dataSource.SelectTablesAndViews, new List<object>() { new ParameterInfo("Database", dataSource.DbName, "System.String") });

				if (result != null)
				{
					AppendToAssistantCommand($@"## table & views in db start ##
{JsonConvert.SerializeObject(result)}
## table & view in db end ##");
				}
			}

			var instruction = await base.Build<FunctionInfo>(goalStep);
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
				return await CreateCreateTable(goalStep, program, functionInfo, dataSource);
			} else if (functionInfo.FunctionName == "Select")
			{
				return await CreateSelect(goalStep, program, functionInfo, dataSource);
			}

			SetAssistant($@"## functions available defined in csharp ##
{typeHelper.GetMethodsAsString(typeof(Program))}
## functions available ends ##
");
			AppendToAssistantCommand(@$"Create SQL statement that works with {dataSource.TypeFullName}.
You MUST provide Parameters if SQL has @parameter.
Choose the best method to use, if the method is not provided that fits the SQL, you can use Execute to run SQL statement.
");

			await AppendTableInfo(dataSource, program, functionInfo.TableNames);

			return await base.Build(goalStep);
		
		}

		private async Task<Instruction> CreateSelect(GoalStep goalStep, Program program, FunctionInfo functionInfo, DataSource dataSource)
		{
			string databaseType = dataSource.TypeFullName.Substring(dataSource.TypeFullName.LastIndexOf(".") + 1);
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "Parameter @id MUST be type System.Int64";
			}
			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
object? Select(String sql, List<object>()? Parameters = null, bool selectOneRow_Top1OrLimit1 = false)
## csharp function ##

## Rules ##
Variable is defined with starting and ending %, e.g. %filePath%.
Parameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, etc.
ReturnValue: User defined variable to write to, e.g. 'write to %result%, or if no variable is defined then Columns being returned with type if defined by user. * will return dynamic. integer/int should always be System.Int64. 
{appendToSystem}

If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {databaseType}.
You MUST provide Parameters if SQL has @parameter.
## Rules ##
");

			SetAssistant(@"# examples #
""select everything from tableX"" => sql: ""SELECT * FROM tableX""
""select from tableB where id=%id%"" => sql: ""SELECT * FROM tableB WHERE id=@id""
""select * from %table% WHERE %name% => sql: ""SELECT * FROM %table% WHERE name=@name""
# examples #");


			await AppendTableInfo(dataSource, program, functionInfo.TableNames);
			return await base.Build(goalStep);
		}

		private Task<Instruction> CreateCreateTable(GoalStep goalStep, Program program, FunctionInfo functionInfo, DataSource dataSource)
		{
			string databaseType = dataSource.TypeFullName.Substring(dataSource.TypeFullName.LastIndexOf(".") + 1);
			string keepHistoryCommand = "";
			if (dataSource.KeepHistory)
			{
				keepHistoryCommand = @$"You MUST add id to create statement.
If id is not defined then add id to the create statement. 
The id MUST NOT be auto incremental, but is primary key.
The id should be datatype long/bigint/.. which fits {databaseType}.";
			} else
			{
				keepHistoryCommand = @"If user does not define a primary key, add it to the create statement as id as auto increment";
			}

			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
void CreateTable(String sql)  
## csharp function ##

If table name is a variable, keep the variable in the sql statement
variable is defined with starting and ending %, e.g. %filePath%.
You MUST generate a valid sql statement for {databaseType}.
{keepHistoryCommand}
");
			SetAssistant("");
			return base.Build(goalStep);
		}

		private async Task<Instruction> CreateDelete(GoalStep goalStep, Program program, FunctionInfo functionInfo, DataSource dataSource)
		{
			string databaseType = dataSource.TypeFullName.Substring(dataSource.TypeFullName.LastIndexOf(".") + 1);
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "Parameter @id MUST be type System.Int64";
			}
			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
Int32 Delete(String sql, List<object>()? Parameters = null)
## csharp function ##

Variable is defined with starting and ending %, e.g. %filePath%.
Parameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, etc.
{appendToSystem}

If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {databaseType}.
You MUST provide Parameters if SQL has @parameter.
");

			SetAssistant(@"# examples #
""delete from tableX"" => sql: ""DELETE FROM tableX"", warning: Missing WHERE statement can affect rows that should not
""delete tableB where id=%id%"" => sql: ""DELETE FROM tableB WHERE id=@id"", warning: null
""delete * from %table% WHERE %name% => sql: ""DELETE FROM %table% WHERE name=@name""
# examples #");
			
			return await BuildCustomStatementsWithWarning(goalStep, dataSource, program, functionInfo);
		}
		private async Task<Instruction> CreateUpdate(GoalStep goalStep, Program program, FunctionInfo functionInfo, DataSource dataSource)
		{
			string databaseType = dataSource.TypeFullName.Substring(dataSource.TypeFullName.LastIndexOf(".") + 1);
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
Int32 Update(String sql, List<object>()? Parameters = null)
## csharp function ##

variable is defined with starting and ending %, e.g. %filePath%. Do not remove %
Sql is the SQL statement that should be executed. Sql MAY NOT contain a variable(except table name), it MUST be injected using Parameter to prevent SQL injection
Parameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, System.DateTime, System.Int64, etc.
All integers are type of System.Int64.
{appendToSystem}
If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {databaseType}.
You MUST provide Parameters if SQL has @parameter.
");

			SetAssistant(@"# examples #
""update table myTable, street=%full_street%, %zip%"" => sql: ""UPDATE myTable SET street = @full_street, zip = @zip"", parameters:[{full_street:%full_street%, zip:%zip%}], Warning: Missing WHERE statement can affect rows that should not
""update tableB, %name%, %phone% where id=%id%"" => sql: ""UPDATE tableB SET name=@name, phone=@phone WHERE id=@id"", parameters:[{name:%name%, phone:%phone%, id=%id%}] 
""update %table% WHERE %name%, set zip=@zip => sql: ""UPDATE %table% SET zip=@zip WHERE name=@name"", parameters:[{name:%name%, zip:%zip%, id=%id%}] 
# examples #");

			
			var instruction = await BuildCustomStatementsWithWarning(goalStep, dataSource, program, functionInfo);
			
			return instruction;


		}

		private async Task<Instruction> BuildCustomStatementsWithWarning(GoalStep goalStep, DataSource dataSource, Program program, FunctionInfo functionInfo, string? errorMessage = null, int errorCount = 0)
		{
			await AppendTableInfo(dataSource, program, functionInfo.TableNames);
			var instruction = await base.Build<DbGenericFunction>(goalStep);

			var gf = instruction.Action as DbGenericFunction;
			if (!string.IsNullOrWhiteSpace(gf.Warning))
			{
				logger.LogWarning(gf.Warning);
			}
			return instruction;
		}

		private async Task<Instruction> CreateInsert(GoalStep goalStep, Program program, FunctionInfo functionInfo, ModuleSettings.DataSource dataSource)
		{
			string databaseType = dataSource.TypeFullName.Substring(dataSource.TypeFullName.LastIndexOf(".") + 1);
			string eventSourcing = (dataSource.KeepHistory) ? "You MUST modify the user command by adding id to the sql statement and parameter %id%." : "";
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "Parameter @id MUST be type System.Int64";
			}
			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
Int32 Insert(String sql, List<object>()? Parameters = null)
## csharp function ##

variable is defined with starting and ending %, e.g. %filePath%.
Parameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, System.DateTime, System.Int64, etc.
{appendToSystem}
{eventSourcing}
If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {databaseType}.
You MUST provide Parameters if SQL has @parameter.
");
			if (dataSource.KeepHistory)
			{
				SetAssistant(@"# examples #
""insert into users, name=%name%"" => sql: ""insert into users (id, name) values (@id, @name)""
""insert into tableX, %phone%, write to %rows%"" => sql: ""insert into tableX (id, phone) values (@id, @phone)""
""insert into %table%, %phone%, write to %rows%"" => sql: ""insert into %table% (id, phone) values (@id, @phone)""
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

		private async Task<Instruction> CreateInsertAndSelectIdOfInsertedRow(GoalStep goalStep, Program program, FunctionInfo functionInfo, ModuleSettings.DataSource dataSource)
		{
			string databaseType = dataSource.TypeFullName.Substring(dataSource.TypeFullName.LastIndexOf(".") + 1);
			string eventSourcing = (dataSource.KeepHistory) ? "You MUST modify the user command by adding id to the sql statement and parameter %id%." : "";
			string appendToSystem = "";
			if (dataSource.KeepHistory)
			{
				appendToSystem = "Parameter @id MUST be type System.Int64";
			}
			SetSystem(@$"Map user command to this c# function: 

## csharp function ##
Object InsertAndSelectIdOfInsertedRow(String sql, List<object>()? Parameters = null)
## csharp function ##

variable is defined with starting and ending %, e.g. %filePath%.
Parameters is List of ParameterInfo(string ParameterName, string VariableNameOrValue, string TypeFullName)
TypeFullName is Full name of the type in c#, System.String, System.Double, System.DateTime, System.Int64, etc.
{appendToSystem}
{eventSourcing}
If table name is a variable, keep the variable in the sql statement
You MUST generate a valid sql statement for {databaseType}.
You MUST provide Parameters if SQL has @parameter.
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
""insert into users, name=%name%, write to %id%"" => sql: ""insert into users (name) values (@name);"", select inserted id ({databaseType})
""insert into tableX, %phone%, write to %id%"" => sql: ""insert into tableX (phone) values (@phone);"", select inserted id ({databaseType})
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
				if (variableHelper.IsVariable(tableName))
				{
					var obj = memoryStack.Get(tableName);
					if (obj != null)
					{
						tableName = obj.ToString();
					}
				}

				string selectColumns = await moduleSettings.FormatSelectColumnsStatement(tableName);
				var columnInfo = await program.Select(selectColumns);
				if (columnInfo != null && ((dynamic) columnInfo).Count > 0)
				{
					AppendToAssistantCommand($"### {tableName} table info starts ###\n{JsonConvert.SerializeObject(columnInfo)}\n### table info ends ###");
				} else
				{
					logger.LogWarning($@"Could not find information about table {tableName}. 
I will not build this step. You need to run the setup file to create tables in database. This is the command: plang run Setup");
					throw new SkipStepException();
				}
			}


		}
	}
}

