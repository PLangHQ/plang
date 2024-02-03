using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.Runtime.InteropServices;
using Websocket.Client.Logging;
using static PLang.Modules.DbModule.Builder;
using static PLang.Modules.DbModule.ModuleSettings;
using Instruction = PLang.Building.Model.Instruction;

namespace PLang.Modules
{


	public abstract class BaseBuilder : IBaseBuilder
	{

		private string? system;
		private string? assistant;
		private string? appendedSystemCommand;
		private string? appendedAssistantCommand;
		private string module;
		private IPLangFileSystem fileSystem;
		private ILlmService aiService;
		private ITypeHelper typeHelper;
		private ILogger logger;
		private MemoryStack memoryStack;
		private PLangAppContext context;
		private VariableHelper variableHelper;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		protected BaseBuilder()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		{ }

		public void InitBaseBuilder(string module, IPLangFileSystem fileSystem, ILlmService llmService, ITypeHelper typeHelper, 
			MemoryStack memoryStack, PLangAppContext context, VariableHelper variableHelper, ILogger logger)
		{
			this.module = module;
			this.fileSystem = fileSystem;
			this.aiService = llmService;
			this.typeHelper = typeHelper;
			this.memoryStack = memoryStack;
			this.context = context;
			this.variableHelper = variableHelper;
			this.logger = logger;
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

		public virtual async Task<Instruction> Build(GoalStep step, Type? responseType = null, string? errorMessage = null, int errorCount = 0)
		{
			if (errorCount > 3)
			{
				logger.LogError(errorMessage);
				throw new BuilderException("Could not get a valid function from LLM. You need to adjust your wording.");
			}
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

			var methodHelper = new MethodHelper(step, variableHelper, memoryStack, typeHelper, aiService);
			var invalidFunctions = methodHelper.ValidateFunctions(instruction.GetFunctions(), step.ModuleType, memoryStack);

			if (invalidFunctions.Count > 0)
			{
				errorMessage = @$"## Error from previous LLM request ## 
Previous response from LLM caused error. This was your response.
{instruction.Action.ToString()}

This is the error(s)
";
				foreach (var invalidFunction in invalidFunctions)
				{
					errorMessage += " - " + invalidFunction.explain;
				}
				errorMessage += $@"Make sure to fix the error and return valid JSON response
## Error from previous LLM request ##

				";
				return await Build(step, responseType, errorMessage, ++errorCount);
			}

			//cleanup for next time
			appendedSystemCommand = "";
			appendedAssistantCommand = "";
			assistant = "";
			system = "";


			return instruction;
		}

		public record Parameter(string Type, string Name, object Value);
		public record ReturnValue(string Type, string VariableName);
		public record GenericFunction(string FunctionName, List<Parameter> Parameters, List<ReturnValue>? ReturnValue = null)
		{

		}

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
				step.Text,
				assistant);

			

			return question;

		}

		private string? getDefaultSystemText()
		{

			return $@"
Your job is: 
1. Parse user intent
2. Map the intent to one of C# function provided to you
3. Return a valid JSON

Variable is defined with starting and ending %, e.g. %filePath%. Variables MUST be wrapped in quotes("") in json response, e.g. {{ ""name"":""%name%"" }}

If there is some api key, settings, config replace it with %Settings.Get(""settingName"", ""defaultValue"", ""Explain"")% 
- settingName would be the api key, config key, 
- defaultValue for settings is the usual value given, make it """" if no value can be default
- Explain is an explanation about the setting that novice user can understand.

JSON scheme information
FunctionName: Name of the function to use from list of functions, if no function matches set as ""N/A""
Parameters: List of parameters that are needed according to the user intent.
- Type: the object type in c#
- Name: name of the variable
- Value: ""%variable%"" or hardcode string that should be used
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
## functions available starts ##
{methods.Trim()}
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
					vars += variable.OriginalKey + " (type:" + (objectValue.Value ?? "object") + "), ";
					
				}
			}
			return vars;
		}

		


	}


}
