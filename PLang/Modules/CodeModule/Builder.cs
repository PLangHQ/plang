using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.CompilerService;
using PLang.Utils.Extractors;
using static PLang.Services.CompilerService.CSharpCompiler;

namespace PLang.Modules.CodeModule
{
	internal class Builder : BaseBuilder
	{

		private readonly IPLangFileSystem fileSystem;
		private readonly PrParser prParser;
		private readonly MemoryStack memoryStack;
		private readonly ILogger logger;
		private int errorCount = 0;

		public Builder(IPLangFileSystem fileSystem, PrParser prParser, MemoryStack memoryStack, ILogger logger) : base()
		{
			this.fileSystem = fileSystem;
			this.prParser = prParser;
			this.memoryStack = memoryStack;
			this.logger = logger;
		}


		public override async Task<Instruction> Build(GoalStep step)
		{
			return await Build(step, null);
		}


		public async Task<Instruction> Build(GoalStep step, string? error = null, int errorCount = 0)
		{
			if (++errorCount > 3)
			{
				throw new BuilderException($"Could not compile code. Code:\n\n{error}");
			}

			var compiler = new CSharpCompiler(fileSystem, prParser, logger);
			var dllName = compiler.GetPreviousBuildDllNamesToExclude(step);

			//TODO: Any file access should have IPLangFileSystem fileSystem injected and use it as fileSystem.File... or fileSystem.Directory....
			SetSystem(@$"Act as a senior C# developer, that converts the user statement into a C#(Version. 9) code. 

## Rules ##
- Generate static class. The code generated should have 1 method with the static method named ExecutePlangCode and return void. 
- A variable in user intent is defined by starting and ending %.
- Variables defined in the user intent can be passed into the ExecutePlangCode function by value, but only if defined by user intent. 
- Variable names passed to ExecutePlangCode function MUST be unmodified from the user statement
- The code will not be modified after you generate it.
- If condition fails, throw Exception, unless defined otherwise by user command
- Exception message should be for non-technical user
- ALWAYS use long or long? instead of int or int?
- Do not reference any DTO classes. Use dynamic? if complex object is needed, else use object?.
- Strings are defined with double quote ("")
- Any class from System.IO, should be replaced with PLang.SafeFileSystem.PLangFileSystem. It contains same classes and methods. 
- If PLangFileSystem is needed, add parameter PLang.SafeFileSystem.PLangFileSystem fileSystem into ExecutePlangCode method, but ONLY if needed. Assembly for PLangFileSystem is already include, do not list it in Assemblies response.
- When condition is checking if variable is null, the variable needs to be defined with ? in the parameter, e.g. ExecutePlangCode(dynamic? variable)
- Variables that are injected into ExecutePlangCode method and contain dot(.), then replace dot(.) with the letter α in the parameter list. e.g. %user.id% to userαid, %product.items[0].title% to productαitemsα0ααtitle, %list[1]% to listα1α
- Keep underscore in variables if defined by user, e.g.  if %data.user_id%(string) is null => ExecutePlangCode(string? dataαuser_id)
- Consider top security measures when generating code and validate code
- When checking type and converting variables to type, use Convert.ChangeType method
- When user defines assembly or using, include them in your answer
- append @ sign for reserved variable in C#
## Rules ##

## Response information ##
- Namespace: MUST be PLangGeneratedCode
- Name: is name of class, it should represent the intent of what the code is doing. 
{dllName}
- Using: must include namespaces that are needed to compile code.
- Assemblies: dll to reference to compile using Roslyn
- ParameterType: {{ Name:string, FullTypeName:string }}
- OutParameterDefinition: If there is out parameter that is ExpandoObject return the names and types that are in the ExpandoObject in OutParameterDefinition, the string in the Dictionary is the name of the out object
## Response information ##
");

			AppendToAssistantCommand($@"
## examples ##
%list.Count%*50, write to %result% => ExecutePlangCode(long? listαCount, out long result) {{
    //validate input parameter 
    result = listαCount*50;
}}
%response.data.total%*%response.data.total_amount%, write to %allTotal%, => ExecutePlangCode(dynamic? response, out long allTotal) {{ 
      //validate input parameter 
      long allTotal = response.data.total*response.data.total_amount;
}}

check if %dirPath% exists, write to %folderExists% => ExecutePlangCode(IPlangFileSystem fileSystem, string dirPath, out bool folderExists) {{
	//validate input parameter 
	folderExists = fileSystem.Directory.Exists(dirPath);
}}
## examples ##");

			if (error != null)
			{
				AppendToAssistantCommand(error);
			}

			base.SetContentExtractor(new CSharpExtractor());
			var instruction = await Build<CodeImplementationResponse>(step);

			//go back to default extractor
			base.SetContentExtractor(new JsonExtractor());
			var answer = (CodeImplementationResponse)instruction.Action;
			try
			{
				var buildStatus = await compiler.BuildCode(answer, step, memoryStack);

				var newInstruction = new Instruction(buildStatus.Implementation!);
				newInstruction.LlmRequest = instruction.LlmRequest;
				return newInstruction;
			}
			catch (BuildStatusException ex)
			{
				logger.LogWarning($"Need to ask LLM again. {ex.Message}");
				return await Build(step, ex.Message, ++errorCount);
			}

		}




	}


}

