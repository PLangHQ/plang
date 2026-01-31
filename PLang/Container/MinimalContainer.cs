using LightInject;
using PLang.Building.Parsers;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Interfaces;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.CachingService;
using PLang.Services.EncryptionService;
using PLang.Services.LlmService;
using PLang.Services.SettingsService;
using PLang.Utils;
using Microsoft.Data.Sqlite;
using System.Data;
using PLang.Services.ArchiveService;
using PLang.Services.IdentityService;
using PLang.Services.SigningService;
using PLang.Services.OutputStream.Sinks;
using PLang.Building;
using PLang.Modules;
using PLang.Modules.WebserverModule;
using System.Reflection;

namespace PLang.Container
{
	/// <summary>
	/// Minimal container registration for bootstrap.
	/// Only registers what's needed to run system/Run.goal which then registers other services via plang.
	/// </summary>
	public static class MinimalContainer
	{
		/// <summary>
		/// Register minimal dependencies needed to bootstrap plang.
		/// After this, system/Run.goal will register additional services.
		/// </summary>
		public static void RegisterBootstrap(this ServiceContainer container, string absoluteAppStartupPath, string relativeStartupAppPath)
		{
			// Layer 1: File System
			RegisterFileSystem(container, absoluteAppStartupPath, relativeStartupAppPath);

			// Layer 2: Logging
			RegisterLogging(container);

			// Layer 3: Context & Memory
			RegisterContextAndMemory(container);

			// Layer 4: Parsing & Assembly Loading
			RegisterParsing(container);

			// Layer 5: Type Utilities
			RegisterTypeUtilities(container);

			// Layer 6: Security
			RegisterSecurity(container);

			// Layer 7: Settings (Option A - keep in bootstrap for now)
			RegisterSettings(container);

			// Layer 8: Engine
			RegisterEngine(container);

			// Layer 9: Modules (reflection scan for now)
			RegisterModules(container);

			// Layer 10: Default services that can be overridden via plang
			RegisterDefaultServices(container);

			// Setup assembly resolve
			SetupAssemblyResolve(container);

			// Set output sinks
			var engine = container.GetInstance<IEngine>();
			engine.SystemSink = new ConsoleSink();
			engine.UserSink = new ConsoleSink();

			// Register error handlers
			container.RegisterErrorHandlerFactory(typeof(ConsoleErrorHandler), true, new ConsoleErrorHandler());
			container.RegisterErrorSystemHandlerFactory(typeof(ConsoleErrorHandler), true, new ConsoleErrorHandler());

			// Register event runtime
			container.RegisterSingleton<IEventRuntime, EventRuntime>();

			// Set base variables
			RegisterBaseVariables(container);
		}

		private static void RegisterFileSystem(ServiceContainer container, string absoluteAppStartupPath, string relativeStartupAppPath)
		{
			container.RegisterSingleton<IPLangFileSystem>(factory =>
			{
				return new PLangFileSystem(absoluteAppStartupPath, relativeStartupAppPath);
			});
			container.RegisterSingleton<IPLangFileSystemFactory>(factory =>
			{
				return new PlangFileSystemFactory(container);
			});
		}

		private static void RegisterLogging(ServiceContainer container)
		{
			container.RegisterSingleton<ILogger, Services.LoggerService.Logger<Executor>>(typeof(Services.LoggerService.Logger<Executor>).FullName);
			container.RegisterSingleton(factory =>
			{
				return factory.GetInstance<ILogger>(typeof(Services.LoggerService.Logger<Executor>).FullName);
			});
		}

		private static void RegisterContextAndMemory(ServiceContainer container)
		{
			container.RegisterSingleton<PLangAppContext>();
			container.Register<IMemoryStackAccessor, MemoryStackAccessor>();
			container.RegisterSingleton<IPLangContextAccessor, ContextAccessor>();
		}

		private static void RegisterParsing(ServiceContainer container)
		{
			container.RegisterSingleton<DependancyHelper>();
			container.RegisterSingleton<PrParser>();
		}

		private static void RegisterTypeUtilities(ServiceContainer container)
		{
			container.RegisterSingleton<ITypeHelper, TypeHelper>();
			container.RegisterSingleton<MethodHelper>();
			container.Register<VariableHelper, VariableHelper>();
		}

