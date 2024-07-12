using LightInject;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NBitcoin.Secp256k1;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Accounts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nostr.Client.Client;
using PLang.Building;
using PLang.Building.Parsers;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Modules.MessageModule;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.AppsRepository;
using PLang.Services.ArchiveService;
using PLang.Services.CachingService;
using PLang.Services.EncryptionService;
using PLang.Services.EventSourceService;
using PLang.Services.IdentityService;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using PLang.Services.OutputStream;
using PLang.Services.SettingsService;
using PLang.Services.SigningService;
using PLang.Utils;
using RazorEngineCore;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Websocket.Client.Logging;
using static PLang.Modules.DbModule.ModuleSettings;


namespace PLang.Container
{
    public static class Instance
	{
		public record InjectedType(string InjectorName, Type ServiceType, Type ImplementationType);
		private static readonly Dictionary<Type, InjectedType> injectedTypes = [];


		public static void RegisterForPLang(this ServiceContainer container, string absoluteAppStartupPath, string relativeAppStartupPath,
			IAskUserHandlerFactory askUserHandlerFactory, IOutputStreamFactory outputStreamFactory, IOutputSystemStreamFactory outputSystemStreamFactory, IErrorHandlerFactory errorHandlerFactory)
		{
			container.RegisterBaseForPLang(absoluteAppStartupPath, relativeAppStartupPath);
			RegisterModules(container);

			container.RegisterSingleton<IOutputStreamFactory>(factory => { return outputStreamFactory; });
			container.RegisterSingleton<IOutputSystemStreamFactory>(factory => { return outputSystemStreamFactory; });
			container.RegisterSingleton<IErrorHandlerFactory>(factory => { return errorHandlerFactory; });
			container.RegisterSingleton<IAskUserHandlerFactory>(factory => { return askUserHandlerFactory; });

			container.RegisterForPLang(absoluteAppStartupPath, relativeAppStartupPath);
			RegisterEventRuntime(container);
		}

		public static void RegisterForPLangWebserver(this ServiceContainer container, string appStartupPath, string relativeAppPath, HttpListenerContext httpContext)
		{
			container.RegisterBaseForPLang(appStartupPath, relativeAppPath);
			RegisterModules(container);
			container.RegisterForPLang(appStartupPath, relativeAppPath);

			container.RegisterOutputStreamFactory(typeof(JsonOutputStream), true, new JsonOutputStream(httpContext));
			container.RegisterOutputSystemStreamFactory(typeof(ConsoleOutputStream), true, new ConsoleOutputStream());

			container.RegisterAskUserHandlerFactory(typeof(AskUserConsoleHandler), true, new AskUserConsoleHandler(container.GetInstance<IOutputSystemStreamFactory>()));
			container.RegisterErrorHandlerFactory(typeof(HttpErrorHandler), true, new HttpErrorHandler(httpContext, container.GetInstance<IAskUserHandlerFactory>(), container.GetInstance<ILogger>()));

			RegisterEventRuntime(container);
		}
		public static void RegisterForPLangWindowApp(this ServiceContainer container, string appStartupPath, string relativeAppPath,
			IAskUserDialog askUserDialog, IErrorDialog errorDialog, SynchronizationContext uiContext, Action<string> onFlush)
		{
			container.RegisterBaseForPLang(appStartupPath, relativeAppPath);
			RegisterModules(container);
			container.RegisterForPLang(appStartupPath, relativeAppPath);


			container.RegisterOutputStreamFactory(typeof(UIOutputStream), true, new UIOutputStream(container.GetInstance<IRazorEngine>(), container.GetInstance<IPLangFileSystem>(), uiContext, onFlush));
			container.RegisterOutputSystemStreamFactory(typeof(UIOutputStream), true, new UIOutputStream(container.GetInstance<IRazorEngine>(), container.GetInstance<IPLangFileSystem>(), uiContext, onFlush));
			
			container.RegisterAskUserHandlerFactory(typeof(AskUserWindowHandler), true, new AskUserWindowHandler(askUserDialog));
			container.RegisterErrorHandlerFactory(typeof(UiErrorHandler), true, new UiErrorHandler(errorDialog, container.GetInstance<IAskUserHandlerFactory>()));

			RegisterEventRuntime(container);
		}

