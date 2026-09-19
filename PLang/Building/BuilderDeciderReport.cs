using PLang.Building.Model;
using PLang.Utils;
using System.Collections.Concurrent;

namespace PLang.Building
{
	public enum DeciderOutcome
	{
		// Built by the decider, no llm call for the step. The decider answers which module and
		// which method; since it stopped answering parameters this means the method was decided
		// and the step needed nothing more.
		Decided,
		// The parameters came from the llm, in one request for the whole goal rather than one for
		// this step. Counted apart from Decided: it is an llm answer, just a shared one.
		Batched,
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

		// Where a build's time goes. Counted per goal, because that is the unit a developer edits
		// and waits for. Steps of a goal build in parallel and so do goals, so these add up to more
		// than the clock: they say what was spent, not how long it took.
		void RecordLlmCall(Goal goal, TimeSpan took);
		void RecordDeciderCall(Goal goal, TimeSpan took);
		void RecordGoalBuilt(Goal goal, TimeSpan took);

		string? Summary();
		string? Timings();
	}

	public class BuilderDeciderReport : IBuilderDeciderReport
	{
		private record Entry(string Goal, int Line, string Step, DeciderOutcome Outcome, string? Reason);

		private readonly ConcurrentDictionary<string, Entry> entries = new();

		private record Spend(int LlmCalls, double LlmSeconds, int DeciderCalls, double DeciderSeconds, double GoalSeconds);
		private readonly ConcurrentDictionary<string, Spend> spend = new();

		private void Add(Goal goal, Func<Spend, Spend> change)
		{
			spend.AddOrUpdate(goal.GoalName, _ => change(new Spend(0, 0, 0, 0, 0)), (_, current) => change(current));
		}

		public void RecordLlmCall(Goal goal, TimeSpan took)
			=> Add(goal, s => s with { LlmCalls = s.LlmCalls + 1, LlmSeconds = s.LlmSeconds + took.TotalSeconds });

		public void RecordDeciderCall(Goal goal, TimeSpan took)
			=> Add(goal, s => s with { DeciderCalls = s.DeciderCalls + 1, DeciderSeconds = s.DeciderSeconds + took.TotalSeconds });

		public void RecordGoalBuilt(Goal goal, TimeSpan took)
			=> Add(goal, s => s with { GoalSeconds = s.GoalSeconds + took.TotalSeconds });

		public string? Timings()
		{
			if (spend.IsEmpty) return null;

			var rows = spend.Where(p => p.Value.GoalSeconds > 0 || p.Value.LlmCalls > 0 || p.Value.DeciderCalls > 0)
				.OrderByDescending(p => p.Value.GoalSeconds).ToList();
			if (rows.Count == 0) return null;

			var text = new System.Text.StringBuilder();
			text.AppendLine("Where the build spent its time, per goal. Goals and the steps inside them build");
			text.AppendLine("in parallel, so these add up to more than the clock:");
			text.AppendLine($"  {"goal",-26} {"built in",9} {"llm",5} {"llm time",9} {"decider",8} {"decider time",12}");
			foreach (var row in rows)
			{
				var s = row.Value;
				text.AppendLine($"  {row.Key.MaxLength(26),-26} {s.GoalSeconds,8:0.0}s {s.LlmCalls,5} {s.LlmSeconds,8:0.0}s {s.DeciderCalls,8} {s.DeciderSeconds,11:0.0}s");
			}
			var totals = rows.Select(r => r.Value).ToList();
			text.AppendLine($"  {"all goals",-26} {totals.Sum(t => t.GoalSeconds),8:0.0}s {totals.Sum(t => t.LlmCalls),5} {totals.Sum(t => t.LlmSeconds),8:0.0}s {totals.Sum(t => t.DeciderCalls),8} {totals.Sum(t => t.DeciderSeconds),11:0.0}s");
			return text.ToString().TrimEnd();
		}

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

			int batched = all.Count(e => e.Outcome == DeciderOutcome.Batched);

			var text = new System.Text.StringBuilder();
			text.AppendLine("Decider:");
			text.AppendLine($"  {decided} of {offered} steps needed no llm call of their own" +
				(offered == 0 ? "" : $", {100.0 * decided / offered:0}%"));
			if (batched > 0) text.AppendLine($"  {batched} steps had their parameters built by the llm in one request for the goal");
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
