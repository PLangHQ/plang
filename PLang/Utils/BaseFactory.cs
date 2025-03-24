using LightInject;
using PLang.Interfaces;

namespace PLang.Utils
{
	public abstract class BaseFactory
	{
		protected IServiceContainer container;

		public BaseFactory(IServiceContainer container)
		{
			this.container = container;
		}

		public string GetServiceName(string key)
		{
			var context = container.GetInstance<PLangAppContext>();

			if (!context.TryGetValue(key, out object? serviceName) || serviceName == null)
			{
				if (!context.TryGetValue(key + "_Default", out serviceName) || serviceName == null)
				{
					throw new Exception($"Could not find service for {key} to load");
				}				
			}
			return serviceName!.ToString()!;
		}
	}
}