		public static void RegisterForPLangConsole(this ServiceContainer container, string appStartupPath, string relativeAppPath)
		{
			container.RegisterBaseForPLang(appStartupPath, relativeAppPath);
			RegisterModules(container);
			container.RegisterForPLang(appStartupPath, relativeAppPath);

			container.RegisterOutputStreamFactory(typeof(ConsoleOutputStream), true, new ConsoleOutputStream());
			container.RegisterOutputSystemStreamFactory(typeof(ConsoleOutputStream), true, new ConsoleOutputStream());
			container.RegisterAskUserHandlerFactory(typeof(AskUserConsoleHandler), true, new AskUserConsoleHandler(container.GetInstance<IOutputSystemStreamFactory>()));
			container.RegisterErrorHandlerFactory(typeof(ConsoleErrorHandler), true, new ConsoleErrorHandler(container.GetInstance<IAskUserHandlerFactory>()));
			RegisterEventRuntime(container);
		}

		public static void RegisterForPLangBuilderConsole(this ServiceContainer container, string appStartupPath, string relativeAppPath)
		{
			container.RegisterBaseForPLang(appStartupPath, relativeAppPath);
			RegisterModules(container);
			container.RegisterForPLang(appStartupPath, relativeAppPath);

			container.RegisterOutputStreamFactory(typeof(ConsoleOutputStream), true, new ConsoleOutputStream());
			container.RegisterOutputSystemStreamFactory(typeof(ConsoleOutputStream), true, new ConsoleOutputStream());
			container.RegisterAskUserHandlerFactory(typeof(AskUserConsoleHandler), true, new AskUserConsoleHandler(container.GetInstance<IOutputSystemStreamFactory>()));
			container.RegisterErrorHandlerFactory(typeof(ConsoleErrorHandler), true, new ConsoleErrorHandler(container.GetInstance<IAskUserHandlerFactory>()));

			RegisterEventRuntime(container, true);
		}

		private static void RegisterBaseForPLang(this ServiceContainer container, string absoluteAppStartupPath, string relativeStartupAppPath)
		{

			container.Register<IServiceContainerFactory, ServiceContainerFactory>();
			container.RegisterSingleton<IPLangFileSystem>(factory =>
			{
				return new PLangFileSystem(absoluteAppStartupPath, relativeStartupAppPath);
			});
			container.RegisterSingleton<PLangAppContext>();

			container.RegisterSingleton<ILogger, Services.LoggerService.Logger<Executor>>(typeof(Logger).FullName);
			container.RegisterSingleton(factory =>
			{
				var context = factory.GetInstance<PLangAppContext>();
				var type = GetImplementation(context, ReservedKeywords.Inject_Logger, typeof(Logger));
				return factory.GetInstance<ILogger>(type);
			});


			container.RegisterSingleton<PrParser>();
		}

		private static void RegisterEventRuntime(this ServiceContainer container, bool isBuilder = false)
		{
			if (!isBuilder)
			{
				container.RegisterSingleton<IEventRuntime, EventRuntime>();
				return;
			}


			var runtimeContainer = new ServiceContainer();
			var fileSystem2 = container.GetInstance<IPLangFileSystem>();
			runtimeContainer.RegisterForPLang(fileSystem2.RootDirectory, fileSystem2.RelativeAppPath, container.GetInstance<IAskUserHandlerFactory>(),
				container.GetInstance<IOutputStreamFactory>(), container.GetInstance<IOutputSystemStreamFactory>(), 
				container.GetInstance<IErrorHandlerFactory>());
			runtimeContainer.RegisterSingleton<IEventRuntime, EventRuntime>();
			var eventRuntime = runtimeContainer.GetInstance<IEventRuntime>();

			container.RegisterSingleton<IEventRuntime>(factory => { return eventRuntime; });

			var engine = runtimeContainer.GetInstance<IEngine>();
			engine.Init(runtimeContainer);

		}

