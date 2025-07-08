using Nostr.Client.Client;
using PLang.Building.Model;
using PLang.Errors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.OutputModule.Program;
using static PLang.Utils.StepHelper;

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
		public GoalStep Step { get; set; }

		public string Output => "text";
		public bool IsStateful => false;

		public bool IsFlushed { get; set; }

		public async Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			throw new NotImplementedException();
		}

		public string Read()
		{
			throw new NotImplementedException();
		}

		public Task Write(object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null)
		{
			throw new NotImplementedException();
		}

	}
}
