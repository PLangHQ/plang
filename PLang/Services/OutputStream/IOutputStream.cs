using PLang.Errors;
using PLang.Runtime;
using static PLang.Modules.OutputModule.Program;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public interface IOutputStream
	{
		public Stream Stream { get; }
		public Stream ErrorStream { get; }
		public string Output { get; }
		public bool IsStateful { get; }
		public bool IsFlushed { get; set; }

		public Task Write(object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null);
        public string Read();
        public Task<(string?, IError?)> Ask(string text, string type = "text", int statusCode = 200, Dictionary<string, object>? parameters = null, Callback? callback = null, List<Option>? options = null);

    }

    
}
