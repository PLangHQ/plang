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

		public T GetProgram<T>() where T : BaseProgram
		{
			var program = _container.GetInstance<T>();
			
			var ctx = _container.GetInstance<PLangAppContext>();
			ctx.TryGetValue(ReservedKeywords.Goal, out var goal);
			ctx.TryGetValue(ReservedKeywords.Step, out var step);
			ctx.TryGetValue(ReservedKeywords.Instruction, out var instr);
			
			typeof(T)
			  .GetMethod("Init", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
			  .Invoke(program, new object[] { _container, goal, step, instr, null });
			
			var ctor = typeof(T)
				.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
				.OrderByDescending(c => c.GetParameters().Length)
				.First();
			var ctorArgs = ctor.GetParameters()
				.Select(p => _container.GetInstance(p.ParameterType))
				.ToArray();

			
			IInterceptor[] interceptor = [new ErrorHandlingInterceptor(_container.GetInstance<IEventRuntime>(), ctx)];

			
			var proxy = _proxyGen.CreateClassProxyWithTarget(typeof(T), program, ctorArgs, interceptor) as BaseProgram;
			proxy.Init(_container, (Goal)goal, (GoalStep)step, (Building.Model.Instruction)instr, null);

			return (T)proxy;
		}


	}
}
