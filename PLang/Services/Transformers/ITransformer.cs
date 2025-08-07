using PLang.Errors;
using System.Text;

namespace PLang.Services.Transformers
{
	public interface ITransformer
	{
		string ContentType { get; }
		Encoding Encoding { get; }

		public (object, IError?) Transform(object? data, Dictionary<string, object?>? properties = null, string type = "text");
		public Task<IError?> Transform(Stream stream, object? data, Dictionary<string, object?>? properties = null, string type = "text");
	}

	

}
