using LightInject;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Services.LlmService
{
	public interface IEncryptionFactory
	{
		IEncryption CreateHandler();
	}

	public class EncryptionFactory : BaseFactory, IEncryptionFactory
	{
		
		public EncryptionFactory(ServiceContainer container) : base(container)
		{
		}

		public IEncryption CreateHandler()
		{
			var serviceName = GetServiceName(ReservedKeywords.Inject_EncryptionService);
			return container.GetInstance<IEncryption>(serviceName);
		}
	}
}
