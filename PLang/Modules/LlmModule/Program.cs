using System.Collections;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;
using PLang.Utils;

namespace PLang.Modules.LlmModule;

[Description("Ask LLM a question and recieve and answer")]
public class Program : BaseProgram
{
    private readonly string AppendToAssistantKey = "__LLM_AppendToAssistant__";
    private readonly string AppendToSystemKey = "__LLM_AppendToSystem__";
    private readonly string AppendToUserKey = "__LLM_AppendToUser__";
    private readonly PLangAppContext context;
    private readonly IPLangIdentityService identityService;
    private readonly ILlmServiceFactory llmServiceFactory;
    private readonly ILogger logger;

    private readonly string PreviousConversationKey = "__LLM_PreviousConversation__";
    private readonly string PreviousConversationSchemeKey = "__LLM_PreviousConversationScheme__";
    private readonly ISettings settings;

    public Program(ILlmServiceFactory llmServiceFactory, IPLangIdentityService identityService, ISettings settings,
        ILogger logger, PLangAppContext context)
    {
        this.llmServiceFactory = llmServiceFactory;
        this.identityService = identityService;
        this.settings = settings;
        this.logger = logger;
        this.context = context;
    }

    public async Task AppendToSystem(string system)
    {
        List<string> systems = new();
        if (context.ContainsKey(AppendToSystemKey))
            systems = context[AppendToSystemKey] as List<string> ?? new List<string>();
        systems.Add(system);
        context.AddOrReplace(AppendToSystemKey, systems);
    }

    public async Task AppendToAssistant(string assistant)
    {
        List<string> assistants = new();
        if (context.ContainsKey(AppendToAssistantKey))
            assistants = context[AppendToAssistantKey] as List<string> ?? new List<string>();
        assistants.Add(assistant);
        context.AddOrReplace(AppendToAssistantKey, assistants);
    }

    public async Task AppendToUser(string user)
    {
        List<string> users = new();
        if (context.ContainsKey(AppendToUserKey))
            users = context[AppendToUserKey] as List<string> ?? new List<string>();
        users.Add(user);
        context.AddOrReplace(AppendToUserKey, users);
    }

    private void AppendToMessage(LlmMessage message)
    {
        string? text = null;
        if (message.Role == "system") text = GetAppendText(AppendToSystemKey);
        if (message.Role == "assistant") text = GetAppendText(AppendToAssistantKey);
        if (message.Role == "user") text = GetAppendText(AppendToUserKey);
        if (text == null) return;
        message.Content.Add(new LlmContent(text));
    }

    private string? GetAppendText(string appendToSystemKey)
    {
        if (!context.ContainsKey(appendToSystemKey)) return null;

        string? text = null;
        var messages = context[appendToSystemKey] as List<string> ?? new List<string>();
        foreach (var message in messages) text += message + Environment.NewLine;
        return text;
    }

