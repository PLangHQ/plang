using PLang.Building.Model;
using PLang.Models;
using PLang.Services.OutputStream.Sinks;
using System.Text;

namespace PLang.Runtime.Actors;

public class ActorChannel
{
	public string Name { get; }
	public IOutputSink Sink { get; set; }
	public string? ContentType { get; set; }
	public Encoding? Encoding { get; set; }

	// Goal to call instead of sink (when registered via OnChannel)
	public GoalToCallInfo? GoalHandler { get; set; }

	// Goal-scoped overrides (keyed by Goal)
	private readonly Dictionary<Goal, ChannelSettings> _goalOverrides = new();

	public ActorChannel(string name, IOutputSink sink, string? contentType = null)
	{
		Name = name;
		Sink = sink;
		ContentType = contentType;
	}

	public void SetScopedSettings(Goal goal, ChannelSettings settings)
		=> _goalOverrides[goal] = settings;

	/// <summary>
	/// Sets the content type for a specific goal scope. When the goal exits, this setting is cleared.
	/// </summary>
	public void SetScopedContentType(Goal goal, string contentType)
	{
		if (_goalOverrides.TryGetValue(goal, out var existing))
		{
			existing.ContentType = contentType;
		}
		else
		{
			_goalOverrides[goal] = new ChannelSettings { ContentType = contentType };
		}
	}

	/// <summary>
	/// Sets the encoding for a specific goal scope. When the goal exits, this setting is cleared.
	/// </summary>
	public void SetScopedEncoding(Goal goal, Encoding encoding)
	{
		if (_goalOverrides.TryGetValue(goal, out var existing))
		{
			existing.Encoding = encoding;
		}
		else
		{
			_goalOverrides[goal] = new ChannelSettings { Encoding = encoding };
		}
	}

	public void ClearScopedSettings(Goal goal)
		=> _goalOverrides.Remove(goal);

	public void ClearAllScopedSettings()
		=> _goalOverrides.Clear();

	// Resolve by walking up call stack
	public string? GetEffectiveContentType(CallStack callStack, string? actorDefault)
	{
		foreach (var frame in callStack.GetFrames())
		{
			if (_goalOverrides.TryGetValue(frame.Goal, out var settings) && settings.ContentType != null)
				return settings.ContentType;
		}
		return ContentType ?? actorDefault;
	}

	/// <summary>
	/// Gets explicitly configured content type (goal-scoped or channel-specific),
	/// returns null if only using defaults. Used to check if Accept header should be overridden.
	/// </summary>
	public string? GetExplicitContentType(CallStack callStack)
	{
		foreach (var frame in callStack.GetFrames())
		{
			if (_goalOverrides.TryGetValue(frame.Goal, out var settings) && settings.ContentType != null)
				return settings.ContentType;
		}
		// Only return if channel has explicit content type set, not actor default
		return ContentType;
	}

	public Encoding? GetEffectiveEncoding(CallStack callStack, Encoding? actorDefault)
	{
		foreach (var frame in callStack.GetFrames())
		{
			if (_goalOverrides.TryGetValue(frame.Goal, out var settings) && settings.Encoding != null)
				return settings.Encoding;
		}
		return Encoding ?? actorDefault;
	}
}