		private static void RegisterSecurity(ServiceContainer container)
		{
			container.RegisterSingleton<IFileAccessHandler, FileAccessHandler>();
		}

		private static void RegisterSettings(ServiceContainer container)
		{
			var context = container.GetInstance<PLangAppContext>();

			// Settings repository factory and default implementation
			container.RegisterSettingsRepositoryFactory(typeof(SqliteSettingsRepository), true,
				new SqliteSettingsRepository(container.GetInstance<IPLangFileSystemFactory>(), context, container.GetInstance<ILogger>()));

			container.RegisterSingleton(factory =>
			{
				string type = Instance.GetImplementation(context, ReservedKeywords.Inject_SettingsRepository, typeof(SqliteSettingsRepository));
				return factory.GetInstance<ISettingsRepository>(type);
			});

			// Settings service
			container.RegisterSingleton<ISettings, Settings>();
		}

		private static void RegisterEngine(ServiceContainer container)
		{
			container.RegisterSingleton<IEngine, Engine>();
			container.RegisterSingleton<IPseudoRuntime, PseudoRuntime>();
		}

		private static void RegisterModules(ServiceContainer container)
		{
			var currentAssembly = Assembly.GetExecutingAssembly();

			// Register module settings
			var moduleSettingsTypes = currentAssembly.GetTypes()
				.Where(t => !t.IsAbstract && !t.IsInterface && typeof(IModuleSettings).IsAssignableFrom(t))
				.ToList();

			foreach (var type in moduleSettingsTypes)
			{
				container.Register(type);
				container.Register(type, type, serviceName: type.FullName);
			}

			// Register factories
			var factoryTypes = currentAssembly.GetTypes()
				.Where(t => !t.IsAbstract && !t.IsInterface && typeof(BaseFactory).IsAssignableFrom(t))
				.ToList();

			foreach (var type in factoryTypes)
			{
				container.Register(type, factory =>
				{
					var instance = Activator.CreateInstance(type, [container]) as BaseFactory;
					return instance;
				});
			}

			// Register module builders and programs
			var moduleTypes = currentAssembly.GetTypes()
				.Where(t => !t.IsAbstract && !t.IsInterface &&
					(typeof(BaseBuilder).IsAssignableFrom(t) || typeof(BaseProgram).IsAssignableFrom(t)))
				.ToList();

			foreach (var type in moduleTypes)
			{
				container.Register(type);
			}
			container.Register<BaseBuilder, BaseBuilder>();

			// Load custom modules from .modules folder
			var fileSystem = container.GetInstance<IPLangFileSystem>();
			if (fileSystem.Directory.Exists(".modules"))
			{
				var dependancyHelper = container.GetInstance<DependancyHelper>();
				var modules = dependancyHelper.LoadModules(typeof(BaseProgram), fileSystem.GoalsPath);
				foreach (var module in modules)
				{
					container.Register(module, factory =>
					{
						var instance = Activator.CreateInstance(module, container);
						return instance;
					}, serviceName: module.FullName);
				}
			}
		}

		private static void RegisterDefaultServices(ServiceContainer container)
		{
			var context = container.GetInstance<PLangAppContext>();
			var settings = container.GetInstance<ISettings>();

			// Register minimal defaults for services
			// These can be overridden via system/Run.goal using inject command
			RegisterMinimalDefaults(container, context, settings);

			// Infrastructure that's always needed
			RegisterInfrastructure(container, settings);
		}

