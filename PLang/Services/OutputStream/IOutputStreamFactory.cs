using LightInject;
using PLang.Interfaces;
using PLang.Utils;

namespace PLang.Services.OutputStream
{
	public interface IOutputStreamFactory
	{
		IOutputStream CreateHandler(string? name = null);
		IOutputStreamFactory SetContext(string? name);
	}

	public class OutputStreamFactory : BaseFactory, IOutputStreamFactory
	{
		private readonly PLangAppContext appContext;
		private string defaultType;
		private string currentType;

		public OutputStreamFactory(IServiceContainer container, string defaultType) : base(container) 
		{
			this.appContext = container.GetInstance<PLangAppContext>();
			this.defaultType = defaultType;
			this.currentType = defaultType;
		}

		public IOutputStreamFactory SetContext(string? name)
		{
			if (string.IsNullOrEmpty(name))
			{
				appContext.Remove(ReservedKeywords.Inject_OutputStream);
				this.currentType = defaultType;

				return this;
			}
			this.currentType = name;
			appContext.AddOrReplace(ReservedKeywords.Inject_OutputStream, name);
			return this;
		}

		public IOutputStream CreateHandler(string? name = null) 
		{
			var serviceName = (name != null) ? name : currentType;

			return container.GetInstance<IOutputStream>(serviceName);
		}
	}
}
