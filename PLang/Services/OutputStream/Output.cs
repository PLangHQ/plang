using PLang.Errors;
using PLang.Models;
using PLang.Models.Actors;
using PLang.Runtime;
using PLang.Services.OutputStream.Messages;
using PLang.Services.OutputStream.Sinks;

namespace PLang.Services.OutputStream;

/// <summary>
/// Holds the actors for output routing.
/// 
/// This class is used by both:
/// - engine.AppContext.Output (app-level, trusted actors)
/// - engine.PlangContext.Output (per-request/job actors)
/// </summary>
public class Output
{
	private readonly OutputService _outputService;

	public Actor System { get; set; }
	public Actor User { get; set; }
	public Actor Service
	{
		get => User.IsTrusted ? User : System;
		set => field = value;
	}

	public Output(Actor system, Actor user, IEngine engine)
	{
		System = system;
		User = user;
		_outputService = new OutputService(this, engine);
	}

	/// <summary>
	/// Get actor by name. Service falls back to User (if trusted) or System (if untrusted).
	/// </summary>
	public Actor GetActor(string? actorName = null)
	{
		actorName ??= "user";

		return actorName.ToLowerInvariant() switch
		{
			"system" => System,
			"user" => User,
			"service" => ResolveServiceActor(),
			_ => User // Default to user
		};
	}

	private Actor ResolveServiceActor()
	{
		// If we have a Service actor, use it
		if (Service != null)
			return Service;

		// Otherwise forward based on trust:
		// - Trusted user (console/desktop) → User
		// - Untrusted user (web request) → System
		return User.IsTrusted ? User : System;
	}

	/// <summary>
	/// Send a message through the appropriate actor/channel.
	/// </summary>
	public Task<IError?> SendAsync(OutMessage message, CancellationToken ct = default)
	{
		return _outputService.SendAsync(message, ct);
	}

	/// <summary>
	/// Send an ask message and wait for response.
	/// </summary>
	public Task<(object? result, IError? error)> AskAsync(AskMessage message, CancellationToken ct = default)
	{
		return _outputService.AskAsync(message, ct);
	}

	/// <summary>
	/// Creates Output for console/desktop apps where user is trusted.
	/// System and User use the same console sink.
	/// </summary>
	public static Output CreateForTrustedUser(IOutputSink consoleSink, IEngine engine)
	{
		var system = new Actor(ActorType.System, "", isTrusted: true, defaultSink: consoleSink)
		{
			ContentType = PlangContentTypes.Text
		};

		var user = new Actor(ActorType.User, "", isTrusted: true, defaultSink: consoleSink)
		{
			ContentType = PlangContentTypes.Text
		};

		return new Output(system, user, engine);
	}

	/// <summary>
	/// Creates Output for external(such as httprequest) requests where user is untrusted.
	/// System points to the app's trusted user (escalates).
	/// </summary>
	public static Output CreateForExternalRequest(
		Actor appTrustedUser,
		IOutputSink httpSink,
		IEngine engine,
		string identity = "", string contentType = PlangContentTypes.Html)
	{
		// System for web request = app's trusted user (escalate)
		var system = appTrustedUser;

		// User for web request = the web client (untrusted)
		var user = new Actor(ActorType.User, identity, isTrusted: false, defaultSink: httpSink)
		{
			ContentType = contentType
		};

		return new Output(system, user, engine);
	}

	/// <summary>
	/// Creates Output that shares actors from app-level Output.
	/// Used for console/desktop apps and background jobs.
	/// </summary>
	public static Output CreateFromAppOutput(Output appOutput, IEngine engine)
	{
		return new Output(appOutput.System, appOutput.User, engine);
	}
}