		private static void RegisterMinimalDefaults(ServiceContainer container, PLangAppContext context, ISettings settings)
		{
			// LLM - register built-in types and factory
			// Default: plang service (can be overridden to openai via inject or --llmservice flag)
			string llmService = AppContext.GetData("llmservice") as string ?? "plang";
			var defaultLlmService = (llmService == "openai") ? typeof(Services.OpenAi.OpenAiService) : typeof(PLangLlmService);

			container.RegisterSingleton<ILlmService, PLangLlmService>(typeof(PLangLlmService).FullName);
			container.RegisterSingleton<ILlmService, Services.OpenAi.OpenAiService>(typeof(Services.OpenAi.OpenAiService).FullName);
			container.RegisterSingleton(factory =>
			{
				var type = Instance.GetImplementation(context, ReservedKeywords.Inject_LLMService, defaultLlmService);
				return factory.GetInstance<ILlmService>(type);
			});
			container.RegisterLlmFactory(defaultLlmService, true);

			// Caching - default: in-memory
			container.RegisterSingleton<IAppCache, InMemoryCaching>(typeof(InMemoryCaching).FullName);
			container.RegisterSingleton(factory =>
			{
				string type = Instance.GetImplementation(context, ReservedKeywords.Inject_Caching, typeof(InMemoryCaching));
				return factory.GetInstance<IAppCache>(type);
			});

			// Database - default: sqlite
			container.Register<IDbConnection, SqliteConnection>(typeof(SqliteConnection).FullName);
			container.Register<IDbConnection, DbConnectionUndefined>(typeof(DbConnectionUndefined).FullName);
			container.Register(factory =>
			{
				var type = Instance.GetImplementation(context, ReservedKeywords.Inject_IDbConnection, typeof(SqliteConnection));
				return factory.GetInstance<IDbConnection>(type);
			});
			container.RegisterDbFactory(typeof(SqliteConnection), true);

			// Archiver - default: zip
			container.RegisterSingleton<IArchiver, Zip>(typeof(Zip).FullName);
			container.RegisterSingleton(factory =>
			{
				string type = Instance.GetImplementation(context, ReservedKeywords.Inject_Archiving, typeof(Zip));
				return factory.GetInstance<IArchiver>(type);
			});

			// Encryption - default: AES
			container.RegisterEncryptionFactory(typeof(Encryption), true, new Encryption(settings));
		}

		private static void RegisterInfrastructure(ServiceContainer container, ISettings settings)
		{
			// Identity & Signing - always needed
			container.RegisterSingleton<IPLangIdentityService, PLangIdentityService>();
			container.RegisterSingleton<IPLangSigningService, PLangSigningService>();
			container.Register<IPublicPrivateKeyCreator, PublicPrivateKeyCreator>();

			// Builder infrastructure (needed even for runtime to support dynamic building)
			container.RegisterSingleton<IBuilder, Building.Builder>();
			container.RegisterSingleton<IBuilderFactory>(factory =>
			{
				return new BuilderFactory(container, container.GetInstance<ITypeHelper>(), container.GetInstance<ILogger>());
			});
			container.RegisterSingleton<IGoalParser>(factory =>
			{
				return new GoalParser(container, container.GetInstance<IPLangFileSystem>(), container.GetInstance<ISettings>(), container.GetInstance<ILogger>());
			});
			container.Register<IGoalBuilder, GoalBuilder>();
			container.Register<IStepBuilder, StepBuilder>();
			container.Register<IInstructionBuilder, InstructionBuilder>();
			container.RegisterSingleton<IEventBuilder, EventBuilder>();
			container.Register<LlmCaching, LlmCaching>();

			// HTTP
			container.Register<IHttpClientFactory, SimpleHttpClientFactory>();
			container.Register<Services.AppsRepository.IPLangAppsRepository, Services.AppsRepository.PLangAppsRepository>();

			// Misc
			container.RegisterSingleton<ICertHelper, CertHelper>();
			container.RegisterSingleton<ProgramFactory>(factory => new ProgramFactory(container));

			// File access from settings
			var fileSystem = container.GetInstance<IPLangFileSystem>();
			fileSystem.SetFileAccess(settings.GetValues<FileAccessControl>(typeof(PLangFileSystem)));
		}

		private static void SetupAssemblyResolve(ServiceContainer container)
		{
			var fileSystem = container.GetInstance<IPLangFileSystem>();
			var logger = container.GetInstance<ILogger>();
			var dependancyHelper = container.GetInstance<DependancyHelper>();
			var engine = container.GetInstance<IEngine>();

			Instance.SetupAssemblyResolve(fileSystem, logger, dependancyHelper, engine);
		}

		private static void RegisterBaseVariables(ServiceContainer container)
		{
			var context = container.GetInstance<PLangAppContext>();
			var fileSystem = container.GetInstance<IPLangFileSystem>();
			var fileAccessHandler = container.GetInstance<IFileAccessHandler>();

			context.AddOrReplace("!plang.osPath", fileSystem.SystemDirectory);
			context.AddOrReplace("!plang.rootPath", fileSystem.RootDirectory);

			fileAccessHandler.GiveAccess(fileSystem.RootDirectory, fileSystem.SystemDirectory);
		}
	}
}
