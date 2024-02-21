using LightInject;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Accounts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nostr.Client.Client;
using PLang.Building;
using PLang.Building.Events;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Exceptions.Handlers;
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
using PLang.Services.OutputStream;
using PLang.Services.SettingsService;
using PLang.Services.SigningService;
using PLang.Utils;
using RazorEngineCore;
using System.Data;
using System.Net;
using System.Reflection;
using System.Text;
using Websocket.Client.Logging;
using static PLang.Modules.DbModule.ModuleSettings;


namespace PLang.Container
{
	public static class Instance
	{
		private static readonly string PrefixForTempInjection = "__Temp__";
		public record InjectedType(string InjectorName, Type ServiceType, Type ImplementationType);
		private static readonly Dictionary<Type, InjectedType> injectedTypes = [];


		public static void RegisterForPLang(this ServiceContainer container, string absoluteAppStartupPath, string relativeAppStartupPath,
			IAskUserHandlerFactory askUserHandlerFactory, IOutputStreamFactory outputStreamFactory, IExceptionHandlerFactory exceptionHandlerFactory)
		{
			container.RegisterBaseForPLang(absoluteAppStartupPath, relativeAppStartupPath);
			RegisterModules(container);
			container.RegisterForPLang(absoluteAppStartupPath, relativeAppStartupPath);

			container.RegisterInstance<IOutputStreamFactory>(outputStreamFactory);
			container.RegisterInstance<IExceptionHandlerFactory>(exceptionHandlerFactory);
			container.RegisterInstance<IAskUserHandlerFactory>(askUserHandlerFactory);


		}
		public static void RegisterForPLangWebserver(this ServiceContainer container, string appStartupPath, string relativeAppPath, HttpListenerContext httpContext)
		{
			container.RegisterBaseForPLang(appStartupPath, relativeAppPath);
			RegisterModules(container);
			container.RegisterForPLang(appStartupPath, relativeAppPath);

			container.RegisterOutputStreamFactory(typeof(JsonOutputStream), true, new JsonOutputStream(httpContext));
			container.RegisterAskUserHandlerFactory(typeof(AskUserConsoleHandler), true, new AskUserConsoleHandler());
			container.RegisterExceptionHandlerFactory(typeof(HttpExceptionHandler), true, new HttpExceptionHandler(httpContext, container.GetInstance<IAskUserHandlerFactory>()));

		}
		public static void RegisterForPLangWindowApp(this ServiceContainer container, string appStartupPath, string relativeAppPath, IAskUserDialog askUserDialog, IErrorDialog errorDialog, Action<string> onFlush)
		{
			container.RegisterBaseForPLang(appStartupPath, relativeAppPath);
			RegisterModules(container);
			container.RegisterForPLang(appStartupPath, relativeAppPath);

			container.RegisterOutputStreamFactory(typeof(UIOutputStream), true, new UIOutputStream(container.GetInstance<IRazorEngine>(), container.GetInstance<IPLangFileSystem>(), onFlush));
			container.RegisterAskUserHandlerFactory(typeof(AskUserWindowHandler), true, new AskUserWindowHandler(askUserDialog));
			container.RegisterExceptionHandlerFactory(typeof(UiExceptionHandler), true, new UiExceptionHandler(errorDialog, container.GetInstance<IAskUserHandlerFactory>()));

			container.RegisterSingleton<IRazorEngine, RazorEngine>();

		}

		public static void RegisterForPLangConsole(this ServiceContainer container, string appStartupPath, string relativeAppPath)
		{
			container.RegisterBaseForPLang(appStartupPath, relativeAppPath);
			RegisterModules(container);
			container.RegisterForPLang(appStartupPath, relativeAppPath);

			container.RegisterOutputStreamFactory(typeof(ConsoleOutputStream), true, new ConsoleOutputStream());
			container.RegisterAskUserHandlerFactory(typeof(AskUserConsoleHandler), true, new AskUserConsoleHandler());
			container.RegisterExceptionHandlerFactory(typeof(ConsoleExceptionHandler), true, new ConsoleExceptionHandler(container.GetInstance<IAskUserHandlerFactory>()));


		}

		private static void RegisterBaseForPLang(this ServiceContainer container, string absoluteAppStartupPath, string relativeStartupAppPath)
		{

			container.Register<IServiceContainerFactory, ServiceContainerFactory>();
			container.RegisterSingleton<IPLangFileSystem>(factory =>
			{
				return new PLangFileSystem(absoluteAppStartupPath, relativeStartupAppPath);
			});
			container.RegisterSingleton<PLangAppContext>();
			container.RegisterSingleton<PrParser>();
		}


