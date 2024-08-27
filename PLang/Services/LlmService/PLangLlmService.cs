﻿using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.AskUser;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream;
using PLang.Services.SigningService;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.Reflection;
using System.Text;

namespace PLang.Services.LlmService
{
	public class PLangLlmService : ILlmService
	{
		private readonly LlmCaching llmCaching;
		private readonly IOutputSystemStreamFactory outputSystemStreamFactory;
		private readonly IPLangSigningService signingService;
		private readonly ILogger logger;
		private readonly PLangAppContext context;
		private readonly IPLangFileSystem fileSystem;
		private readonly MemoryStack memoryStack;
		private string url = "https://llm.plang.is/api/Llm";
		private readonly string appId = "206bb559-8c41-4c4a-b0b7-283ef73dc8ce";
		private readonly string BuyCreditInfo = @"You need to purchase credits to use Plang LLM service, click this link to purchase: {0}. Run again after payment.

Make sure to backup the folder {1} as it contains your private key. If you loose your private key your account at Plang will be lost";

		public IContentExtractor Extractor { get; set; }

		public PLangLlmService(LlmCaching llmCaching, IOutputSystemStreamFactory outputSystemStreamFactory, IPLangSigningService signingService,
			ILogger logger, PLangAppContext context, IPLangFileSystem fileSystem, MemoryStack memoryStack)
		{
			this.llmCaching = llmCaching;
			this.outputSystemStreamFactory = outputSystemStreamFactory;
			this.signingService = signingService;
			this.logger = logger;
			this.context = context;
			this.fileSystem = fileSystem;
			this.memoryStack = memoryStack;
			this.Extractor = new JsonExtractor();

			//Only for development of plang
			var plangLlmService = Environment.GetEnvironmentVariable("PLangLllmServiceUrl");
			if (!string.IsNullOrEmpty(plangLlmService) && plangLlmService.StartsWith("http"))
			{
				url = plangLlmService;
			}
		}


