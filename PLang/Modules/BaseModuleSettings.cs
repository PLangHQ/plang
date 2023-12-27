using LightInject;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Services.SettingsService;

namespace PLang.Modules
{
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

		public void AddOrReplace(string key, object value)
		{
			
			if (ContainsKey(key))
			{
				context[classNamespace + "_" + key] = value;
			} else
			{
				context.Add(classNamespace + "_" + key, value);
			}
		}
		public void Add(string key, object value)
		{

			if (!ContainsKey(key))
			{
				context.Add(classNamespace + "_" + key, value);
			}
		}
		public void Remove(string key)
		{
			if (ContainsKey(key))
			{
				context.Remove(classNamespace + "_" + key);
			}
		}

		public bool ContainsKey(string key)
		{
			return context.ContainsKey(classNamespace + "_" + key);
		}

		public object? GetByKey(string key)
		{
			if (ContainsKey(key))
			{
				return context[classNamespace + "_" + key];
			}

			throw new RuntimeException($"Could not find {key} in settings.");
		}
	}
}
