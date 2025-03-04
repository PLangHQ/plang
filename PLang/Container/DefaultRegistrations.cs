using LightInject;
using NBitcoin.Secp256k1;
using PLang.Errors.Handlers;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Modules.DbModule;
using PLang.Services.LlmService;
using PLang.Services.OutputStream;
using PLang.Services.SettingsService;
using PLang.Utils;
using System.Data;

namespace PLang.Container
{
	public static class DefaultRegistrations
	{


		public static void RegisterErrorHandlerFactory(this ServiceContainer container, Type type, bool isDefault = false, IErrorHandler? instance = null)
		{
			container.Register<IErrorHandlerFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_ErrorHandler, isDefault);
				return new ErrorHandlerFactory(container);
			});

			if (instance != null)
			{
				container.Register(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}
		}
		public static void RegisterErrorSystemHandlerFactory(this ServiceContainer container, Type type, bool isDefault = false, IErrorHandler? instance = null)
		{
			container.Register<IErrorSystemHandlerFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_ErrorSystemHandler, isDefault);
				return new ErrorSystemHandlerFactory(container);
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

		public static void RegisterOutputStreamFactory(this ServiceContainer container, Type type, bool isDefault = false, IOutputStream? instance = null, bool setToContext = false)
		{
			SetContext(container, type, ReservedKeywords.Inject_OutputStream, isDefault, setToContext);
			container.RegisterSingleton<IOutputStreamFactory>(factory =>
			{
				var defaultType = GetDefault(container, ReservedKeywords.Inject_OutputStream);
				return new OutputStreamFactory(container, defaultType);
			});

			if (instance != null)
			{
				container.RegisterSingleton(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
			}
		}
		public static void RegisterOutputSystemStreamFactory(this ServiceContainer container, Type type, bool isDefault = false, IOutputStream? instance = null, bool setToContext = false)
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


		public static void RegisterDbRepositoryFactory(this ServiceContainer container, Type type, bool isDefault = false, IDbConnection? instance = null)
		{
			container.Register<IDbFactory>(factory =>
			{
				SetContext(container, type, ReservedKeywords.Inject_IDbConnection, isDefault);
				return new DbFactory(container);
			});

			if (instance != null)
			{
				container.RegisterSingleton(factor =>
				{
					return instance;
				}, instance.GetType().FullName);
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
