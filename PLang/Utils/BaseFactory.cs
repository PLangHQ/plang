using LightInject;
using PLang.Building.Model;
using PLang.Interfaces;
using PLang.Modules;

namespace PLang.Utils
{
	public abstract class BaseFactory
	{
		protected IServiceContainer container;

		public BaseFactory(IServiceContainer container)
		{
			this.container = container;
		}

		public string GetServiceName(string key)
		{
			var context = container.GetInstance<PLangAppContext>();

			if (context.TryGetValue(key, out object? serviceName) && serviceName != null) return serviceName.ToString()!;
			if (context.TryGetValue(key + "_Default", out serviceName) && serviceName != null) return serviceName.ToString()!;

			throw new Exception($"Could not find service for {key} to load");
		}


		public T GetProgram<T>() where T : BaseProgram
		{
			var program = container.GetInstance<T>();
			var context = container.GetInstance<PLangAppContext>();
			context.TryGetValue(ReservedKeywords.Goal, out var goal);
			context.TryGetValue(ReservedKeywords.Step, out var step);
			context.TryGetValue(ReservedKeywords.Instruction, out var instruction);

			program.Init(container, goal as Goal, step as GoalStep, instruction as Building.Model.Instruction, null);
			return program;
		}
	}
}
