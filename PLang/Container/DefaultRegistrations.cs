using LightInject;
using PLang.Exceptions.AskUser;
using PLang.Exceptions.Handlers;
using PLang.Interfaces;
using PLang.Services.LlmService;
using PLang.Services.OutputStream;
using PLang.Services.SettingsService;
using PLang.Utils;

namespace PLang.Container
{
	public static class DefaultRegistrations
	{
		

		public static void RegisterExceptionHandlerFactory(this ServiceContainer container, Type type, bool isDefault = false, IExceptionHandler? instance = null)
		{
			container.Register<IExceptionHandlerFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_ExceptionHandler, isDefault);
				return new ExceptionHandlerFactory(container);
			});

			if (instance != null)
			{
				container.Register(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}
		}
		public static void RegisterAskUserHandlerFactory(this ServiceContainer container, Type type, bool isDefault = false, IAskUserHandler? instance = null)
		{
			container.Register<IAskUserHandlerFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_AskUserHandler, isDefault);
				return new AskUserHandlerFactory(container);
			});

			if (instance != null)
			{
				container.Register(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}
		}

		public static void RegisterOutputStreamFactory(this ServiceContainer container, Type type, bool isDefault = false, IOutputStream? instance = null)
		{
			container.RegisterSingleton<IOutputStreamFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_OutputStream, isDefault);
				return new OutputStreamFactory(container);
			});

			if (instance != null)
			{
				container.RegisterSingleton(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}
		}
		
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
		}


		private static void SetContext(ServiceContainer container, Type type, string serviceReservedKeyword, bool isDefault = false)
		{
			var context = container.GetInstance<PLangAppContext>();
			if (!context.ContainsKey(serviceReservedKeyword))
			{
				context.AddOrReplace(serviceReservedKeyword, type.FullName);
			}

			if (isDefault && AppContext.GetData(serviceReservedKeyword) == null)
			{
				AppContext.SetData(serviceReservedKeyword, type.FullName);
			}
		}
	}
}
