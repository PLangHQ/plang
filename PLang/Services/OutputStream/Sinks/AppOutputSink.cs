using PLang.Errors;
using PLang.Interfaces;
using PLang.Services.OutputStream.Messages;

namespace PLang.Services.OutputStream.Sinks;


public class AppOutputSink : IOutputSink
{
	private readonly IPLangFileSystem fileSystem;
	private readonly IForm form;

	public AppOutputSink(IPLangFileSystem fileSystem, IForm form)
	{
		this.fileSystem = fileSystem;
		this.form = form;
	}
	public bool IsStateful => true;
	public IForm IForm { get { return form;  } }
	public string Id { get; } = Guid.NewGuid().ToString();

	public Task<(object? result, IError? error)> AskAsync(AskMessage message, CancellationToken ct = default)
	{
		throw new NotImplementedException();
	}

	public Task<IError?> SendAsync(OutMessage message, CancellationToken ct = default)
	{
		throw new NotImplementedException();
	}

	internal void Flush()
	{
		throw new NotImplementedException();
	}
}

