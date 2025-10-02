using PLang.Building.Model;
using System.Text;

public class CallStack
{
	private readonly Stack<CallStackFrame> _frames = new Stack<CallStackFrame>();

	public CallStackFrame CurrentFrame => _frames.Count > 0 ? _frames.Peek() : null;
	public Goal CurrentGoal => CurrentFrame?.Goal;
	public GoalStep CurrentStep => CurrentFrame?.CurrentStep;
	public ExecutionPhase CurrentPhase => CurrentFrame?.Phase ?? ExecutionPhase.None;
	public string EventScope => CurrentFrame?.EventScope;
	public string EventType => CurrentFrame?.EventType;

	public void EnterGoal(Goal goal, string eventScope = null, string eventType = null)
	{
		_frames.Push(new CallStackFrame(goal, eventScope, eventType));
	}

	public void ExitGoal()
	{
		if (_frames.Count > 0)
			_frames.Pop();
	}

	public void SetCurrentStep(GoalStep step, int stepIndex)
	{
		if (CurrentFrame != null)
		{
			CurrentFrame.CurrentStep = step;
			CurrentFrame.StepIndex = stepIndex;
		}
	}

	public void SetPhase(ExecutionPhase phase)
	{
		if (CurrentFrame != null)
		{
			CurrentFrame.Phase = phase;
		}
	}

	public int Depth => _frames.Count;

	public IEnumerable<CallStackFrame> Frames => _frames;

	// Check if currently in an event handler
	public bool IsInEvent => !string.IsNullOrEmpty(CurrentFrame?.EventScope);

	// Check specific event scope
	public bool IsInEventScope(string scope) => CurrentFrame?.EventScope == scope;

	// For error reporting
	public string GetStackTrace()
	{
		var trace = new StringBuilder();
		foreach (var frame in _frames)
		{
			var eventInfo = "";
			if (!string.IsNullOrEmpty(frame.EventScope))
			{
				eventInfo = $" [Event: {frame.EventScope}";
				if (!string.IsNullOrEmpty(frame.EventType))
					eventInfo += $".{frame.EventType}";
				eventInfo += "]";
			}

			trace.AppendLine($"  at {frame.Goal.GoalName}{eventInfo} ({frame.Phase})");
			if (frame.CurrentStep != null)
				trace.AppendLine($"    step {frame.StepIndex}: {frame.CurrentStep.Text}");
		}
		return trace.ToString();
	}

	public IEnumerable<Goal> GetGoalHierarchy()
	{
		if (CurrentGoal == null) yield break;

		var goal = CurrentGoal;
		while (goal != null)
		{
			yield return goal;
			goal = goal.ParentGoal;
		}
	}
}

public class CallStackFrame
{
	public Goal Goal { get; }
	public GoalStep CurrentStep { get; set; }
	public int StepIndex { get; set; }
	public ExecutionPhase Phase { get; set; }

	// Event information
	public string EventScope { get; }  // Goal, Step, Module, StartOfApp, etc.
	public string EventType { get; }   // Before, After, OnCreate, OnChange, etc.

	public Dictionary<string, object> LocalVariables { get; }

	public CallStackFrame(Goal goal, string eventScope = null, string eventType = null)
	{
		Goal = goal;
		EventScope = eventScope;
		EventType = eventType;
		Phase = ExecutionPhase.None;
		LocalVariables = new Dictionary<string, object>();
	}
}

public enum ExecutionPhase
{
	None,
	ExecutingGoal,
	ExecutingStep
}



public class SerializableCallStack
{
	public List<SerializableCallStackFrame> Frames { get; set; }
	public int Depth { get; set; }
	public string CurrentGoalName { get; set; }
	public string CurrentStepText { get; set; }
	public string EventScope { get; set; }
	public string EventType { get; set; }
	public string Phase { get; set; }

	public static SerializableCallStack FromCallStack(CallStack callStack)
	{
		return new SerializableCallStack
		{
			Frames = callStack.Frames.Select(f => SerializableCallStackFrame.FromFrame(f)).ToList(),
			Depth = callStack.Depth,
			CurrentGoalName = callStack.CurrentGoal?.GoalName,
			CurrentStepText = callStack.CurrentStep?.Text,
			EventScope = callStack.EventScope,
			EventType = callStack.EventType,
			Phase = callStack.CurrentPhase.ToString()
		};
	}
}

public class SerializableCallStackFrame
{
	public string GoalName { get; set; }
	public string GoalPath { get; set; }
	public string CurrentStepText { get; set; }
	public int StepIndex { get; set; }
	public string Phase { get; set; }
	public string EventScope { get; set; }
	public string EventType { get; set; }
	public Dictionary<string, object> LocalVariables { get; set; }

	public static SerializableCallStackFrame FromFrame(CallStackFrame frame)
	{
		return new SerializableCallStackFrame
		{
			GoalName = frame.Goal?.GoalName,
			GoalPath = frame.Goal?.RelativeGoalPath,
			CurrentStepText = frame.CurrentStep?.Text,
			StepIndex = frame.StepIndex,
			Phase = frame.Phase.ToString(),
			EventScope = frame.EventScope,
			EventType = frame.EventType,
			LocalVariables = frame.LocalVariables // Be careful with this too
		};
	}
}