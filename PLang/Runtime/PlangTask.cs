using PLang.Building.Model;
using PLang.Errors;

namespace PLang.Runtime
{
	/// <summary>
	/// A goal running without its caller waiting for it: a "dont wait" call or a scheduled run.
	/// Kept in the KeepAliveRegistry, so the list of what is running tells which goal each task runs and who called it.
	/// </summary>
	public record PlangTask(Task<(object? Variables, IError? Error)> Task, Goal Goal, Goal? CallingGoal)
	{
		public bool IsCompleted => Task.IsCompleted;
	}
}
