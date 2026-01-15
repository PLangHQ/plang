using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Runtime;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules;
using PLang.Runtime;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.Reflection;
using System.Text;
using HttpResponse = PLang.Models.HttpResponse;

namespace PLang.Services.LlmService
{
	public class PLangLlmService : ILlmService
	{
		private readonly LlmCaching llmCaching;

		private readonly ILogger logger;
		private readonly PLangAppContext context;
		private readonly IPLangFileSystem fileSystem;
		private readonly MemoryStack memoryStack;
		private readonly Modules.IdentityModule.Program signer; 
		private readonly Modules.HttpModule.Program http;
		
		private string url = "https://llm.plang.is/api/Llm";
		private string? modelOverwrite = null;
		private readonly string appId = "206bb559-8c41-4c4a-b0b7-283ef73dc8ce";
		private readonly string BuyCreditInfo = @"You need to purchase credits to use Plang LLM service, click this link to purchase: {0}. Run again after payment.

Make sure to backup the folder {1} as it contains your private key. If you loose your private key your account at Plang will be lost";

		public IContentExtractor Extractor { get; set; }

		public PLangLlmService(LlmCaching llmCaching, Modules.IdentityModule.Program signer,
			ILogger logger, PLangAppContext context, IPLangFileSystem fileSystem, IMemoryStackAccessor memoryStackAccessor, Modules.HttpModule.Program http)
		{
			this.llmCaching = llmCaching;
			this.signer = signer;
			this.logger = logger;
			this.context = context;
			this.fileSystem = fileSystem;
			this.memoryStack = memoryStackAccessor.Current;
			this.http = http;
			
			this.Extractor = new JsonExtractor();

			//Only for development of plang
			var plangLlmService = Environment.GetEnvironmentVariable("PLangLlmServiceUrl");
			if (!string.IsNullOrEmpty(plangLlmService) && plangLlmService.StartsWith("http"))
			{
				url = plangLlmService;
			}

			var model = Environment.GetEnvironmentVariable("PLangLlmModelOverwrite");
			if (!string.IsNullOrEmpty(model))
			{
				modelOverwrite = model;
			}
		}


