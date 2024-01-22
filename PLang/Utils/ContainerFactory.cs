using LightInject;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.Services.OutputStream;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
    public interface IServiceContainerFactory
	{
		ServiceContainer CreateContainer(PLangAppContext context, string path, string goalPath, IOutputStream outputStream);
	}

	public class ServiceContainerFactory : IServiceContainerFactory
	{
		public ServiceContainer CreateContainer(PLangAppContext context, string path, string goalPath, IOutputStream outputStream)
		{
			var container = new ServiceContainer();
			string? askUserHandler = context.GetOrDefault<string>(ReservedKeywords.Inject_AskUserHandler, "");
			if (askUserHandler == null)
			{
				throw new NullReferenceException($"Could not find askUserHandler. It must be defined");
			}
			container.RegisterForPLang(path, goalPath, askUserHandler, outputStream);

			return container;
		}
	}

}
