using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PLang.Exceptions;
using PLang.Interfaces;
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
		private readonly HttpHelper httpHelper;
		private readonly IPLangFileSystem fileSystem;
		private readonly VariableHelper variableHelper;

		public Program(HttpHelper httpHelper, IPLangFileSystem fileSystem, VariableHelper variableHelper) : base()
		{
			this.httpHelper = httpHelper;
			this.fileSystem = fileSystem;
			this.variableHelper = variableHelper;
		}

		public async Task<object> Post(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 100)
		{
			return await Request(url, "POST", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Patch(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 100)
		{
			return await Request(url, "PATCH", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Get(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 100)
		{
			return await Request(url, "GET", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Option(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 100)
		{
			return await Request(url, "OPTION", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Head(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 100)
		{
			return await Request(url, "HEAD", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Put(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 100)
		{
			return await Request(url, "PUT", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object> Delete(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 100)
		{
			return await Request(url, "DELETE", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}

		[Description("Post a FileStream to url. When a variable is defined with @ sign, it defines that it should be a FileStream.")]
		public async Task<object> PostMultipartFormData(string url, object data, string httpMethod = "POST", bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", int timeoutInSeconds = 100)
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
					httpHelper.SignRequest(request);
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
			Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 100)
		{

			var httpClient = new HttpClient();
			var httpMethod = new HttpMethod(method);
			var request = new HttpRequestMessage(httpMethod, variableHelper.LoadVariables(url).ToString());
			
			if (headers != null)
			{
				foreach (var header in headers)
				{
					var value = variableHelper.LoadVariables(header.Value).ToString();
					request.Headers.TryAddWithoutValidation(header.Key, value);
				}
			}
			request.Headers.UserAgent.ParseAdd("plang v0.1");

			string body = StringHelper.ConvertToString(data);
	
			request.Content = new StringContent(body, System.Text.Encoding.GetEncoding(encoding), contentType);
			if (!doNotSignRequest)
			{
				httpHelper.SignRequest(request);
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
						return JObject.Parse(responseBody);
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


	}
}
