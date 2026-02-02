using PLang.Models;
using PLang.Services.OutputStream.Sinks;
using System.Collections.Concurrent;
using System.Text;

namespace PLang.Runtime.Actors;

public abstract class Actor
{
	public ActorType Type { get; }
	public string? Identity { get; set; }
	public bool IsTrusted { get; }

	// Changeable via PLang
	public string ContentType { get; set; }
	public Encoding Encoding { get; set; } = Encoding.UTF8;

	// Channels with sinks
	private readonly ConcurrentDictionary<string, ActorChannel> _channels = new(StringComparer.OrdinalIgnoreCase);

	protected Actor(ActorType type, string? identity = null, bool isTrusted = false, string contentType = "text/plain")
	{
		Type = type;
		Identity = identity;
		IsTrusted = isTrusted;
		ContentType = contentType;
	}

	public ActorChannel? GetChannel(string name)
		=> _channels.TryGetValue(name, out var ch) ? ch : null;

	public ActorChannel GetOrCreateChannel(string name, IOutputSink? defaultSink = null)
	{
		return _channels.GetOrAdd(name, n =>
		{
			var sink = defaultSink ?? CreateDefaultSink();
			return new ActorChannel(n, sink);
		});
	}

	public void RegisterChannel(string name, IOutputSink sink, string? contentType = null)
	{
		var channel = new ActorChannel(name, sink, contentType);
		_channels[name] = channel;
	}

	public void RegisterChannelHandler(string channelName, GoalToCallInfo goalHandler)
	{
		var channel = GetOrCreateChannel(channelName);
		channel.GoalHandler = goalHandler;
	}

	public void UnregisterChannel(string name) => _channels.TryRemove(name, out _);

	public IEnumerable<ActorChannel> GetAllChannels() => _channels.Values;

	protected abstract IOutputSink CreateDefaultSink();

	public string GetContentType(string? channelName)
	{
		if (string.IsNullOrEmpty(channelName)) return ContentType;
		return GetChannel(channelName)?.ContentType ?? ContentType;
	}

	/// <summary>
	/// Gets the sink for a channel, or creates the default channel if it doesn't exist
	/// </summary>
	public IOutputSink GetSink(string? channelName = null)
	{
		var name = string.IsNullOrEmpty(channelName) ? "default" : channelName;
		var channel = GetOrCreateChannel(name);
		return channel.Sink;
	}

	/// <summary>
	/// Checks if a channel has a goal handler registered
	/// </summary>
	public GoalToCallInfo? GetChannelHandler(string? channelName = null)
	{
		var name = string.IsNullOrEmpty(channelName) ? "default" : channelName;
		return GetChannel(name)?.GoalHandler;
	}
}
