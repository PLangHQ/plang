namespace app.Channels.Channel.Session;

/// <summary>
/// Channel pattern abstract: kept-open, stateful connection.
/// Ask blocks reading from the connection until an answer arrives, returns the answer Data.
/// Stream-backed (stdin loop) and Goal-backed channels both extend Session.
/// </summary>
public abstract class @this : Channel.@this
{
}
