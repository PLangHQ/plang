using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Services.OutputStream;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.Text;

namespace PLang.Services.LlmService
{
	public class PLangLlmService : ILlmService
	{
		private readonly CacheHelper cacheHelper;
		private readonly PLangAppContext context;
		private readonly IPLangFileSystem fileSystem;
		private readonly ISettingsRepository settingsRepository;
		private readonly IOutputStream outputStream;

		public IContentExtractor Extractor { get; set; }

		public PLangLlmService(CacheHelper cacheHelper, PLangAppContext context, IPLangFileSystem fileSystem, ISettingsRepository settingsRepository, IOutputStream outputStream)
		{
			this.cacheHelper = cacheHelper;
			this.context = context;
			this.fileSystem = fileSystem;
			this.settingsRepository = settingsRepository;
			this.outputStream = outputStream;
			this.Extractor = new JsonExtractor();

		}


		public virtual async Task<T?> Query<T>(LlmRequest question)
		{
			return (T)await Query(question, typeof(T));
		}

		public virtual async Task<object?> Query(LlmRequest question, Type responseType)
		{
			return await Query(question, responseType, 0);
		}
		public virtual async Task<object?> Query(LlmRequest question, Type responseType, int errorCount = 0)
		{
			
			var cachedLlmQuestion = cacheHelper.GetCachedQuestion(question);
			if (!question.Reload && question.caching && cachedLlmQuestion != null)
			{
				try
				{
					var result = Extractor.Extract(cachedLlmQuestion.RawResponse, responseType);
					if (result != null) return result;
				}
				catch { }
			}

			Dictionary<string, object?> parameters = new();
			parameters.Add("messages", question.promptMessage);
			parameters.Add("temperature", question.temperature);
			parameters.Add("top_p", question.top_p);
			parameters.Add("model", question.model);
			parameters.Add("type", question.type);
			parameters.Add("maxLength", question.maxLength);
			var httpClient = new HttpClient();
			var httpMethod = new HttpMethod("POST");
			var request = new HttpRequestMessage(httpMethod, "http://localhost:10000/api/llm");
			request.Headers.UserAgent.ParseAdd("plang llm v0.1");

			string body = StringHelper.ConvertToString(parameters);

			request.Content = new StringContent(body, Encoding.GetEncoding("utf-8"), "application/json");
			httpClient.Timeout = new TimeSpan(0, 5, 0);
			SignRequest(request);

			var response = await httpClient.SendAsync(request);

			string responseBody = await response.Content.ReadAsStringAsync();

			if (response.IsSuccessStatusCode)
			{
				var obj = Extractor.Extract(responseBody, responseType);

				if (question.caching)
				{
					question.RawResponse = responseBody;
					cacheHelper.SetCachedQuestion(question);
				}
				return obj;
			}

			if (response.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
			{
				var obj = JObject.Parse(responseBody);
				if (obj["url"].ToString() != "")
				{
					await outputStream.Write("You can buy more voucher at this url: " + obj["url"] + ". Restart after payment", "error", 402);
				}
				else
				{
					throw new AskUserConsole("You need to fill up your account at plang.is. Lets do this now.\n\nWhat is name of payer?", GetCountry);
				}
			}


			throw new HttpRequestException(responseBody, null, response.StatusCode);


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
				var request = new HttpRequestMessage(httpMethod, "http://localhost:10000/api/GetOrCreatePaymentLink");
				request.Headers.UserAgent.ParseAdd("plang llm v0.1");
				Dictionary<string, object?> parameters = new();
				parameters.Add("name", nameOfPayer);
				parameters.Add("country", country);
				string body = StringHelper.ConvertToString(parameters);

				request.Content = new StringContent(body, Encoding.GetEncoding("utf-8"), "application/json");
				httpClient.Timeout = new TimeSpan(0, 0, 30);
				SignRequest(request);

				var response = await httpClient.SendAsync(request);

				string responseBody = await response.Content.ReadAsStringAsync();
				var obj = JObject.Parse(responseBody);
				if (obj["url"] != null)
				{
					await outputStream.Write("You can buy more voucher at this url: " + obj["url"] + ". Restart after payment", "error", 402);
				} else
				{
					throw new AskUserConsole("Could not create url. Lets try again. What is your name?", GetCountry);
				}
			});
		}


		public void SignRequest(HttpRequestMessage request)
		{
			string method = request.Method.Method;
			string url = request.RequestUri.PathAndQuery;
			string contract = "C0";
			using (var reader = new StreamReader(request.Content.ReadAsStream()))
			{
				string body = reader.ReadToEnd();

				var dict = Sign(body, method, url, contract);
				foreach (var item in dict)
				{
					request.Headers.TryAddWithoutValidation(item.Key, item.Value);
				}
			}
		}

		private Dictionary<string, string> Sign(string data, string method, string url, string contract)
		{
			var dict = new Dictionary<string, string>();
			DateTime created = SystemTime.UtcNow();
			string nonce = Guid.NewGuid().ToString();
			string dataToSign = StringHelper.CreateSignatureData(method, url, created.ToFileTimeUtc(), nonce, data, contract);

			var settings = new Settings(settingsRepository, fileSystem);

			var p = new Modules.BlockchainModule.Program(settings, context, null, null, null, null, null);
			string signedMessage = p.SignMessage(dataToSign).Result;
			string address = p.GetCurrentAddress().Result;

			dict.Add("X-Signature", signedMessage);
			dict.Add("X-Signature-Contract", contract);
			dict.Add("X-Signature-Created", created.ToFileTimeUtc().ToString());
			dict.Add("X-Signature-Nonce", nonce);
			dict.Add("X-Signature-Address", address);
			return dict;
		}







		/* All this is depricated */
		public virtual async Task<T?> Query<T>(LlmQuestion question)
		{
			return (T)await Query(question, typeof(T));
		}

		public virtual async Task<object?> Query(LlmQuestion question, Type responseType)
		{
			return await Query(question, responseType, 0);
		}


		private class Message
		{
			public Message()
			{
				content = new();
			}
			public string role { get; set; }
			public List<Content> content { get; set; }
		}
		private class Content
		{
			public string type = "text";
			public string text { get; set; }
		}
		public virtual async Task<object?> Query(LlmQuestion question, Type responseType, int errorCount = 0)
		{
			// todo: should remove this function, should just use LlmRequest.
			// old setup, and should be removed.
			var promptMessage = new List<Message>();
			if (!string.IsNullOrEmpty(question.system))
			{
				var contents = new List<Content>();
				contents.Add(new Content
				{
					text = question.system
				});
				promptMessage.Add(new Message()
				{
					role = "system",
					content = contents
				});
			}
			if (!string.IsNullOrEmpty(question.assistant))
			{
				var contents = new List<Content>();
				contents.Add(new Content
				{
					text = question.assistant
				});
				promptMessage.Add(new Message()
				{
					role = "assistant",
					content = contents
				});
			}
			if (!string.IsNullOrEmpty(question.question))
			{
				var contents = new List<Content>();
				contents.Add(new Content
				{
					text = question.question
				});
				promptMessage.Add(new Message()
				{
					role = "user",
					content = contents
				});
			}

			LlmRequest llmRequest = new LlmRequest(question.type, JsonConvert.SerializeObject(promptMessage), question.model, question.caching);
			return await Query(llmRequest, responseType);

		}
	}
}
