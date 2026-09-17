using PLang.Building.Model;
using System.Collections.Concurrent;

namespace PLang.Building
{
	// The decider's answers for one goal, fetched in one request per phase before the goal's steps
	// are built. A step then reads its answer instead of asking for it.
	//
	// An answer is stored under GoalStep.Index and handed back only when the step text still
	// matches. Index alone is not enough, because this cache outlives a single build: the same
	// process builds a goal again when a step fails and retries, and again every time a running app
	// rebuilds a goal it has just edited. Insert a step at index 3 and every later step would be
	// handed the answer belonging to its neighbour. Text alone is not enough either, since a goal
	// may hold the same step twice, and Idea.goal does, writing the same file in two places.
	// Together they are exact, and it is how StepHasBeenBuild already decides whether a step is
	// still the one that was built.
	public class GoalDeciderAnswers
	{
		private readonly ConcurrentDictionary<int, (string Text, ModuleChoice Choice)> modules = new();
		private readonly ConcurrentDictionary<int, (string Text, MethodChoice Choice)> methods = new();
		private readonly ConcurrentDictionary<int, (string Text, Dictionary<string, ParameterChoice> Choices)> parameters = new();
		private readonly ConcurrentDictionary<int, (string Text, ParameterChoice Choice)> returns = new();

		public int ModuleCount => modules.Count;
		public int MethodCount => methods.Count;
		public int ParameterStepCount => parameters.Count;

		public void SetModule(GoalStep step, ModuleChoice choice) => modules[step.Index] = (step.Text, choice);
		public void SetMethod(GoalStep step, MethodChoice choice) => methods[step.Index] = (step.Text, choice);
		public void SetParameters(GoalStep step, Dictionary<string, ParameterChoice> choices) => parameters[step.Index] = (step.Text, choices);

		public Dictionary<string, ParameterChoice>? Parameters(GoalStep step)
			=> parameters.TryGetValue(step.Index, out var found) && found.Text == step.Text ? found.Choices : null;

		// The variable a step writes its result into, asked alongside the methods. A parameter can
		// depend on it, and questions in one request are answered in isolation, so it has to be
		// known before the parameters are asked, not in the same request as them.
		public void SetReturn(GoalStep step, ParameterChoice choice) => returns[step.Index] = (step.Text, choice);

		public ParameterChoice? Return(GoalStep step)
			=> returns.TryGetValue(step.Index, out var found) && found.Text == step.Text ? found.Choice : null;

		public ModuleChoice? Module(GoalStep step)
			=> modules.TryGetValue(step.Index, out var found) && found.Text == step.Text ? found.Choice : null;

		public MethodChoice? Method(GoalStep step)
			=> methods.TryGetValue(step.Index, out var found) && found.Text == step.Text ? found.Choice : null;

		// A goal that is built again starts from nothing, so a prefetch that fails or is skipped can
		// never leave an earlier build's answers in place for the new one to read.
		public void Clear()
		{
			modules.Clear();
			methods.Clear();
			parameters.Clear();
			returns.Clear();
		}
	}

	// Goals build in parallel (Builder.Start), so answers are held per goal and never in one
	// shared bag. Populated before a goal's step loop and only read after that.
	public interface IBuilderDeciderCache
	{
		GoalDeciderAnswers ForGoal(Goal goal);
	}

	public class BuilderDeciderCache : IBuilderDeciderCache
	{
		private readonly ConcurrentDictionary<string, GoalDeciderAnswers> byGoal = new(StringComparer.OrdinalIgnoreCase);

		private static string Key(Goal goal) => goal.AbsolutePrFilePath ?? goal.AbsoluteGoalPath ?? goal.GoalName;

		public GoalDeciderAnswers ForGoal(Goal goal) => byGoal.GetOrAdd(Key(goal), _ => new GoalDeciderAnswers());
	}
}
