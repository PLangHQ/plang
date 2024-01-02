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
using static PLang.Modules.Compiler;
using PLang.Exceptions;
using PLang.Utils.Extractors;
using PLang.Runtime;

namespace PLang.Modules.ConditionalModule
{
	public class Builder : BaseBuilder
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly PrParser prParser;
		private readonly MemoryStack memoryStack;

		public Builder(IPLangFileSystem fileSystem, PrParser prParser, MemoryStack memoryStack) : base()
		{
			this.fileSystem = fileSystem;
			this.prParser = prParser;
			this.memoryStack = memoryStack;
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

			var compiler = new Compiler(fileSystem, prParser);
			var dllName = compiler.GetPreviousBuildDllNamesToExclude(step);
			AppendToAssistantCommand(dllName);

			SetSystem(@$"Act as a senior c# developer, that converts the user statement into a c#(Ver. 9) code. 

A variable is defined by starting and ending %.
Generate static class. The code generated should have 1 method with the static method named Process and return bool. 
Variables defined in the user statement can be passed into the Process function by value, but only if defined in statement. 
Statement should return true

The code will not be modified after it's generated.
ALWAYS use long or long? instead of int or int?
Name: is CamelCase name of class
Goals should be prefixed with !, e.g. Call !Process, Call !ConditionFalse
Do not reference any DTO classes. Use dynamic.
Strings are defined with double quote ("")
Any class from System.IO, should be replaced with PLang.SafeFileSystem.PLangFileSystem. It contains same classes and methods. Add parameter PLang.SafeFileSystem.PLangFileSystem fileSystem into method. Assembly is already include, do not list it in Assemblies response.

Replace the dot(.) in variables with the letter α e.g. %user.id% to userαid, %product.items[0].title% to productαitemsα0ααtitle, %list[1]% to listα1α
Keep underscore in variables if defined by user, e.g.  if %user_id% is null => return user_id == null.

You must return ```csharp for the code implementation and ```json scheme 
{{Name:string, Using:string[]?= null,  Assemblies:string[]? = null,  GoalToCallOnTrue:string? = null, string? GoalToCallOnFalse:string? = null}}
");
			AppendToAssistantCommand(@"## examples ##
'if %isValid% is true then', this condition would return true if %isValid% is true. 
'if %address% is empty then', this would check if the %address% variable is empty and return true if it is, else false.

'if %data% (string) is null, call !CreateData, else !AppendData' => return string.IsNullOrEmpty(userIdentity);
'if %exists% (bool) is null, call !CreateUser' => return exists == null;
'if %exists% (bool) is not null, call !CreateUser' => return exists != null;
'if %data.user_id% is empty, call !CreateUser' => return (dataαuser_id == null || (dataαuser_id is string str && string.IsNullOrEmpty(str))); //if we dont know the type of %data.user_id%
## examples ##
");
			if (error != null)
			{
				AppendToAssistantCommand(error);
			}

			base.SetContentExtractor(new CSharpExtractor());
			var instruction = await Build<CodeImplementationResponse>(step);
			//go back to default extractor
			base.SetContentExtractor(new JsonExtractor());

			var answer = (CodeImplementationResponse)instruction.Action;

			var buildStatus = await compiler.BuildCode(answer, step, memoryStack);
			if (buildStatus.Error != null)
			{
				return await Build(step, buildStatus.Error, ++errorCount);
			}

			var newInstruction = new Instruction(buildStatus.Implmentation!);
			newInstruction.LlmQuestion = instruction.LlmQuestion;
			return newInstruction;

		}


	}
}

