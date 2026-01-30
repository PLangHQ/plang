using PLang.Building.Model;
using PLang.Errors;
using PLang.Events;
using PLang.Events.Types;
using PLang.Models;
using PLang.Modules;
using PLang.Utils;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace PLang.Runtime;

public class CallStack
{
	private readonly ConcurrentStack<CallStackFrame> _frames = new();
	private readonly object _peekActLock = new();

	private CallStackFrame? CurrentFrameOrNull
	{
		get
		{
			_frames.TryPeek(out var frame);
			return frame;
		}
	}

	public CallStackFrame CurrentFrame
	{
		get
		{
			if (_frames.TryPeek(out var frame))
			{
				return frame;
			}
			throw new InvalidOperationException("No frame on CallStack. Did you forget to call EnterGoal?");
		}
	}

	public Goal CurrentGoal => CurrentFrame.Goal;
	public GoalStep? CurrentStep => CurrentFrame.CurrentStep;
	public ExecutionPhase CurrentPhase => CurrentFrame.Phase;
	public RuntimeEvent? Event => CurrentFrame.Event;
	public int Depth => _frames.Count;
	public bool HasFrames => !_frames.IsEmpty;

	public CallStackFrame EnterGoal(Goal goal, RuntimeEvent? eventBinding = null)
	{
		var parentFrame = CurrentFrameOrNull;
		var frame = new CallStackFrame(goal, parentFrame, eventBinding);

		_frames.Push(frame);
		SetPhase(ExecutionPhase.ExecutingGoal);

		if (_frames.Count > 1000)
		{
			throw new Exception("1000 frames");
		}

		return frame;
	}

	public CallStackFrame ExitGoal()
	{
		if (!_frames.TryPop(out var frame))
		{
			throw new InvalidOperationException("No frame to exit. CallStack is empty.");
		}

		frame.Complete();
		DisposeOfDisposables(frame);
		return frame;
	}

	public void SetCurrentStep(GoalStep step, int stepIndex)
	{
		lock (_peekActLock)
		{
			if (_frames.IsEmpty)
			{
				EnterGoal(new Goal() { GoalName = "AppStart" });
			}
			CurrentFrame.SetCurrentStep(step, stepIndex);
		}
		SetPhase(ExecutionPhase.ExecutingStep);
	}

	public void CompleteCurrentStep(object? returnValue = null, IError? error = null)
	{
		CurrentFrame.CompleteCurrentStep(returnValue, error);
	}

	public void SetPhase(ExecutionPhase phase)
	{
		CurrentFrame.Phase = phase;
	}

	public void AddDisposable(IDisposable disposable)
	{
		CurrentFrame.Disposables.Add(disposable);
	}

	public void AddError(IError error)
	{
		CurrentFrame.AddError(error);
	}

	private void DisposeOfDisposables(CallStackFrame frame)
	{
		if (frame.Disposables.IsEmpty) return;

		var hasErrors = !frame.Errors.IsEmpty;
		foreach (var disposable in frame.Disposables)
		{
			if (hasErrors && disposable is BaseProgram program)
			{
				program.HasError = true;
			}
			disposable.Dispose();
		}
	}

	public bool IsInEvent => CurrentFrame.Event != null;
	public bool IsInEventScope(string scope) => CurrentFrame.Event?.EventScope == scope;

	public IReadOnlyList<CallStackFrame> GetFrames()
	{
		return _frames.ToArray();
	}

	public bool IsEventGoalInStack(string id)
	{
		if (string.IsNullOrEmpty(id)) throw new Exception("goal path should not be empty");

		return _frames
			.Where(f => f.Event != null)
			.Any(f => f.Event.Id.Equals(id, StringComparison.OrdinalIgnoreCase) == true);
	}

	public IEnumerable<ExecutedStepWithContext> GetFlatExecutionHistory()
	{
		var frames = _frames.ToArray().Reverse().ToList();

		return frames
			.SelectMany(f => f.GetExecutedSteps().Select(s => new ExecutedStepWithContext(s, f)))
			.OrderBy(s => s.Step.StartedAt);
	}

