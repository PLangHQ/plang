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
		return goal.GetVariable<List<LlmMessage>>(PreviousConversationKey) ?? new();
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
			var prevMessages = goal.GetVariable<List<LlmMessage>>(PreviousConversationKey) ?? new();
			if (prevMessages != null)
			{
				promptMessages.InsertRange(0, prevMessages);
			}
			if (scheme == null)
			{
				scheme = goal.GetVariable<string>(PreviousConversationSchemeKey) ?? null;
			}
		}
		else
		{
			goal.RemoveVariable(PreviousConversationKey);
			goal.RemoveVariable(PreviousConversationSchemeKey);
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
		goal.AddVariable(promptMessages, variableName: PreviousConversationKey);
		goal.AddVariable(scheme, variableName: PreviousConversationSchemeKey);

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
