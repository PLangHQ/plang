using Microsoft.Extensions.Logging;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Collections;
using System.ComponentModel;
using static PLang.Modules.BaseBuilder;

namespace PLang.Modules.LlmModule;

[Description("Ask LLM a question and recieve and answer")]
public class Program : BaseProgram
{
	private readonly ILlmServiceFactory llmServiceFactory;
	private readonly IPLangIdentityService identityService;
	private readonly ISettings settings;
	private readonly ILogger logger;
	private readonly PLangAppContext appContext;

	public Program(ILlmServiceFactory llmServiceFactory, IPLangIdentityService identityService, ISettings settings, ILogger logger, PLangAppContext appContext) : base()
	{
		this.llmServiceFactory = llmServiceFactory;
		this.identityService = identityService;
		this.settings = settings;
		this.logger = logger;
		this.appContext = appContext;
	}

	public record AgentRun(string? Answer, int Rounds, int ToolCalls, int ToolErrors, long InputTokens, long OutputTokens, bool StoppedAtMaxRounds);

	[Description(@"Runs an agent: sends messages and tools to the llm, runs each tool the llm asks for by calling the tool's goal with the arguments as parameters, appends the results to messages and repeats until the llm answers with text. messages is the conversation and is updated in place. tools is a list of {name, description, parameters (json schema), call (goal path)}. Events: onProgress runs once per round that has tool calls, before them, with %text% (what the llm wrote alongside the calls, may be empty), %round% and %toolCallCount%; onToolCall runs before a tool with %toolCall%; onToolResult runs after it with %toolCall% and %toolResult% and may return a replacement result; onRoundEnd runs after the round's tools with %round%. reasoning: none|low|medium|high. Returns {Answer, Rounds, ToolCalls, ToolErrors, InputTokens, OutputTokens, StoppedAtMaxRounds}")]
	public async Task<(AgentRun? Run, IError? Error)> RunAgent([HandlesVariable] string messages, List<AgentTool>? tools = null,
		string? model = null, string? reasoning = null, int maxRounds = 30,
		GoalToCallInfo? onToolCall = null, GoalToCallInfo? onToolResult = null, GoalToCallInfo? onProgress = null, GoalToCallInfo? onRoundEnd = null,
		int timeoutInSeconds = 600)
	{
		var messagesValue = memoryStack.GetObjectValue(messages);
		if (messagesValue.Value is not IList history)
		{
			return (null, new ProgramError($"{messages} must be a list of messages", goalStep, function));
		}
		tools ??= new();
		var llm = llmServiceFactory.CreateHandler();
		var callGoal = GetProgramModule<CallGoalModule.Program>();

		int toolCalls = 0, toolErrors = 0;
		long inputTokens = 0, outputTokens = 0;
		for (int round = 1; round <= maxRounds; round++)
		{
			var request = new LlmChatRequest()
			{
				Model = model,
				Reasoning = reasoning,
				Messages = history.Cast<object>().ToList(),
				Tools = tools,
				TimeoutInSeconds = timeoutInSeconds,
				Step = goalStep
			};
			var (turn, error) = await llm.Chat(request);
			if (error != null) return (null, error);
			if (turn == null) return (null, new ProgramError("The llm service returned nothing", goalStep, function));

			foreach (var item in turn.Items) AddToHistory(history, item);
			inputTokens += turn.Usage.InputTokens;
			outputTokens += turn.Usage.OutputTokens;

			if (turn.ToolCalls.Count == 0)
			{
				return (new AgentRun(turn.Text, round, toolCalls, toolErrors, inputTokens, outputTokens, false), null);
			}

			if (onProgress != null)
			{
				var progressError = await RunEvent(callGoal, onProgress, new() { ["text"] = turn.Text ?? "", ["round"] = round, ["toolCallCount"] = turn.ToolCalls.Count });
				if (progressError != null) return (null, progressError);
			}

			foreach (var call in turn.ToolCalls)
			{
				toolCalls++;
				var tool = tools.FirstOrDefault(t => t.Name == call.Name);
				Dictionary<string, object?> args;
				try
				{
					args = JsonConvert.DeserializeObject<Dictionary<string, object?>>(call.Arguments) ?? new();
				}
				catch (Exception ex)
				{
					args = new();
					toolErrors++;
					AddToHistory(history, ToolOutput(call.Id, $"Error: the arguments were not valid json: {ex.Message}"));
					continue;
				}
				var callInfo = new Dictionary<string, object?> { ["id"] = call.Id, ["name"] = call.Name, ["arguments"] = args };

				if (onToolCall != null)
				{
					var startError = await RunEvent(callGoal, onToolCall, new() { ["toolCall"] = callInfo });
					if (startError != null) return (null, startError);
				}

				object? output;
				if (tool == null)
				{
					toolErrors++;
					output = $"Error: there is no tool named {call.Name}";
				}
				else
				{
					var toolGoal = new GoalToCallInfo(tool.Call, args);
					var (returned, toolError) = await callGoal.RunGoal(toolGoal);
					if (toolError != null)
					{
						toolErrors++;
						// The model, and whoever reads the log, needs to know which step failed, not just why.
						output = "Error: " + FirstLine(toolError.Message);
						if (toolError.Step != null)
						{
							output += $" (in step \"{FirstLine(toolError.Step.Text)}\" of {toolError.Step.Goal?.RelativeGoalPath ?? toolError.Step.Goal?.GoalName})";
						}
						if (toolError.Exception != null)
						{
							logger.LogWarning(toolError.Exception, "Tool {Tool} failed: {Message}", call.Name, toolError.Message);
						}
					}
					else
					{
						output = Unwrap(returned);
					}
				}

				if (onToolResult != null)
				{
					var (replaced, resultError) = await callGoal.RunGoal(new GoalToCallInfo(onToolResult.Name, new() { ["toolCall"] = callInfo, ["toolResult"] = output }) { Path = onToolResult.Path });
					if (resultError != null) return (null, resultError);
					replaced = Unwrap(replaced);
					if (replaced != null) output = replaced;
				}

				AddToHistory(history, ToolOutput(call.Id, output is string s ? s : JsonConvert.SerializeObject(output, Formatting.Indented)));
			}

			if (onRoundEnd != null)
			{
				var endError = await RunEvent(callGoal, onRoundEnd, new() { ["round"] = round });
				if (endError != null) return (null, endError);
			}
		}
		return (new AgentRun($"Stopped after {maxRounds} rounds", maxRounds, toolCalls, toolErrors, inputTokens, outputTokens, true), null);
	}

