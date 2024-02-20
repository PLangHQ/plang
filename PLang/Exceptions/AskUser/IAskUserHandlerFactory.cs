using LightInject;
using PLang.Exceptions.Handlers;
using PLang.Interfaces;
using PLang.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Exceptions.AskUser
{
	public interface IAskUserHandlerFactory
	{
		IAskUserHandler CreateHandler();
	}

	public class AskUserHandlerFactory : BaseFactory, IAskUserHandlerFactory
	{
		
		public AskUserHandlerFactory(ServiceContainer container) : base(container) 
		{
		}

		public IAskUserHandler CreateHandler()
		{
			var serviceName = GetServiceName(ReservedKeywords.Inject_AskUserHandler);
			return container.GetInstance<IAskUserHandler>(serviceName);
		}
	}
}
