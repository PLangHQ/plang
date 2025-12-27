using NBitcoin;
using PLang.Errors;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.OutputStream.Messages;

namespace PLang.Services.OutputStream.Sinks;

public class GoalToCallSink : IOutputSink
{
	private readonly IEngine engine;
	private readonly GoalToCallInfo goalToCallInfo;

	public GoalToCallSink(IEngine engine, GoalToCallInfo goalToCallInfo)
	{
		this.engine = engine;
		this.goalToCallInfo = goalToCallInfo;
	}

	public string Id => Guid.NewGuid().ToString();

	public bool IsStateful => true;

	public async Task<(object? result, IError? error)> AskAsync(AskMessage message, CancellationToken ct = default)
	{
		goalToCallInfo.Parameters.AddOrReplace("plang.message", message);
		return await engine.RunGoal(goalToCallInfo, engine.Context.CallingStep.Goal, engine.Context);
	}

	public async Task<IError?> SendAsync(OutMessage message, CancellationToken ct = default)
	{
		goalToCallInfo.Parameters.AddOrReplace("plang.message", message);
		var result = await engine.RunGoal(goalToCallInfo, engine.Context.CallingStep.Goal, engine.Context);
		return result.Error;
	}
}