	private async Task<IError?> RunEvent(CallGoalModule.Program callGoal, GoalToCallInfo eventGoal, Dictionary<string, object?> parameters)
	{
		var info = new GoalToCallInfo(eventGoal.Name, parameters) { Path = eventGoal.Path };
		var (_, error) = await callGoal.RunGoal(info);
		return error;
	}

	// A goal that returns hands back its return variables as ObjectValues; a goal that only
	// runs to the end hands back nothing. The tool result is the value, not the wrapper.
	private static object? Unwrap(object? returned)
	{
		if (returned is ObjectValue ov) return ov.Value;
		if (returned is not List<ObjectValue> list) return returned;
		if (list.Count == 0) return null;
		if (list.Count == 1) return list[0].Value;
		return list.ToDictionary(v => v.Name, v => v.Value);
	}

	private static Dictionary<string, object?> ToolOutput(string callId, string output)
	{
		return new Dictionary<string, object?> { ["type"] = "function_call_output", ["call_id"] = callId, ["output"] = output };
	}

	private static void AddToHistory(IList history, object item)
	{
		if (history is JArray jArray)
		{
			jArray.Add(item is JToken token ? token : JToken.FromObject(item));
			return;
		}
		history.Add(item);
	}

	private static string FirstLine(string? message)
	{
		if (string.IsNullOrEmpty(message)) return "unknown error";
		var cut = message.IndexOf("🔴");
		if (cut > 0) message = message.Substring(0, cut);
		return message.Trim();
	}

