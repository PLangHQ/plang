using PLang.Building.Model;
using PLang.Utils;
using System.Collections.Concurrent;

namespace PLang.Building
{
	public enum DeciderOutcome
	{
		// Built by the decider, no llm call for the step.
		Decided,
		// The module builds its own instruction, because what the step needs is written rather than
		// chosen: sql, c#, a regex. The llm is the right answer for these and always will be.
		ModuleBuildsItsOwn,
		// The decider was asked and could not answer. These are the ones worth reading: each is
		// either a description that does not say enough, or a value the decider cannot yet express.
		FellBack,
		// Not offered to the decider at all: it is off, the goal is a system goal, or the step is
		// being retried after a failure, where the llm gets the second attempt by design.
		NotOffered,
	}

	// What the decider managed, step by step, for one build. Printed when the build ends.
	//
	// The number that matters is not how many steps were decided, it is how many fell back that
	// should not have. A build where the llm writes the sql and nothing else is working; a build
	// where it quietly starts writing half the steps is not, and without this the difference is
	// invisible until someone reads a log line at a time.
	public interface IBuilderDeciderReport
	{
		void Record(Goal goal, GoalStep step, DeciderOutcome outcome, string? reason = null);
		string? Summary();
	}

	public class BuilderDeciderReport : IBuilderDeciderReport
	{
		private record Entry(string Goal, int Line, string Step, DeciderOutcome Outcome, string? Reason);

		private readonly ConcurrentDictionary<string, Entry> entries = new();

		public void Record(Goal goal, GoalStep step, DeciderOutcome outcome, string? reason = null)
		{
			var key = $"{goal.RelativeGoalPath}:{step.Index}";
			entries[key] = new Entry(goal.GoalName, step.LineNumber, step.Text.Trim().Replace("\n", " "), outcome, reason);
		}

		public string? Summary()
		{
			if (entries.IsEmpty) return null;

			var all = entries.Values.ToList();
			int decided = all.Count(e => e.Outcome == DeciderOutcome.Decided);
			int generated = all.Count(e => e.Outcome == DeciderOutcome.ModuleBuildsItsOwn);
			var fellBack = all.Where(e => e.Outcome == DeciderOutcome.FellBack).ToList();
			int offered = decided + fellBack.Count;

			var text = new System.Text.StringBuilder();
			text.AppendLine("Decider:");
			text.AppendLine($"  {decided} of {offered} steps built without the llm" +
				(offered == 0 ? "" : $", {100.0 * decided / offered:0}%"));
			if (generated > 0) text.AppendLine($"  {generated} steps the module writes itself, sql and the like, which the llm is meant to build");

			if (fellBack.Count > 0)
			{
				text.AppendLine($"  {fellBack.Count} went to the llm and did not have to. Each one is a description that does not say enough, or a value the decider cannot express yet:");
				foreach (var entry in fellBack.OrderBy(e => e.Goal).ThenBy(e => e.Line))
				{
					text.AppendLine($"    {entry.Goal}:{entry.Line}  {entry.Step.MaxLength(64)}");
					text.AppendLine($"        {entry.Reason}");
				}
			}
			return text.ToString().TrimEnd();
		}
	}
}
