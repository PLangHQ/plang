using LightInject;
using PLang.Interfaces;
using PLang.Services.DbService;
using PLang.Services.EncryptionService;
using PLang.Services.LlmService;
using PLang.Services.SettingsService;
using PLang.Utils;

namespace PLang.Container
{
	public static class DefaultRegistrations
	{
		/*

		public static void RegisterOutputStreamFactory(this ServiceContainer container, IEngine engine, Type type, bool isDefault = false, IOutputSink? instance = null, bool setToContext = false)
		{
			SetContext(container, type, ReservedKeywords.Inject_OutputStream, isDefault, setToContext);
			container.Register<IOutputStreamFactory>(factory =>
			{
				var defaultType = GetDefault(container, ReservedKeywords.Inject_OutputStream);
				return new OutputStreamFactory(container, engine, defaultType);
			});

			if (instance != null)
			{
				container.Register(factor =>
				{
					return instance;
				}, instance.GetType().FullName);

				
			}

			
		}
		public static void RegisterOutputSystemStreamFactory(this ServiceContainer container, Type type, bool isDefault = false, IOutputSink? instance = null, bool setToContext = false)
		{
			SetContext(container, type, ReservedKeywords.Inject_OutputSystemStream, isDefault, setToContext);
			container.RegisterSingleton<IOutputSystemStreamFactory>(factory =>
			{
				var defaultType = GetDefault(container, ReservedKeywords.Inject_OutputSystemStream);
				return new OutputSystemStreamFactory(container, defaultType);
			});

			if (instance != null)
			{
				container.RegisterSingleton(factor =>
				{
					return instance;
				}, instance.GetType().FullName);

			}
		}
		*/
		public static void RegisterEncryptionFactory(this ServiceContainer container, Type type, bool isDefault = false, IEncryption? instance = null)
		{
			container.Register<IEncryptionFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_EncryptionService, isDefault);
				return new EncryptionFactory(container);
			});

			if (instance != null)
			{
				container.Register(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}
		}

		public static void RegisterLlmFactory(this ServiceContainer container, Type type, bool isDefault = false, ILlmService? instance = null)
		{
			container.Register<ILlmServiceFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_LLMService, isDefault);
				return new LlmServiceFactory(container);
			});

			if (instance != null)
			{
				container.Register(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}
		}

		public static void RegisterDbFactory(this ServiceContainer container, Type type, bool isDefault = false)
		{
			container.Register<IDbServiceFactory>(factory =>
			{
				if (AppContext.TryGetSwitch("Builder", out bool isBuilder) && isBuilder) ;
				SetContext(container, type, ReservedKeywords.Inject_IDbConnection, isDefault);
				return new DbServiceFactory(container, isBuilder);
			});

		}


		public static void RegisterSettingsRepositoryFactory(this ServiceContainer container, Type type, bool isDefault = false, ISettingsRepository? instance = null)
		{
			container.Register<ISettingsRepositoryFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_SettingsRepository, isDefault);
				return new SettingsRepositoryFactory(container);
			});

			if (instance != null)
			{
				container.RegisterSingleton(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}

			if (isDefault)
			{
				SetContext(container, type, ReservedKeywords.Inject_SettingsRepository, isDefault, true);
			}
		}


		private static void SetContext(IServiceContainer container, Type type, string serviceReservedKeyword, bool isDefault = false, bool setToContext = false)
		{
			var context = container.GetInstance<PLangAppContext>();
			if (setToContext)
			{
				context.AddOrReplace(serviceReservedKeyword, type.FullName);
			}

			if (isDefault)
			{
				context.AddOrReplace(serviceReservedKeyword + "_Default", type.FullName);
			}
		}

		private static string GetDefault(IServiceContainer container, string serviceReservedKeyword)
		{
			var context = container.GetInstance<PLangAppContext>();
			context.TryGetValue(serviceReservedKeyword + "_Default", out object? value);
			if (value == null) throw new Exception("Default registration not available");

			return value.ToString()!;
		}
	}
}
