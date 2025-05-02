
using System.Text;
using System.Threading.Tasks;
using static PLang.Utils.StepHelper;

namespace PLang.Services.OutputStream
{
	public class MemoryOutputStream : MemoryStream, IOutputStream, IDisposable
	{
		MemoryStream errorStream;
		public MemoryOutputStream()
		{
			errorStream = new MemoryStream();
		}
		public Stream Stream => this;

		public Stream ErrorStream => errorStream;

		public string Output => "text";

		public new void Dispose() {
			base.Dispose();
			errorStream.Dispose();
		}

		public Task<string> Ask(string text, string type = "text", int statusCode = 200, Dictionary<string, object>? parameters = null, Callback? callback = null)
		{
			throw new NotImplementedException();
		}

		public string Read()
		{
			return this.Read();
		}

		public async Task Write(object? obj, string type = "text", int statusCode = 200)
		{
			if (obj == null) return;
			var bytes = Encoding.Default.GetBytes(obj.ToString());
			this.Write(bytes, 0, bytes.Length);
			return;
		}

		public async Task WriteToBuffer(object? obj, string type = "text", int statusCode = 200)
		{
			if (obj == null) return;
			var bytes = Encoding.Default.GetBytes(obj.ToString());
			this.Write(bytes, 0, bytes.Length);
			return;
		}
	}
}
