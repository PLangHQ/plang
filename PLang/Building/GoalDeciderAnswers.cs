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
		private readonly ConcurrentDictionary<int, (string Text, Modules.BaseBuilder.GenericFunction Function, Models.LlmRequest Request)> functions = new();

		public int ModuleCount => modules.Count;
		public int MethodCount => methods.Count;
		public int FunctionCount => functions.Count;

		// A whole function, method and parameters and return values together, built for the whole
		// goal in one llm request. The request itself is kept with it: it is what gets written into
		// the .pr as the step's LlmRequest, so a step built this way still records what was asked.
		public void SetFunction(GoalStep step, Modules.BaseBuilder.GenericFunction function, Models.LlmRequest request)
			=> functions[step.Index] = (step.Text, function, request);

		public (Modules.BaseBuilder.GenericFunction Function, Models.LlmRequest Request)? Function(GoalStep step)
			=> functions.TryGetValue(step.Index, out var found) && found.Text == step.Text
				? (found.Function, found.Request) : null;

		// A step whose batched answer did not survive validation must not be handed the same answer
		// again on the retry, or the build loops on it.
		public void ForgetFunction(GoalStep step) => functions.TryRemove(step.Index, out _);

		// How a step is run: its error handlers, caching, whether it is waited for, its log level.
		// Asked for the whole goal in the same request as the goal's description, because neither
		// needs the method information and both are read off the step's own words.
		private readonly ConcurrentDictionary<int, (string Text, Model.StepProperties Properties)> properties = new();

		public int PropertyCount => properties.Count;

		public void SetProperties(GoalStep step, Model.StepProperties value) => properties[step.Index] = (step.Text, value);

		public Model.StepProperties? Properties(GoalStep step)
			=> properties.TryGetValue(step.Index, out var found) && found.Text == step.Text ? found.Properties : null;

		// The goal's own description and the variables it has to be handed, answered beside the
		// step properties. Held against the goal's text so a goal edited and built again does not
		// read the previous version's description.
		private (string Text, string Description, Dictionary<string, string>? Incoming)? description;

		public void SetDescription(Goal goal, string text, string value, Dictionary<string, string>? incoming)
			=> description = (text, value, incoming);

		public (string Description, Dictionary<string, string>? Incoming)? Description(string goalText)
			=> description != null && description.Value.Text == goalText
				? (description.Value.Description, description.Value.Incoming) : null;

		public void SetModule(GoalStep step, ModuleChoice choice) => modules[step.Index] = (step.Text, choice);
		public void SetMethod(GoalStep step, MethodChoice choice) => methods[step.Index] = (step.Text, choice);

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
			functions.Clear();
			properties.Clear();
			description = null;
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