	private readonly string PreviousConversationKey = "__LLM_PreviousConversation__";
	private readonly string PreviousConversationSchemeKey = "__LLM_PreviousConversationScheme__";
	private readonly string AppendToSystemKey = "__LLM_AppendToSystem__";
	private readonly string AppendToUserKey = "__LLM_AppendToUser__";
	private readonly string AppendToAssistantKey = "__LLM_AppendToAssistant__";
	public async Task AppendToSystem(string system)
	{
		List<string> systems = new List<string>();
		if (appContext.ContainsKey(AppendToSystemKey))
		{
			systems = appContext[AppendToSystemKey] as List<string> ?? new();
		}
		systems.Add(system);
		appContext.AddOrReplace(AppendToSystemKey, systems);
	}
	public async Task AppendToAssistant(string assistant)
	{
		List<string> assistants = new List<string>();
		if (appContext.ContainsKey(AppendToAssistantKey))
		{
			assistants = appContext[AppendToAssistantKey] as List<string> ?? new();
		}
		assistants.Add(assistant);
		appContext.AddOrReplace(AppendToAssistantKey, assistants);
	}
	public async Task AppendToUser(string user)
	{
		List<string> users = new List<string>();
		if (appContext.ContainsKey(AppendToUserKey))
		{
			users = appContext[AppendToUserKey] as List<string> ?? new();
		}
		users.Add(user);
		appContext.AddOrReplace(AppendToUserKey, users);
	}

	private void AppendToMessage(LlmMessage message)
	{
		string? text = null;
		if (message.Role == "system")
		{
			text = GetAppendText(AppendToSystemKey);
		}
		if (message.Role == "assistant")
		{
			text = GetAppendText(AppendToAssistantKey);
		}
		if (message.Role == "user")
		{
			text = GetAppendText(AppendToUserKey);
		}
		if (text == null) return;
		message.Content.Add(new LlmContent(text));
	}

	private string? GetAppendText(string appendToSystemKey)
	{

		if (!appContext.ContainsKey(appendToSystemKey)) return null;

		string? text = null;
		var messages = appContext[appendToSystemKey] as List<string> ?? new();
		foreach (var message in messages)
		{
			text += message + Environment.NewLine;
		}
		return text;
	}

	public record AskLlmResponse(string Result);

	[Description("Retrieves all previous messages")]
	public async Task<List<LlmMessage>?> GetPreviousMessages()
	{
		return context.GetVariable<List<LlmMessage>>(PreviousConversationKey) ?? new();
	}

	[Description("a Goal is object Goal.Path = %goal.path%")]
	public record Tools(List<Goal> Goals, int MaximumToolsExecuted = 10)
	{
		public List<GoalToCallInfo>? GoalsToCall { get; set; } = null;
	};

