using Newtonsoft.Json;
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
	public class BinaryOutputStream : IOutputStream
	{

		private readonly Stream stream;
		private readonly Encoding encoding;
		private bool isStateful;
		private readonly int bufferSize;

		public BinaryOutputStream(Stream stream, Encoding encoding, bool isStateful, int bufferSize = 4096)
		{
			this.stream = stream;
			this.encoding = encoding;
			this.isStateful = isStateful;
			this.bufferSize = bufferSize;
		}
		public Stream Stream { get { return this.stream; } }
		public Stream ErrorStream { get { return this.stream; } }
		public GoalStep Step { get; set; }
		public IEngine Engine { get; set; }
		public string Output => "json";

		public bool IsStateful => isStateful;

		public bool IsFlushed { get; set; }

		public async Task<(object?, IError?)> Ask(AskOptions askOptions, Callback? callback = null, IError? error = null)
		{
			
			return (null, null);
		}

		public string Read()
		{
			return "";
		}

		public async Task Write(object? obj, string type, int httpStatusCode = 200, Dictionary<string, object?>? paramaters = null)
		{
			if (obj is IError)
			{
				await Stream.FlushAsync();
				IsFlushed = true;

			} else
			{
				throw new NotImplementedException($"obj.type:{obj?.GetType()} | obj:{obj}");
			}
				

		}

	}
}