		private static void RegisterForPLang(this ServiceContainer container, string absoluteAppStartupPath, string relativeStartupAppPath)
		{


			container.RegisterSingleton<MemoryStack>();
			container.RegisterSingleton<FileAccessHandler>();

			container.RegisterSingleton<IRazorEngine, RazorEngine>();
			container.RegisterSingleton<IEngine, Engine>();
			container.RegisterSingleton<ISettings, Settings>();
			container.RegisterSingleton<IBuilder, Building.Builder>();
			container.RegisterSingleton<ITypeHelper, TypeHelper>();
			container.RegisterSingleton<IPseudoRuntime, PseudoRuntime>();
			container.RegisterSingleton<IBuilderFactory>(factory =>
			{
				return new BuilderFactory(container, container.GetInstance<ITypeHelper>());
			});


			container.RegisterOutputStreamFactory(typeof(MemoryOutputStream), false, new MemoryOutputStream());
			container.RegisterSingleton<IEventBuilder, EventBuilder>();
			if (AppContext.TryGetSwitch("Builder", out bool isBuilder) && !isBuilder)
			{

			}
			container.RegisterSingleton<IPLangIdentityService, PLangIdentityService>();
			container.RegisterSingleton<IPLangSigningService, PLangSigningService>();
			container.Register<IPublicPrivateKeyCreator, PublicPrivateKeyCreator>();



			container.Register<IGoalParser>(factory =>
			{
				return new GoalParser(container, container.GetInstance<IPLangFileSystem>(), container.GetInstance<ISettings>());
			});
			container.Register<IGoalBuilder, GoalBuilder>();
			container.Register<IStepBuilder, StepBuilder>();
			container.Register<IInstructionBuilder, InstructionBuilder>();

			container.Register<LlmCaching, LlmCaching>();
			container.Register<VariableHelper, VariableHelper>();

			container.Register<IPLangAppsRepository, PLangAppsRepository>();
			container.Register<IHttpClientFactory, SimpleHttpClientFactory>();

			container.RegisterSingleton<INostrClient>(factory =>
			{
				var moduleSettings = new ModuleSettings(container.GetInstance<ISettings>(), container.GetInstance<ILlmServiceFactory>());
				var nostrClientManager = new NostrClientManager();

				var multi = nostrClientManager.GetClient(moduleSettings.GetRelays());
				return multi;
			});

			container.RegisterSingleton<IWeb3>(factory =>
			{
				var moduleSettings = new Modules.BlockchainModule.ModuleSettings(container.GetInstance<ISettings>(), container.GetInstance<ILlmServiceFactory>());

				var rpcServer = moduleSettings.GetRpcServers().FirstOrDefault(p => p.IsDefault);
				var wallet = moduleSettings.GetWallets().FirstOrDefault(p => p.IsDefault);

				IAccount account;
				if (!string.IsNullOrEmpty(wallet.PrivateKey))
				{
					account = new Account(wallet.PrivateKey);
				}
				else
				{
					var seed = Encoding.UTF8.GetBytes(wallet.Seed);
					var hdWallet = new Nethereum.HdWallet.Wallet(seed);
					account = new Account(hdWallet.GetPrivateKey(wallet.Addresses[0]));
				}

				var web3 = new Web3(account, new WebSocketClient(rpcServer.Url));
				return web3;
			}, absoluteAppStartupPath);


			// These are injectable by user
			var context = container.GetInstance<PLangAppContext>();
			string llmService = AppContext.GetData("llmservice") as string ?? "plang";
			var defaultLlmService = (llmService == "openai") ? typeof(OpenAiService) : typeof(PLangLlmService);

			container.RegisterSingleton<ILlmService, PLangLlmService>(typeof(PLangLlmService).FullName);
			container.RegisterSingleton<ILlmService, OpenAiService>(typeof(OpenAiService).FullName);
			container.RegisterSingleton(factory =>
			{
				var type = GetImplementation(context, ReservedKeywords.Inject_LLMService, defaultLlmService);
				return factory.GetInstance<ILlmService>(type);
			});
			container.RegisterLlmFactory(defaultLlmService, true);





			container.RegisterSingleton<IAppCache, InMemoryCaching>(typeof(InMemoryCaching).FullName);
			container.RegisterSingleton(factory =>
			{
				string type = GetImplementation(context, ReservedKeywords.Inject_Caching, typeof(InMemoryCaching));
				return factory.GetInstance<IAppCache>(type);
			});

			container.RegisterSingleton<IArchiver, Zip>(typeof(Zip).FullName);
			container.RegisterSingleton(factory =>
			{
				string type = GetImplementation(context, ReservedKeywords.Inject_Archiving, typeof(Zip));
				return factory.GetInstance<IArchiver>(type);
			});


			container.RegisterSettingsRepositoryFactory(typeof(SqliteSettingsRepository), true,
				new SqliteSettingsRepository(container.GetInstance<IPLangFileSystem>(), context, container.GetInstance<ILogger>()));
			container.RegisterSingleton(factory =>
			{
				string type = GetImplementation(context, ReservedKeywords.Inject_SettingsRepository, typeof(SqliteSettingsRepository));
				return factory.GetInstance<ISettingsRepository>(type);
			});

			container.Register<IDbConnection, SqliteConnection>(typeof(SqliteConnection).FullName);
			container.Register<IDbConnection, DbConnectionUndefined>(typeof(DbConnectionUndefined).FullName);
			container.Register(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				var fileSystem = container.GetInstance<IPLangFileSystem>();
				var settings = container.GetInstance<ISettings>();
				var llmServiceFactory = container.GetInstance<ILlmServiceFactory>();
				var logger = container.GetInstance<ILogger>();


				IDbConnection? dbConnection = GetDbConnection(factory, context);
				if (dbConnection != null) return dbConnection;

				dbConnection = factory.GetInstance<IDbConnection>(typeof(DbConnectionUndefined).FullName);
				var moduleSettings = new Modules.DbModule.ModuleSettings(fileSystem, settings, context, llmServiceFactory, logger);

				dbConnection = moduleSettings.GetDefaultDbConnection(factory).Result;
				if (dbConnection != null) return dbConnection;

				var supportedTypes = moduleSettings.GetSupportedDbTypes();
				if (supportedTypes.Count == 1)
				{
					moduleSettings.CreateDataSource("data", "./db/data.sqlite", "sqlite", true, true).Wait();
					dbConnection = GetDbConnection(factory, context);
					if (dbConnection != null) return dbConnection;
				}

				if (AppContext.TryGetSwitch("Builder", out bool isBuilder) && isBuilder)
				{
					var dataSource = moduleSettings.GetCurrentDataSource().Result;
					dbConnection = factory.GetInstance<IDbConnection>(dataSource.TypeFullName);
					dbConnection.ConnectionString = dataSource.ConnectionString;
				}

				return dbConnection;
			});