    public async Task<(IReturnDictionary?, IError?)> AskLlm(
        [HandlesVariable] List<LlmMessage> promptMessages,
        string? scheme = null,
        string model = "gpt-4o-mini",
        double temperature = 0,
        double topP = 0,
        double frequencyPenalty = 0.0,
        double presencePenalty = 0.0,
        int maxLength = 4000,
        bool cacheResponse = true,
        string? llmResponseType = null,
        string loggerLevel = "trace",
        bool continuePrevConversation = false
    )
    {
        if (promptMessages == null || promptMessages.Count == 0)
            return (null, new StepError("The message to the llm service is empty. You must ask it something.", goalStep,
                "LlmError",
                FixSuggestion: "If you are loading data from file or variable, make sure that the data loads fully",
                HelpfulLinks:
                "https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.LlmModule.md"));

        if (continuePrevConversation)
        {
            var prevMessages = context.GetOrDefault<List<LlmMessage>>(PreviousConversationKey, new List<LlmMessage>());
            if (prevMessages != null) promptMessages.InsertRange(0, prevMessages);
            if (scheme == null) scheme = context.GetOrDefault<string>(PreviousConversationSchemeKey, null);
        }
        else
        {
            context.Remove(PreviousConversationKey);
        }

        for (var i = 0; i < promptMessages.Count; i++)
        {
            var message = promptMessages[i];
            for (var idx = 0; idx < message.Content.Count; idx++)
            {
                var c = message.Content[idx];
                if (c.Text != null)
                {
                    var obj = variableHelper.LoadVariables(c.Text);
                    c.Text = GetObjectRepresentation(obj);
                }

                if (c.ImageUrl != null)
                {
                    var imageUrls = variableHelper.LoadVariables(c.ImageUrl.Url);
                    if (imageUrls is IList list)
                    {
                        c.ImageUrl.Url = list[0].ToString();
                        for (var b = 1; b < list.Count; b++)
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


        var llmQuestion = new LlmRequest("LlmModule", promptMessages, model, cacheResponse);
        llmQuestion.maxLength = maxLength;
        llmQuestion.temperature = temperature;
        llmQuestion.top_p = topP;
        llmQuestion.frequencyPenalty = frequencyPenalty;
        llmQuestion.presencePenalty = presencePenalty;
        llmQuestion.llmResponseType = llmResponseType;
        llmQuestion.scheme = scheme;

        IError? error = null;
        try
        {
            var (response, queryError) = await llmServiceFactory.CreateHandler().Query<object?>(llmQuestion);

            if (queryError != null) return (null, queryError);

            promptMessages.Add(new LlmMessage("assistant", llmQuestion.RawResponse));
            context.AddOrReplace(PreviousConversationKey, promptMessages);
            context.AddOrReplace(PreviousConversationSchemeKey, scheme);

            if (function == null || function.ReturnValues == null || function.ReturnValues.Count == 0)
                if (response is JObject)
                {
                    var objResult = (JObject)response;
                    foreach (var property in objResult.Properties())
                        if (property.Value is JValue)
                        {
                            var value = ((JValue)property.Value).Value;
                            memoryStack.Put(property.Name, value);
                        }
                        else
                        {
                            memoryStack.Put(property.Name, property.Value);
                        }
                }

            if (function != null && function.ReturnValues != null && function.ReturnValues.Count > 0)
            {
                var returnDict = new ReturnDictionary<string, object?>();
                foreach (var returnValue in function.ReturnValues)
                    returnDict.AddOrReplace(returnValue.VariableName, response);
                return (returnDict, null);
            }
        }
        catch (Exception ex)
        {
            error = new ProgramError(ex.Message, goalStep, function);
        }
        finally
        {
            var logLevel = LogLevel.Trace;
            Enum.TryParse(loggerLevel, true, out logLevel);

            logger.Log(logLevel, "Llm question - prompt:{0}", JsonConvert.SerializeObject(llmQuestion.promptMessage));
            logger.Log(logLevel, "Llm question - response:{0}", llmQuestion.RawResponse);
        }

        return (null, error);
    }


    public async Task UseSharedIdentity(bool useSharedIdentity = true)
    {
        identityService.UseSharedIdentity(useSharedIdentity ? settings.AppId : null);
    }


    public async Task<string> GetLlmIdentity()
    {
        return identityService.GetCurrentIdentity().Identifier;
    }

    [Description("Get the current balance at the LLM service")]
    public async Task<object?> GetBalance()
    {
        var (response, queryError) = await llmServiceFactory.CreateHandler().GetBalance();
        return response;
    }

    private string? GetObjectRepresentation(object obj)
    {
        if (obj == null) return "";

        var type = obj.GetType();

        // Check for null, primitive types, string, DateTime, Guid, Decimal, TimeSpan, Enum, or any type you find suitable
        if (type.IsPrimitive || obj is string || obj is DateTime || obj is Guid || obj is decimal || obj is TimeSpan ||
            type.IsEnum || obj is Uri) return obj.ToString();

        if (Nullable.GetUnderlyingType(type) != null && (obj == null || obj.ToString() != ""))
            // Handle nullable types that are not null and have a meaningful ToString
            return obj.ToString();

        // For complex types or null values in nullable types, use JSON serialization
        return JsonConvert.SerializeObject(obj);
    }

    public record AskLlmResponse(string Result);
}