	[Description("When user intent is to write the result into a %variable% it MUST have ReturnValues, e.g. `... write to %result% => ReturnValues should contain the %result%")]
	public async Task<(object?, IError?, Properties?)> AskLlm(
		[HandlesVariable] List<LlmMessage> promptMessages,
		string? scheme = null,
		string model = "gpt-4.1-mini",
		double temperature = 0,
		double topP = 0,
		double frequencyPenalty = 0.0,
		double presencePenalty = 0.0,
		int maxLength = 4000,
		bool cacheResponse = true,
		string? llmResponseType = null,
		bool continuePrevConversation = false,
		Tools? tools = null
		)
	{

		if (promptMessages == null || promptMessages.Count == 0)
		{
			return (null, new StepError("The message to the llm service is empty. You must ask it something.", goalStep, "LlmError",
				FixSuggestion: "If you are loading data from file or variable, make sure that the data loads fully",
				HelpfulLinks: "https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.LlmModule.md"), null);
		}

		if (continuePrevConversation)
		{
			var prevMessages = context.GetVariable<List<LlmMessage>>(PreviousConversationKey) ?? new();
			if (prevMessages != null)
			{
				promptMessages.InsertRange(0, prevMessages);
			}
			if (scheme == null)
			{
				scheme = context.GetVariable<string>(PreviousConversationSchemeKey) ?? null;
			}
		}
		else
		{
			context.RemoveVariable(PreviousConversationKey);
			context.RemoveVariable(PreviousConversationSchemeKey);
		}

		for (int i = 0; i < promptMessages.Count; i++)
		{
			var message = promptMessages[i];
			for (int idx = 0; idx < message.Content.Count; idx++)
			{
				var c = message.Content[idx];
				if (c.Text != null)
				{
					var obj = memoryStack.LoadVariables(c.Text);
					c.Text = GetObjectRepresentation(obj);

				}

				if (c.ImageUrl != null)
				{
					var imageUrls = memoryStack.LoadVariables(c.ImageUrl.Url);
					if (imageUrls is IList list)
					{
						c.ImageUrl.Url = list[0].ToString();
						for (int b = 1; b < list.Count; b++)
						{

							var imageUrl = new ImageUrl(list[b].ToString());

							var llmContent = new LlmContent(c.Text, c.Type, imageUrl);

							message.Content.Add(llmContent);
							idx++;

						}
					}
					else
					{

						c.ImageUrl.Url = imageUrls.ToString();
					}
				}

			}
			AppendToMessage(message);
		}

		if (tools != null && tools.Goals.Count > 0)
		{
			string system = @"These are the tools(<goals>) you can call.
<goals>{JsonConvert.SerializeObject(goals)}</goals>";
			var systemPrompt = promptMessages.FirstOrDefault(p => p.Role == "system");
			if (systemPrompt == null)
			{
				var goals = tools.Goals
							.Select(p => new { Name = p.GoalName, p.Description, p.RelativeGoalPath });

				promptMessages.Insert(0, new LlmMessage("system", system));
			}
			else
			{
				systemPrompt.Content[0].Text += "\n{system}";
			}
		}

		var llmQuestion = new LlmRequest("LlmModule", promptMessages, model, cacheResponse);
		llmQuestion.Step = goalStep;
		llmQuestion.Goal = goal;
		llmQuestion.maxLength = maxLength;
		llmQuestion.temperature = temperature;
		llmQuestion.top_p = topP;
		llmQuestion.frequencyPenalty = frequencyPenalty;
		llmQuestion.presencePenalty = presencePenalty;
		llmQuestion.llmResponseType = llmResponseType;
		llmQuestion.scheme = scheme;
		llmQuestion.Tools = tools;
		if (tools != null && tools.Goals.Count > 0)
		{
			llmQuestion.llmResponseType = "json";
		}

		var properties = new Properties();
		properties.Add(new ObjectValue("Llm", llmQuestion));

		(var response, var queryError) = await llmServiceFactory.CreateHandler().Query(llmQuestion, typeof(object));

		if (queryError != null) return (null, queryError, properties);
		if (response == null) return (null, new ProgramError("Response was empty", goalStep), properties);

		promptMessages.Add(new LlmMessage("assistant", llmQuestion.RawResponse));
		context.AddVariable(promptMessages, variableName: PreviousConversationKey);
		context.AddVariable(scheme, variableName: PreviousConversationSchemeKey);

		if (function != null && function.ReturnValues != null && function.ReturnValues.Count > 0)
		{
			return await HandleTools(response, properties, promptMessages, scheme, model, temperature, topP, frequencyPenalty, presencePenalty, maxLength, cacheResponse, llmResponseType, continuePrevConversation, tools, llmQuestion);

		}

		if (response is not JObject)
		{
			return (response, new ProgramError("Response from LLM is not written to variable", FixSuggestion: $"Add `write to %result%` to you step, e.g. {goalStep.Text}\n\twrite to %result%"), properties);
		}

		var returnValues = new List<ObjectValue>();
		var objResult = (JObject)response;
		foreach (var property in objResult.Properties())
		{
			if (property.Value is JValue)
			{
				var value = ((JValue)property.Value).Value;

				var objectValue = new ObjectValue(property.Name, value);
				returnValues.Add(objectValue);

			}
			else
			{
				var objectValue = new ObjectValue(property.Name, property.Value);
				returnValues.Add(objectValue);
			}
		}


		return await HandleTools(objResult, properties, promptMessages, scheme, model, temperature, topP, frequencyPenalty, presencePenalty, maxLength, cacheResponse, llmResponseType, continuePrevConversation, tools, llmQuestion);


		



	}

