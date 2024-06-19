using LightInject;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Services.OutputStream
{
	public class OutputSystemStreamFactory : BaseFactory, IOutputSystemStreamFactory
	{
		private readonly PLangAppContext appContext;
		private string defaultType;
		private string currentType;

		public OutputSystemStreamFactory(IServiceContainer container, string defaultType) : base(container)
		{
			this.appContext = container.GetInstance<PLangAppContext>();
			this.defaultType = defaultType;
			this.currentType = defaultType;
		}

		public IOutputSystemStreamFactory SetContext(string? name)
		{
			if (string.IsNullOrEmpty(name))
			{
				appContext.Remove(ReservedKeywords.Inject_OutputSystemStream);
				this.currentType = defaultType;

				return this;
			}
			this.currentType = name;
			appContext.AddOrReplace(ReservedKeywords.Inject_OutputSystemStream, name);
			return this;
		}

		public IOutputStream CreateHandler(string? name = null)
		{
			var serviceName = (name != null) ? name : currentType;

			return container.GetInstance<IOutputStream>(serviceName);
		}
	}
}
