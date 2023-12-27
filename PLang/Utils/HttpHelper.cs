using PLang.Interfaces;
using PLang.Modules.BlockchainModule;
using PLang.Runtime;
using PLang.Services.SettingsService;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	public class HttpHelper
	{
		private readonly ISettings settings;
		private readonly PLangAppContext context;
		private readonly ILlmService aiService;
		private readonly Signature signature;

		public HttpHelper(ISettings settings, PLangAppContext context, ILlmService aiService, Signature signature)
		{
			this.settings = settings;
			this.context = context;
			this.aiService = aiService;
			this.signature = signature;
		}

		public void SignRequest(HttpRequestMessage request)
		{
			if (request.Content == null) return;

			string method = request.Method.Method;
			string url = request.RequestUri.PathAndQuery;
			string contract = "C0";			

			using (var reader = new StreamReader(request.Content.ReadAsStream()))
			{
				string body = reader.ReadToEnd();

				var dict = signature.Sign(body, method, url, contract);
				foreach (var item in dict)
				{
					request.Headers.TryAddWithoutValidation(item.Key, item.Value);
				}
			}
		}

		public bool VerifySignature(HttpListenerRequest request, string body, MemoryStack memoryStack)
		{
			if (request.Headers.Get("X-Signature") == null ||
				request.Headers.Get("X-Signature-Created") == null ||
				request.Headers.Get("X-Signature-Nonce") == null ||
				request.Headers.Get("X-Signature-Address") == null ||
				request.Headers.Get("X-Signature-Contract") == null
				) return false;

			var validationHeaders = new Dictionary<string, string>();
			validationHeaders.Add("X-Signature", request.Headers.Get("X-Signature"));
			validationHeaders.Add("X-Signature-Created", request.Headers.Get("X-Signature-Created"));
			validationHeaders.Add("X-Signature-Nonce", request.Headers.Get("X-Signature-Nonce"));
			validationHeaders.Add("X-Signature-Address", request.Headers.Get("X-Signature-Address"));
			validationHeaders.Add("X-Signature-Contract", request.Headers.Get("X-Signature-Contract") ?? "C0");

			var url = request.Url.PathAndQuery;

			string address = signature.VerifySignature(body, request.HttpMethod, url, validationHeaders);
			if (address != null)
			{
				memoryStack.Put(ReservedKeywords.Identity, address.ComputeHash(salt: context[ReservedKeywords.Salt].ToString()));
				memoryStack.Put(ReservedKeywords.IdentityNotHashed, address);
				return true;
			}
			return false;
		}
	}
}