	public IEnumerable<CompressedStepGroup> GetFlatExecutionHistoryCompressed()
	{
		var allSteps = GetFlatExecutionHistory().ToList();
		if (allSteps.Count == 0) yield break;

		CompressedStepGroup? currentGroup = null;

		foreach (var item in allSteps)
		{
			if (currentGroup == null)
			{
				currentGroup = new CompressedStepGroup(item.Step, item.Frame);
			}
			else if (currentGroup.IsSameStep(item.Step, item.Frame))
			{
				currentGroup.Add(item.Step);
			}
			else
			{
				yield return currentGroup;
				currentGroup = new CompressedStepGroup(item.Step, item.Frame);
			}
		}

		if (currentGroup != null)
			yield return currentGroup;
	}

	public string GetStackTrace(bool compressed = true)
	{
		var trace = new StringBuilder();
		var frames = _frames.ToArray().Reverse();

		foreach (var frame in frames)
		{
			var eventInfo = FormatEventInfo(frame.Event);
			var status = frame.IsComplete ? $"[{frame.Duration.TotalMilliseconds:F1}ms]" : "[running]";
			trace.AppendLine($"  at {frame.Goal.GoalName}{eventInfo} ({frame.Phase}) {status}");

			var steps = compressed
				? frame.GetExecutedStepsCompressed()
				: frame.GetExecutedSteps().Select(s => new CompressedStepGroup(s, frame));

			foreach (var group in steps)
			{
				AppendStepToTrace(trace, group);
			}
		}
		return trace.ToString();
	}

	private string FormatEventInfo(RuntimeEvent? eventBinding)
	{
		if (eventBinding == null || string.IsNullOrEmpty(eventBinding.EventScope))
			return "";

		var info = $" [Event: {eventBinding.EventScope}";
		if (!string.IsNullOrEmpty(eventBinding.EventType))
			info += $".{eventBinding.EventType}";
		return info + "]";
	}

	private void AppendStepToTrace(StringBuilder trace, CompressedStepGroup group)
	{
		var step = group.Step;
		var errorMark = step.Error != null ? " ❌" : "";
		var status = step.IsComplete
			? $"[{group.TotalDuration.TotalMilliseconds:F1}ms]"
			: "[running]";
		var repeat = group.RepeatCount > 1 ? $" (×{group.RepeatCount})" : "";

		trace.AppendLine($"      step {step.Index}: {step.Step.Text}{repeat} {status}{errorMark}");
	}

	public IEnumerable<Goal> GetGoalHierarchy()
	{
		if (!HasFrames) yield break;

		var goal = CurrentFrame.Goal;
		while (goal != null)
		{
			yield return goal;
			goal = goal.ParentGoal;
		}
	}

	public SerializableCallStack ToSerializable(bool compressed = true)
	{
		return SerializableCallStack.FromCallStack(this, compressed);
	}

	public string ToJson(bool indented = false, bool compressed = true)
	{
		var serializable = ToSerializable(compressed);
		return Newtonsoft.Json.JsonConvert.SerializeObject(serializable,
			indented ? Newtonsoft.Json.Formatting.Indented : Newtonsoft.Json.Formatting.None);
	}
}

public class CallStackFrame : VariableContainer
{
	public Goal Goal { get; }
	public GoalStep? CurrentStep { get; private set; }
	public int StepIndex { get; private set; }
	public ExecutionPhase Phase { get; set; }
	public RuntimeEvent? Event { get; }
	public CallStackFrame? ParentFrame { get; }

	public ConcurrentBag<IDisposable> Disposables { get; } = new();
	public ConcurrentBag<IError> Errors { get; } = new();

	private readonly ConcurrentQueue<ExecutedStep> _executedSteps = new();
	private ExecutedStep? _currentExecutingStep;
	private readonly Stopwatch _frameStopwatch;

	public TimeSpan Duration => _frameStopwatch.Elapsed;
	public bool IsComplete { get; private set; }
	public bool IsEvent => Event != null;

	private const int MaxStepsPerFrame = 100_000;

	public CallStackFrame(Goal goal, CallStackFrame? parentFrame, RuntimeEvent? eventBinding = null)
	{
		Goal = goal;
		if (goal.GoalName == "SendDebug" || goal.GoalName == "SendDebugInfo")
		{
			int i = 0;
		}
		ParentFrame = parentFrame;
		Event = eventBinding;
		Phase = ExecutionPhase.None;
		_frameStopwatch = Stopwatch.StartNew();
	}

	public void SetCurrentStep(GoalStep step, int stepIndex)
	{
		_currentExecutingStep?.CompleteIfRunning(null, null);
		if (step.Goal.RelativePrPath != Goal.RelativePrPath)
		{
			//throw new Exception("Here it is");
		}
		CurrentStep = step;
		StepIndex = stepIndex;

		_currentExecutingStep = new ExecutedStep(step, stepIndex);

		if (_executedSteps.Count < MaxStepsPerFrame)
		{
			_executedSteps.Enqueue(_currentExecutingStep);
		}
	}

