using Microsoft.AspNetCore.Mvc;
using PLang.Errors;
using PLang.Services.OutputStream.Messages;

namespace PLang.Services.OutputStream.Sinks;
public interface IOutputSink
{

	public string Id { get; }
	bool IsStateful { get; }
	Task<IError?> SendAsync(OutMessage message, CancellationToken ct = default);

	Task<(object? result, IError? error)> AskAsync(AskMessage message, CancellationToken ct = default);
}
