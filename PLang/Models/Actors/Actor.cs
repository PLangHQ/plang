using PLang.Models;
using PLang.Services.OutputStream;
using PLang.Services.OutputStream.Sinks;
using System.Text;

namespace PLang.Models.Actors;

public enum ActorType { System, User, Service }

public class Actor
{
    public ActorType Type { get; }
    public string Identity { get; }
    public bool IsTrusted { get; }

    public string ContentType { get; set; } = PlangContentTypes.Text;
    public Encoding Encoding { get; set; } = Encoding.UTF8;

    private readonly Dictionary<string, ActorChannel> _channels = new();
    private IOutputSink? _defaultSink;

    public Actor(ActorType type, string identity = "", bool isTrusted = false, IOutputSink? defaultSink = null)
    {
        Type = type;
        Identity = identity;
        IsTrusted = isTrusted;
        _defaultSink = defaultSink;
    }

    /// <summary>
    /// Gets a channel by name. Returns default channel if name is "default" or not found.
    /// </summary>
    public ActorChannel GetChannel(string? name = null)
    {
        name ??= "default";

        if (_channels.TryGetValue(name, out var channel))
            return channel;

        // Return or create default channel
        if (!_channels.TryGetValue("default", out var defaultChannel))
        {
            if (_defaultSink == null)
                throw new InvalidOperationException($"No sink registered for channel '{name}' and no default sink available");

            defaultChannel = new ActorChannel("default", _defaultSink, null);
            _channels["default"] = defaultChannel;
        }

        return defaultChannel;
    }

    public void RegisterChannel(string name, IOutputSink sink, string? contentType = null)
    {
        _channels[name] = new ActorChannel(name, sink, contentType);
    }

    public void UnregisterChannel(string name)
    {
        _channels.Remove(name);
    }

    public void SetDefaultSink(IOutputSink sink)
    {
        _defaultSink = sink;
        
        // Update default channel if it exists
        if (_channels.TryGetValue("default", out var channel))
        {
            channel.Sink = sink;
        }
    }

    public string GetContentType(string? channelName = null)
    {
        var channel = GetChannel(channelName);
        return channel.ContentType ?? ContentType;
    }

    public IReadOnlyDictionary<string, ActorChannel> Channels => _channels;
}
