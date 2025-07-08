using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1.Cmp;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Errors.Types;
using PLang.Interfaces;
using PLang.Models;
using PLang.Models.Formats;
using PLang.Utils;
using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace PLang.Modules.HttpModule
{
	[Description("Make Http request")]
	public class Program(IPLangFileSystem fileSystem, IHttpClientFactory httpClientFactory,
		Modules.ProgramFactory programFactory, VariableHelper variableHelper) : BaseProgram()
	{
		private new readonly VariableHelper variableHelper = variableHelper;

		public record HttpRequest(string Url, string Method = "GET", object? Data = null, Dictionary<string, object?>? Headers = null, string Encoding = "utf-8", string ContentType = "text/html", int TimeoutInSeconds = 30, bool SignRequest = true);


		public async Task<(string Path, IError? Error, Properties? Properties)> DownloadFile(string url, string pathToSaveTo,
			bool overwriteFile = false,
			Dictionary<string, object>? headers = null, bool createPathToSaveTo = true, bool doNotDownloadIfFileExists = false)
		{

			var absoluteSaveTo = GetPath(pathToSaveTo);
			if (doNotDownloadIfFileExists && fileSystem.File.Exists(absoluteSaveTo))
			{
				return (absoluteSaveTo, null, null);
			}

			using (var client = httpClientFactory.CreateClient())
			{
				if (headers != null)
				{
					foreach (var header in headers)
					{
						var value = this.variableHelper.LoadVariables(header.Value);
						if (value != null)
						{
							client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, value.ToString());
						}
					}
				}
				client.DefaultRequestHeaders.UserAgent.ParseAdd("plang v0.1");
				using (HttpResponseMessage response = await client.GetAsync(url))
				{
					response.EnsureSuccessStatusCode();

					if (fileSystem.File.Exists(absoluteSaveTo))
					{
						if (overwriteFile)
						{
							fileSystem.File.Delete(absoluteSaveTo);
						}
						else
						{
							var responseObj = GetHttpResponse(response);
							return (absoluteSaveTo, new ProgramError($"File already exists at that location", goalStep, function), responseObj);
						}
					}
					if (createPathToSaveTo)
					{
						string? dirPath = Path.GetDirectoryName(absoluteSaveTo);
						if (dirPath != null && !fileSystem.Directory.Exists(dirPath))
						{
							fileSystem.Directory.CreateDirectory(dirPath);
						}
					}

					await using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
								 fileStream = fileSystem.FileStream.New(absoluteSaveTo, FileMode.Create, FileAccess.Write, FileShare.None))
					{
						await contentStream.CopyToAsync(fileStream);
					}

					var resObj = GetHttpResponse(response);
					return (absoluteSaveTo, null, resObj);
				}
			}
		}

		public async Task<(object? Data, IError? Error, Properties Properties)> Request(HttpRequest request)
		{
			return await Request(request.Url, request.Method, request.Data, !request.SignRequest, request.Headers, request.Encoding, request.ContentType, request.TimeoutInSeconds);
		}
		public async Task<(object? Data, IError? Error, Properties Properties)> Post(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "POST", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<(object? Data, IError? Error, Properties Properties)> Patch(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "PATCH", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<(object? Data, IError? Error, Properties Properties)> Get(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "GET", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<(object? Data, IError? Error, Properties Properties)> Option(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "OPTION", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<(object? Data, IError? Error, Properties Properties)> Head(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "HEAD", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<(object? Data, IError? Error, Properties Properties)> Put(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "PUT", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<(object? Data, IError? Error, Properties Properties)> Delete(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "DELETE", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}

		[Description("Send binary file to server. Make sure to set correct headers on correct header variable, requestHeaders or contentHeader")]
		public async Task<(object? Data, IError? Error, Properties Properties)> SendBinaryOfFile(string url, string filePath, string httpMethod = "POST",
			Dictionary<string, object>? requestHeaders = null, Dictionary<string, object>? contentHeaders = null,
			string encoding = "utf-8", int timeoutInSeconds = 30)
		{
			var requestUrl = this.variableHelper.LoadVariables(url);
			if (requestUrl == null)
			{
				return (null, new ProgramError("url cannot be empty", goalStep, function), null);
			}


			using (var httpClient = httpClientFactory.CreateClient())
			using (var fileStream = File.OpenRead(filePath))
			{
				using (var request = new HttpRequestMessage(new HttpMethod(httpMethod), requestUrl.ToString()))
				{


					if (requestHeaders != null)
					{
						foreach (var header in requestHeaders)
						{
							var value = this.variableHelper.LoadVariables(header.Value).ToString();
							request.Headers.TryAddWithoutValidation(header.Key, value);
						}
					}
					request.Headers.UserAgent.ParseAdd("plang v0.1");
					var content = new StreamContent(fileStream);

					content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
					if (contentHeaders != null)
					{
						foreach (var header in contentHeaders)
						{
							var value = this.variableHelper.LoadVariables(header.Value).ToString();
							content.Headers.TryAddWithoutValidation(header.Key, value);
						}
					}

					request.Content = content;
					using (var response = await httpClient.SendAsync(request))
					{

						if (response.IsSuccessStatusCode)
						{

							string responseBody = await response.Content.ReadAsStringAsync();
							var responseObj = GetHttpResponse(response);
							return (responseBody, null, responseObj);
						}
						else
						{

							var errorDetails = await response.Content.ReadAsStringAsync();
							var responseObj = GetHttpResponse(response);
							return (null, new ProgramError(errorDetails, goalStep, function), responseObj);
						}
					}
				}
			}
		}

		[Description("Post a FileStream to url. When a variable is defined with @ sign, it defines that it should be a FileStream. data may contain something like file=@%fileName%;type=%fileType%, then keep as one value for the file parameter. The function will parse the file and type")]
		public async Task<(object? Data, IError? Error, Properties? Properties)> PostMultipartFormData(string url, object data, string httpMethod = "POST",
			bool doNotSignRequest = false, Dictionary<string, object>? headers = null,
			string encoding = "utf-8", int timeoutInSeconds = 30)
		{
			using (var httpClient = httpClientFactory.CreateClient())
			{
				httpClient.Timeout = new TimeSpan(0, 0, timeoutInSeconds);

				var requestUrl = this.variableHelper.LoadVariables(url);
				if (requestUrl == null)
				{
					return (null, new ProgramError("url cannot be empty", goalStep, function), null);
				}

				using (var request = new HttpRequestMessage(new HttpMethod(httpMethod), requestUrl.ToString()))
				{

					using (var content = new MultipartFormDataContent())
					{
						Stream? fileStream = null;
						var properties = JObject.Parse(data.ToString()).Properties();
						foreach (var property in properties)
						{
							if (property.Value == null) continue;

							if (property.Value.ToString().StartsWith("@"))
							{
								string fileName = property.Value.ToString().Substring(1);
								string typeValue = null;
								fileName = this.variableHelper.LoadVariables(fileName).ToString();

								if (fileName != null && fileName.Contains(";"))
								{
									string type = fileName.Substring(fileName.IndexOf(";") + 1);
									typeValue = type.Substring(type.IndexOf("=") + 1);

									string newFileName = fileName.Substring(0, fileName.IndexOf(";"));
									fileName = newFileName; //todo: some compile caching issue, fix, can be removed (I think)
								}
								if (!fileSystem.File.Exists(fileName))
								{
									if (IsBase64(fileName, out byte[]? bytes))
									{
										fileStream = new MemoryStream(bytes, 0, bytes.Length);
										fileName = Guid.NewGuid().ToString();
									}
									else
									{
										return (null, new ProgramError($"{fileName} could not be found", goalStep, function, StatusCode: 404), null);
									}
								}
								else
								{

									fileStream = fileSystem.FileStream.New(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
								}
								var fileContent = new StreamContent(fileStream);
								if (!string.IsNullOrEmpty(typeValue))
								{
									fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(typeValue);
								}
								else
								{
									var mediaTypeHeader = GetMimeTypeHeader(fileName);
									fileContent.Headers.ContentType = mediaTypeHeader;
								}
								content.Add(fileContent, property.Name, Path.GetFileName(fileName));
								fileStream?.Dispose();
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
								var value = this.variableHelper.LoadVariables(header.Value).ToString();
								request.Headers.TryAddWithoutValidation(header.Key, value);
							}
						}
						request.Headers.UserAgent.ParseAdd("plang v0.1");
						if (!doNotSignRequest)
						{
							//await SignRequest(request);
						}
						//request.Content.Headers.ContentType = "multipart/form-data";
						request.Content = content;
						try
						{
							using (var response = await httpClient.SendAsync(request))
							{
								string responseBody = await response.Content.ReadAsStringAsync();

								if (response.Content.Headers.ContentType != null && response.Content.Headers.ContentType.MediaType == "application/json")
								{
									var responseObj = GetHttpResponse(response);
									return (JsonConvert.DeserializeObject(responseBody), null, responseObj);
								}
								else if (response.Content.Headers.ContentType != null && IsXml(response.Content.Headers.ContentType.MediaType))
								{
									// todo: here we convert any xml to json so user can use JSONPath to get the content. 
									// better/faster would be to return the xml object, then when user wants to use json path, it uses xpath.
									XmlDocument xmlDoc = new XmlDocument();
									xmlDoc.LoadXml(responseBody);

									string jsonString = JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented, true);
									var responseObj = GetHttpResponse(response);
									return (JsonConvert.DeserializeObject(jsonString), null, responseObj);

								}

								var resObj = GetHttpResponse(response);
								return (responseBody, null, resObj);
							}
						}
						finally
						{
							if (fileStream != null) fileStream.Dispose();
							if (httpClient != null) httpClient.Dispose();
						}
					}
				}

			}
		}

		private bool IsBase64(string value, out byte[]? bytes)
		{
			bytes = null;
			value = value.Trim();
			if (value.Length % 4 != 0)
			{
				return false;
			}

			try
			{
				bytes = Convert.FromBase64String(value);
				return true;
			}
			catch (FormatException)
			{
				return false;
			}
		}

		private System.Net.Http.Headers.MediaTypeHeaderValue GetMimeTypeHeader(string fileName)
		{
			string mimeType = MimeTypeHelper.GetMimeType(fileName);

			return System.Net.Http.Headers.MediaTypeHeaderValue.Parse(mimeType);
		}

		private bool IsXml(string? mediaType)
		{
			if (mediaType == null) return false;

			return (mediaType.Contains("application/xml") || mediaType.Contains("text/xml") || mediaType.Contains("application/rss+xml"));
		}

		public virtual async Task<(object? Data, IError? Error, Properties Properties)> Request(string url, string method, object? data = null, bool doNotSignRequest = false,
			Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{

			var requestUrl = this.variableHelper.LoadVariables(url);
			if (requestUrl == null)
			{
				return (null, new ProgramError("url cannot be empty", goalStep, function), null);
			}
			if (!requestUrl.ToString().ToLower().StartsWith("http"))
			{
				requestUrl = "https://" + requestUrl;
			}

			using var httpClient = httpClientFactory.CreateClient();
			var httpMethod = new HttpMethod(method);
			using var request = new HttpRequestMessage(httpMethod, requestUrl.ToString());

			if (headers != null)
			{
				foreach (var header in headers)
				{
					var value = this.variableHelper.LoadVariables(header.Value);
					if (value != null)
					{
						request.Headers.TryAddWithoutValidation(header.Key, value.ToString());
					}
				}
			}
			if (context.ContainsKey("!callback"))
			{
				request.Headers.TryAddWithoutValidation("!callback", JsonConvert.SerializeObject(context["!callback"]));
			}
			if (context.ContainsKey("!contract"))
			{
				request.Headers.TryAddWithoutValidation("!contract", JsonConvert.SerializeObject(context["!contract"]));
			}
			if (request.Headers.Accept.Count == 0)
			{
				request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
			}

			request.Headers.UserAgent.ParseAdd("plang v0.1");
			if (data != null)
			{
				string body = StringHelper.ConvertToString(data);

				request.Content = new StringContent(body, System.Text.Encoding.GetEncoding(encoding), contentType);
			}

			if (!doNotSignRequest)
			{
				await SignRequest(request);
			}

			httpClient.Timeout = new TimeSpan(0, 0, timeoutInSeconds);
			Properties? properties = null;

			var task = httpClient.SendAsync(request);
			try
			{

				using var response = await task;

				var mediaType = response.Content.Headers.ContentType?.MediaType;
				if (!response.IsSuccessStatusCode)
				{
					
					var result = await programFactory.GetProgram<SerializerModule.Program>(goalStep).Deserialize(response.Content.ReadAsStream(), mediaType);
					if (result.Error != null && result.Error.Key != "SerializerNotDefined") return (null, result.Error, GetHttpResponse(response));


					string responseStr = await response.Content.ReadAsStringAsync();
					var obj = result.Object as Dictionary<string, object?>;
					if (obj == null)
					{						
						if (string.IsNullOrEmpty(responseStr))
						{
							responseStr = $"{response.ReasonPhrase} ({(int)response.StatusCode})";
						}
						properties = GetHttpResponse(response);
						return (result.Object, new ProgramError(responseStr, goalStep, function, StatusCode: (int)response.StatusCode), properties);
					}

					var signatureJson = obj.FirstOrDefault(p => p.Key.Equals("signature", StringComparison.OrdinalIgnoreCase));
					if (signatureJson.Key != null)
					{
						var signature = await programFactory.GetProgram<IdentityModule.Program>(goalStep).VerifySignature(signatureJson.Value.ToString());
						if (signature.Error != null) return (obj.ToString(), signature.Error, GetHttpResponse(response));

						if (signature.Signature != null)
						{
							context.AddOrReplace(ReservedKeywords.Signature, signature.Signature);
							context.AddOrReplace(ReservedKeywords.ServiceIdentity, signature.Signature.Identity);
						}
					}
					var responseJson = obj.FirstOrDefault(p => p.Key.Equals("response", StringComparison.OrdinalIgnoreCase));
					if (responseJson.Key != null)
					{
						var jsonRpc = JsonConvert.DeserializeObject<JsonRpcErrorResponse>(responseJson.Value.ToString());
						if (jsonRpc != null && jsonRpc.Error != null)
						{
							return (obj.ToString(), await GetError(jsonRpc.Error), GetHttpResponse(response));
						}
					}

					if (string.IsNullOrEmpty(responseStr))
					{
						responseStr = $"{response.ReasonPhrase} ({(int)response.StatusCode})";
					}
					properties = GetHttpResponse(response);
					return (null, new ProgramError(responseStr, goalStep, function, StatusCode: (int)response.StatusCode), properties);
				}

				if (!IsTextResponse(mediaType))
				{
					var bytes = await response.Content.ReadAsByteArrayAsync();
					properties = GetHttpResponse(response);
					return (bytes, null, properties);
				}

				string responseBody = await response.Content.ReadAsStringAsync();
				if (response.Content.Headers.ContentType?.MediaType == "application/json" && JsonHelper.IsJson(responseBody))
				{
					properties = GetHttpResponse(response);
					try
					{
						return (JsonConvert.DeserializeObject(responseBody), null, properties);
					}
					catch (Exception ex)
					{
						return (null, new ProgramError(ex.Message, goalStep, function), properties);
					}
				}
				else if (IsXml(response.Content.Headers.ContentType?.MediaType))
				{
					// todo: here we convert any xml to json so user can use JSONPath to get the content. 
					// better/faster would be to return the xml object, then when user wants to use json path, it uses xpath.
					XmlDocument xmlDoc = new XmlDocument();
					xmlDoc.LoadXml(Regex.Replace(responseBody, "<\\?xml.*?\\?>", "", RegexOptions.IgnoreCase));

					string jsonString = JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented, true);
					properties = GetHttpResponse(response);
					return (JsonConvert.DeserializeObject(jsonString), null, properties);

				}
				properties = GetHttpResponse(response);

				return (responseBody, null, properties);

			}
			catch (System.Net.Http.HttpRequestException ex)
			{
				int statusCode = (int?)ex.StatusCode ?? 503;
				return (null, new ProgramError(ex.Message, goalStep, function, StatusCode: statusCode), properties);
			}
			catch (Exception ex)
			{
				return (null, new ProgramError(ex.Message, goalStep, function, Exception: ex), null);
			}


		}

		private async Task<IError?> GetError(JsonRpcError error)
		{
			if (error.Code == 402)
			{
				var paymentRequest = JsonConvert.DeserializeObject<PaymentRequest>(error.Data.ToString());

				return new PaymentRequestError(goalStep, paymentRequest.contract, error.Message, Callback: paymentRequest.Callback);
			}

			var param = new Dictionary<string, object?>();
			param.Add("data", error.Data);
			return new ProgramError(error.Message, goalStep, function, param, "HttpError");
		}

		private static Dictionary<string, object?> GetHeadersObject(HttpHeaders headers)
		{

			Dictionary<string, object?> headerDict = new();
			foreach (var item in headers)
			{
				if (headerDict.ContainsKey(item.Key))
				{
					object obj;
					if (item.Value.FirstOrDefault() is string str)
					{
						obj = new List<string?> { str, item.Value.ToString() };
					}
					else
					{
						obj = item.Value.FirstOrDefault();
					}

					headerDict[item.Key] = obj;
				}
				else
				{
					headerDict.Add(item.Key, item.Value.ToList());
				}
			}
			return headerDict;
		}
		private static Properties GetHttpResponse(HttpResponseMessage response)
		{
			var httpResponse = new Models.HttpResponse()
			{
				IsSuccess = response.IsSuccessStatusCode,
				ReasonPhrase = response.ReasonPhrase,
				StatusCode = (int)response.StatusCode
			};

			httpResponse.Headers = GetHeadersObject(response.Headers);
			httpResponse.ContentHeaders = GetHeadersObject(response.Content.Headers);

			Properties properties = new();
			properties.Add(new Runtime.ObjectValue("Response", httpResponse));
			return properties;
		}

		private bool IsTextResponse(string? mediaType)
		{
			if (mediaType == null) return false;

			if (mediaType.Contains("text/")) return true;

			if (mediaType.Contains("application/"))
			{
				string[] possibleTextTypes = { "json", "xml", "html", "javascript", "x-yaml", "rtf", "toml", "x-latex", "sgml", "ecmascript", "x-sh", "x-perl", "x-python", "x-ruby" };
				foreach (var item in possibleTextTypes)
				{
					if (mediaType.Contains(item)) return true;
				}
			}
			return false;
		}

		private async Task SignRequest(HttpRequestMessage request)
		{

			if (request.RequestUri == null) return;

			string method = request.Method.Method;
			string url = request.RequestUri.PathAndQuery;
			string[] contracts = ["C0"];
			string? body = null;

			if (request.Content != null)
			{
				using (var reader = new StreamReader(request.Content.ReadAsStream()))
				{
					body = await reader.ReadToEndAsync();
				}
			}

			var headers = new Dictionary<string, object>();
			headers.Add("url", url);
			headers.Add("method", method);

			var identity = programFactory.GetProgram<Modules.IdentityModule.Program>(goalStep);
			var signature = await identity.Sign(body, contracts: contracts.ToList(), headers: headers);
			var json = JsonConvert.SerializeObject(signature);
			var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

			request.Headers.TryAddWithoutValidation("X-Signature", base64);

		}


	}
}
