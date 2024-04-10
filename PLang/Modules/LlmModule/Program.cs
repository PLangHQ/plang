using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.LlmService;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.ComponentModel;
using System.Dynamic;
using static PLang.Services.LlmService.PLangLlmService;

namespace PLang.Modules.LlmModule
{
	[Description("Ask LLM a question and recieve and answer")]
	public class Program : BaseProgram
	{
		private readonly ILlmServiceFactory llmServiceFactory;
		private readonly IPLangIdentityService identityService;
		private readonly ISettings settings;
		private readonly ILogger logger;

		public Program(ILlmServiceFactory llmServiceFactory, IPLangIdentityService identityService, ISettings settings, ILogger logger) : base()
		{
			this.llmServiceFactory = llmServiceFactory;
			this.identityService = identityService;
			this.settings = settings;
			this.logger = logger;
		}

		public record AskLlmResponse(string Result);

		public async Task AskLlm(
			[HandlesVariable] List<LlmMessage> promptMessages,
			string? scheme = null,
			string model = "gpt-4-turbo",
			double temperature = 0,
			double topP = 0,
			double frequencyPenalty = 0.0,
			double presencePenalty = 0.0,
			int maxLength = 4000,
			bool cacheResponse = true,
			string? llmResponseType = null, 
			string loggerLevel = "trace")
		{

			foreach (var message in promptMessages)
			{
				foreach (var c in message.Content)
				{
					if (c.Text != null)
					{
						var obj = variableHelper.LoadVariables(c.Text);
						c.Text = GetObjectRepresentation(obj);
						
					}

					if (c.ImageUrl != null)
					{
						c.ImageUrl.Url = variableHelper.LoadVariables(c.ImageUrl.Url).ToString();
					}
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

			var response = await llmServiceFactory.CreateHandler().Query<object?>(llmQuestion);

			LogLevel logLevel = LogLevel.Trace;
			Enum.TryParse(loggerLevel, true, out logLevel);
			logger.Log(logLevel, "Llm question - prompt:{0}", JsonConvert.SerializeObject(llmQuestion.promptMessage));
			logger.Log(logLevel, "Llm question - response:{0}", llmQuestion.RawResponse);

			if (response is JObject)
			{
				var objResult = (JObject)response;
				foreach (var property in objResult.Properties())
				{
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
			}

			if (function != null && function.ReturnValue != null && function.ReturnValue.Count > 0)
			{
				foreach (var returnValue in function.ReturnValue)
				{
					memoryStack.Put(returnValue.VariableName, response);
				}
			}
		}

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


	}
}
