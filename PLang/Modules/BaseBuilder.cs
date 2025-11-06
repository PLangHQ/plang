using Jil;
using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building;
using PLang.Building.Model;
using PLang.Container;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using PLang.Utils.Extractors;
using PLang.Utils.JsonConverters;
using System;
using System.Diagnostics;
using System.Text.Json.Serialization;
using Instruction = PLang.Building.Model.Instruction;

namespace PLang.Modules
{

	[GreatLeapAttribute]  // or [Class]
	public abstract class BaseBuilder : IBaseBuilder
	{

		private string? system;
		private string? assistant;
		private List<string> appendedSystemCommand;
		private List<string> appendedAssistantCommand;
		private string module;
		private IPLangFileSystem fileSystem;
		private ILlmServiceFactory llmServiceFactory;
		private ITypeHelper typeHelper;
		private ILogger logger;
		protected MemoryStack memoryStack;
		protected PLangContext context;
		private VariableHelper variableHelper;
		private IContentExtractor contentExtractor;
		protected GoalStep GoalStep;


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		protected BaseBuilder()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		{ }



		[Init]
		public void InitBaseBuilder(GoalStep goalStep, IPLangFileSystem fileSystem, ILlmServiceFactory llmServiceFactory, ITypeHelper typeHelper,
			MemoryStack memoryStack, PLangContext context, VariableHelper variableHelper, ILogger logger)
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			logger.LogDebug($"        - Start InitBaseBuilder - {stopwatch.ElapsedMilliseconds}");
			this.GoalStep = goalStep;
			this.module = goalStep.ModuleType;
			this.fileSystem = fileSystem;
			this.llmServiceFactory = llmServiceFactory;
			this.typeHelper = typeHelper;
			this.memoryStack = memoryStack;
			this.context = context;
			this.variableHelper = variableHelper;
			this.logger = logger;


			appendedSystemCommand = new List<string>();
			appendedAssistantCommand = new List<string>();

			logger.LogDebug($"        - End InitBaseBuilder - {stopwatch.ElapsedMilliseconds}");
		}

		public void SetStep(GoalStep step)
		{
			this.GoalStep = step;
		}

		public void SetContentExtractor(IContentExtractor contentExtractor)
		{
			this.contentExtractor = contentExtractor;
		}
		protected string GetPath(string? path, Goal goal)
		{
			return PathHelper.GetPath(path, fileSystem, goal);
		}

		public async Task<(T?, IBuilderError? Error)> LlmRequest<T>(string system, GoalStep step)
		{
			List<LlmMessage> messages = new();

			messages.Add(new LlmMessage("system", system));
			messages.Add(new LlmMessage("user", step.Text));
			if (step.ValidationErrors.Count > 0)
			{
				var builderError = new BuilderError("");
				builderError.ErrorChain.AddRange(step.ValidationErrors);

				messages.Add(new LlmMessage("assistant", ErrorHelper.MakeForLlm(builderError)));
			}
			LlmRequest llmRequest = new LlmRequest(typeof(T).FullName, messages);

			(var result, var queryError) = await llmServiceFactory.CreateHandler().Query(llmRequest, typeof(T));
			if (queryError != null)
			{
				return ((T?)result, new BuilderError(queryError));
			}
			return ((T?)result, null);
		}


