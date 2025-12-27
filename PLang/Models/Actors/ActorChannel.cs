using PLang.Models;
using PLang.Services.OutputStream.Sinks;
using System.Text;

namespace PLang.Models.Actors;

/// <summary>
/// A channel routes messages to either an IOutputSink or a PLang goal.
/// </summary>
public class ActorChannel
{
    public string Name { get; }
    public IOutputSink Sink { get; set; }
    
    /// <summary>
    /// Goal to call when messages are sent to this channel.
    /// Uses existing GoalToCallInfo class from PLang.
    /// </summary>
    public GoalToCallInfo? GoalToCall { get; set; }
    
    public string? ContentType { get; set; }
	public Encoding Encoding { get; set; } = Encoding.UTF8;

	public bool IsGoalChannel => GoalToCall != null;

    public ActorChannel(string name, IOutputSink sink, string? contentType = null)
    {
        Name = name;
        Sink = sink;
        ContentType = contentType;
    }

}
