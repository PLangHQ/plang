using NBitcoin;
using PLang.Errors;
using PLang.Models;
using PLang.Runtime;
using PLang.Models.Actors;
using PLang.Services.OutputStream.Messages;
using System.Reflection;

namespace PLang.Services.OutputStream;

/// <summary>
/// Service for sending output messages through the actor system.
/// Handles routing to sinks and goal channels.
/// 
/// NOTE: This was discussed as a central place for send logic,
/// used by both AppContext.Output and PlangContext.Output.
/// </summary>
public class OutputService
{
	private readonly Output _output;
	private readonly IEngine _engine;

	public OutputService(Output output, IEngine engine)
	{
		_output = output;
		_engine = engine;
	}

	/// <summary>
	/// Send a message to the appropriate actor/channel.
	/// </summary>
	public async Task<IError?> SendAsync(OutMessage message, CancellationToken ct = default)
	{
		var actor = _output.GetActor(message.Actor);
		var channel = actor.GetChannel(message.Channel);

		if (channel.Sink == null)
		{
			return new Error($"No sink configured for channel '{message.Channel}' on actor '{message.Actor}'");
		}

		return await channel.Sink.SendAsync(message, ct);
	}

	/// <summary>
	/// Send an ask message and wait for response.
	/// </summary>
	public async Task<(object? result, IError? error)> AskAsync(AskMessage message, CancellationToken ct = default)
	{
		var actor = _output.GetActor(message.Actor);
		var channel = actor.GetChannel(message.Channel);

		if (channel.IsGoalChannel)
		{
			// Goal channels don't support Ask - fall back to default channel
			var defaultChannel = actor.GetChannel("default");
			if (defaultChannel.Sink != null)
			{
				return await defaultChannel.Sink.AskAsync(message, ct);
			}
			return (null, new Error("Goal channels do not support Ask operations"));
		}

		if (channel.Sink == null)
		{
			return (null, new Error($"No sink configured for channel '{message.Channel}' on actor '{message.Actor}'"));
		}

		return await channel.Sink.AskAsync(message, ct);
	}

	/// <summary>
	/// Calls a goal with plang.* variables set from the message.
	/// Uses engine.RunGoal(goalToCall, parentGoal, context)
	/// </summary>
	/*
	private async Task<IError?> SendToGoalAsync(OutMessage message, GoalToCallInfo goalToCall, CancellationToken ct)
	{
		// Build the plang.* variables from the message using reflection
		var plangVariables = BuildPlangVariables(message);

		// Get the parent goal from current execution context
		var context = _engine.GetContext();
		var parentGoal = context.Goal;
		foreach (var var in plangVariables) {
			goalToCall.Parameters.AddOrReplace(var.Key, var.Value);
		}

		// Run the goal with plang.* variables injected into context
		return await _engine.RunGoal(goalToCall, parentGoal, context, ct);
	}*/

	/// <summary>
	/// Builds the plang.* variables dictionary from an OutMessage using reflection.
	/// All public properties become available as %plang.propertyName% in the goal.
	/// </summary>
	private static Dictionary<string, object?> BuildPlangVariables(OutMessage message)
	{
		var variables = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

		// Add the full message object
		variables["plang.message"] = message;

		// Get all public properties from the message type (includes inherited properties)
		var type = message.GetType();
		var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

		foreach (var prop in properties)
		{
			try
			{
				var name = ToCamelCase(prop.Name);
				var value = prop.GetValue(message);
				variables[$"plang.{name}"] = value;
			}
			catch
			{
				// Skip properties that throw on access
			}
		}

		return variables;
	}

	/// <summary>
	/// Converts PascalCase to camelCase.
	/// Content → content, StatusCode → statusCode
	/// </summary>
	private static string ToCamelCase(string name)
	{
		if (string.IsNullOrEmpty(name))
			return name;

		if (name.Length == 1)
			return name.ToLowerInvariant();

		return char.ToLowerInvariant(name[0]) + name.Substring(1);
	}
}