		private static void RegisterForPLang(this ServiceContainer container, string absoluteAppStartupPath, string relativeStartupAppPath)
		{
			container.RegisterSingleton<MemoryStack>();			
			container.RegisterSingleton<FileAccessHandler>();
			container.RegisterSingleton<IEngine, Engine>();
			container.RegisterSingleton<ISettings, Settings>();
			container.RegisterSingleton<IBuilder, Building.Builder>();
			container.RegisterSingleton<ITypeHelper, TypeHelper>();
			container.RegisterSingleton<IPseudoRuntime, PseudoRuntime>();
			container.RegisterSingleton<IBuilderFactory>(factory =>
			{
				return new BuilderFactory(container, container.GetInstance<ITypeHelper>());
			});



			container.RegisterSingleton<IEventBuilder, EventBuilder>();
			container.RegisterSingleton<IEventRuntime, EventRuntime>();
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


			container.RegisterSingleton<ILlmService, PLangLlmService>(typeof(PLangLlmService).FullName);
			container.RegisterSingleton(factory =>
			{
				var type = GetImplementation(context, ReservedKeywords.Inject_LLMService, typeof(PLangLlmService));
				return factory.GetInstance<ILlmService>(type);
			});
			container.RegisterLlmFactory(typeof(PLangLlmService), true);


			/*container.Register(factory =>
			{
				return new Lazy<ILlmService>(() =>
				{
					var type = GetImplementation(context, ReservedKeywords.Inject_LLMService, typeof(PLangLlmService));
					return factory.GetInstance<ILlmService>(type);
				});
			});

			container.Register(factory =>
			{
				ILlmService? llmService = null;
				while (llmService == null)
				{
					try
					{
						var type = GetImplementation(context, ReservedKeywords.Inject_LLMService, typeof(PLangLlmService));
						llmService = factory.GetInstance<ILlmService>(type);
					}
					catch (Exception ex)
					{
						if (ex is AskUserException)
						{
							var askUserHandler = GetImplementation(context, ReservedKeywords.Inject_AskUserHandler);
							var askUser = factory.GetInstance<IAskUserHandler>(askUserHandler);

							var task = askUser.Handle(ex as AskUserException);
							task.Wait();

						}
						else
						{
							throw;
						}
					}
				}
				return llmService;
			});*/

			container.RegisterSingleton<ILogger, Services.LoggerService.Logger<Executor>>(typeof(Logger).FullName);
			container.RegisterSingleton(factory =>
			{
				var type = GetImplementation(context, ReservedKeywords.Inject_Logger, typeof(Logger));
				return factory.GetInstance<ILogger>(type);
			});

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

			
			container.RegisterSettingsRepositoryFactory(typeof(SqliteSettingsRepository), true, new SqliteSettingsRepository(container.GetInstance<IPLangFileSystem>(), context, container.GetInstance<ILogger>()));
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
				var moduleSettings = new Modules.DbModule.ModuleSettings(fileSystem, settings, context, llmServiceFactory, dbConnection, logger);

				dbConnection = moduleSettings.GetDefaultDbConnection(factory).Result;
				if (dbConnection != null) return dbConnection;

				var supportedTypes = moduleSettings.GetSupportedDbTypes();
				if (supportedTypes.Count == 1)
				{
					moduleSettings.CreateDataSource().Wait();
					dbConnection = GetDbConnection(factory, context);
					if (dbConnection != null) return dbConnection;
				}

				if (AppContext.TryGetSwitch("builder", out bool isBuilder) && isBuilder)
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

				var moduleSettings = new Modules.DbModule.ModuleSettings(fileSystem, settings, context, llmServiceFactory, dbConnection, logger);
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

			RegisterGlobalInjections(container);

		}
		private static void RegisterGlobalInjections(ServiceContainer container)
		{
			var prParser = container.GetInstance<PrParser>();
			var goals = prParser.GetAllGoals();
			var goalsWithInjections = goals.Where(p => p.Injections.FirstOrDefault(x => x.IsGlobal) != null);
			foreach (var goal in goalsWithInjections) {
				foreach (var injection in goal.Injections)
				{
					container.RegisterForPLangUserInjections(injection.Type, injection.Path, true);
				}
			}
		}

