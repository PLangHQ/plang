using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils.Extractors;
using static PLang.Modules.Compiler;

namespace PLang.Modules.CodeModule
{
	internal class Builder : BaseBuilder
	{
	
		private readonly IPLangFileSystem fileSystem;
		private readonly PrParser prParser;
		private readonly MemoryStack memoryStack;
		private int errorCount = 0;

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


		public async Task<Instruction> Build(GoalStep step, string? error = null)
		{
			if (++errorCount > 3)
			{
				throw new BuilderException($"Could not compile code. Code:\n\n{error}");
			}

			var compiler = new Compiler(fileSystem, prParser);
			var dllName = compiler.GetPreviousBuildDllNamesToExclude(step);
			AppendToAssistantCommand(dllName);
			
			//TODO: Any file access should have IPLangFileSystem fileSystem injected and use it as fileSystem.File... or fileSystem.Directory....
			SetSystem(@$"Act as a senior c# developer, that converts the user statement into a c#(Ver. 9) code. 

A variable is defined by starting and ending %.
Generate static class. The class generated should have 1 method with the static method named Process and return void. 
Variables defined in the user statement can be passed into the Process function by value, but only if defined in statement. 

The code will not be modified after it's generated.
If condition fails, throw Exception, unless defined otherwise by user command
Exception message should be for non-technical user

Name: is CamelCase name of class
Assemblies: dll to reference to compile using Roslyn
Variables that user expect to be written to should be provided with out, parameter in the function.
Replace the dot(.) in variables with the letter α e.g. %user.id% to userαid, %product.items[0].title% to productαitemsαtitle
Keep underscore in variables if defined by user, e.g.  if %user_id% is null => return user_id == null.
Any class from System.IO, should be replaced with PLang.SafeFileSystem.PLangFileSystem. It contains same classes and methods. Add parameter PLang.SafeFileSystem.PLangFileSystem fileSystem into method. Assembly is already include, do not list it in Assemblies response.

String are defined with double quote ("")
Do not reference any dto classes. Use ExpandoObject.
If there is out parameter that is ExpandoObject return the names and types that are in the ExpandoObject in OutParameterDefinition, the string in the Dictionary is the name of the out object

You must return ```csharp for the code implementation and ```json scheme 
{{Name:string, OutParameterDefinition:Dictionary<string, ParameterType[]>?=null, Using:string[]?= null,  Assemblies:string[]? = null,  GoalToCallOnTrue:string? = null, string? GoalToCallOnFalse:string? = null}}

record ParameterType(string Name, string FullTypeName)

Be Concise");
			
			if (error != null)
			{
				AppendToAssistantCommand(error);
			}
			AppendToAssistantCommand(GetVariablesInStep(step));

			base.SetContentExtractor(new CSharpExtractor());
			var instruction = await Build<CodeImplementationResponse>(step);

			//go back to default extractor
			base.SetContentExtractor(new JsonExtractor());
			var answer = (CodeImplementationResponse)instruction.Action;
			
			var buildStatus = await compiler.BuildCode(answer, step, memoryStack);
			if (buildStatus.Error != null)
			{
				return await Build(step, buildStatus.Error);
			}
			
			instruction = new Instruction(buildStatus.Implmentation!);
			instruction.LlmQuestion = instruction.LlmQuestion;
			return instruction;


		}

		


	}


}

