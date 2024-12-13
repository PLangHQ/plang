using PLang.Runtime;

namespace PLang.Services.OutputStream
{
	public interface IOutputStream
	{
		public Stream Stream { get; }
		public Stream ErrorStream { get; }
		public Task Write(object? obj, string type = "text", int statusCode = 200);
        public Task WriteToBuffer(object? obj, string type = "text", int statusCode = 200);
        public string Read();
        public Task<string> Ask(string text, string type = "text", int statusCode = 200, Dictionary<string, object>? parameters = null);

    }

    
}
