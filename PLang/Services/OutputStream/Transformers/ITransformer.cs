using PLang.Errors;
using PLang.Services.OutputStream.Messages;
using System.IO.Pipelines;
using System.Text;

namespace PLang.Services.OutputStream.Transformers;
public interface ITransformer
{
	string ContentType { get; }
	Encoding Encoding { get; }

	public Task<(long, IError?)> Transform(HttpContext httpContext, PipeWriter writer, OutMessage? data, CancellationToken ct = default);
}



