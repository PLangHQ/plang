using LightInject;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Services.SettingsService;

namespace PLang.Modules
{/*
	public abstract class BaseModuleSettings
	{
		private PLangAppContext context;
		private ISettings settings;
		private string classNamespace;

		protected ISettings Settings { get { return settings; } }
		public BaseModuleSettings()
		{

		}

		public void Init(IServiceContainer container)
		{
			var nspace = this.GetType().Namespace;
			if (nspace == null)
			{
				throw new Exception("Namespace cannot be empty");
			}
			this.classNamespace = nspace;

			this.context = container.GetInstance<PLangAppContext>();
			this.settings = container.GetInstance<ISettings>();
		}

	}*/
}
