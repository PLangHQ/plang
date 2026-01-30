using LightInject;
using PLang.Interfaces;
using PLang.Utils;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Services.EventSourceService;

public interface IEventSourceFactory
{
	IEventSourceRepository CreateHandler(DataSource dataSource);
}

public class EventSourceFactory : BaseFactory, IEventSourceFactory
{
	
	public EventSourceFactory(IServiceContainer container) : base(container)
	{
	}

	public IEventSourceRepository CreateHandler(DataSource dataSource)
	{
		if (!dataSource.KeepHistory) {
			return new DisableEventSourceRepository();
		}
		return new SqliteEventSourceRepository(container.GetInstance<IPLangFileSystem>(), container.GetInstance<IEncryption>());
	}
}
