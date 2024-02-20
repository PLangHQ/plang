using LightInject;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Exceptions.Handlers
{
	public interface IExceptionHandlerFactory
	{
		IExceptionHandler CreateHandler();
	}

	public class ExceptionHandlerFactory : BaseFactory, IExceptionHandlerFactory
	{

		public ExceptionHandlerFactory(ServiceContainer container) : base(container) 
		{
		}

		public IExceptionHandler CreateHandler()
		{
			var serviceName = GetServiceName(ReservedKeywords.Inject_ExceptionHandler);
			return container.GetInstance<IExceptionHandler>(serviceName);
		}
	}
}
