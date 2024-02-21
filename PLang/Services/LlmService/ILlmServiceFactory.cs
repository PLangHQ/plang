using LightInject;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Services.LlmService
{
	public interface ILlmServiceFactory
	{
		ILlmService CreateHandler();
	}

	public class LlmServiceFactory : BaseFactory, ILlmServiceFactory
	{
		
		public LlmServiceFactory(ServiceContainer container) : base(container)
		{
		}

		public ILlmService CreateHandler()
		{
			var serviceName = GetServiceName(ReservedKeywords.Inject_LLMService);
			return container.GetInstance<ILlmService>(serviceName);
		}
	}
}
