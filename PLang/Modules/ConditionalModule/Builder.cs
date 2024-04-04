using PLang.Utils;
using Newtonsoft.Json;
using PLang.Building.Model;

using System.Net;
using PLang.Modules.ConditionalModule;
using System;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using System.Runtime.InteropServices;
using PLang.Building;
using PLang.Building.Parsers;
using Sprache;
using PLang.Interfaces;
using static PLang.Services.CompilerService.CSharpCompiler;
using PLang.Exceptions;
using PLang.Utils.Extractors;
using PLang.Runtime;
using PLang.Services.CompilerService;
using Microsoft.Extensions.Logging;

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

		public override async Task<Instruction> Build(GoalStep step)
		{
			return await Build(step, null);
		}

		private async Task<Instruction> Build(GoalStep step, string? error = null, int errorCount = 0)
		{
			if (errorCount > 2)
			{
				throw new BuilderException($"Could not create condition. Please try to refine the step text:{step.Text}");
			}

			
			var compiler = new CSharpCompiler(fileSystem, prParser,logger);
			var dllName = compiler.GetPreviousBuildDllNamesToExclude(step);

			SetSystem(@$"Act as a senior C# developer, that converts the user statement into a C#(Version. 9) code. 

## Rules ##
- Generate static class. The code generated should have 1 method with the static method named ExecutePlangCode and return bool. 
- A variable in user intent is defined by starting and ending %.
- Variables defined in the user intent can be passed into the ExecutePlangCode function by value, but only if defined by user intent. 
- Variable names passed to ExecutePlangCode function MUST be unmodified from the user statement
- The code will not be modified after you generate it.
- ALWAYS use long or long? instead of int or int?
- Do not reference any DTO classes. Use dynamic? if complex object is needed, else use object?.
- Strings are defined with double quote ("")
- Any class from System.IO, should be replaced with PLang.SafeFileSystem.PLangFileSystem. It contains same classes and methods. 
- If PLangFileSystem is needed, add parameter PLang.SafeFileSystem.PLangFileSystem fileSystem into ExecutePlangCode method, but ONLY if needed. Assembly for PLangFileSystem is already include, do not list it in Assemblies response.
- When condition is checking if variable is null, the variable needs to be defined with ? in the parameter, e.g. ExecutePlangCode(dynamic? variable)
- Replace the dot(.) in variables with the letter α e.g. %user.id% to userαid, %product.items[0].title% to productαitemsα0ααtitle, %list[1]% to listα1α
- Keep underscore in variables if defined by user, e.g.  if %user_id%(string) is null => ExecutePlangCode(string? user_id)
- Consider top security measures when generating code and validate code
## Rules ##

## Response information ##
- Namespace: MUST be PLangGeneratedCode
- Name: is name of class, it should represent the intent of what the code is doing. 
{dllName}
- Goals should be prefixed with !, e.g. Call !ValidateUser, Call !ConditionFalse
- GoalToCallOnTrue or GoalToCallOnFalse is optional, if not defined by user, set as null
- Goals can be called with parameters using GoalToCallOnTrueParameters and GoalToCallOnFalseParameters, e.g. Call !UpdateProduct id=%id%, call !FalseCall status='false'. Then id is parameter for True, and status for False
- Using: must include namespaces that are needed to compile code.
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
## examples ##
");
			if (error != null)
			{
				AppendToAssistantCommand(error);
			}

			base.SetContentExtractor(new CSharpExtractor());
			var codeInstruction = await Build<ConditionImplementationResponse>(step);
			//go back to default extractor
			base.SetContentExtractor(new JsonExtractor());

			var answer = (ImplementationResponse)codeInstruction.Action;

			var buildStatus = await compiler.BuildCode(answer, step, memoryStack);
			if (buildStatus.Error != null)
			{
				return await Build(step, buildStatus.Error, ++errorCount);
			}

			var newInstruction = new Instruction(buildStatus.Implementation!);
			newInstruction.LlmRequest = codeInstruction.LlmRequest;
			return newInstruction;

		}


	}
}