		public virtual async Task<(T? Response, IError? Error)> Query<T>(LlmRequest question) where T : class
		{
			var result = await Query(question, typeof(T));
			if (result.Item2 != null) return (default(T), result.Item2);

			if (result.Item1 is T && result.Item1 != null)
			{
				return ((T?)result.Item1, result.Item2);
			}

			return (default(T), new ServiceError($@"Answer from LLM was not valid. 
LlmRequest:{question}

The answer was:{result.Item1}", GetType(), "LlmService"));
		}

		public virtual async Task<(object? Response, IError? Error)> Query(LlmRequest question, Type responseType)
		{
			return await Query(question, responseType, 0);
		}
		public virtual async Task<(object? Response, IError? Error)> Query(LlmRequest question, Type responseType, int errorCount = 0)
		{
			Extractor = ExtractorFactory.GetExtractor(question, responseType);
			AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool isDebug);

			if (question.model.StartsWith("o"))
			{
				for (int i = 0; i < question.promptMessage.Count; i++)
				{
					if (question.promptMessage[i].Role == "system" || question.promptMessage[i].Role == "assistant")
					{
						question.promptMessage[i].Role = "developer";
					}
				}
			}

			var cachedLlmQuestion = llmCaching.GetCachedQuestion(appId, question);
			if (!question.Reload && question.caching && cachedLlmQuestion != null && cachedLlmQuestion.RawResponse != null)
			{
				try
				{
					if (isDebug)
					{
						context.AddOrReplace(ReservedKeywords.Llm, cachedLlmQuestion.RawResponse);
					}
					logger.LogTrace("Using cached response from LLM:" + cachedLlmQuestion.RawResponse);

					var result2 = Extractor.Extract(cachedLlmQuestion.RawResponse, responseType, null);
					if (result2 != null && !string.IsNullOrEmpty(result2.ToString()))
					{
						question.RawResponse = cachedLlmQuestion.RawResponse;
						return (result2, null);
					}

				}
				catch { }
			}



			Dictionary<string, object?> parameters = new();
			parameters.Add("messages", question.promptMessage);
			parameters.Add("temperature", question.temperature);
			parameters.Add("top_p", question.top_p);
			parameters.Add("model", (modelOverwrite == null) ? question.model : modelOverwrite);
			parameters.Add("frequency_penalty", question.frequencyPenalty);
			parameters.Add("presence_penalty", question.presencePenalty);
			parameters.Add("type", question.type);
			if (question.model.ToLower().StartsWith("o"))
			{
				parameters.Add("max_completion_tokens", question.maxLength);
			}
			else
			{
				parameters.Add("maxLength", question.maxLength);
			}
			parameters.Add("responseType", question.llmResponseType);

			var assembly = Assembly.GetAssembly(this.GetType());
			if (assembly != null && assembly.GetName().Version != null)
			{
				parameters.Add("buildVersion", assembly.GetName().Version?.ToString());
			}

			var result = await http.Post(url, parameters);
			if (result.Error != null) return (result.Data, result.Error);

			string? responseContent = result.Data?.ToString();


			if (string.IsNullOrWhiteSpace(responseContent))
			{
				return (null, new ServiceError("llm.plang.is appears to be down. Try again in few minutes. If it does not come back up soon, check out our Discord https://discord.gg/A8kYUymsDD for a chat", this.GetType()));
			}
			logger.LogTrace("LLM response:" + responseContent);

			string? rawResponse = null;
			try
			{
				rawResponse = JsonConvert.DeserializeObject(responseContent)?.ToString() ?? "";
			}
			catch (Exception ex)
			{
				throw new Exception($"Error parsing JSON response from LLM. The response was {responseContent}", ex);
			}

			question.RawResponse = rawResponse;
			if (isDebug)
			{
				context.AddOrReplace(ReservedKeywords.Llm, rawResponse);
			}
			Models.HttpResponse? hr = null;
			if (result.Properties is IDictionary<string, object?> properties && properties.TryGetValue("Response", out object? resp)) 
			{
				hr = resp as HttpResponse;

				ShowCosts(hr);

				var obj = Extractor.Extract(rawResponse, responseType, question.Tools);
				if (obj == null)
				{
					return (null, new ServiceError(rawResponse, this.GetType()));
				}
				if (question.caching)
				{

					llmCaching.SetCachedQuestion(appId, question);
				}
				return (obj, null);
			}

			if (hr != null && hr.StatusCode == (int) System.Net.HttpStatusCode.PaymentRequired)
			{
				var obj = JObject.Parse(rawResponse);
				if (obj != null && obj["url"]?.ToString() != "")
				{
					string dbLocation = Path.Join(fileSystem.SharedPath, appId);

					return (null, new ServiceError(string.Format(BuyCreditInfo, obj["url"], dbLocation), GetType(), ContinueBuild: false));
				}
				else
				{
					AppContext.TryGetSwitch("Builder", out bool isBuilder);
					string strIsBuilder = (isBuilder) ? " build" : "";
					return (null, new AskUserError("system", "default", @$"You need to purchase credits to use Plang LLM service. Lets do this now.
(If you have OpenAI API key, you can run 'plang {strIsBuilder} --llmservice=openai')

What is name of payer?", GetCountry));
				}
			}

			return (null, new ServiceError(rawResponse, GetType()));


		}


		private void ShowCosts(Models.HttpResponse response)
		{
			string? costWarning = "";
			if (response.Headers.ContainsKey("X-User-Balance"))
			{
				string strBalance = response.Headers["X-User-Balance"].ToString();
				if (strBalance != null && long.TryParse(strBalance, out long balance))
				{
					costWarning += "$" + (((double)balance) / 1000000).ToString("N2");
					memoryStack.Put("__LLM_Balance__", balance);
				}


			}
			if (response.Headers.ContainsKey("X-User-Used"))
			{
				string strUsed = response.Headers["X-User-Used"].ToString();
				if (strUsed != null && long.TryParse(strUsed, out long used))
				{
					costWarning += " - used now $" + (((double)used) / 1000000).ToString("N6");
					memoryStack.Put("__LLM_Used__", used);
				}
			}

			if (response.Headers.ContainsKey("X-User-PaymentUrl"))
			{
				string strUrl = response.Headers["X-User-PaymentUrl"].ToString();
				if (!string.IsNullOrEmpty(strUrl))
				{
					costWarning += $" - add to balance: {strUrl}";
					memoryStack.Put("__LLM_PaymentUrl__", strUrl);
				}
			}

			if (!string.IsNullOrEmpty(costWarning))
			{
				logger.LogWarning($"Current balance with LLM service: {costWarning}");
			}
		}

		private string nameOfPayer = "";
		private async Task<(bool, IError?)> GetCountry(object? value)
		{
			if (value == null)
			{
				var error = new AskUserError("system", "default", "Name cannot be empty.\n\nWhat is name of payer?", GetCountry);
				return (false, error);
			}

			object[] nameArray = (object[])value;
			if (nameOfPayer == "" && (nameArray == null || string.IsNullOrEmpty(nameArray[0].ToString())))
			{
				var error = new AskUserError("system", "default", "Name cannot be empty.\n\nWhat is name of payer?", GetCountry);
				return (false, error);
			}

			if (nameOfPayer == "")
			{
				nameOfPayer = nameArray[0].ToString();
			}

			return (false, new AskUserError("system", "default", "What is your two letter country? (e.g. US, UK, FR, ...)", async (object[]? countryArray) =>
			{
				if (countryArray == null || countryArray.Length == 0 || string.IsNullOrEmpty(countryArray[0].ToString()))
				{
					return (false, new AskUserError("system", "default", "Country must be legal 2 country code.\n\nWhat is your two letter country? (e.g. US, UK, FR, ...)?", GetCountry));
				}

				var responseBody = await DoPlangRequest(countryArray);
				if (string.IsNullOrEmpty(responseBody))
				{
					return (false, new ServiceError("Got empty response from llm service. Service might be down, try again later", GetType()));
				}
				var obj = JObject.Parse(responseBody);
				if (obj["url"] != null)
				{
					string dbLocation = Path.Join(fileSystem.SharedPath, appId);
					//await outputSystemStreamFactory.CreateHandler().Write(string.Format(BuyCreditInfo, obj["url"], dbLocation), "error", 402);
					return (false, new ExceptionError(new Error(string.Format(BuyCreditInfo, obj["url"], dbLocation), "PaymentRequired", StatusCode: 402)));
				}
				else
				{
					if (obj["status"] != null && obj["status"]["error_code"] != null && obj["status"]["error_code"].ToString().Contains("COUNTRY"))
					{
						return (false, new AskUserError("system", "default", "Country must be legal 2 country code.\n\nWhat is your two letter country? (e.g. US, UK, FR, ...)?", GetCountry));
					}
					return (false, new AskUserError("system", "default", "Could not create url. Lets try again. What is your name?", GetCountry));
				}
			}));
		}

		private async Task<string> DoPlangRequest(object[] countryArray)
		{
			var country = countryArray[0].ToString();
			var requestUrl = url.Replace("api/Llm", "").TrimEnd('/');
			using (var httpClient = new HttpClient())
			{
				var httpMethod = new HttpMethod("POST");
				using (var request = new HttpRequestMessage(httpMethod, requestUrl + "/api/GetOrCreatePaymentLink"))
				{
					request.Headers.UserAgent.ParseAdd("plang llm v0.1");
					Dictionary<string, object?> parameters = new();
					parameters.Add("name", nameOfPayer);
					parameters.Add("country", country);
					string body = StringHelper.ConvertToString(parameters);

					request.Content = new StringContent(body, Encoding.GetEncoding("utf-8"), "application/json");
					httpClient.Timeout = new TimeSpan(0, 0, 30);
					await SignRequest(request);

					using (var response = await httpClient.SendAsync(request))
					{

						return await response.Content.ReadAsStringAsync();
					}
				}
			}
		}

		public async Task SignRequest(HttpRequestMessage request)
		{
			string method = request.Method.Method;
			string url = request.RequestUri?.PathAndQuery ?? "/";

			string? body = null;
			if (request.Content != null)
			{
				using (var reader = new StreamReader(request.Content!.ReadAsStream(), leaveOpen: true))
				{
					body = await reader.ReadToEndAsync();
				}
			}

			var result = await signer.Sign(body, ["C0"], 60 * 5, new Dictionary<string, object> { { "url", url }, { "method", method } });
			var json = JsonConvert.SerializeObject(result);
			var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
			request.Headers.TryAddWithoutValidation("X-Signature", base64);

		}


	}
}
