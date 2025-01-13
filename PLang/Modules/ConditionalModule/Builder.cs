using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors.Builder;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.CompilerService;
using PLang.Utils.Extractors;

namespace PLang.Modules.ConditionalModule
{
	public class Builder : BaseBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly PrParser prParser;
		private readonly MemoryStack memoryStack;
		private readonly ILogger logger;

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

		private async Task<(Instruction?, IBuilderError?)> Build(GoalStep step, CompilerError? error = null, int errorCount = 0)
		{
			if (errorCount++ > 3)
			{
				return (null, error ?? new StepBuilderError("Could not compile code for this step", step));
			}

			var compiler = new CSharpCompiler(fileSystem, prParser, logger);
			var dllName = compiler.GetPreviousBuildDllNamesToExclude(step);

			SetSystem(@$"Act as a senior C# developer, that converts the user intent into a valid C#(Version. 11) code. 

## Rules ##
- Generate static class. The code generated should have 1 method with the static method named ExecutePlangCode and return bool. 
- A variable in user intent is defined by starting and ending %.
- Variables defined in the user intent can be passed into the ExecutePlangCode function by value, but only if defined by user. 
- Variable names passed to ExecutePlangCode function MUST be unmodified from the user statement
- The code will not be modified after you generate it.
- ALWAYS use long or long? instead of int or int?
- Use long.TryParse when validating if variable is long
- Do not reference any DTO classes. Choose the type that seems to fit best when not defined by user input. Use dynamic? if complex object is needed.
- Strings are defined with double quote ("")
- Any class from System.IO, should be replaced with PLang.SafeFileSystem.PLangFileSystem. It contains same classes and methods. 
- ONLY IF PLangFileSystem is needed, add parameter PLang.SafeFileSystem.PLangFileSystem fileSystem into ExecutePlangCode method, but ONLY if needed. Assembly for PLangFileSystem is already include, do not list it in Assemblies response.
- When condition is checking if variable is null, the variable needs to be defined with ? in the parameter, e.g. ExecutePlangCode(dynamic? variable)
- Replace the dot(.) in variables with the letter α e.g. %user.id% to userαid, %product.items[0].title% to productαitemsα0ααtitle, %list[1]% to listα1α
- Keep underscore in variables if defined by user, e.g.  if %user_id%(string) is null => ExecutePlangCode(string? user_id)
- Consider top security measures when generating code and validate code
- append @ sign for variable that match reserved keywords in C#
- C# code MUST only contain the contition code and return bool, an external system will call goals(methods) that user defines in his intent.
- initialize variables before using them in TryParse
- Always convert object to correct type in code, e.g. if code requires the object to be string use Convert.ToString(obj), when object should be bool Convert.ToBoolean(obj), etc.
## Rules ##

## Response information ##
- Namespace: MUST be PLangGeneratedCode
- Name: is name of class, it should represent the intent of what the code is doing. 
{dllName}
- Goals should be prefixed with !, e.g. Call !ValidateUser, Call !ConditionFalse
- GoalToCallOnTrue or GoalToCallOnFalse is optional, if not defined by user, set as null
- Goals can be called with parameters using GoalToCallOnTrueParameters and GoalToCallOnFalseParameters, e.g. Call !UpdateProduct id=%id%, call !FalseCall status='false'. Then id is parameter for True, and status for False
- Using: must include namespaces that are needed to compile code.
- InputParameters: InputParameters MUST match parameter count sent to ExecutePlangCode. Keep format as is defined by user, e.g. if %!error% then => Parameters would be [""%!error%""], if %items.count% > 0 and %isValid% then => Parameters would be [""%list[0]%"", ""%isValid%""]
- Assemblies: dll to reference to compile using Roslyn
## Response information ##
");
			AppendToAssistantCommand(@"## examples ##
'if %isValid% is true then', this condition would return true if %isValid% is true. 
'if %address% is empty then', this would check if the %address% variable is empty and return true if it is, else false.

'if %data% (string) is null, call !CreateData, else !AppendData' => public static bool ExecutePlangCode(string? dataαuser_id) { return string.IsNullOrEmpty(userIdentity); }, GoalToCallOnTrue=CreateData, GoalToCallOnFalse=AppendData
'if %exists% (bool) is null, call !CreateUser' => public static bool ExecutePlangCode(bool? dataαuser_id) { return exists == null;}, GoalToCallOnTrue=CreateUser, GoalToCallOnFalse=null
'if %exists% (bool) is not null, call !CreateUser' => public static bool ExecutePlangCode(bool? dataαuser_id) { return exists != null;}, GoalToCallOnTrue=CreateUser, GoalToCallOnFalse=null
'if %data.user_id% is empty, call !CreateUser' => public static bool ExecutePlangCode(dynamic? dataαuser_id) { return (dataαuser_id == null || (dataαuser_id is string str && string.IsNullOrEmpty(str))); } //if we dont know the type of %data.user_id%, , GoalToCallOnTrue=CreateUser, GoalToCallOnFalse=null
'if !%isValid% then => public static bool ExecutePlangCode(bool? isValid) { return !isValid; }, GoalToCallOnTrue=null, GoalToCallOnFalse=null
'if %first_name% is null, call !UpdateFirstName' => public static bool ExecutePlangCode(string? first_name) { return (first_name == null || string.IsNullOrEmpty(str)); }
'if directory %path% exists, call DoStuff => public static bool ExecutePlangCode(string? path, IPlangFileSystem fileSystem) { return fileSystem.Directory.Exists(path); }
'if file %path% exists, call DoStuff => public static bool ExecutePlangCode(string? path, IPlangFileSystem fileSystem) { return fileSystem.File.Exists(path); }
## examples ##
");
			if (error != null)
			{
				AppendToAssistantCommand(error.LlmInstruction);
			}

			base.SetContentExtractor(new CSharpExtractor());
			(var codeInstruction, var buildError) = await Build<ConditionImplementationResponse>(step);
			if (buildError != null) return (null, buildError);

			//go back to default extractor
			base.SetContentExtractor(new JsonExtractor());

			var answer = (ImplementationResponse)codeInstruction.Action;
			(var implementation, var compilerError) = await compiler.BuildCode(answer, step, memoryStack);
			if (compilerError != null)
			{
				logger.LogWarning($"- Error compiling code - will ask LLM again ({errorCount} of 3 attempts) - Error:{compilerError}");
				return await Build(step, compilerError, errorCount);
			}

			var newInstruction = new Instruction(implementation!);
			newInstruction.LlmRequest = codeInstruction.LlmRequest;
			return (newInstruction, null);


		}


	}
}

