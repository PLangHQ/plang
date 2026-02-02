using PLang.Services.OutputStream.Sinks;

namespace PLang.Runtime.Actors;

public class UserActor : Actor
{
	private readonly IOutputSink _defaultSink;

	/// <summary>
	/// Creates a UserActor with appropriate default content type.
	/// Console uses text/plain, web uses text/html.
	/// Only use application/x-ndjson when client explicitly requests application/plang.
	/// </summary>
	public UserActor(string? identity = null, IOutputSink? defaultSink = null, bool isTrusted = false)
		: base(ActorType.User, identity, isTrusted, contentType: "text/html")
	{
		_defaultSink = defaultSink ?? new ConsoleSink();
		RegisterChannel("default", _defaultSink);
	}

	protected override IOutputSink CreateDefaultSink() => _defaultSink;
}
