using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Services;
using PLang.Services.SigningService;
using PLang.Utils;
using System.ComponentModel;
using System.IO.Abstractions;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace PLang.Modules.HttpModule
{
	[Description("Make Http request")]
	public class Program : BaseProgram
	{
		private readonly IPLangFileSystem fileSystem;
		private readonly IPLangSigningService signingService;

		public Program(IPLangFileSystem fileSystem, IPLangSigningService signingService) : base()
		{
			this.fileSystem = fileSystem;
			this.signingService = signingService;
		}

		public async Task<object> Post(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "POST", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Patch(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "PATCH", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Get(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "GET", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Option(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "OPTION", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Head(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "HEAD", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Put(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "PUT", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Delete(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "DELETE", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}

		[Description("Post a FileStream to url. When a variable is defined with @ sign, it defines that it should be a FileStream.")]
		public async Task<object> PostMultipartFormData(string url, object data, string httpMethod = "POST", bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", int timeoutInSeconds = 30)
		{
			var httpClient = new HttpClient();
			httpClient.Timeout = new TimeSpan(0, 0, timeoutInSeconds);
			var request = new HttpRequestMessage(new HttpMethod(httpMethod), variableHelper.LoadVariables(url).ToString());
			using (var content = new MultipartFormDataContent())
			{
				FileSystemStream fileStream = null;
				var properties = JObject.Parse(data.ToString()).Properties();
				foreach (var property in properties)
				{
					if (property.Value == null) continue;

					if (property.Value.ToString().StartsWith("@"))
					{
						string fileName = property.Value.ToString().Substring(1);
						string typeValue = null;
						if (fileName.Contains(";"))
						{
							string type = fileName.Substring(fileName.IndexOf(";") + 1);
							typeValue = type.Substring(type.IndexOf("=") + 1);

							string newFileName = fileName.Substring(0, fileName.IndexOf(";"));
							fileName = newFileName; //todo: some compile caching issue, fix, can be removed (I think)
						}
						fileStream = fileSystem.FileStream.New(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
						var fileContent = new StreamContent(fileStream);
						if (typeValue != null)
						{
							fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(typeValue);
						}
						content.Add(fileContent, property.Name, Path.GetFileName(fileStream.Name));
					}
					else
					{
						content.Add(new StringContent(property.Value.ToString()), property.Name);
					}

				}
				if (headers != null)
				{
					foreach (var header in headers)
					{
						var value = variableHelper.LoadVariables(header.Value).ToString();
						request.Headers.TryAddWithoutValidation(header.Key, value);
					}
				}
				request.Headers.UserAgent.ParseAdd("plang v0.1");
				if (!doNotSignRequest)
				{
					await SignRequest(request);
				}

				request.Content = content;
				try
				{
					var response = await httpClient.SendAsync(request);
					string responseBody = await response.Content.ReadAsStringAsync();

					if (response.Content.Headers.ContentType.MediaType == "application/json")
					{
						return JsonConvert.DeserializeObject(responseBody);
					}
					return responseBody;
				}
				finally
				{
					if (fileStream != null) fileStream.Dispose();
					if (httpClient != null) httpClient.Dispose();
				}

			}
		}

		public async Task<object> Request(string url, string method, object? data = null, bool doNotSignRequest = false, 
			Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			var requestUrl = variableHelper.LoadVariables(url);
			if (requestUrl == null)
			{
				throw new RuntimeException("url cannot be empty");
			}

			var httpClient = new HttpClient();
			var httpMethod = new HttpMethod(method);
			var request = new HttpRequestMessage(httpMethod, requestUrl.ToString());
			
			if (headers != null)
			{
				foreach (var header in headers)
				{
					var value = variableHelper.LoadVariables(header.Value);
					if (value != null)
					{
						request.Headers.TryAddWithoutValidation(header.Key, value.ToString());
					}
				}
			}
			request.Headers.UserAgent.ParseAdd("plang v0.1");

			string body = StringHelper.ConvertToString(data);
	
			request.Content = new StringContent(body, System.Text.Encoding.GetEncoding(encoding), contentType);
			if (!doNotSignRequest)
			{
				await SignRequest(request);
			}
			httpClient.Timeout = new TimeSpan(0, 0, timeoutInSeconds);
			var response = await httpClient.SendAsync(request);

			string responseBody = await response.Content.ReadAsStringAsync();

			if (response.IsSuccessStatusCode)
			{
				if (response.Content.Headers.ContentType?.MediaType == "application/json" && JsonHelper.IsJson(responseBody))
				{
					try
					{
						return JsonConvert.DeserializeObject(responseBody);
					} catch (Exception ex)
					{
						throw;
					}
				} 

				return responseBody;
			}
			else
			{
				throw new RuntimeException(responseBody, goal);
				//throw new HttpRequestException(responseBody, null, response.StatusCode);
			}
		}

		private async Task SignRequest(HttpRequestMessage request)
		{
		
				if (request.Content == null) return;
				if (request.RequestUri == null) return;

				string method = request.Method.Method;
				string url = request.RequestUri.PathAndQuery;
				string contract = "C0";

				using (var reader = new StreamReader(request.Content.ReadAsStream()))
				{
					string body = await reader.ReadToEndAsync();

					var dict = signingService.Sign(body, method, url, contract);
					foreach (var item in dict)
					{
						request.Headers.TryAddWithoutValidation(item.Key, item.Value.ToString());
					}
				}
			
		}
	}
}
