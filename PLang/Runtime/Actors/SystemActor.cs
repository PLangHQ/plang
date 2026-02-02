using PLang.Services.OutputStream.Sinks;

namespace PLang.Runtime.Actors;

public class SystemActor : Actor
{
	private readonly IOutputSink _defaultSink;

	public SystemActor(string? identity = null, IOutputSink? defaultSink = null)
		: base(ActorType.System, identity, isTrusted: true, contentType: "text/plain")
	{
		_defaultSink = defaultSink ?? new ConsoleSink();
		RegisterChannel("default", _defaultSink);
	}

	protected override IOutputSink CreateDefaultSink() => _defaultSink;
}
