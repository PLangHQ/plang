using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Services.OutputStream;
using PLang.Services.SigningService;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.Text;

namespace PLang.Services.LlmService
{
	public class PLangLlmService : ILlmService
	{
		private readonly LlmCaching llmCaching;
		private readonly IOutputStreamFactory outputStreamFactory;
		private readonly IPLangSigningService signingService;
		private readonly ILogger logger;
		private readonly PLangAppContext context;
		private readonly IPLangFileSystem fileSystem;
		private readonly string url = "https://llm.plang.is";
		//private readonly string url = "http://localhost:10000";
		private readonly string appId = "206bb559-8c41-4c4a-b0b7-283ef73dc8ce";

		public IContentExtractor Extractor { get; set; }

		public PLangLlmService(LlmCaching llmCaching, IOutputStreamFactory outputStreamFactory, IPLangSigningService signingService, ILogger logger, PLangAppContext context, IPLangFileSystem fileSystem)
		{
			this.llmCaching = llmCaching;
			this.outputStreamFactory = outputStreamFactory;
			this.signingService = signingService;
			this.logger = logger;
			this.context = context;
			this.fileSystem = fileSystem;
			this.Extractor = new JsonExtractor();
		}


		public virtual async Task<T?> Query<T>(LlmRequest question)
		{
			return (T?)await Query(question, typeof(T));
		}

		public virtual async Task<object?> Query(LlmRequest question, Type responseType)
		{
			return await Query(question, responseType, 0);
		}
		public virtual async Task<object?> Query(LlmRequest question, Type responseType, int errorCount = 0)
		{
			Extractor = ExtractorFactory.GetExtractor(question, responseType);
			AppContext.TryGetSwitch(ReservedKeywords.Debug, out bool isDebug);
			var cachedLlmQuestion = llmCaching.GetCachedQuestion(question);			
			if (!question.Reload && question.caching && cachedLlmQuestion != null)
			{
				try
				{
					if (isDebug)
					{
						context.AddOrReplace(ReservedKeywords.Llm, cachedLlmQuestion.RawResponse);
					}

					var result = Extractor.Extract(cachedLlmQuestion.RawResponse, responseType);
					if (result != null && !string.IsNullOrEmpty(result.ToString())) return result;
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
			var httpClient = new HttpClient();
			var httpMethod = new HttpMethod("POST");
			var request = new HttpRequestMessage(httpMethod, url + "/api/Llm");
			request.Headers.UserAgent.ParseAdd("plang llm v0.1");

			string body = StringHelper.ConvertToString(parameters);

			request.Content = new StringContent(body, Encoding.GetEncoding("utf-8"), "application/json");
			httpClient.Timeout = new TimeSpan(0, 5, 0);
			await SignRequest(request);

			var response = await httpClient.SendAsync(request);

			string responseBody = await response.Content.ReadAsStringAsync();
			if (isDebug)
			{
				context.AddOrReplace(ReservedKeywords.Llm, responseBody);
			}

			if (response.IsSuccessStatusCode)
			{
				ShowCosts(response);
				
				var obj = Extractor.Extract(responseBody, responseType);

				if (question.caching)
				{
					question.RawResponse = responseBody;
					llmCaching.SetCachedQuestion(question);
				}
				return obj;
			}

			if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
			{
				var obj = JObject.Parse(responseBody);
				if (obj != null && obj["url"].ToString() != "")
				{
					await outputStreamFactory.CreateHandler().Write("You need to fill up your account at plang.is. You can buy at this url: " + obj["url"] + ". Try again after payment", "error", 402);
					throw new StopBuilderException();
				}
				else
				{
					throw new AskUserConsole("You need to fill up your account at plang.is. Lets do this now.\n\nWhat is name of payer?", GetCountry);
				}
			}


			throw new HttpRequestException(responseBody, null, response.StatusCode);


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
				}
			}
			if (response.Headers.Contains("X-User-Used"))
			{
				string strUsed = response.Headers.GetValues("X-User-Used").FirstOrDefault();
				if (strUsed != null && long.TryParse(strUsed, out long used))
				{
					costWarning += " - used now $" + (((double)used) / 1000000).ToString("N2");
				}
			}

			if (response.Headers.Contains("X-User-PaymentUrl"))
			{
				string strUrl = response.Headers.GetValues("X-User-PaymentUrl").FirstOrDefault();
				if (!string.IsNullOrEmpty(strUrl))
				{
					costWarning += $" - add to balance: {strUrl}";
				}
			}			
			
			if (!string.IsNullOrEmpty(costWarning))
			{
				logger.LogWarning($"Current balance with LLM service: {costWarning}");
			}
		}

		private string nameOfPayer = "";
		private Task GetCountry(object value)
		{
			object[] nameArray = (object[])value;
			if (nameOfPayer == "" && (nameArray == null || string.IsNullOrEmpty(nameArray[0].ToString())))
			{
				throw new AskUserConsole("Name cannot be empty.\n\nWhat is name of payer?", GetCountry);
			}

			if (nameOfPayer == "") { 
				nameOfPayer = nameArray[0].ToString();
			}

			throw new AskUserConsole("What is your two letter country? (e.g. US, UK, FR, ...)", async (object? value) =>
			{
				object[] countryArray = (object[])value;
				if (value == null || string.IsNullOrEmpty(countryArray[0].ToString()))
				{
					throw new AskUserConsole("Country must be legal 2 country code.\n\nWhat is your two letter country? (e.g. US, UK, FR, ...)?", GetCountry);
				}
				var country = countryArray[0].ToString();

				var httpClient = new HttpClient();
				var httpMethod = new HttpMethod("POST");
				var request = new HttpRequestMessage(httpMethod, url + "/api/GetOrCreatePaymentLink");
				request.Headers.UserAgent.ParseAdd("plang llm v0.1");
				Dictionary<string, object?> parameters = new();
				parameters.Add("name", nameOfPayer);
				parameters.Add("country", country);
				string body = StringHelper.ConvertToString(parameters);

				request.Content = new StringContent(body, Encoding.GetEncoding("utf-8"), "application/json");
				httpClient.Timeout = new TimeSpan(0, 0, 30);
				await SignRequest(request);

				var response = await httpClient.SendAsync(request);

				string responseBody = await response.Content.ReadAsStringAsync();
				var obj = JObject.Parse(responseBody);
				if (obj["url"] != null)
				{
					await outputStreamFactory.CreateHandler().Write("You can buy more voucher at this url: " + obj["url"] + ". Try again after payment.", "error", 402);
					throw new StopBuilderException();
				} else
				{
					throw new AskUserConsole("Could not create url. Lets try again. What is your name?", GetCountry);
				}
			});
		}


		public async Task SignRequest(HttpRequestMessage request)
		{
			string method = request.Method.Method;
			string url = request.RequestUri?.PathAndQuery ?? "/";
			string contract = "C0";
			using (var reader = new StreamReader(request.Content!.ReadAsStream(), leaveOpen: true))
			{
				string body = await reader.ReadToEndAsync();
				
				var signature = signingService.Sign(body, method, url, contract, appId);

				foreach (var item in signature)
				{
					request.Headers.TryAddWithoutValidation(item.Key, item.Value.ToString());
				}
			}
		}

	}
}