	public void CompleteCurrentStep(object? returnValue, IError? error)
	{
		if (_currentExecutingStep == null) return;

		_currentExecutingStep.Complete(returnValue, error);

		if (error != null)
		{
			Errors.Add(error);
		}
	}

	public void Complete()
	{
		if (IsComplete) return;

		_frameStopwatch.Stop();
		_currentExecutingStep?.CompleteIfRunning(null, null);
		IsComplete = true;
	}

	public void AddError(IError error)
	{
		Errors.Add(error);
	}

	public IReadOnlyList<ExecutedStep> GetExecutedSteps()
	{
		return _executedSteps.ToArray();
	}

	public IEnumerable<CompressedStepGroup> GetExecutedStepsCompressed()
	{
		var steps = _executedSteps.ToArray();
		if (steps.Length == 0) yield break;

		CompressedStepGroup? currentGroup = null;

		foreach (var step in steps)
		{
			if (currentGroup == null)
			{
				currentGroup = new CompressedStepGroup(step, this);
			}
			else if (currentGroup.Step.Step.Text == step.Step.Text)
			{
				currentGroup.Add(step);
			}
			else
			{
				yield return currentGroup;
				currentGroup = new CompressedStepGroup(step, this);
			}
		}

		if (currentGroup != null)
			yield return currentGroup;
	}

	protected override CallStackFrame? GetParent() => ParentFrame;

	protected override GoalStep? GetStep() => CurrentStep;

	protected override void SetVariableOnEvent(Variable goalVariable)
	{
		if (!IsEvent) return;

		// Traverse up until we find a non-event frame, tracking the topmost frame
		var target = ParentFrame;
		CallStackFrame? topmost = null;

		while (target != null)
		{
			topmost = target;
			if (!target.IsEvent)
			{
				// Found non-event frame, add here and stop
				target._variables[goalVariable.VariableName] = goalVariable;
				return;
			}
			target = target.ParentFrame;
		}

		// All parent frames are events, add to topmost frame
		topmost?._variables[goalVariable.VariableName] = goalVariable;
	}
}

public class ExecutedStep
{
	public GoalStep Step { get; }
	public int Index { get; }
	public int LineNumber { get; }
	public DateTime StartedAt { get; }
	public DateTime? CompletedAt { get; private set; }
	public TimeSpan Duration { get; private set; }
	public object? ReturnValue { get; private set; }
	public IError? Error { get; private set; }
	public bool IsComplete { get; private set; }

	private readonly Stopwatch _stopwatch;

	public ExecutedStep(GoalStep step, int index)
	{
		Step = step;
		Index = index;
		LineNumber = step.LineNumber;
		StartedAt = DateTime.UtcNow;
		_stopwatch = Stopwatch.StartNew();
	}

	public void Complete(object? returnValue, IError? error)
	{
		if (IsComplete) return;

		_stopwatch.Stop();
		Duration = _stopwatch.Elapsed;
		CompletedAt = DateTime.UtcNow;
		ReturnValue = returnValue;
		Error = error;
		IsComplete = true;
	}

	public void CompleteIfRunning(object? returnValue, IError? error)
	{
		if (!IsComplete)
		{
			Complete(returnValue, error);
		}
	}
}

public record ExecutedStepWithContext(ExecutedStep Step, CallStackFrame Frame);

public class CompressedStepGroup
{
	public ExecutedStep Step { get; }
	public CallStackFrame Frame { get; }
	public int RepeatCount { get; private set; } = 1;
	public TimeSpan TotalDuration { get; private set; }

	public CompressedStepGroup(ExecutedStep step, CallStackFrame frame)
	{
		Step = step;
		Frame = frame;
		TotalDuration = step.Duration;
	}

	public void Add(ExecutedStep step)
	{
		RepeatCount++;
		TotalDuration += step.Duration;
	}

	public bool IsSameStep(ExecutedStep other, CallStackFrame otherFrame)
	{
		return Frame.Goal.RelativeGoalPath == otherFrame.Goal.RelativeGoalPath
			&& Step.Step.RelativePrPath == other.Step.RelativePrPath;
	}
}

public enum ExecutionPhase
{
	None,
	ExecutingGoal,
	ExecutingStep
}