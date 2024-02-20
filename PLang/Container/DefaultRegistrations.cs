using LightInject;
using PLang.Exceptions.AskUser;
using PLang.Exceptions.Handlers;
using PLang.Interfaces;
using PLang.Services.EncryptionService;
using PLang.Services.OutputStream;
using PLang.Utils;

namespace PLang.Container
{
	public static class DefaultRegistrations
	{
		private static void SetContext(ServiceContainer container, Type type, string serviceReservedKeyword, bool isDefault = false)
		{
			var context = container.GetInstance<PLangAppContext>();
			context.AddOrReplace(serviceReservedKeyword, type.FullName);
			if (isDefault)
			{
				AppContext.SetData(serviceReservedKeyword, type.FullName);
			}
		}

		public static void RegisterExceptionHandlerFactory(this ServiceContainer container, Type type, bool isDefault = false, IExceptionHandler? instance = null)
		{
			container.RegisterSingleton<IExceptionHandlerFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_ExceptionHandler, isDefault);
				return new ExceptionHandlerFactory(container);
			});

			if (instance != null)
			{
				container.RegisterSingleton(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}
		}
		public static void RegisterAskUserHandlerFactory(this ServiceContainer container, Type type, bool isDefault = false, IAskUserHandler? instance = null)
		{
			container.RegisterSingleton<IAskUserHandlerFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_AskUserHandler, isDefault);
				return new AskUserHandlerFactory(container);
			});

			if (instance != null)
			{
				container.RegisterSingleton(factor =>
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
			container.RegisterSingleton<IEncryptionFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_EncryptionService, isDefault);
				return new EncryptionFactory(container);
			});

			if (instance != null)
			{
				container.RegisterSingleton(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}
		}
		
		public static void RegisterLlmFactory(this ServiceContainer container, Type type, bool isDefault = false, ILlmService? instance = null)
		{
			container.RegisterSingleton<ILlmServiceFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_EncryptionService, isDefault);
				return new LlmServiceFactory(container);
			});

			if (instance != null)
			{
				container.RegisterSingleton(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}
		}
	}
}