			container.Register<IEventSourceRepository, SqliteEventSourceRepository>(typeof(SqliteEventSourceRepository).FullName);
			container.Register<IEventSourceRepository, DisableEventSourceRepository>(typeof(DisableEventSourceRepository).FullName);
			container.Register(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				var fileSystem = container.GetInstance<IPLangFileSystem>();
				var settings = container.GetInstance<ISettings>();
				string dbType = GetImplementation(context, ReservedKeywords.Inject_IDbConnection);
				var dbConnection = factory.GetInstance<IDbConnection>(dbType);
				var logger = container.GetInstance<ILogger>();
				var llmServiceFactory = container.GetInstance<ILlmServiceFactory>();

				var moduleSettings = new Modules.DbModule.ModuleSettings(fileSystem, settings, context, llmServiceFactory, logger);
				var dataSources = moduleSettings.GetAllDataSources().Result;
				if (dataSources.Count == 0)
				{
					return factory.GetInstance<IEventSourceRepository>(typeof(DisableEventSourceRepository).FullName);
				}

				DataSource dataSource = moduleSettings.GetCurrentDataSource().Result;
				if (!dataSource.KeepHistory)
				{
					context.AddOrReplace(ReservedKeywords.Inject_IEventSourceRepository, typeof(DisableEventSourceRepository).FullName);
					return factory.GetInstance<IEventSourceRepository>(typeof(DisableEventSourceRepository).FullName);
				} else if (dataSource.KeepHistory && dataSource.TypeFullName == typeof(SqliteConnection).FullName)
				{
					context.AddOrReplace(ReservedKeywords.Inject_IEventSourceRepository, typeof(SqliteEventSourceRepository).FullName);
				}

				string type = GetImplementation(context, ReservedKeywords.Inject_IEventSourceRepository, typeof(SqliteEventSourceRepository));
				var eventSourceRepo = factory.GetInstance<IEventSourceRepository>(type);
				eventSourceRepo.DataSource = dataSource;

				return eventSourceRepo;
			});

