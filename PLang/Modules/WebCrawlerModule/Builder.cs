using PLang.Building.Model;


namespace PLang.Modules.WebCrawlerModule
{
	public class Builder : BaseBuilder
	{
		public Builder() : base() { }

		public override Task<Instruction> Build(GoalStep goalStep)
		{
			AppendToAssistantCommand("Make sure to convert html tags into correct css selector format");
			return base.Build<GenericFunction>(goalStep);
		}


	}
}

