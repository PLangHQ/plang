using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Builder;
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


		public override async Task<(Instruction?, IBuilderError?)> Build(GoalStep step)
		{
			return await Build(step, null);
		}


		public async Task<(Instruction?, IBuilderError?)> Build(GoalStep step, CompilerError? error = null, int errorCount = 0)
		{
			if (errorCount++ > 3)
			{
				return (null, error ?? new StepBuilderError("Could not compile code for this step", step));
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
- Use long.TryParse When validating if variable is long
- Do not reference any DTO classes. Choose the type that seems to fit best when not defined by user input. Use dynamic? if complex object is needed.
- Strings are defined with double quote ("")
- Any class from System.IO, should be replaced with PLang.SafeFileSystem.PLangFileSystem. It contains same classes and methods. 
- If PLangFileSystem is needed, add parameter PLang.SafeFileSystem.PLangFileSystem fileSystem into ExecutePlangCode method, but ONLY if needed. Assembly for PLangFileSystem is already include, do not list it in Assemblies response.
- System.IO.Path needs to be mapped to PLang.SafeFileSystem.Path which DOES not contain static methods, e.g. Path.GetFileName => fileSystem.Path.GetFileName. fileSystem IS provided as parameter as part of ExecutePlangCode method 
- When condition is checking if variable is null, the variable needs to be defined with ? in the parameter, e.g. ExecutePlangCode(dynamic? variable)
- Variables that are injected into ExecutePlangCode method and contain dot(.), then replace dot(.) with the letter α in the parameter list. e.g. %user.id% to userαid, %product.items[0].title% to productαitemsα0ααtitle, %list[1]% to listα1α
- Keep underscore in variables if defined by user, e.g.  if %data.user_id%(string) is null => ExecutePlangCode(string? dataαuser_id)
- Consider top security measures when generating code and validate code
- When checking type and converting variables to type, use Convert.ChangeType method
- When user defines assembly or using, include them in your answer
- append @ sign for reserved variable in C#
- when input and output variable is same only define it once
## Rules ##

## Response information ##
- Namespace: MUST be PLangGeneratedCode
- Name: is name of class, it should represent the intent of what the code is doing. 
{dllName}
- Using: must include namespaces that are needed to compile code.
- Assemblies: dll to reference to compile using Roslyn
- InputParameters: InputParameters MUST match parameter count sent to ExecutePlangCode. Keep format as is defined by user, e.g. user: 'convert %name% to upper, write to %nameUpper%' => InputParameters would be [""%name%""]. user: 'check %items.count% > 0 and %isValid% and %!error.status%, write ""yes"" to %answer%' => InputParameters would be [""%list[0]%"", ""%isValid%"", ""%!error.status%""]
- OutParameters: keep as is defined by user, e.g. user: 'is leap year, write ""yes"" to %answer% => OutParameters would be [""%answer%""]
## Response information ##
");

			AppendToAssistantCommand($@"
## examples ##
replace ""<strong>"" with """" from %html%, write to %html% => ExecutePlangCode(ref string? html) {{
    //validate input parameter 
    html = html.Replace(""<strong>"", """");
}}
InputParameters: [""%html%""]
OutParameters: [""%html%""]

%list.Count%*50, write to %result% => ExecutePlangCode(long? listαCount, out long result) {{
    //validate input parameter 
    result = listαCount*50;
}}
InputParameters: [""%list.Count%""]
OutParameters: [""%result%""]

%response.data.total%*%response.data.total_amount%, write to %allTotal%, => ExecutePlangCode(dynamic? response.data.total, dynamic? response.data.total_amount, out long allTotal) {{ 
      //validate input parameter 
      long allTotal = response.data.total*response.data.total_amount;
}}
InputParameters: [""%response.data.total%"", ""%response.data.total_amount%""]
OutParameters: [""%allTotal%""]

check if %dirPath% exists, write to %folderExists% => ExecutePlangCode(IPlangFileSystem fileSystem, string dirPath, out bool folderExists) {{
	//validate input parameter 
	folderExists = fileSystem.Directory.Exists(dirPath);
}}
InputParameters: [""%dirPath%""]
OutParameters: [""%folderExists%""]

get filename of %filePath%, write to %fileName% => ExecutePlangCode(IPlangFileSystem fileSystem, string filePath, out string fileName) {{
	//validate input parameter 
	fileName = fileSystem.Path.GetFileName(filePath);
}}
InputParameters: [""fileSystem"", ""%filePath%""]
OutParameters: [""%fileName%""]
## examples ##");

			if (error != null)
			{
				AppendToAssistantCommand(error.LlmInstruction);
			}

			base.SetContentExtractor(new CSharpExtractor());
			
			(var instruction, var buildError) = await Build<CodeImplementationResponse>(step);
			if (buildError != null) return (null, buildError);

			if (instruction == null)
			{
				return (null, new StepBuilderError("Could not create instruction file", step));
			}

			//go back to default extractor
			base.SetContentExtractor(new JsonExtractor());
			var answer = (CodeImplementationResponse)instruction.Action;

			(var implementation, var compilerError) = await compiler.BuildCode(answer, step, memoryStack);
			if (compilerError != null)
			{
				logger.LogWarning($"- Error compiling code - will ask LLM again - Error:{compilerError} - Code:{compilerError.LlmInstruction}");
				return await Build(step, compilerError, errorCount);
			}

			var newInstruction = new Instruction(implementation!);
			newInstruction.LlmRequest = instruction.LlmRequest;
			return (newInstruction, null);


		}




	}


}

