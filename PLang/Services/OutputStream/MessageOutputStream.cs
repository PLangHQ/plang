using Nostr.Client.Client;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Runtime;
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

		public string Output => "text";
		public bool IsStateful => false;

		public bool IsFlushed { get; set; }
		public IEngine Engine { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public async Task<(object?, IError?)> Ask(GoalStep step, AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			throw new NotImplementedException();
		}

		public string Read()
		{
			throw new NotImplementedException();
		}

		public Task Write(GoalStep step, object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null)
		{
			throw new NotImplementedException();
		}

	}
}
