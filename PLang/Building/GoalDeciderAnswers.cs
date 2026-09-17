using PLang.Building.Model;
using System.Collections.Concurrent;

namespace PLang.Building
{
	// The decider's answers for one goal, fetched in one request per phase before the goal's steps
	// are built. A step then reads its answer instead of asking for it.
	//
	// Keyed by GoalStep.LineNumber. Not by step text, because a goal may hold the same step twice
	// and Idea.goal does, writing the same file in two places. Not by GoalStep.Number either: the
	// parser sets that 1 based and StepBuilder overwrites it with the 0 based index while building,
	// so a prefetch keyed by it handed every step the answer belonging to its neighbour. LineNumber
	// is written once by the parser, never again, and is what the build log prints.
	public class GoalDeciderAnswers
	{
		public ConcurrentDictionary<int, ModuleChoice> Modules { get; } = new();
		public ConcurrentDictionary<int, MethodChoice> Methods { get; } = new();
	}

	// Goals build in parallel (Builder.Start), so answers are held per goal and never in one
	// shared bag. Populated before a goal's step loop and only read after that.
	public interface IBuilderDeciderCache
	{
		GoalDeciderAnswers ForGoal(Goal goal);
		void Clear(Goal goal);
	}

	public class BuilderDeciderCache : IBuilderDeciderCache
	{
		private readonly ConcurrentDictionary<string, GoalDeciderAnswers> byGoal = new(StringComparer.OrdinalIgnoreCase);

		private static string Key(Goal goal) => goal.AbsolutePrFilePath ?? goal.AbsoluteGoalPath ?? goal.GoalName;

		public GoalDeciderAnswers ForGoal(Goal goal) => byGoal.GetOrAdd(Key(goal), _ => new GoalDeciderAnswers());

		public void Clear(Goal goal) => byGoal.TryRemove(Key(goal), out _);
	}
}
