using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using PLang.Utils.Extractors;
using Instruction = PLang.Building.Model.Instruction;

namespace PLang.Modules
{


	public abstract class BaseBuilder : IBaseBuilder
	{

		private Type type;
		private string? system;
		private string? assistant;
		private string? appendedSystemCommand;
		private string? appendedAssistantCommand;
		private string moduleNamespace = "PLang.Modules.BaseBuilder";
		private string module;
		private IPLangFileSystem fileSystem;
		private ILlmService aiService;
		private ITypeHelper typeHelper;
		private MemoryStack memoryStack;
		private PLangAppContext context;
		private VariableHelper variableHelper;

		protected BaseBuilder()
		{ }

		public void InitBaseBuilder(string module, IPLangFileSystem fileSystem, ILlmService llmService, ITypeHelper typeHelper, MemoryStack memoryStack, PLangAppContext context, VariableHelper variableHelper)
		{
			this.module = module;
			this.fileSystem = fileSystem;
			this.aiService = llmService;
			this.typeHelper = typeHelper;
			this.memoryStack = memoryStack;
			this.context = context;
			this.variableHelper = variableHelper;
		}


		public void SetContentExtractor(IContentExtractor contentExtractor)
		{
			this.aiService.Extractor = contentExtractor;
		}
		public virtual async Task<Instruction> Build<T>(GoalStep step)
		{
			return await Build(step, typeof(T));
		}
		public virtual async Task<Instruction> Build(GoalStep step)
		{
			return await Build(step, typeof(GenericFunction));
		}

		public virtual async Task<Instruction> Build(GoalStep step, Type? responseType = null)
		{
			if (responseType == null) responseType = typeof(GenericFunction);

			var question = GetQuestion(step, responseType);
			question.Reload = step.Reload;

			var result = await aiService.Query(question, responseType);
			if (result == null)
			{
				throw new BuilderException($"Could not build for {responseType.Name}");
			}


			var instruction = new Instruction(result);
			instruction.LlmQuestion = question;
			return instruction;
		}

		public record Parameter(string Type, string Name, object Value);
		public record ReturnValue(string Type, string VariableName);
		public record GenericFunction(string FunctionName, List<Parameter> Parameters, List<ReturnValue>? ReturnValue = null)
		{

		}

		public async Task ChangeCommands(GoalStep step) { }

		public void AppendToSystemCommand(string appendedSystemCommand)
		{
			this.appendedSystemCommand += appendedSystemCommand;
		}
		public void SetSystem(string systemCommand)
		{
			this.system = systemCommand;
		}
		public void AppendToAssistantCommand(string appendedAssistantCommand)
		{
			this.appendedAssistantCommand += appendedAssistantCommand;
		}
		public void SetAssistant(string assistantCommand)
		{
			this.assistant = assistantCommand;
		}

		public virtual LlmQuestion GetQuestion(GoalStep step, Type responseType)
		{
			if (string.IsNullOrEmpty(system))
			{
				this.system = getDefaultSystemText();
			}
			if (string.IsNullOrEmpty(assistant))
			{
				assistant = getDefaultAssistantText(step);
			}

			if (!string.IsNullOrEmpty(appendedSystemCommand))
			{
				system += "\n" + appendedSystemCommand;
			}
			if (!string.IsNullOrEmpty(appendedAssistantCommand))
			{
				assistant += "\n" + appendedAssistantCommand;
			}

			string requiredResponse = aiService.Extractor.GetRequiredResponse(responseType);
			
			var question = new LlmQuestion(GetType().FullName,
				system + "\n\n" + requiredResponse,
				"command: " + step.Text,
				assistant);

			//cleanup for next time
			appendedSystemCommand = "";
			appendedAssistantCommand = "";
			assistant = "";
			system = "";

			return question;

		}

		private string GetExternalServiceInfo(ExternalServiceHandler? externalServiceHandler)
		{
			if (externalServiceHandler == null) return null;
			if (externalServiceHandler.Uri.StartsWith("http"))
			{
				return null;
			}

			string fileName = externalServiceHandler.Uri;
			if (!fileSystem.File.Exists(fileName)) return null;

			var content = fileSystem.File.ReadAllText(fileName);
			return @$"### Context information ##
Following content is provide as help and to give context.
{content}
### Context information ##
";
		}

		private string? getDefaultSystemText()
		{

			return $@"
			Parse user command.

Select the correct function from list of available functions based on user command

variable is defined with starting and ending %, e.g. %filePath%

If there is some api key, settings, config replace it with %Settings.Get(""settingName"", ""defaultValue"", ""Explain"")% 
- settingName would be the api key, config key, 
- defaultValue for settings is the usual value given, make it """" if no value can be default
- Explain is an explanation about the setting that novice user can understand.

OnExceptionContainingTextCallGoal - if no text is defined, set as ""*"", goal to call is required from user

JSON scheme information
Type: the object type in c#
Name: name of the variable
Value: %variable% or hardcode string that should be used
FunctionName: Name of the function to use from list of functions, if no function matches set as ""N/A""
ReturnValue: Only if the function returns a value AND if user defines %variable% to write into. If no %variable% is defined then set as null.
".Trim();
		}

		private string getDefaultAssistantText(GoalStep step)
		{
			var programType = typeHelper.GetRuntimeType(module);
			var variables = GetVariablesInStep(step).Replace("%", "");
			var methods = typeHelper.GetMethodsAsString(programType);

			string assistant = "";
			if (!string.IsNullOrEmpty(methods))
			{
				assistant = $@"
## functions available defined in csharp ##
{methods}
## functions available ends ##";
			}

			if (!string.IsNullOrEmpty(variables))
			{
				assistant += @$"
## defined variables ##
{variables}
## defined variables ##";
			}
			return assistant.Trim();
		}

		protected string GetVariablesInStep(GoalStep step)
		{
			var variables = variableHelper.GetVariables(step.Text);
			string vars = "";
			foreach (var variable in variables)
			{
				var objectValue = memoryStack.GetObjectValue(variable.OriginalKey, false);
				if (objectValue.Initiated)
				{
					vars += variable.OriginalKey + " (" + objectValue.Value + "), ";
				} else
				{
					vars += variable.OriginalKey + " (type:" + objectValue.Value + "), ";
				}
			}
			return vars;
		}


	}


}
