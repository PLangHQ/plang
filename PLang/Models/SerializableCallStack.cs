using PLang.Building.Model;
using PLang.Errors;
using PLang.Events;

namespace PLang.Runtime;

public class SerializableCallStack
{
	public List<SerializableCallStackFrame> Frames { get; set; } = new();
	public int Depth { get; set; }
	public string? CurrentGoalName { get; set; }
	public string? CurrentStepText { get; set; }
	public int? CurrentStepIndex { get; set; }
	public string? EventScope { get; set; }
	public string? EventType { get; set; }
	public string Phase { get; set; } = ExecutionPhase.None.ToString();
	public double TotalDurationMs { get; set; }
	public bool IsCompressed { get; set; }

	public static SerializableCallStack FromCallStack(CallStack callStack, bool compressed = false)
	{
		var frames = callStack.GetFrames();
		var hasFrames = frames.Count > 0;
		var currentFrame = hasFrames ? frames[0] : null;

		return new SerializableCallStack
		{
			Frames = frames.Reverse()
				.Select(f => SerializableCallStackFrame.FromFrame(f, compressed))
				.ToList(),
			Depth = callStack.Depth,
			CurrentGoalName = currentFrame?.Goal?.GoalName,
			CurrentStepText = currentFrame?.CurrentStep?.Text,
			CurrentStepIndex = currentFrame?.StepIndex,
			EventScope = currentFrame?.Event?.EventScope,
			EventType = currentFrame?.Event?.EventType,
			Phase = currentFrame?.Phase.ToString() ?? ExecutionPhase.None.ToString(),
			TotalDurationMs = frames.Sum(f => f.Duration.TotalMilliseconds),
			IsCompressed = compressed
		};
	}
}

public class SerializableCallStackFrame
{
	public string? Name { get; set; }
	public string? Path { get; set; }
	public string? AbsolutePath { get; set; }
	public string Phase { get; set; } = ExecutionPhase.None.ToString();
	public string? EventScope { get; set; }
	public string? EventType { get; set; }
	public double DurationMs { get; set; }
	public bool IsComplete { get; set; }
	public bool IsEvent { get; set; }
	public int TotalStepCount { get; set; }

	public List<SerializableExecutedStep> ExecutedSteps { get; set; } = new();
	public List<SerializableVariable> Variables { get; set; } = new();
	public List<string> Errors { get; set; } = new();

	public static SerializableCallStackFrame FromFrame(CallStackFrame frame, bool compressed = false)
	{
		var allSteps = frame.GetExecutedSteps();

		var steps = compressed
			? frame.GetExecutedStepsCompressed()
				.Select(SerializableExecutedStep.FromCompressedGroup)
				.ToList()
			: allSteps
				.Select(SerializableExecutedStep.FromExecutedStep)
				.ToList();

		return new SerializableCallStackFrame
		{
			Name = frame.Goal?.GoalName,
			Path = frame.Goal?.RelativeGoalPath,
			AbsolutePath = frame.Goal?.AbsoluteGoalPath,
			Phase = frame.Phase.ToString(),
			EventScope = frame.Event?.EventScope,
			EventType = frame.Event?.EventType,
			DurationMs = frame.Duration.TotalMilliseconds,
			IsComplete = frame.IsComplete,
			IsEvent = frame.IsEvent,
			TotalStepCount = allSteps.Count,
			ExecutedSteps = steps,
			Variables = frame.GetVariables()
				.Select(SerializableVariable.FromVariable)
				.ToList(),
			Errors = frame.Errors.Select(e => e.Message ?? e.ToString() ?? "Unknown error").ToList()
		};
	}
}

