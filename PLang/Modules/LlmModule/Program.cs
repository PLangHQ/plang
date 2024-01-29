using Newtonsoft.Json.Linq;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Interfaces;
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

		public Program(ILlmService llmService) : base()
		{
			this.llmService = llmService;
		}

		public record AskLlmResponse(string Result);
		/*
		[Description("")]
		public async Task AskLlm(
			string scheme = "",
			string? system = null, string? assistant = null, string? user = null,
			string model = "gpt-4",
			double temperature = 0,
			double topP = 0,
			double frequencyPenalty = 0.0,
			double presencePenalty = 0.0,
			int maxLength = 4000,
			bool cacheResponse = true,
			string? llmResponseType = null)
		{
			if (llmResponseType == "text")
			{
				llmService.Extractor = new TextExtractor();
			}
			else if (llmResponseType == "json")
			{
				system += $"\n\nYou MUST respond in JSON, scheme: {scheme}";
			} else
			{
				llmService.Extractor = new GenericExtractor(llmResponseType); 
			}

			user = LoadVariables(user) ?? "";
			system = LoadVariables(system);
			assistant = LoadVariables(assistant);
			
			var llmQuestion = new LlmQuestion("LlmModule", system, user, assistant, model, cacheResponse);
			int tokenLength = user.Length + ((system == null) ? 0 : system.Length) + ((assistant == null) ? 0 : assistant.Length) / 4;
			int ml = maxLength + tokenLength;
			if (ml > maxLength)
			{
				ml = maxLength - (ml - maxLength);
			}

			llmQuestion.maxLength = maxLength;
			llmQuestion.temperature = temperature;
			llmQuestion.top_p = topP;
			llmQuestion.frequencyPenalty = frequencyPenalty;
			llmQuestion.presencePenalty = presencePenalty;
			
			var response = await llmService.Query(llmQuestion, typeof(ExpandoObject));

			if (scheme.StartsWith("{") && scheme.EndsWith("}"))
			{
				var variables = scheme.Replace("{", "").Replace("}", "").Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
				var objResult = (IDictionary<string, object>)response;
				foreach (var variable in variables)
				{
					string varName = (variable.Contains(":")) ? variable.Substring(0, variable.IndexOf(":")) : variable;
					if (objResult.TryGetValue(varName, out object? val))
					{
						memoryStack.Put(varName, val);
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

			llmService.Extractor = new JsonExtractor();
		}
		*/

		[Description("")]
		public async Task AskLlm(
			[HandlesVariable] List<Message> promptMessages,
			string scheme = "", 
			string model = "gpt-4",
			double temperature = 0,
			double topP = 0,
			double frequencyPenalty = 0.0,
			double presencePenalty = 0.0,
			int maxLength = 4000,
			bool cacheResponse = true,
			string? llmResponseType = null)
		{
			if (llmResponseType == "text")
			{
				llmService.Extractor = new TextExtractor();
			}
			else if (llmResponseType == "json" || !string.IsNullOrEmpty(scheme))
			{
				var systemMessage = promptMessages.FirstOrDefault(p => p.role == "system");
				if (systemMessage == null)
				{
					systemMessage = new Message() { role = "system", content = new() };
				}
				systemMessage.content.Add(new Content() { text = $"You MUST respond in JSON, scheme: {scheme}" });
			}
			else
			{
				llmService.Extractor = new GenericExtractor(llmResponseType);
			}
			foreach (var message in promptMessages)
			{
				foreach (var c in message.content)
				{
					c.text = variableHelper.LoadVariables(c.text).ToString();
				}

			}

			var llmQuestion = new LlmRequest("LlmModule", promptMessages, model, cacheResponse);
			llmQuestion.maxLength = maxLength;
			llmQuestion.temperature = temperature;
			llmQuestion.top_p = topP;
			llmQuestion.frequencyPenalty = frequencyPenalty;
			llmQuestion.presencePenalty = presencePenalty;

			var response = await llmService.Query<object>(llmQuestion);

			if (response is JObject)
			{
				var objResult = (JObject)response;
				foreach (var property in objResult.Properties())
				{
					memoryStack.Put(property.Name, property.Value);
				}
			}
			else if (function != null && function.ReturnValue != null && function.ReturnValue.Count > 0)
			{
				foreach (var returnValue in function.ReturnValue)
				{
					memoryStack.Put(returnValue.VariableName, response);
				}
			}

			
		}

		private string? LoadVariables(string? content)
		{
			if (content == null) return null;

			var variables = variableHelper.GetVariables(content);
			foreach (var variable in variables)
			{
				var varValue = memoryStack.Get(variable.Key);
				if (varValue != null)
				{
					content = content.Replace(variable.OriginalKey, varValue.ToString());
				}
			}
			return content;
		}



	}
}
