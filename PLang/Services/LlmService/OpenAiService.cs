using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenAI.Chat;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.LlmService;
using PLang.Utils.Extractors;
using System.Text;

namespace PLang.Services.OpenAi
{
	public class OpenAiService : ILlmService
	{
		private readonly ISettings settings;
		private readonly ILogger logger;
		private readonly LlmCaching llmCaching;
		private readonly PLangAppContext context;

		protected string appId = "7d3112c4-d4a1-462b-bf83-417bb4f02994";
		protected string url = "https://api.openai.com/v1/chat/completions";
		protected string settingKey = "OpenAiKey";


		public IContentExtractor Extractor { get; set; }

		public OpenAiService(ISettings settings, ILogger logger, LlmCaching llmCaching, PLangAppContext context)
		{
			this.logger = logger;
			this.llmCaching = llmCaching;
			this.context = context;
			this.settings = settings;

			this.Extractor = new JsonExtractor();
			if (this.GetType() != typeof(OpenAiService) && appId == "7d3112c4-d4a1-462b-bf83-417bb4f02994")
			{
				appId = "";
			}
		}


		public virtual async Task<(T? Response, IError? Error)> Query<T>(LlmRequest question) where T : class
		{
			var result = await Query(question, typeof(T));
			return ((T?)result.Item1, result.Item2);
		}
		public virtual async Task<(object? Response, IError? Error)> Query(LlmRequest question, Type responseType)
		{
			return await Query(question, responseType, 0);
		}
		public virtual async Task<(object? Response, IError? Error)> Query(LlmRequest question, Type responseType, int errorCount)
		{
			Extractor = ExtractorFactory.GetExtractor(question, responseType);

			var q = llmCaching.GetCachedQuestion(appId, question);
			if (!question.Reload && question.caching && q != null && q.RawResponse != null)
			{
				try
				{
					question.RawResponse = q.RawResponse;
					return (Extractor.Extract(q.RawResponse, responseType), null);

				}
				catch { }
			}

			var httpClient = new HttpClient();
			var httpMethod = new HttpMethod("POST");
			var request = new HttpRequestMessage(httpMethod, url);
			string? bearer = null;
			try
			{
				bearer = settings.Get(this.GetType(), settingKey, "", "Type in API key for LLM service");
			} catch { }
			if (string.IsNullOrEmpty(bearer))
			{
				settings.SetSharedSettings(appId);
				bearer = settings.Get(this.GetType(), settingKey, "", "Type in API key for LLM service");
				settings.SetSharedSettings(null);
			}
			
			string data = $@"{{
		""prompt_cache_key"": ""{question.CacheKey}"",
		""model"":""{question.model}"",
		""temperature"":{question.temperature},
		""max_tokens"":{question.maxLength},
		""top_p"":{question.top_p},
		""frequency_penalty"":{question.frequencyPenalty},
		""presence_penalty"":{question.presencePenalty},
		""messages"":{JsonConvert.SerializeObject(question.promptMessage)}
			}}";
			request.Headers.UserAgent.ParseAdd("plang v0.1");
			request.Headers.Add("Authorization", $"Bearer {bearer}");
			request.Content = new StringContent(data, Encoding.GetEncoding("UTF-8"), "application/json");
			httpClient.Timeout = new TimeSpan(0, 5, 0);
			try
			{

				using (var response = await httpClient.SendAsync(request))
				{

					string responseBody = await response.Content.ReadAsStringAsync();
					if (!response.IsSuccessStatusCode)
					{
						return (null, new ServiceError(responseBody, this.GetType()));
					}

					var json = JsonConvert.DeserializeObject<dynamic>(responseBody);
					if (json == null || json.choices == null || json.choices.Count == 0)
					{
						return (null, new ServiceError("Could not parse Llm response: " + responseBody, this.GetType(),
							HelpfulLinks: "This error should not happen under normal circumstances, please report the issue https://github.com/PLangHQ/plang/issues"
							));
					}

					question.RawResponse = json.choices[0].message.content.ToString();

					var obj = Extractor.Extract(question.RawResponse, responseType);
					if (question.caching)
					{
						llmCaching.SetCachedQuestion(appId, question);
					}
					return (obj, null);
				}
			}
			catch (Exception ex)
			{
				if (errorCount < 3)
				{
					string assitant = $@"
### error in your response ###
I could not deserialize your response. This is the error. Please try to fix it.
{ex.ToString()}
### error in your response ###
";
					question.promptMessage.Add(new LlmMessage("system", assitant));

					var qu = new LlmRequest(question.type, question.promptMessage, question.model, question.caching);
					return await Query(qu, responseType, ++errorCount);
				}
				return (null, new ServiceError(ex.Message, this.GetType(), Exception: ex));

			} finally
			{
				request.Dispose();
				httpClient.Dispose();
			}
		}


	}
}
