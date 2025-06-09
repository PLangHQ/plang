using PLang.Building.Model;
using PLang.Errors.Builder;


namespace PLang.Modules.WebCrawlerModule
{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public override Task<(Instruction? Instruction, IBuilderError? BuilderError)> Build(GoalStep goalStep, IBuilderError? previousBuildError = null)
		{
			AppendToAssistantCommand("Make sure to convert html tags into correct css selector format");
			return base.Build<GenericFunction>(goalStep, previousBuildError);
		}


	}
}

