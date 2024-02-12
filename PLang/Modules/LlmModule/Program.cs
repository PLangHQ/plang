using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
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
		private readonly ILlmService llmService;
		private readonly IPLangIdentityService identityService;

		public Program(ILlmService llmService, IPLangIdentityService identityService) : base()
		{
			this.llmService = llmService;
			this.identityService = identityService;
		}

		public record AskLlmResponse(string Result);
		
		public async Task AskLlm(
			[HandlesVariable] List<LlmMessage> promptMessages,
			string? scheme = null,
			string model = "gpt-4",
			double temperature = 0,
			double topP = 0,
			double frequencyPenalty = 0.0,
			double presencePenalty = 0.0,
			int maxLength = 4000,
			bool cacheResponse = true,
			string? llmResponseType = null)
		{

			foreach (var message in promptMessages)
			{
				foreach (var c in message.Content)
				{
					if (c.Text != null)
					{
						c.Text = variableHelper.LoadVariables(c.Text).ToString();
					} else if (c.ImageUrl != null)
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

			var response = await llmService.Query<object>(llmQuestion);

			if (response is JObject)
			{
				var objResult = (JObject)response;
				foreach (var property in objResult.Properties())
				{
					if (property.Value is JValue)
					{
						var value = ((JValue)property.Value).Value;
						memoryStack.Put(property.Name, value);
					} else
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

		public async Task<string> GetLlmIdentity()
		{
			identityService.UseSharedIdentity(llmService.GetType().FullName);
			return identityService.GetCurrentIdentity().Identifier;
		}


	}
}