			var fileSystem = container.GetInstance<IPLangFileSystem>();
			var settings = container.GetInstance<ISettings>();

			container.RegisterEncryptionFactory(typeof(Encryption), true, new Encryption(settings));
			fileSystem.SetFileAccess(settings.GetValues<FileAccessControl>(typeof(PLangFileSystem)));
		}

		private static string GetImplementation(PLangAppContext context, string reservedKeyword, Type? defaultType = null)
		{
			if (context.TryGetValue(reservedKeyword, out object? value) && value != null)
			{
				return value.ToString()!;
			}

			object? obj = AppContext.GetData(reservedKeyword);
			if (obj != null)
			{
				return obj.ToString()!;
			}
			if (defaultType != null)
			{
				AppContext.SetData(reservedKeyword, defaultType.FullName);
				return defaultType.FullName!;
			}

			throw new RuntimeException($"Could not get implementaion name for {reservedKeyword}");
		}

		private static void RegisterModules(ServiceContainer container)
		{

			var currentAssembly = Assembly.GetExecutingAssembly();

			// Scan the current assembly for types that inherit from BaseBuilder
			var modulesFromCurrentAssembly = currentAssembly.GetTypes()
																.Where(t => !t.IsAbstract && !t.IsInterface &&
																(typeof(BaseBuilder).IsAssignableFrom(t) || typeof(BaseProgram).IsAssignableFrom(t)))
																.ToList();

			// Register these types with the DI container
			foreach (var type in modulesFromCurrentAssembly)
			{
				container.Register(type);  // or register with a specific interface if needed
			}
			container.Register<BaseBuilder, BaseBuilder>();

			var fileSystem = container.GetInstance<IPLangFileSystem>();
			if (fileSystem.Directory.Exists(".modules"))
			{

				AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
				{
					string assemblyPath = Path.Combine(fileSystem.RootDirectory, ".modules", new AssemblyName(resolveArgs.Name).Name + ".dll");
					if (fileSystem.File.Exists(assemblyPath))
					{
						return Assembly.LoadFile(assemblyPath);
					}
					return null;
				};


				var assemblyFiles = fileSystem.Directory.GetFiles(".modules", "*.dll");
				foreach (var file in assemblyFiles)
				{

					var assembly = Assembly.LoadFile(file);
					 var builderTypes = assembly.GetTypes()
											   .Where(t => !t.IsAbstract && !t.IsInterface &&
											   (typeof(BaseBuilder).IsAssignableFrom(t) || typeof(BaseProgram).IsAssignableFrom(t)))
											   .ToList();

					foreach (var type in builderTypes)
					{
						container.Register(type);  // or register with a specific interface if needed
					}
				}
			}

			RegisterUserGlobalInjections(container);

		}
		private static void RegisterUserGlobalInjections(ServiceContainer container)
		{
			var prParser = container.GetInstance<PrParser>();
			var goals = prParser.GetAllGoals();
			var goalsWithInjections = goals.Where(p => p.Injections.FirstOrDefault(x => x.IsGlobal) != null);

			foreach (var goal in goalsWithInjections)
			{
				foreach (var injection in goal.Injections)
				{
					container.RegisterForPLangUserInjections(injection.Type, injection.Path, true, injection.EnvironmentVariable, injection.EnvironmentVariableValue);
				}
			}
		}
		private static Dictionary<string, bool> NotifiedAboutNotRegister = new();
		public static void RegisterForPLangUserInjections(this IServiceContainer container, string injectorType, string pathToModule, bool isGlobalForApp, string? environmentVariable = null, string? environmentVariableValue = null, Type? injectionType = null)
		{
			var logger = container.GetInstance<ILogger>();
			if (!string.IsNullOrEmpty(environmentVariable) && !string.IsNullOrEmpty(environmentVariableValue))
			{
				if (Environment.GetEnvironmentVariable(environmentVariable) != environmentVariableValue)
				{
					if (!NotifiedAboutNotRegister.ContainsKey(injectorType + "_" + pathToModule))
					{
						logger.LogWarning($"Will not register {injectorType} - {pathToModule}. Environment variable {environmentVariable} does not match {environmentVariableValue}");
						NotifiedAboutNotRegister.AddOrReplace(injectorType + "_" + pathToModule, true);
					}
					return;
				}
			}

			if (injectorType.ToLower() == "db")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(IDbConnection), pathToModule);
				RegisterType(container, "db", typeof(IDbConnection), type, ReservedKeywords.Inject_IDbConnection, isGlobalForApp, pathToModule);

			}

			if (injectorType.ToLower() == "settings")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(ISettingsRepository), pathToModule);
				RegisterType(container, "settings", typeof(ISettingsRepository), type, ReservedKeywords.Inject_SettingsRepository, isGlobalForApp, pathToModule);

			}

			if (injectorType.ToLower() == "caching")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(IAppCache), pathToModule);
				RegisterType(container, "caching", typeof(IAppCache), type, ReservedKeywords.Inject_Caching, isGlobalForApp, pathToModule);

			}

			if (injectorType.ToLower() == "logger")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(ILogger), pathToModule);
				RegisterType(container, "logger", typeof(ILogger), type, ReservedKeywords.Inject_Logger, isGlobalForApp, pathToModule);

			}

			if (injectorType.ToLower() == "llm")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(ILlmService), pathToModule);
				RegisterType(container, "llm", typeof(ILlmService), type, ReservedKeywords.Inject_LLMService, isGlobalForApp, pathToModule);

			}

			if (injectorType.ToLower() == "askuser")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(IAskUserHandler), pathToModule);
				RegisterType(container, "askuser", typeof(IAskUserHandler), type, ReservedKeywords.Inject_AskUserHandler, isGlobalForApp, pathToModule);

			}

			if (injectorType.ToLower() == "encryption")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(IEncryption), pathToModule);
				RegisterType(container, "encryption", typeof(IEncryption), type, ReservedKeywords.Inject_EncryptionService, isGlobalForApp, pathToModule);

			}

			if (injectorType.ToLower() == "archiver")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(IArchiver), pathToModule);
				RegisterType(container, "archiver", typeof(IArchiver), type, ReservedKeywords.Inject_Archiving, isGlobalForApp, pathToModule);

			}

		}
		public static Type? GetInjectionType(IServiceContainer container, Type typeToFind, string injectorType)
		{
			var fileSystem = container.GetInstance<IPLangFileSystem>();

			if (!fileSystem.Directory.Exists(".services"))
			{
				var logger = container.GetInstance<ILogger>();
				logger.LogWarning($".services folder not found in {fileSystem.RootDirectory}. If you have modified injection you need to Delete the file Start/00. Goal.pr or events/00. Goal.pr. Will be fixed in future release.");
				return null;
			}

			AppDomain.CurrentDomain.AssemblyResolve += (sender, resolveArgs) =>
			{
				string assemblyPath = Path.Combine(fileSystem.RootDirectory, ".services", new AssemblyName(resolveArgs.Name).Name + ".dll");
				if (fileSystem.File.Exists(assemblyPath))
				{
					return Assembly.LoadFile(assemblyPath);
				}
				return null;
			};

			string dllFilePath = Path.GetDirectoryName(Path.Combine(fileSystem.GoalsPath, ".services", injectorType));
			string[] dllFiles = [ dllFilePath ];
			if (!fileSystem.File.Exists(dllFilePath))
			{
				//var dirName = Path.GetDirectoryName(injectorType);
				var moduleFolderPath = Path.Combine(fileSystem.GoalsPath, ".services", dllFilePath);
				if (!fileSystem.Directory.Exists(moduleFolderPath))
				{
					var logger = container.GetInstance<ILogger>();
					logger.LogWarning($"{injectorType} injection folder could not be found. Path {moduleFolderPath}");
					return null;
				}


				dllFiles = fileSystem.Directory.GetFiles(moduleFolderPath, "*.dll", SearchOption.AllDirectories);
			}


			Type? type = null;
			foreach (var dllFile in dllFiles)
			{
				Assembly assembly = Assembly.LoadFile(dllFile);
				if (type == null)
				{
					type = assembly.GetTypes().FirstOrDefault(p => p.GetInterfaces().Contains(typeToFind));
				}

			}

			if (type != null)
			{
				return type;
			}

			throw new RuntimeException($"Cannot find {injectorType} in {dllFilePath}. Make sure that the class inherits from {typeToFind.Name} and the name of the dll is {injectorType}.dll");
		}

		private static void RegisterType(IServiceContainer container, string injectorType, Type interfaceType, Type? implementationType, string reservedKeyword, bool isGlobalForApp, string pathToModule)
		{
			var logger = container.GetInstance<ILogger>();
			if (implementationType == null)
			{
				logger.LogError($"ERROR: implementationType is null for {injectorType} - pathToModule:{pathToModule}");
				return;
				//throw new RuntimeException($"Loading '{injectorType}': interface:{injectorType} | type is:{implementationType} | reservedKeyword:{reservedKeyword} | isGlobalForApp:{isGlobalForApp} | pathToModule:{pathToModule}");
			}
			if (container.CanGetInstance(interfaceType, implementationType.FullName)) return;

			logger.LogDebug($"Loading '{injectorType}' in type of {implementationType}");

			container.Register(interfaceType, implementationType, implementationType.FullName);

			var context = container.GetInstance<PLangAppContext>();
			if (!context.ContainsKey(reservedKeyword))
			{
				context.AddOrReplace(reservedKeyword, implementationType.FullName);
			}

			if (isGlobalForApp)
			{
				context.AddOrReplace(reservedKeyword, implementationType.FullName);
				injectedTypes.AddOrReplace(interfaceType, new InjectedType(injectorType, interfaceType, implementationType));
				AppContext.SetData(reservedKeyword, implementationType.FullName);
			}
		}


		private static IDbConnection? GetDbConnection(IServiceFactory factory, PLangAppContext context)
		{
			DataSource? dataSource = null;
			if (context.TryGetValue(ReservedKeywords.CurrentDataSource, out object? obj) && obj != null)
			{
				dataSource = (DataSource)obj;
			}
			else if ((obj = AppContext.GetData(ReservedKeywords.CurrentDataSource)) != null)
			{
				dataSource = (DataSource)obj;
			}

			if (dataSource == null) return null;

			var dbConnection = factory.GetInstance<IDbConnection>(dataSource.TypeFullName);
			dbConnection.ConnectionString = dataSource.ConnectionString;
			return dbConnection;

		}

	}
}