public class SerializableExecutedStep
{
	public string? StepText { get; set; }
	public int Index { get; set; }
	public int LineNumber { get; set; }
	public string? PrPath { get; set; }
	public string? Path { get; set; }
	public string? AbsolutePath { get; set; }
	public DateTime StartedAt { get; set; }
	public DateTime? CompletedAt { get; set; }
	public double DurationMs { get; set; }
	public bool IsComplete { get; set; }
	public bool HasError { get; set; }
	public string? ErrorMessage { get; set; }
	public string? ReturnValueType { get; set; }
	public string? ReturnValuePreview { get; set; }

	public int RepeatCount { get; set; } = 1;
	public double TotalDurationMs { get; set; }

	public static SerializableExecutedStep FromExecutedStep(ExecutedStep step)
	{
		var durationMs = step.Duration.TotalMilliseconds;

		return new SerializableExecutedStep
		{
			StepText = step.Step?.Text,
			Path = step.Step?.RelativeGoalPath,
			AbsolutePath = step.Step?.Goal.AbsoluteGoalPath,
			PrPath = step.Step?.RelativePrPath,
			Index = step.Index,
			LineNumber = step.LineNumber,
			StartedAt = step.StartedAt,
			CompletedAt = step.CompletedAt,
			DurationMs = durationMs,
			IsComplete = step.IsComplete,
			HasError = step.Error != null,
			ErrorMessage = step.Error?.Message,
			ReturnValueType = step.ReturnValue?.GetType().Name,
			ReturnValuePreview = GetValuePreview(step.ReturnValue),
			RepeatCount = 1,
			TotalDurationMs = durationMs
		};
	}

	public static SerializableExecutedStep FromCompressedGroup(CompressedStepGroup group)
	{
		var step = group.Step;

		return new SerializableExecutedStep
		{
			StepText = step.Step?.Text,
			Path = step.Step?.RelativeGoalPath,
			AbsolutePath = step.Step?.Goal.AbsoluteGoalPath,
			PrPath = step.Step?.RelativePrPath,
			Index = step.Index,
			LineNumber = step.LineNumber,
			StartedAt = step.StartedAt,
			CompletedAt = step.CompletedAt,
			DurationMs = step.Duration.TotalMilliseconds,
			IsComplete = step.IsComplete,
			HasError = step.Error != null,
			ErrorMessage = step.Error?.Message,
			ReturnValueType = step.ReturnValue?.GetType().Name,
			ReturnValuePreview = GetValuePreview(step.ReturnValue),
			RepeatCount = group.RepeatCount,
			TotalDurationMs = group.TotalDuration.TotalMilliseconds
		};
	}

	private static string? GetValuePreview(object? value, int maxLength = 100)
	{
		if (value == null) return null;

		try
		{
			var str = value switch
			{
				string s => s,
				byte[] bytes => $"byte[{bytes.Length}]",
				System.Collections.IEnumerable list when list is not string => $"[{list.Cast<object>().Count()} items]",
				_ => value.ToString()
			};

			if (str != null && str.Length > maxLength)
			{
				return str.Substring(0, maxLength) + "...";
			}
			return str;
		}
		catch
		{
			return $"<{value.GetType().Name}>";
		}
	}
}

public class SerializableVariable
{
	public string Name { get; set; } = "";
	public string? Type { get; set; }
	public string? ValuePreview { get; set; }

	public static SerializableVariable FromVariable(Variable variable)
	{
		return new SerializableVariable
		{
			Name = variable.VariableName,
			Type = variable.Value?.GetType().Name,
			ValuePreview = GetValuePreview(variable.Value)
		};
	}

	private static string? GetValuePreview(object? value, int maxLength = 200)
	{
		if (value == null) return null;

		try
		{
			var str = value switch
			{
				string s => s,
				byte[] bytes => $"byte[{bytes.Length}]",
				System.Collections.IEnumerable list when list is not string => $"[{list.Cast<object>().Count()} items]",
				_ => value.ToString()
			};

			if (str != null && str.Length > maxLength)
			{
				return str.Substring(0, maxLength) + "...";
			}
			return str;
		}
		catch
		{
			return $"<{value.GetType().Name}>";
		}
	}
}