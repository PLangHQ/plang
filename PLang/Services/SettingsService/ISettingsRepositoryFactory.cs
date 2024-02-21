using LightInject;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Services.SettingsService
{

	public interface ISettingsRepositoryFactory
	{
		ISettingsRepository CreateHandler();
	}

	public class SettingsRepositoryFactory : BaseFactory, ISettingsRepositoryFactory
	{
		public SettingsRepositoryFactory(IServiceContainer container) : base(container)
		{
		}

		public ISettingsRepository CreateHandler()
		{
			var serviceName = GetServiceName(ReservedKeywords.Inject_SettingsRepository);

			return container.GetInstance<ISettingsRepository>(serviceName);
		}
	}

}
