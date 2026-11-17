using PLang.Building.Model;
using PLang.Events;
using PLang.Interfaces;
using System.Collections.Concurrent;

namespace PLang.Runtime;

/// <summary>
/// Values that are set on a goal or step while it runs and only make sense for the request
/// that set them: which goal called this goal, which step a goal is on, whether an error
/// handler asked for a step to be retried and how many times it has been.
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
/// </summary>
public sealed class GoalRuntimeState
{
	private readonly ConcurrentDictionary<Goal, Goal?> parentGoals = new(ReferenceEqualityComparer.Instance);
	private readonly ConcurrentDictionary<Goal, int> currentStepIndexes = new(ReferenceEqualityComparer.Instance);
	private readonly ConcurrentDictionary<GoalStep, bool> retries = new(ReferenceEqualityComparer.Instance);
	private readonly ConcurrentDictionary<GoalStep, int> retryCounts = new(ReferenceEqualityComparer.Instance);

	/// <summary>The state of the request running in this async flow, null outside a request.</summary>
	public static GoalRuntimeState? Current => ContextAccessor.Ambient?.GoalState;

	public bool TryGetParentGoal(Goal goal, out Goal? parentGoal) => parentGoals.TryGetValue(goal, out parentGoal);
	public void SetParentGoal(Goal goal, Goal? parentGoal) => parentGoals[goal] = parentGoal;

	private readonly ConcurrentDictionary<Goal, RuntimeEvent?> events = new(ReferenceEqualityComparer.Instance);
	public bool TryGetEvent(Goal goal, out RuntimeEvent? runtimeEvent) => events.TryGetValue(goal, out runtimeEvent);
	public void SetEvent(Goal goal, RuntimeEvent? runtimeEvent) => events[goal] = runtimeEvent;

	public static void SetCurrentStepIndex(Goal goal, int stepIndex)
	{
		var state = Current;
		if (state != null)
		{
			state.currentStepIndexes[goal] = stepIndex;
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
		var state = Current;
		if (state != null && state.currentStepIndexes.TryGetValue(goal, out var index)) return index;
		return goal.CurrentStepIndex;
	}

	/// <summary>An error handler asked for the step to run again ("retry step").</summary>
	public static void RequestRetry(GoalStep step)
	{
		var state = Current;
		if (state != null)
		{
			state.retries[step] = true;
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
			return state.retries.TryRemove(step, out var retry) && retry;
		}
		var requested = step.Retry;
		step.Retry = false;
		return requested;
	}

	/// <summary>The attempt number "retry step" compares with its limit, starting at the step's RetryCount (1).</summary>
	public static int GetRetryCount(GoalStep step)
	{
		var state = Current;
		if (state != null && state.retryCounts.TryGetValue(step, out var count)) return count;
		return step.RetryCount;
	}

	public static void SetRetryCount(GoalStep step, int count)
	{
		var state = Current;
		if (state != null)
		{
			state.retryCounts[step] = count;
			return;
		}
		step.RetryCount = count;
	}
}