	private async Task<(object?, IError?, Properties?)> HandleTools(object response, Properties properties, List<LlmMessage> promptMessages, string? scheme, string model, double temperature, double topP, double frequencyPenalty, double presencePenalty, int maxLength, bool cacheResponse, string? llmResponseType, bool continuePrevConversation, Tools? tools, LlmRequest llmQuestion)
	{
		if (llmQuestion.Tools == null || llmQuestion.Tools.GoalsToCall == null || llmQuestion.Tools.GoalsToCall.Count == 0) return (response, null, properties);
		if (toolsMaxCalls >= llmQuestion.Tools.MaximumToolsExecuted) return (response, null, properties);

		foreach (var goalToCall in llmQuestion.Tools.GoalsToCall)
		{
			GoalToCallInfo gci = goalToCall as GoalToCallInfo;
			var (returns, error) = await engine.RunGoal(gci, goal, context);

			var returnsJson = JsonConvert.SerializeObject(returns);
			var returnStr = $"\n\n### returns: {returnsJson}";

			string errorStr = "";
			if (error != null)
			{
				errorStr = $"\n\n### error:\n{error.ToString()}";
			}

			promptMessages.Add(new LlmMessage("user", $"## {goalToCall.Name}\n{errorStr}{returnStr}"));
		}

		return await AskLlm(promptMessages, scheme, model, temperature, topP, frequencyPenalty, presencePenalty, maxLength, cacheResponse, llmResponseType, continuePrevConversation, tools);
	}
	private int toolsMaxCalls = 0;

	public async Task UseSharedIdentity(bool useSharedIdentity = true)
	{
		identityService.UseSharedIdentity(useSharedIdentity ? settings.AppId : null);
	}


	public async Task<string> GetLlmIdentity()
	{
		return identityService.GetCurrentIdentity().Identifier;
	}



	private string? GetObjectRepresentation(object obj)
	{
		if (obj == null) return "";

		Type type = obj.GetType();

		// Check for null, primitive types, string, DateTime, Guid, Decimal, TimeSpan, Enum, or any type you find suitable
		if (type.IsPrimitive || obj is string || obj is DateTime || obj is Guid || obj is Decimal || obj is TimeSpan || type.IsEnum || obj is Uri)
		{
			return obj.ToString();
		}
		else if (Nullable.GetUnderlyingType(type) != null && ((obj == null) || obj.ToString() != ""))
		{
			// Handle nullable types that are not null and have a meaningful ToString
			return obj.ToString();
		}
		else
		{
			// For complex types or null values in nullable types, use JSON serialization
			return JsonConvert.SerializeObject(obj);
		}
	}
	/*
	public async Task<(object?, IError?, Properties?)> AskLlm2<T>(ILlmRequest<T> request)
	{
		// request.Data is what you serialize and send to HTTP
		var httpBody = engine.Http.Post(request)

		// ...
	}*/
}
