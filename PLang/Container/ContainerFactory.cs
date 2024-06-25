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
            IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory, 
            IErrorHandlerFactory exceptionHandlerFactory, IAskUserHandlerFactory askUserHandlerFactory);
    }

    public class ServiceContainerFactory : IServiceContainerFactory
    {
        public ServiceContainer CreateContainer(PLangAppContext context, string absoluteAppStartupPath, string relativeAppStartupPath,
            IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory, IErrorHandlerFactory exceptionHandlerFactory, IAskUserHandlerFactory askUserHandlerFactory)
        {
            var container = new ServiceContainer();
            string? askUserHandler = context.GetOrDefault(ReservedKeywords.Inject_AskUserHandler, "");
            if (askUserHandler == null)
            {
                throw new NullReferenceException($"Could not find askUserHandler. It must be defined");
            }
            container.RegisterForPLang(absoluteAppStartupPath, relativeAppStartupPath, askUserHandlerFactory, outputStreamFactory, outputSystemStreamFactory, exceptionHandlerFactory);

            return container;
        }
    }

}