		public virtual async Task<(T?, IError?)> Query<T>(LlmRequest question)
		{
			var result = await Query(question, typeof(T));
			if (result.Item2 != null) return (default(T), result.Item2);

			if (result.Item1 is T?)
			{
				return ((T?)result.Item1, result.Item2);
			}

			return (default(T), new ServiceError($@"Answer from LLM was not valid. 
LlmRequest:{question}

The answer was:{result.Item1}", GetType(), "LlmService"));
		}

		public virtual async Task<(object?, IError?)> Query(LlmRequest question, Type responseType)
		{
			return await Query(question, responseType, 0);
		}
		public virtual async Task<(object?, IError?)> Query(LlmRequest question, Type responseType, int errorCount = 0)
		{
			Extractor = ExtractorFactory.GetExtractor(question, responseType);
			AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool isDebug);
			var cachedLlmQuestion = llmCaching.GetCachedQuestion(appId, question);
			if (!question.Reload && question.caching && cachedLlmQuestion != null && cachedLlmQuestion.RawResponse != null)
			{
				try
				{
					if (isDebug)
					{
						context.AddOrReplace(ReservedKeywords.Llm, cachedLlmQuestion.RawResponse);
					}

					var result = Extractor.Extract(cachedLlmQuestion.RawResponse, responseType);
					if (result != null && !string.IsNullOrEmpty(result.ToString()))
					{
						return (result, null);
					}

				}
				catch { }
			}

			Dictionary<string, object?> parameters = new();
			parameters.Add("messages", question.promptMessage);
			parameters.Add("temperature", question.temperature);
			parameters.Add("top_p", question.top_p);
			parameters.Add("model", question.model);
			parameters.Add("frequency_penalty", question.frequencyPenalty);
			parameters.Add("presence_penalty", question.presencePenalty);
			parameters.Add("type", question.type);
			parameters.Add("maxLength", question.maxLength);
			parameters.Add("responseType", question.llmResponseType);

			var assembly = Assembly.GetAssembly(this.GetType());
			if (assembly != null && assembly.GetName().Version != null)
			{
				parameters.Add("buildVersion", assembly.GetName().Version?.ToString());
			}
			var httpClient = new HttpClient();
			var httpMethod = new HttpMethod("POST");
			var request = new HttpRequestMessage(httpMethod, url);
			request.Headers.UserAgent.ParseAdd("plang llm v0.1");

			string body = StringHelper.ConvertToString(parameters);

			request.Content = new StringContent(body, Encoding.GetEncoding("utf-8"), "application/json");
			httpClient.Timeout = new TimeSpan(0, 5, 0);
			await SignRequest(request);

			var response = await httpClient.SendAsync(request);

			string responseContent = await response.Content.ReadAsStringAsync();
			if (string.IsNullOrWhiteSpace(responseContent))
			{
				return (null, new ServiceError("llm.plang.is appears to be down. Try again in few minutes. If it does not come back up soon, check out our Discord https://discord.gg/A8kYUymsDD for a chat", this.GetType()));
			}			

			var rawResponse = JsonConvert.DeserializeObject(responseContent)?.ToString() ?? "";
			question.RawResponse = rawResponse;
			if (isDebug)
			{
				context.AddOrReplace(ReservedKeywords.Llm, rawResponse);
			}

			if (response.IsSuccessStatusCode)
			{
				ShowCosts(response);

				var obj = Extractor.Extract(rawResponse, responseType);
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

			if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
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
					return (null, new AskUserError(@$"You need to purchase credits to use Plang LLM service. Lets do this now.
(If you have OpenAI API key, you can run 'plang {strIsBuilder} --llmservice=openai')

What is name of payer?", GetCountry));
				}
			}

			return (null, new ServiceError(rawResponse, GetType()));


		}


		private void ShowCosts(HttpResponseMessage response)
		{
			string? costWarning = "";
			if (response.Headers.Contains("X-User-Balance"))
			{
				string strBalance = response.Headers.GetValues("X-User-Balance").FirstOrDefault();
				if (strBalance != null && long.TryParse(strBalance, out long balance))
				{
					costWarning += "$" + (((double)balance) / 1000000).ToString("N2");
					memoryStack.Put("__LLM_Balance__", balance);
				}


			}
			if (response.Headers.Contains("X-User-Used"))
			{
				string strUsed = response.Headers.GetValues("X-User-Used").FirstOrDefault();
				if (strUsed != null && long.TryParse(strUsed, out long used))
				{
					costWarning += " - used now $" + (((double)used) / 1000000).ToString("N6");
					memoryStack.Put("__LLM_Used__", used);
				}
			}

			if (response.Headers.Contains("X-User-PaymentUrl"))
			{
				string strUrl = response.Headers.GetValues("X-User-PaymentUrl").FirstOrDefault();
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
				var error = new AskUserError("Name cannot be empty.\n\nWhat is name of payer?", GetCountry);
				return (false, error);
			}

			object[] nameArray = (object[])value;
			if (nameOfPayer == "" && (nameArray == null || string.IsNullOrEmpty(nameArray[0].ToString())))
			{
				var error = new AskUserError("Name cannot be empty.\n\nWhat is name of payer?", GetCountry);
				return (false, error);
			}

			if (nameOfPayer == "")
			{
				nameOfPayer = nameArray[0].ToString();
			}

			return (false, new AskUserError("What is your two letter country? (e.g. US, UK, FR, ...)", async (object[]? countryArray) =>
			{
				if (countryArray == null || countryArray.Length == 0 || string.IsNullOrEmpty(countryArray[0].ToString()))
				{
					return (false, new AskUserError("Country must be legal 2 country code.\n\nWhat is your two letter country? (e.g. US, UK, FR, ...)?", GetCountry));
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
					await outputSystemStreamFactory.CreateHandler().Write(string.Format(BuyCreditInfo, obj["url"], dbLocation), "error", 402);
					return (false, new ErrorHandled(new Error("ErrorHandled")));
				}
				else
				{
					if (obj["status"] != null && obj["status"]["error_code"] != null && obj["status"]["error_code"].ToString().Contains("COUNTRY"))
					{
						return (false, new AskUserError("Country must be legal 2 country code.\n\nWhat is your two letter country? (e.g. US, UK, FR, ...)?", GetCountry));
					}
					return (false, new AskUserError("Could not create url. Lets try again. What is your name?", GetCountry));
				}
			}));
		}

		private async Task<string> DoPlangRequest(object[] countryArray)
		{
			var country = countryArray[0].ToString();
			var requestUrl = url.Replace("api/Llm", "").TrimEnd('/');
			var httpClient = new HttpClient();
			var httpMethod = new HttpMethod("POST");
			var request = new HttpRequestMessage(httpMethod, requestUrl + "/api/GetOrCreatePaymentLink");
			request.Headers.UserAgent.ParseAdd("plang llm v0.1");
			Dictionary<string, object?> parameters = new();
			parameters.Add("name", nameOfPayer);
			parameters.Add("country", country);
			string body = StringHelper.ConvertToString(parameters);

			request.Content = new StringContent(body, Encoding.GetEncoding("utf-8"), "application/json");
			httpClient.Timeout = new TimeSpan(0, 0, 30);
			await SignRequest(request);

			var response = await httpClient.SendAsync(request);

			return await response.Content.ReadAsStringAsync();
		}

		public async Task SignRequest(HttpRequestMessage request)
		{
			string method = request.Method.Method;
			string url = request.RequestUri?.PathAndQuery ?? "/";
			string contract = "C0";
			string? body = null;
			if (request.Content != null)
			{
				using (var reader = new StreamReader(request.Content!.ReadAsStream(), leaveOpen: true))
				{
					body = await reader.ReadToEndAsync();
				}
			}

			var signature = signingService.Sign(body, method, url, contract, appId);

			foreach (var item in signature)
			{
				request.Headers.TryAddWithoutValidation(item.Key, item.Value.ToString());
			}
		}


		public async Task<(object?, IError?)> GetBalance()
		{
			var requestUrl = url.Replace("api/Llm", "").TrimEnd('/');
			var httpClient = new HttpClient();
			var httpMethod = new HttpMethod("GET");
			var request = new HttpRequestMessage(httpMethod, requestUrl + "/api/Balance.goal");
			request.Headers.UserAgent.ParseAdd("plang llm v0.1");

			httpClient.Timeout = new TimeSpan(0, 0, 30);
			await SignRequest(request);

			var response = await httpClient.SendAsync(request);

			var content = await response.Content.ReadAsStringAsync();
			return (content, null);
		}
	}
}
