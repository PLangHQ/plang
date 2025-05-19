using Castle.DynamicProxy;
using LightInject;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Events;
using PLang.Interfaces;
using PLang.Utils;
using System.Reflection;


namespace PLang.Modules
{
	public class ProgramFactory
	{
		private readonly IServiceContainer _container;
		private readonly IEventRuntime eventRuntime;
		readonly ProxyGenerator _proxyGen = new ProxyGenerator();
		
		public ProgramFactory(IServiceContainer container)
		{
			_container = container;
		}

		public T GetProgram<T>(GoalStep goalStep) where T : BaseProgram
		{
			var program = _container.GetInstance<T>();
			if (goalStep == null)
			{
				throw new Exception("Goal step is null;");
			}
			var instruction = goalStep.PrFile as Building.Model.Instruction;
			if (instruction == null) {
				int i = 0;
			}


			typeof(T)
			  .GetMethod("Init", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
			  .Invoke(program, new object[] { _container, goalStep.Goal, goalStep, instruction, null });
			
			var ctor = typeof(T)
				.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
				.OrderByDescending(c => c.GetParameters().Length)
				.First();
			var ctorArgs = ctor.GetParameters()
				.Select(p => _container.GetInstance(p.ParameterType))
				.ToArray();


			var ctx = _container.GetInstance<PLangAppContext>();
			IInterceptor[] interceptor = [new ErrorHandlingInterceptor(_container.GetInstance<IEventRuntime>(), ctx)];
			
			
			var proxy = _proxyGen.CreateClassProxyWithTarget(typeof(T), program, ctorArgs, interceptor) as BaseProgram;
			proxy.Init(_container, goalStep.Goal, goalStep, instruction, null);

			return (T)proxy;
		}


	}
}
