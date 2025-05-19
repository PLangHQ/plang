using PLang.Runtime;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public interface IOutputStream
	{
		public Stream Stream { get; }
		public Stream ErrorStream { get; }
		public string Output { get; }
		public Task Write(object? obj, string type = "text", int statusCode = 200, Dictionary<string, object?>? paramaters = null);
        public Task WriteToBuffer(object? obj, string type = "text", int statusCode = 200);
        public string Read();
        public Task<string> Ask(string text, string type = "text", int statusCode = 200, Dictionary<string, object>? parameters = null, Callback? callback = null);

    }

    
}
