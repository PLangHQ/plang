using Microsoft.Extensions.Logging;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.Runtime.InteropServices;
using Websocket.Client.Logging;
using static PLang.Modules.DbModule.Builder;
using static PLang.Modules.DbModule.ModuleSettings;
using static System.Net.Mime.MediaTypeNames;
using Instruction = PLang.Building.Model.Instruction;

namespace PLang.Modules
{


    public abstract class BaseBuilder : IBaseBuilder
	{

		private string? system;
		private string? assistant;
		private List<string> appendedSystemCommand;
		private List<string> appendedAssistantCommand;
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

			appendedSystemCommand = new List<string>();
			appendedAssistantCommand = new List<string>();
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

			var question = GetLlmRequest(step, responseType, errorMessage);
			question.Reload = step.Reload;

			var result = await aiService.Query(question, responseType);
			if (result == null)
			{
				throw new BuilderException($"Could not build for {responseType.Name}");
			}

			var instruction = new Instruction(result);
			instruction.LlmRequest = question;

			var methodHelper = new MethodHelper(step, variableHelper, memoryStack, typeHelper, aiService);
			var invalidFunctions = methodHelper.ValidateFunctions(instruction.GetFunctions(), step.ModuleType, memoryStack);

			if (invalidFunctions.Count > 0)
			{
				if (invalidFunctions[0].functionName == "N/A")
				{
					throw new FunctionNotFoundException(step.ModuleType);
				}

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
			appendedSystemCommand.Clear();
			appendedAssistantCommand.Clear();
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
			this.appendedSystemCommand.Add(appendedSystemCommand);
		}
		public void SetSystem(string systemCommand)
		{
			this.system = systemCommand;
		}
		public void AppendToAssistantCommand(string appendedAssistantCommand)
		{
			this.appendedAssistantCommand.Add(appendedAssistantCommand);
		}
		public void SetAssistant(string assistantCommand)
		{
			this.assistant = assistantCommand;
		}

		public virtual LlmRequest GetLlmRequest(GoalStep step, Type responseType, string? errorMessage = null)
		{			
			var promptMessage = new List<LlmMessage>();

			if (string.IsNullOrEmpty(system))
			{
				system = GetDefaultSystemText();
			}
			var systemContent = new List<LlmContent>();
			systemContent.Add(new LlmContent(system));
			foreach (var append in appendedSystemCommand) 
			{
				systemContent.Add(new LlmContent(append));
			}
			promptMessage.Add(new LlmMessage("system", systemContent));

			if (string.IsNullOrEmpty(assistant))
			{
				assistant = GetDefaultAssistantText(step);
			}
			var assistantContent = new List<LlmContent>();
			assistantContent.Add(new LlmContent(assistant));
			foreach (var append in appendedAssistantCommand)
			{
				assistantContent.Add(new LlmContent(append));
			}
			promptMessage.Add(new LlmMessage("assistant", assistantContent));
			if (errorMessage != null)
			{
				promptMessage.Add(new LlmMessage("assistant", errorMessage));
			}
			var userContent = new List<LlmContent>();
			userContent.Add(new LlmContent(step.Text));
			promptMessage.Add(new LlmMessage("user", userContent));

			var llmRequest = new LlmRequest(GetType().FullName, promptMessage);
			llmRequest.llmResponseType = aiService.Extractor.LlmResponseType;
			llmRequest.scheme = TypeHelper.GetJsonSchema(responseType);
			llmRequest.top_p = 0;
			llmRequest.temperature = 0;
			llmRequest.frequencyPenalty = 0;
			llmRequest.presencePenalty = 0;

			return llmRequest;

		}

		private string GetDefaultSystemText()
		{

			return $@"
Your job is: 
1. Parse user intent
2. Map the intent to one of C# function provided to you
3. Return a valid JSON

Variable is defined with starting and ending %, e.g. %filePath%. 
Variables MUST be wrapped in quotes("") in json response, e.g. {{ ""name"":""%name%"" }}
Variables should not be changed, they can include dot(.) and parentheses()

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
ReturnValue rules
- Only if the function returns a value AND if user defines %variable% to write into, e.g. ' write into %data%' or 'into %result%', or simliar intent to write return value into variable
- If no %variable% is defined then set as null.
".Trim();
		}

		private string GetDefaultAssistantText(GoalStep step)
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
				}
				else
				{
					vars += variable.OriginalKey + " (type:" + (objectValue.Value ?? "object") + "), ";

				}
			}
			return vars;
		}




	}


}
