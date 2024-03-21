using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Asn1;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Services.SigningService;
using PLang.Utils;
using System.ComponentModel;
using System.IO.Abstractions;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Xml;

namespace PLang.Modules.HttpModule
{
	[Description("Make Http request")]
	public class Program(IPLangFileSystem fileSystem, IPLangSigningService signingService, IHttpClientFactory httpClientFactory) : BaseProgram()
	{
		public async Task DownloadFile(string url, string pathToSaveTo, 
			bool overwriteFile = false)
		{
			using (var client = httpClientFactory.CreateClient())
			{
				using (HttpResponseMessage response = await client.GetAsync(url))
				{
					response.EnsureSuccessStatusCode();
					if (fileSystem.File.Exists(pathToSaveTo))
					{
						if (overwriteFile)
						{
							fileSystem.File.Delete(pathToSaveTo);
						} else
						{
							throw new RuntimeStepException($"File already exists at that location", goalStep);
						}
					}

					await using (Stream contentStream = await response.Content.ReadAsStreamAsync(),
								 fileStream = fileSystem.FileStream.New(pathToSaveTo, FileMode.Create, FileAccess.Write, FileShare.None))
					{
						await contentStream.CopyToAsync(fileStream);
					}
				}
			}
		}
		public async Task<object?> Post(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "POST", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object?> Patch(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "PATCH", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object?> Get(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "GET", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object?> Option(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "OPTION", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object?> Head(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "HEAD", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object?> Put(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "PUT", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}
		public async Task<object?> Delete(string url, object? data = null, bool doNotSignRequest = false, Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			return await Request(url, "DELETE", data, doNotSignRequest, headers, encoding, contentType, timeoutInSeconds);
		}

		[Description("Post a FileStream to url. When a variable is defined with @ sign, it defines that it should be a FileStream. data may contain something like file=@%fileName%;type=%fileType%, then keep as one value for the file parameter. The function will parse the file and type")]
		public async Task<object?> PostMultipartFormData(string url, object data, string httpMethod = "POST",
			bool doNotSignRequest = false, Dictionary<string, object>? headers = null,
			string encoding = "utf-8", int timeoutInSeconds = 30)
		{
			using (var httpClient = httpClientFactory.CreateClient())
			{
				httpClient.Timeout = new TimeSpan(0, 0, timeoutInSeconds);

				var requestUrl = variableHelper.LoadVariables(url);
				if (requestUrl == null)
				{
					throw new RuntimeException("url cannot be empty");
				}

				var request = new HttpRequestMessage(new HttpMethod(httpMethod), requestUrl.ToString());

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
							fileName = variableHelper.LoadVariables(fileName).ToString();

							if (fileName.Contains(";"))
							{
								string type = fileName.Substring(fileName.IndexOf(";") + 1);
								typeValue = type.Substring(type.IndexOf("=") + 1);

								string newFileName = fileName.Substring(0, fileName.IndexOf(";"));
								fileName = newFileName; //todo: some compile caching issue, fix, can be removed (I think)
							}
							if (!fileSystem.File.Exists(fileName))
							{
								throw new RuntimeException($"{fileName} could not be found");
							}

							fileStream = fileSystem.FileStream.New(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
							var fileContent = new StreamContent(fileStream);
							if (!string.IsNullOrEmpty(typeValue))
							{
								fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(typeValue);
							}
							else
							{
								var mediaTypeHeader = GetMimeTypeHeader(fileName);
								fileContent.Headers.ContentType = mediaTypeHeader;
							}
							content.Add(fileContent, property.Name, Path.GetFileName(fileStream.Name));
							//content.Headers.Add("Content-Type", "multipart/form-data");
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
						//await SignRequest(request);
					}
					//request.Content.Headers.ContentType = "multipart/form-data";
					request.Content = content;
					try
					{
						var response = await httpClient.SendAsync(request);
						string responseBody = await response.Content.ReadAsStringAsync();

						if (response.Content.Headers.ContentType != null && response.Content.Headers.ContentType.MediaType == "application/json")
						{
							return JsonConvert.DeserializeObject(responseBody);
						}
						else if (response.Content.Headers.ContentType != null && IsXml(response.Content.Headers.ContentType.MediaType))
						{
							// todo: here we convert any xml to json so user can use JSONPath to get the content. 
							// better/faster would be to return the xml object, then when user wants to use json path, it uses xpath.
							XmlDocument xmlDoc = new XmlDocument();
							xmlDoc.LoadXml(responseBody);

							string jsonString = JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented, true);
							return JsonConvert.DeserializeObject(jsonString);

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
		}

		private MediaTypeHeaderValue GetMimeTypeHeader(string fileName)
		{
			string extension = Path.GetExtension(fileName).ToLowerInvariant();

			string mimeType = extension switch
			{
				".mp3" => "audio/mpeg",
				".wav" => "audio/wav",
				".ogg" => "audio/ogg",
				".m4a" => "audio/mp4",
				".aac" => "audio/aac",
				".midi" => "audio/midi",
				".mid" => "audio/midi",
				".flac" => "audio/flac",
				".weba" => "audio/webm",
				".mp4" => "video/mp4",
				".avi" => "video/x-msvideo",
				".mpeg" => "video/mpeg",
				".ogv" => "video/ogg",
				".webm" => "video/webm",
				".3gp" => "video/3gpp",
				".3g2" => "video/3gpp2",
				".mkv" => "video/x-matroska",
				".jpeg, .jpg" => "image/jpeg",
				".png" => "image/png",
				".gif" => "image/gif",
				".bmp" => "image/bmp",
				".svg" => "image/svg+xml",
				".webp" => "image/webp",
				".ico" => "image/vnd.microsoft.icon",
				".tif, .tiff" => "image/tiff",
				".txt" => "text/plain",
				".html, .htm" => "text/html",
				".css" => "text/css",
				".csv" => "text/csv",
				".json" => "application/json",
				".pdf" => "application/pdf",
				".doc" => "application/msword",
				".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
				".xls" => "application/vnd.ms-excel",
				".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
				".ppt" => "application/vnd.ms-powerpoint",
				".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
				".xml" => "application/xml",
				".zip" => "application/zip",
				".tar" => "application/x-tar",
				".rar" => "application/vnd.rar",
				".7z" => "application/x-7z-compressed",
				".js" => "application/javascript",
				".php" => "application/x-httpd-php",
				".bin" => "application/octet-stream",
				_ => "application/octet-stream", // Default MIME type if no match is found

			};

			return MediaTypeHeaderValue.Parse(mimeType);
		}

		private bool IsXml(string? mediaType)
		{
			if (mediaType == null) return false;

			return (mediaType.Contains("application/xml") || mediaType.Contains("text/xml") || mediaType.Contains("application/rss+xml"));
		}

		public async Task<object?> Request(string url, string method, object? data = null, bool doNotSignRequest = false,
			Dictionary<string, object>? headers = null, string encoding = "utf-8", string contentType = "application/json", int timeoutInSeconds = 30)
		{
			var requestUrl = variableHelper.LoadVariables(url);
			if (requestUrl == null)
			{
				throw new RuntimeException("url cannot be empty");
			}
			if (!requestUrl.ToString().ToLower().StartsWith("http"))
			{
				requestUrl = "https://" + requestUrl;
			}

			using (var httpClient = httpClientFactory.CreateClient())
			{
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

				var response = await httpClient.SendAsync(request);
				string responseBody = await response.Content.ReadAsStringAsync();

				if (response.IsSuccessStatusCode)
				{
					if (response.Content.Headers.ContentType?.MediaType == "application/json" && JsonHelper.IsJson(responseBody))
					{
						try
						{
							return JsonConvert.DeserializeObject(responseBody);
						}
						catch (Exception ex)
						{
							throw;
						}
					}
					else if (IsXml(response.Content.Headers.ContentType?.MediaType))
					{
						// todo: here we convert any xml to json so user can use JSONPath to get the content. 
						// better/faster would be to return the xml object, then when user wants to use json path, it uses xpath.
						XmlDocument xmlDoc = new XmlDocument();
						xmlDoc.LoadXml(Regex.Replace(responseBody, "<\\?xml.*?\\?>", "", RegexOptions.IgnoreCase));

						string jsonString = JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented, true);
						return JsonConvert.DeserializeObject(jsonString);

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

		private async Task SignRequest(HttpRequestMessage request)
		{

			if (request.RequestUri == null) return;

			string method = request.Method.Method;
			string url = request.RequestUri.PathAndQuery;
			string contract = "C0";
			string? body = null;

			if (request.Content != null)
			{
				using (var reader = new StreamReader(request.Content.ReadAsStream()))
				{
					body = await reader.ReadToEndAsync();
				}
			}

			var dict = signingService.Sign(body, method, url, contract);
			foreach (var item in dict)
			{
				request.Headers.TryAddWithoutValidation(item.Key, item.Value.ToString());
			}
		}


	}
}
