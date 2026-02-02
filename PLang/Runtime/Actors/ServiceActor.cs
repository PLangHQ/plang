using PLang.Services.OutputStream.Sinks;

namespace PLang.Runtime.Actors;

/// <summary>
/// Represents an external service that the app reaches out to (you went to them).
/// Output to a ServiceActor typically forwards to the System actor.
/// </summary>
public class ServiceActor : Actor
{
	public string? Endpoint { get; }
	private readonly Actor? _fallbackActor;

	public ServiceActor(string? identity = null, string? endpoint = null, Actor? fallbackActor = null)
		: base(ActorType.Service, identity, isTrusted: false, contentType: "application/json")
	{
		Endpoint = endpoint;
		_fallbackActor = fallbackActor;
	}

	protected override IOutputSink CreateDefaultSink()
	{
		// Forward to fallback actor's sink if available
		if (_fallbackActor != null)
		{
			return _fallbackActor.GetSink("default");
		}
		return new ConsoleSink();
	}
}
