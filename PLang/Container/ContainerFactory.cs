using LightInject;
using PLang.Errors.Handlers;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Services.OutputStream;
using PLang.Utils;

namespace PLang.Container
{
    public interface IServiceContainerFactory
    {
        ServiceContainer CreateContainer(PLangAppContext context, string path, string goalPath, 
			IErrorHandlerFactory errorHandlerFactory, IErrorSystemHandlerFactory errorSystemHandlerFactory);
    }
	/*
    public class ServiceContainerFactory : IServiceContainerFactory
    {
        public ServiceContainer CreateContainer(PLangAppContext context, string absoluteAppStartupPath, string relativeAppStartupPath, 
            IErrorHandlerFactory errorHandlerFactory, IErrorSystemHandlerFactory errorSystemHandlerFactory)
        {
            var container = new ServiceContainer();
            string? askUserHandler = context.GetOrDefault(ReservedKeywords.Inject_AskUserHandler, "");
            if (askUserHandler == null)
            {
                throw new NullReferenceException($"Could not find askUserHandler. It must be defined");
            }
            container.RegisterForPLang(absoluteAppStartupPath, relativeAppStartupPath, errorHandlerFactory, errorSystemHandlerFactory);

            return container;
        }
    }*/

}
