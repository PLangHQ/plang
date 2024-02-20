using LightInject;
using PLang.Utils;

namespace PLang.Services.OutputStream
{
	public interface IOutputStreamFactory
	{
		IOutputStream CreateHandler();
	}

	public class OutputStreamFactory : BaseFactory, IOutputStreamFactory
	{
		public OutputStreamFactory(ServiceContainer container) : base(container) 
		{
		}

		public IOutputStream CreateHandler()
		{
			var serviceName = GetServiceName(ReservedKeywords.Inject_OutputStream);

			return container.GetInstance<IOutputStream>(serviceName);
		}
	}
}