		public static void RegisterForPLangUserInjections(this IServiceContainer container, string injectorType, string pathToModule, bool isGlobalForApp, string? environmentVariable = null, string? environmentVariableValue = null, Type? injectionType = null)
		{
			if (environmentVariable != null && environmentVariableValue != null)
			{
				if (Environment.GetEnvironmentVariable(environmentVariable) != environmentVariableValue) return;
			}

			var context = container.GetInstance<PLangAppContext>();
			if (injectorType.ToLower() == "db")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(IDbConnection), pathToModule);
				if (type != null)
				{
					container.Register(typeof(IDbConnection), type, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(typeof(IDbConnection), new InjectedType("db", typeof(IDbConnection), type));
						AppContext.SetData(ReservedKeywords.Inject_IDbConnection, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "settings")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(ISettingsRepository), pathToModule);
				if (type != null)
				{
					if (container.CanGetInstance(typeof(ISettingsRepository), type.FullName)) return;

					container.RegisterSingleton(typeof(ISettingsRepository), type, type.FullName);
					context.AddOrReplace(ReservedKeywords.Inject_SettingsRepository, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(typeof(ISettingsRepository), new InjectedType("settings", typeof(ISettingsRepository), type));
						AppContext.SetData(ReservedKeywords.Inject_SettingsRepository, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "caching")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(IAppCache), pathToModule);
				if (type != null)
				{
					container.Register(typeof(IAppCache), type, type.FullName);
					context.AddOrReplace(ReservedKeywords.Inject_Caching, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(typeof(IAppCache), new InjectedType("caching", typeof(IAppCache), type));
						AppContext.SetData(ReservedKeywords.Inject_Caching, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "logger")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(ILogger), pathToModule);
				if (type != null)
				{
					container.Register(typeof(ILogger), type, type.FullName);
					context.AddOrReplace(ReservedKeywords.Inject_Logger, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(typeof(ILogger), new InjectedType("logger", typeof(ILogger), type));
						AppContext.SetData(ReservedKeywords.Inject_Logger, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "llm")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(ILlmService), pathToModule);
				if (type != null)
				{
					container.Register(typeof(ILlmService), type, type.FullName);
					context.AddOrReplace(ReservedKeywords.Inject_LLMService, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(typeof(ILlmService), new InjectedType("llm", typeof(ILlmService), type));
						AppContext.SetData(ReservedKeywords.Inject_LLMService, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "askuser")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(IAskUserHandler), pathToModule);
				if (type != null)
				{
					container.Register(typeof(IAskUserHandler), type, type.FullName);
					context.AddOrReplace(ReservedKeywords.Inject_AskUserHandler, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(typeof(IAskUserHandler), new InjectedType("askuser", typeof(IAskUserHandler), type));
						AppContext.SetData(ReservedKeywords.Inject_AskUserHandler, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "encryption")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(IEncryption), pathToModule);
				if (type != null)
				{
					container.Register(typeof(IEncryption), type, type.FullName);
					context.AddOrReplace(ReservedKeywords.Inject_EncryptionService, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(typeof(IEncryption), new InjectedType("encryption", typeof(IEncryption), type));
						AppContext.SetData(ReservedKeywords.Inject_EncryptionService, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "archiver")
			{
				var type = injectionType ?? GetInjectionType(container, typeof(IArchiver), pathToModule);

				if (type != null)
				{
					container.Register(typeof(IArchiver), type, type.FullName);
					context.AddOrReplace(ReservedKeywords.Inject_Archiving, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(typeof(IArchiver), new InjectedType("archiver", typeof(IArchiver), type));
						AppContext.SetData(ReservedKeywords.Inject_Archiving, type.FullName);
					}
				}
			}

		}

		public static Type? GetInjectionType(IServiceContainer container, Type typeToFind, string injectorType)
		{
			var fileSystem = container.GetInstance<IPLangFileSystem>();

			if (!fileSystem.Directory.Exists(".services"))
			{
				throw new RuntimeException($".services folder not found in {fileSystem.RootDirectory}");
			}

			//injectorType = (injectorType.EndsWith(".dll")) ? injectorType : injectorType + ".dll";

			string dllFilePath = Path.GetDirectoryName(Path.Combine(fileSystem.GoalsPath, ".services", injectorType));
			string[] dllFiles = new string[] { dllFilePath };
			if (!fileSystem.File.Exists(dllFilePath))
			{
				//var dirName = Path.GetDirectoryName(injectorType);
				var moduleFolderPath = Path.Combine(fileSystem.GoalsPath, ".services", dllFilePath);
				if (!fileSystem.Directory.Exists(moduleFolderPath))
				{
					throw new RuntimeException($"{injectorType} injection folder could not be found. Path {moduleFolderPath}");
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

			AppDomain.CurrentDomain.AssemblyResolve += (sender, eventArgs) =>
			{
				string assemblyName = new AssemblyName(eventArgs.Name).Name + ".dll";
				string assemblyPath = Path.Combine(Path.GetDirectoryName(eventArgs.RequestingAssembly.Location), assemblyName);
				if (File.Exists(assemblyPath))
				{
					return Assembly.LoadFile(assemblyPath);
				}
				return null;
			};

			if (type != null)
			{
				return type;
			}

			throw new RuntimeException($"Cannot find {injectorType} in {dllFilePath}. Make sure that the class inherits from {typeToFind.Name} and the name of the dll is {injectorType}.dll");
		}

		private static IDbConnection? GetDbConnection(IServiceFactory factory, PLangAppContext context)
		{
			DataSource? dataSource = null;
			if (context.TryGetValue(ReservedKeywords.CurrentDataSourceName, out object? obj) && obj != null)
			{
				dataSource = (DataSource)obj;
			}
			else if ((obj = AppContext.GetData(ReservedKeywords.CurrentDataSourceName)) != null)
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