		public virtual async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build<T>(GoalStep step,
			IBuilderError? previousBuildError = null)
		{
			return await Build(step, typeof(T), previousBuildError);
		}
		public virtual async Task<(Instruction? Instruction, IBuilderError? BuilderError)> BuildWithClassDescription<T>(GoalStep step, ClassDescription classDescription,
					IBuilderError? previousBuildError = null)
		{
			return await BuildInternal(step, typeof(T), previousBuildError, classDescription);
		}
		public virtual async Task<(Instruction? Instruction, IBuilderError? BuilderError)> BuildWithClassDescription(GoalStep step, ClassDescription classDescription,
			IBuilderError? previousBuildError = null)
		{
			return await BuildInternal(step, typeof(GenericFunction), previousBuildError, classDescription);
		}
		public virtual async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step,
			IBuilderError? previousBuildError = null)
		{
			return await Build(step, typeof(GenericFunction), previousBuildError);
		}

		[Method]
		public virtual async Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep step, Type responseType,
			IBuilderError? previousBuildError = null)
		{
			var result = await BuildInternal(step, responseType, previousBuildError, null);
			return result;
		}

		private async Task<(Instruction? Instruction, IBuilderError? BuilderError)> BuildInternal(GoalStep step, Type? responseType = null,
			IBuilderError? previousBuildError = null, ClassDescription? classDescription = null)
		{

			if (responseType == null) responseType = typeof(GenericFunction);

			var question = GetLlmRequest(step, responseType, previousBuildError, classDescription);

			try
			{


				(var result, var queryError) = await llmServiceFactory.CreateHandler().Query(question, responseType);
				if (queryError != null) return (null, new BuilderError(queryError));

				if (result == null || (result is string str && string.IsNullOrEmpty(str)))
				{
					return (null, new StepBuilderError($"Could not build for {responseType.Name}", step));
				}

				var instruction = InstructionCreator.Create(result, responseType, step, question);


				//cleanup for next time
				appendedSystemCommand.Clear();
				appendedAssistantCommand.Clear();
				assistant = "";
				system = "";


				return (instruction, null);
			}
			catch (ParsingException ex)
			{
				string? innerMessage = ex.InnerException?.Message;
				if (ex.InnerException?.InnerException != null)
				{
					innerMessage = ex.InnerException?.InnerException.Message;
				}

				return (null, new StepBuilderError(
					$@"
<error>
{innerMessage}
{ex.Message}
<error>

Previous LLM request resulted in this error, see in <error>. 
Make sure to use the information in <error> to return valid JSON response"
, step));
			} catch	(Exception ex2)
			{
				string? innerMessage = ex2.InnerException?.Message;
				if (ex2.InnerException?.InnerException != null)
				{
					innerMessage = ex2.InnerException?.InnerException.Message;
				}
				

				return (null, new StepBuilderError(
					$@"
<error>
{innerMessage}
{ex2.Message}
<error>
<llm_response>
{question.RawResponse}
<llm_response>
"
, step, ex: ex2, Retry: false, ContinueBuild: false));
			}
		}

		[Property]
		public record Parameter(string Type, string Name, object? Value);
		[Property]
		public record ReturnValue(string Type, string VariableName);
		[Property]
		public record ComplexReturnValue(string Type, string VariableName, List<ReturnValue> Properties) : ReturnValue(Type, VariableName);
		[Property]
		public record GenericFunction(string Reasoning, string Name, List<Parameter>? Parameters = null, List<ReturnValue>? ReturnValues = null) : IGenericFunction
		{
			[LlmIgnore]
			[Newtonsoft.Json.JsonIgnore]
			public Instruction Instruction { get; set; }

			public T? GetParameter<T>(string name, T? defaultValue)
			{
				var item = GetParameter<T>(name);
				if (item == null || (item is string str && string.IsNullOrEmpty(str))) return defaultValue;
				return item;
			}

			public T? GetParameter<T>(string name)
			{
				var parameter = Parameters?.FirstOrDefault(p => p.Name == name);
				if (parameter == null) return default;

				return (T?)TypeHelper.ConvertToType(parameter.Value, typeof(T), new PlaceholderPrimitiveConverter());
			}
			public GenericFunction SetParameter(string name, object value)
			{
				var parameter = Parameters?.FirstOrDefault(p => p.Name == name);
				if (parameter == null) return default;

				parameter = parameter with { Value = value };

				return this;
			}
		}

		public interface IGenericFunction
		{
			string Reasoning { get; }
			string Name { get; }
			List<Parameter>? Parameters { get; }
			List<ReturnValue>? ReturnValues { get; }

			[LlmIgnore]
			[Newtonsoft.Json.JsonIgnore]
			public Instruction Instruction { get; set; }
		}
		[Method]
		public void AppendToSystemCommand(string appendedSystemCommand)
		{
			this.appendedSystemCommand.Add(appendedSystemCommand);
		}
		[Method]
		public void SetSystem(string systemCommand)
		{
			this.system = systemCommand;
		}
		[Method]
		public void AppendToAssistantCommand(string appendedAssistantCommand)
		{
			this.appendedAssistantCommand.Add(appendedAssistantCommand);
		}
		[Method]
		public void SetAssistant(string assistantCommand)
		{
			this.assistant = assistantCommand;
		}
		string model = null;
		[Method]
		public void SetModel(string model)
		{
			this.model = model;
		}
		[Method]
		public virtual LlmRequest GetLlmRequest(GoalStep step, Type responseType, IBuilderError? previousBuildError = null, ClassDescription? classDescription = null)
		{
			var promptMessage = new List<LlmMessage>();

			if (string.IsNullOrEmpty(system))
			{
				system = GetDefaultSystemText(step);
			}
			var systemContent = new List<LlmContent>();
			systemContent.Add(new LlmContent(system));
			foreach (var append in appendedSystemCommand)
			{
				systemContent.Add(new LlmContent(append));
			}

			promptMessage.Add(new LlmMessage("system", string.Join("\n", systemContent.Select(p => p.Text))));

			var assistantContent = new List<LlmContent>();
			if (!string.IsNullOrEmpty(step.UserIntent))
			{
				assistantContent.Add(new LlmContent($"## Canonical User Intent (already disambiguated by LLM)\r\n{step.UserIntent}"));
			}

			if (string.IsNullOrEmpty(assistant))
			{
				(assistant, var error) = GetDefaultAssistantText(step, previousBuildError, classDescription);
				if (error != null) throw new ExceptionWrapper(error);
			}

			assistantContent.Add(new LlmContent(assistant));
			foreach (var append in appendedAssistantCommand)
			{
				assistantContent.Add(new LlmContent(append));
			}
			if (assistantContent.Count > 0)
			{
				promptMessage.Add(new LlmMessage("assistant", string.Join("\n", assistantContent.Select(p => p.Text))));
			}

			var userContent = new List<LlmContent>();
			string user = step.LlmText ?? step.Text;
			user = user.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t");
			userContent.Add(new LlmContent(user));
			promptMessage.Add(new LlmMessage("user", userContent));

			if (previousBuildError != null || step.ValidationErrors.Count > 0)
			{
				if (step.ValidationErrors.Count > 0)
				{
					if (previousBuildError == null) previousBuildError = new BuilderError("");
					previousBuildError.ErrorChain.AddRange(step.ValidationErrors);
				}

				string errorInfoForLlm = ErrorHelper.MakeForLlm(previousBuildError);
				promptMessage.Add(new LlmMessage("assistant", errorInfoForLlm));
				//step.Reload = true;

				// upgrade the model since there was error
				model = "gpt-4o";
			}


			var llmRequest = new LlmRequest(GetType().FullName, promptMessage);

			llmRequest.Reload = step.Reload;
			if (contentExtractor != null)
			{
				llmRequest.llmResponseType = contentExtractor.LlmResponseType;
			}
			llmRequest.scheme = TypeHelper.GetJsonSchema(responseType);
			llmRequest.top_p = 0;
			llmRequest.temperature = 0;
			llmRequest.frequencyPenalty = 0;
			llmRequest.presencePenalty = 0;
			if (model != null)
			{
				llmRequest.model = model;
			}

			return llmRequest;

		}

		[Method]
		public string GetDefaultSystemText(GoalStep step)
		{
			/*
			 * file.read system.txt, write to %content%
			 * write out content
			 * */
			return $@"User is writing code in plang programming language.

Your job is: 
1. Parse and understand user intent
2. Map the intent to one of C# function provided to you
3. Return a valid JSON

## Rules
%variable% in plang is defined with starting and ending percentage sign (%)
%variable% MUST be wrapped in quotes("") in json response, e.g. {{ ""name"":""%name%"" }}
leave %variable% as is and do not change text to a variable
null is used to represent no value, e.g. {{ ""name"": null }}
Variables MUST not be changed, they can include dot(.) and parentheses()
Keep \n, \r, \t that are submitted to you for string variables
Parameters that is type System.String MUST be without escaping quotes. See <Example>
Error handling is process by another step, if you see 'on error...' you can ignore it
If there is some api key, settings, config replace it with %Settings.NameOfApiKey% 
- NameOfApiKey should named in relation to what is happening if change is needed
Dictionary<T1, T2> value is {{key: value, ... }} => a dictionary parameter defined as %variable% without key should have the same key and value as %variable%, e.g. %userId% => {{ key: ""userId"", value:""%userId%""}}
Variable with ToString with date/time formatting, assume it is System.DateTime, e.g. %updated.ToString(""yyyy-MM-dd"")% then type of %updated% is System.DateTime 
List, ReadOnlyList are array of the object => e.g. user defines single property for List, return it as array
When you see t%variable% or t""this is text"", set the Type to Plang.TString. This is for translation
<Example>
get url ""http://example.org"" => Value: ""http://example.org""
write out 'Hello world' => Value: ""Hello world""
<Example>

## JSON scheme information
Reasoning: A brief description of the reasoning behind the selection of the function and parameters based on the user's intent. This property provides context for why a particular function was chosen and how it aligns with the user intent.
Name: Name of the function to use from list of functions, if no function matches set as ""N/A""
Parameters: List of parameters that are needed according to the user intent.
- Type: the object type in c#
- Name: name of the variable
- Value: ""%variable%"" or hardcode string that should be used
ReturnValue rules
- Only if the function returns a value AND if user defines %variable% to write into, e.g. ' write into %data%' or 'into %result%', or simliar intent to write return value into variable
- If no %variable% is defined then set as null.
".Trim();
		}
		[Method]
		/*
		 * TODO: SignatureInfo should be append to each return, roslyn? who can do it? #good-first-issue
		 * */
		public (string?, IBuilderError?) GetDefaultAssistantText(GoalStep step, IBuilderError? previousBuildError = null, ClassDescription? classDescription = null)
		{
			var programType = typeHelper.GetRuntimeType(module);
			if (programType == null) return (null, new StepBuilderError($"Could not load type {module}", step));

			var variables = GetVariablesInStep(step).Replace("%", "");

			if (classDescription == null)
			{
				var classDescriptionHelper = new ClassDescriptionHelper();
				(classDescription, var error) = classDescriptionHelper.GetClassDescription(programType);
				if (error != null) return (null, error);
			}

			string assistant = "";
			if (classDescription != null)
			{
				var json = JsonConvert.SerializeObject(classDescription, new JsonSerializerSettings
				{
					NullValueHandling = NullValueHandling.Ignore
				});
				assistant = $@"
## functions available starts ##
{json}
## functions available ends ##";
			}

			if (!string.IsNullOrEmpty(variables))
			{
				assistant += @$"
## defined variables ##
{variables}
## defined variables ##";
			}
			return (assistant.Trim(), null);
		}

		[Method]
		public string GetVariablesInStep(GoalStep step)
		{
			var variables = variableHelper.GetVariables(step.Text, memoryStack).DistinctBy(p => p.PathAsVariable);
			string vars = "";

			// todo: hack, why is Goal null?
			memoryStack.Goal = step.Goal;

			foreach (var variable in variables)
			{
				if (variable.Initiated && !variable.Name.StartsWith("Settings"))
				{
					vars += variable.PathAsVariable + " (" + variable.Value + "), ";
				}
				else
				{
					vars += variable.PathAsVariable + " (type:" + (variable.Value?.GetType().FullName ?? "object") + "), ";

				}
			}
			return vars;
		}



	}


}
