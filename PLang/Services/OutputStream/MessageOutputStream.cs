using Nostr.Client.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Services.OutputStream
{
	public class MessageOutputStream : IOutputStream
	{
		private readonly INostrClient client;

		public MessageOutputStream(INostrClient client) {
			this.client = client;
		}


		public Stream Stream => throw new NotImplementedException();

		public Stream ErrorStream => throw new NotImplementedException();

		public string Output => "text";

		public Task<string> Ask(string text, string type = "ask", int statusCode = 104, Dictionary<string, object>? parameters = null)
		{
			throw new NotImplementedException();
		}

		public string Read()
		{
			throw new NotImplementedException();
		}

		public Task Write(object? obj, string type = "text", int statusCode = 200)
		{
			throw new NotImplementedException();
		}

		public Task WriteToBuffer(object? obj, string type = "text", int statusCode = 200)
		{
			throw new NotImplementedException();
		}
	}
}
