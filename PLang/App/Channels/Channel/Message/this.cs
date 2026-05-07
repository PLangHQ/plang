namespace App.Channels.Channel.Message;

/// <summary>
/// Channel pattern abstract: stateless, one-shot exchange.
/// AskCore returns a Suspend sentinel — engine suspends the goal, callback resumes when answer arrives.
/// Web extends Message (when shipped).
/// </summary>
public abstract class @this : Channel.@this
{
}
