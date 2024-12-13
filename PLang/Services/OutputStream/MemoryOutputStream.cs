
using System.Text;
using System.Threading.Tasks;

namespace PLang.Services.OutputStream
{
	public class MemoryOutputStream : MemoryStream, IOutputStream
	{
		public Stream Stream => this;

		public Stream ErrorStream => new MemoryStream();

		public Task<string> Ask(string text, string type = "text", int statusCode = 200, Dictionary<string, object>? parameters = null)
		{
			return null;
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
