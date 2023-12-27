using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Building.Model;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.SafeFileSystem;
using PLang.Services.SettingsService;
using PLang.Utils;
using PLang.Utils.Extractors;
using System.Text;
using static PLang.Runtime.Startup.ModuleLoader;

namespace PLang.Services.LlmService
{
	public class PLangLlmService : ILlmService
	{
		private readonly CacheHelper cacheHelper;
		private readonly PLangAppContext context;


		public IContentExtractor Extractor { get; set; }

		public PLangLlmService(CacheHelper cacheHelper, PLangAppContext context)
		{
			this.cacheHelper = cacheHelper;
			this.context = context;
			this.Extractor = new JsonExtractor();

		}

		public virtual async Task<T?> Query<T>(LlmQuestion question)
		{
			return (T)await Query(question, typeof(T));
		}

		public virtual async Task<object?> Query(LlmQuestion question, Type responseType)
		{
			return await Query(question, responseType, 0);
		}
		public virtual async Task<object?> Query(LlmQuestion question, Type responseType, int errorCount = 0)
		{

			var cachedLlmQuestion = cacheHelper.GetCachedQuestion(question);
			if (!question.Reload && question.caching && cachedLlmQuestion != null)
			{
				try
				{
					return Extractor.Extract(cachedLlmQuestion.RawResponse, responseType);
				}
				catch { }
			}

			Dictionary<string, object?> parameters = new();
			parameters.Add("user", question.question);
			parameters.Add("system", question.system);
			parameters.Add("assistant", question.assistant);
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
					Console.WriteLine("You can buy more voucher at this url: " + obj["url"] + ". Restart after payment");
				}
				else
				{
					throw new AskUserConsole("You need to fill up your account at plang.is. Lets do this now.\n\nWhat is name of payer?", GetCountry);
				}
			}

			
			throw new HttpRequestException(response.ReasonPhrase, null, response.StatusCode);
			

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
					Console.WriteLine("You can buy more voucher at this url: " + obj["url"] + ". Restart after payment");
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

			
			var fileSystem = new PLangFileSystem(Settings.GlobalPath, "/");
			var settingsRepository = new SqliteSettingsRepository(fileSystem, context);

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
	}
}
