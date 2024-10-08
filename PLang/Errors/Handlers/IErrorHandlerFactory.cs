﻿using LightInject;
using PLang.Utils;

namespace PLang.Errors.Handlers
{
	public interface IErrorHandlerFactory
    {
		IErrorHandler CreateHandler();
    }
    public interface IErrorSystemHandlerFactory
	{
		IErrorHandler CreateHandler();
    }

    public class ErrorHandlerFactory : BaseFactory, IErrorHandlerFactory
    {

        public ErrorHandlerFactory(ServiceContainer container) : base(container)
        {
        }

        public IErrorHandler CreateHandler()
        {
            var serviceName = GetServiceName(ReservedKeywords.Inject_ErrorHandler);
            return container.GetInstance<IErrorHandler>(serviceName);
        }
    }

	public class ErrorSystemHandlerFactory : BaseFactory, IErrorSystemHandlerFactory
	{

		public ErrorSystemHandlerFactory(ServiceContainer container) : base(container)
		{
		}

		public IErrorHandler CreateHandler()
		{
			var serviceName = GetServiceName(ReservedKeywords.Inject_ErrorSystemHandler);
			return container.GetInstance<IErrorHandler>(serviceName);
		}
	}
}
