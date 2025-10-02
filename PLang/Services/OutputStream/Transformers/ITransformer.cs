using PLang.Errors;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;
using System.IO.Pipelines;
using System.Text;

namespace PLang.Services.OutputStream.Transformers;
public interface ITransformer
{
	string ContentType { get; }
	Encoding Encoding { get; }

	public Task<(long, IError?)> Transform(PLangContext httpContext, PipeWriter writer, OutMessage? data, CancellationToken ct = default);
}



