using PLang.Building.Model;
using PLang.Events;
using PLang.Interfaces;
using System.Runtime.CompilerServices;

namespace PLang.Runtime;

/// <summary>
/// Values that are set on a goal or step while it runs and only make sense for the request
/// that set them: which goal called this goal, which step a goal is on, the event that called
/// the goal, whether an error handler asked for a step to be retried and how many times it has been.
///
/// They used to be properties on Goal and GoalStep. That only worked because every engine
/// parsed its own copy of the app, so an engine (which serves one request at a time) had
/// its own objects to write to. The goal graph is now parsed once and shared by every
/// engine, so these values are kept per request instead: one GoalRuntimeState per
/// PLangContext, shared with the contexts cloned from it for "dont wait" calls, the same
/// way those calls used to see the caller's Goal objects.
///
/// The request is found the way modules find it, through the context of the current async
/// flow (ContextAccessor). Outside a request (no context) the values are read from and
/// written to the Goal and GoalStep properties, as before.
///
/// Goals and steps are held weakly (ConditionalWeakTable): an engine keeps the context of its
/// last request alive, and after a .pr reload that context must not keep the old goals alive.
/// </summary>
public sealed class GoalRuntimeState
{
	// A null field means "not set in this request"; the boxes tell a value of null apart from that.
	private sealed class GoalValues
	{
		public volatile StrongBox<Goal?>? ParentGoal;
		public volatile StrongBox<RuntimeEvent?>? Event;
		public volatile StrongBox<int>? CurrentStepIndex;
	}

	private sealed class StepValues
	{
		public StrongBox<bool>? Retry; // read and cleared with Interlocked.Exchange
		public volatile StrongBox<int>? RetryCount;
	}

	private readonly ConditionalWeakTable<Goal, GoalValues> goals = new();
	private readonly ConditionalWeakTable<GoalStep, StepValues> steps = new();

	/// <summary>The state of the request running in this async flow, null outside a request.</summary>
	public static GoalRuntimeState? Current => ContextAccessor.Ambient?.GoalState;

	private GoalValues? Find(Goal goal) => goals.TryGetValue(goal, out var values) ? values : null;
	private GoalValues For(Goal goal) => goals.GetValue(goal, _ => new GoalValues());
	private StepValues? Find(GoalStep step) => steps.TryGetValue(step, out var values) ? values : null;
	private StepValues For(GoalStep step) => steps.GetValue(step, _ => new StepValues());

	public bool TryGetParentGoal(Goal goal, out Goal? parentGoal)
	{
		var box = Find(goal)?.ParentGoal;
		parentGoal = box?.Value;
		return box != null;
	}
	public void SetParentGoal(Goal goal, Goal? parentGoal) => For(goal).ParentGoal = new StrongBox<Goal?>(parentGoal);

	public bool TryGetEvent(Goal goal, out RuntimeEvent? runtimeEvent)
	{
		var box = Find(goal)?.Event;
		runtimeEvent = box?.Value;
		return box != null;
	}
	public void SetEvent(Goal goal, RuntimeEvent? runtimeEvent) => For(goal).Event = new StrongBox<RuntimeEvent?>(runtimeEvent);

	public static void SetCurrentStepIndex(Goal goal, int stepIndex)
	{
		var state = Current;
		if (state != null)
		{
			state.For(goal).CurrentStepIndex = new StrongBox<int>(stepIndex);
			return;
		}
		goal.CurrentStepIndex = stepIndex;
	}

	/// <summary>
	/// The step the goal is on in the current request. A goal that has not run a step in this
	/// request reads the value on the goal, which is what was read before this class existed.
	/// </summary>
	public static int GetCurrentStepIndex(Goal goal)
	{
		var box = Current?.Find(goal)?.CurrentStepIndex;
		return box?.Value ?? goal.CurrentStepIndex;
	}

	/// <summary>An error handler asked for the step to run again ("retry step").</summary>
	public static void RequestRetry(GoalStep step)
	{
		var state = Current;
		if (state != null)
		{
			state.For(step).Retry = new StrongBox<bool>(true);
			return;
		}
		step.Retry = true;
	}

	/// <summary>Whether a retry was requested for the step, and resets the request.</summary>
	public static bool TakeRetry(GoalStep step)
	{
		var state = Current;
		if (state != null)
		{
			var values = state.Find(step);
			if (values == null) return false;
			var box = Interlocked.Exchange(ref values.Retry, null);
			return box?.Value == true;
		}
		var requested = step.Retry;
		step.Retry = false;
		return requested;
	}

	/// <summary>The attempt number "retry step" compares with its limit, starting at the step's RetryCount (1).</summary>
	public static int GetRetryCount(GoalStep step)
	{
		var box = Current?.Find(step)?.RetryCount;
		return box?.Value ?? step.RetryCount;
	}

	public static void SetRetryCount(GoalStep step, int count)
	{
		var state = Current;
		if (state != null)
		{
			state.For(step).RetryCount = new StrongBox<int>(count);
			return;
		}
		step.RetryCount = count;
	}
}
