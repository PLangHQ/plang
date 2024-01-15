

using LightInject;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Accounts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nostr.Client.Client;
using Nostr.Client.Communicator;
using PLang.Building;
using PLang.Building.Events;
using PLang.Building.Parsers;
using PLang.Exceptions;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Modules.DbModule;
using PLang.Modules.MessageModule;
using PLang.Runtime;
using PLang.SafeFileSystem;
using PLang.Services.ArchiveService;
using PLang.Services.CachingService;
using PLang.Services.EncryptionService;
using PLang.Services.EventSourceService;
using PLang.Services.LlmService;
using PLang.Services.OutputStream;
using PLang.Services.SettingsService;
using RazorEngineCore;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Websocket.Client.Logging;
using static Org.BouncyCastle.Math.EC.ECCurve;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLang.Utils
{
    public static class Instance
	{
		private static readonly string PrefixForTempInjection = "__Temp__";
		public record InjectedType(Type ServiceType, Type ImplementationType);
		private static readonly Dictionary<string, InjectedType> injectedTypes = new();


		public static void RegisterForPLang(this ServiceContainer container, string path, string appPath, string askUserHandlerFullName, IOutputStream outputStream)
		{
			if (string.IsNullOrEmpty(askUserHandlerFullName))
			{
				throw new ArgumentNullException("askUserHandlerFullName cannot be empty when creating a new container");
			}
			container.RegisterInstance<IOutputStream>(outputStream);

			RegisterForPLang(container, path, appPath);

			var context = container.GetInstance<PLangAppContext>();
			askUserHandlerFullName = AppContext.GetData(ReservedKeywords.Inject_AskUserHandler) as string ?? askUserHandlerFullName;
			context.AddOrReplace(ReservedKeywords.Inject_AskUserHandler, askUserHandlerFullName);

			

			var fileSystem = container.GetInstance<IPLangFileSystem>();
			RegisterModules(container, fileSystem);
		}
		public static void RegisterForPLangWebserver(this ServiceContainer container, string path, string appPath, HttpListenerContext httpContext)
		{
			RegisterForPLang(container, path, appPath);

			var context = container.GetInstance<PLangAppContext>();
			string askUserHandlerFullName = AppContext.GetData(ReservedKeywords.Inject_AskUserHandler) as string ?? typeof(AskUserConsoleHandler).FullName;
			context.AddOrReplace(ReservedKeywords.Inject_AskUserHandler, askUserHandlerFullName);
			container.Register<IAskUserHandler, AskUserConsoleHandler>(typeof(AskUserConsoleHandler).FullName);

			container.RegisterSingleton<IOutputStream>(factory =>
			{
				var outputStream = new JsonOutputStream(httpContext);

				var askUserHandler = new AskUserHandler(outputStream);
				container.Register<IAskUserHandler>(askFactory =>
				{
					return new AskUserHandler(outputStream);
				});


				return outputStream;
			});

			var fileSystem = container.GetInstance<IPLangFileSystem>();
			RegisterModules(container, fileSystem);
		}
		public static void RegisterForPLangWindowApp(this ServiceContainer container, string path, string appPath, IAskUserDialog askUserDialog)
		{
			container.RegisterSingleton<IOutputStream, UIOutputStream>();

			RegisterForPLang(container, path, appPath);

			var context = container.GetInstance<PLangAppContext>();
			context.AddOrReplace(ReservedKeywords.Inject_AskUserHandler, typeof(AskUserWindowHandler).FullName);

			container.Register<IAskUserHandler>(factory =>
			{
				var askUserHandler = new AskUserWindowHandler(askUserDialog);
				return askUserHandler;
			}, typeof(AskUserWindowHandler).FullName);

			container.RegisterSingleton<IRazorEngine, RazorEngine>();
			var fileSystem = container.GetInstance<IPLangFileSystem>();
			RegisterModules(container, fileSystem);
		}


		public static void RegisterForPLangConsole(this ServiceContainer container, string path, string appPath)
		{
			container.RegisterSingleton<IOutputStream, ConsoleOutputStream>();

			RegisterForPLang(container, path, appPath);

			var context = container.GetInstance<PLangAppContext>();
			context.AddOrReplace(ReservedKeywords.Inject_AskUserHandler, typeof(AskUserConsoleHandler).FullName);
			container.Register<IAskUserHandler, AskUserConsoleHandler>(typeof(AskUserConsoleHandler).FullName);


			var fileSystem = container.GetInstance<IPLangFileSystem>();
			RegisterModules(container, fileSystem);
		}

		private static void RegisterForPLang(this ServiceContainer container, string path, string appPath)
		{

			container.Register<IServiceContainerFactory, ServiceContainerFactory>();
			container.RegisterSingleton<IPLangFileSystem>(factory =>
			{
				return new PLangFileSystem(path, appPath);
			});
			container.RegisterSingleton<IEngine, Engine>();
			container.RegisterSingleton<ISettings, Settings>();
			container.RegisterSingleton<IBuilder, PLang.Building.Builder>();
			container.RegisterSingleton<ITypeHelper, TypeHelper>();
			container.RegisterSingleton<IPseudoRuntime, PseudoRuntime>();
			container.RegisterSingleton<IBuilderFactory>(factory =>
			{
				return new BuilderFactory(container, container.GetInstance<ITypeHelper>());
			});

			container.RegisterSingleton<PrParser>();
			container.RegisterSingleton<MemoryStack>();
			container.RegisterSingleton<PLangAppContext>();
			container.RegisterSingleton<FileAccessHandler>();
			container.RegisterSingleton<Signature>();

			container.RegisterSingleton<IEventBuilder, EventBuilder>();
			container.RegisterSingleton<IEventRuntime, EventRuntime>();



			container.Register<IGoalParser>(factory =>
			{
				return new GoalParser(container, container.GetInstance<IPLangFileSystem>(), container.GetInstance<ISettings>());
			});
			container.Register<IGoalBuilder, GoalBuilder>();
			container.Register<IStepBuilder, StepBuilder>();
			container.Register<IInstructionBuilder, InstructionBuilder>();
			container.Register<IErrorHelper, ErrorHelper>();
			container.Register<SettingsBuilder, SettingsBuilder>();
			container.Register<HttpHelper, HttpHelper>();
			container.Register<CacheHelper, CacheHelper>();
			container.Register<VariableHelper, VariableHelper>();

			container.RegisterSingleton<INostrClient>(factory =>
			{
				var moduleSettings = new PLang.Modules.MessageModule.ModuleSettings(container.GetInstance<ISettings>(), container.GetInstance<ILlmService>());
				var nostrClientManager = new NostrClientManager();
				return nostrClientManager.GetClient(moduleSettings.GetRelays());
			}, path);

			container.RegisterSingleton<IWeb3>(factory =>
			{
				var moduleSettings = new PLang.Modules.BlockchainModule.ModuleSettings(container.GetInstance<ISettings>(), container.GetInstance<ILlmService>());

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
			}, path);


			// These are injectable by user
			var context = container.GetInstance<PLangAppContext>();

			var encryptionFullName = AppContext.GetData(ReservedKeywords.Inject_EncryptionService) ?? typeof(Encryption).FullName;
			context.AddOrReplace(ReservedKeywords.Inject_EncryptionService, encryptionFullName);
			container.Register<IEncryption, Encryption>(typeof(Encryption).FullName);
			container.Register<IEncryption>(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				var encryptionService = context[ReservedKeywords.Inject_EncryptionService].ToString();
				return factory.GetInstance<IEncryption>(encryptionService);
			});



			container.Register<IAskUserHandler>(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				var askUserHandler = context[ReservedKeywords.Inject_AskUserHandler].ToString();
				return factory.GetInstance<IAskUserHandler>(askUserHandler);
			});

			var llmFullName = AppContext.GetData(ReservedKeywords.Inject_LLMService) ?? typeof(PLangLlmService).FullName;
			context.AddOrReplace(ReservedKeywords.Inject_LLMService, llmFullName);
			container.Register<ILlmService, PLangLlmService>(typeof(PLangLlmService).FullName);
			container.Register<Lazy<ILlmService>>(factory =>
			{
				return new Lazy<ILlmService>(() =>
				{
					var context = container.GetInstance<PLangAppContext>();
					string type = context[ReservedKeywords.Inject_LLMService].ToString();
					if (context.ContainsKey(PrefixForTempInjection + ReservedKeywords.Inject_LLMService))
					{
						type = context[PrefixForTempInjection + ReservedKeywords.Inject_LLMService].ToString();
					}
					return factory.GetInstance<ILlmService>(type);
				});
			});
			container.Register<ILlmService>(factory =>
			{
				ILlmService? llmService = null;
				while (llmService == null)
				{
					try
					{
						var context = container.GetInstance<PLangAppContext>();
						string type = context[ReservedKeywords.Inject_LLMService].ToString();
						if (context.ContainsKey(PrefixForTempInjection + ReservedKeywords.Inject_LLMService))
						{
							type = context[PrefixForTempInjection + ReservedKeywords.Inject_LLMService].ToString();
						}
						llmService = factory.GetInstance<ILlmService>(type);
					}
					catch (Exception ex)
					{
						if (ex is AskUserException)
						{
							var askUserHandler = context[ReservedKeywords.Inject_AskUserHandler].ToString();
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
			});

			var loggerFullName = AppContext.GetData(ReservedKeywords.Inject_Logger) ?? typeof(Logger).FullName;
			context.AddOrReplace(ReservedKeywords.Inject_Logger, loggerFullName);
			container.RegisterSingleton<ILogger, Services.LoggerService.Logger<Executor>>(typeof(Logger).FullName);
			container.RegisterSingleton<ILogger>(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				string type = context[ReservedKeywords.Inject_Logger].ToString();
				return factory.GetInstance<ILogger>(type);
			});

			var cachingFullName = AppContext.GetData(ReservedKeywords.Inject_Caching) ?? typeof(InMemoryCaching).FullName;
			context.AddOrReplace(ReservedKeywords.Inject_Caching, cachingFullName);
			container.RegisterSingleton<IAppCache, InMemoryCaching>(typeof(InMemoryCaching).FullName);
			container.RegisterSingleton<IAppCache>(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				string type = context[ReservedKeywords.Inject_Caching].ToString();
				return factory.GetInstance<IAppCache>(type);
			});

			var zipFullName = AppContext.GetData(ReservedKeywords.Inject_Archiving) ?? typeof(Zip).FullName;
			context.AddOrReplace(ReservedKeywords.Inject_Archiving, zipFullName);
			container.RegisterSingleton<IArchiver, Zip>(typeof(Zip).FullName);
			container.RegisterSingleton<IArchiver>(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				string type = context[ReservedKeywords.Inject_Archiving].ToString();
				return factory.GetInstance<IArchiver>(type);
			});

			var settingRepositoryFullName = AppContext.GetData(ReservedKeywords.Inject_SettingsRepository) ?? typeof(SqliteSettingsRepository).FullName;
			context.AddOrReplace(ReservedKeywords.Inject_SettingsRepository, settingRepositoryFullName);
			container.RegisterSingleton<ISettingsRepository, SqliteSettingsRepository>(typeof(SqliteSettingsRepository).FullName);
			container.RegisterSingleton<ISettingsRepository>(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				string type = context[ReservedKeywords.Inject_SettingsRepository].ToString();
				return factory.GetInstance<ISettingsRepository>(type);
			});

			var dbConnectionFullName = AppContext.GetData(ReservedKeywords.Inject_IDbConnection) ?? typeof(SQLiteConnection).FullName;
			context.AddOrReplace(ReservedKeywords.Inject_IDbConnection, dbConnectionFullName);
			container.Register<IDbConnection, SQLiteConnection>(typeof(SQLiteConnection).FullName);
			container.Register<IDbConnection>(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				var fileSystem = container.GetInstance<IPLangFileSystem>();
				var settings = container.GetInstance<ISettings>();
				var llService = container.GetInstance<ILlmService>();
				var logger = container.GetInstance<ILogger>();

				string type = context[ReservedKeywords.Inject_IDbConnection].ToString();
				var dbConnection = factory.GetInstance<IDbConnection>(type);

				var moduleSettings = new PLang.Modules.DbModule.ModuleSettings(fileSystem, settings, context, llService, dbConnection, logger);
				DataSource dataSource = moduleSettings.GetCurrentDatasource().Result;

				dbConnection.ConnectionString = dataSource.ConnectionString;
				return dbConnection;
			});


			var eventSourceFullName = AppContext.GetData(ReservedKeywords.Inject_IEventSourceRepository) ?? typeof(SqliteEventSourceRepository).FullName;
			context.AddOrReplace(ReservedKeywords.Inject_IEventSourceRepository, eventSourceFullName);
			container.RegisterSingleton<IEventSourceRepository, SqliteEventSourceRepository>(typeof(SqliteEventSourceRepository).FullName);
			container.RegisterSingleton<IEventSourceRepository, DisableEventSourceRepository>(typeof(DisableEventSourceRepository).FullName);
			container.RegisterSingleton<IEventSourceRepository>(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				var fileSystem = container.GetInstance<IPLangFileSystem>();
				var settings = container.GetInstance<ISettings>();
				string dbType = context[ReservedKeywords.Inject_IDbConnection].ToString();
				var dbConnection = factory.GetInstance<IDbConnection>(dbType);
				var logger = container.GetInstance<ILogger>();

				var moduleSettings = new PLang.Modules.DbModule.ModuleSettings(fileSystem, settings, context, null, dbConnection, logger);
				DataSource dataSource = moduleSettings.GetCurrentDatasource().Result;

				if (!dataSource.KeepHistory)
				{
					context.AddOrReplace(ReservedKeywords.Inject_IEventSourceRepository, typeof(DisableEventSourceRepository).FullName);
					return factory.GetInstance<IEventSourceRepository>(typeof(DisableEventSourceRepository).FullName);
				}

				string type = context[ReservedKeywords.Inject_IEventSourceRepository].ToString();
				var eventSourceRepo = factory.GetInstance<IEventSourceRepository>(type);
				eventSourceRepo.DataSource = dataSource;
				return eventSourceRepo;
			});


			var fileSystem = container.GetInstance<IPLangFileSystem>();
			var settings = container.GetInstance<ISettings>();
			fileSystem.SetFileAccess(settings.GetValues<FileAccessControl>(typeof(PLangFileSystem)));
		}



		private static void RegisterModules(ServiceContainer container, IPLangFileSystem fileSystem)
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

			if (fileSystem.Directory.Exists("modules"))
			{
				var assemblyFiles = fileSystem.Directory.GetFiles("modules", "*.dll");
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
			foreach (var type in injectedTypes)
			{
				container.Register(type.Value.ServiceType, type.Value.ImplementationType, type.Key);
			}
		}

		private static void RegisterSetup(ServiceContainer container, IPLangFileSystem fileSystem)
		{
			var setupBuildPath = Path.Combine(fileSystem.RootDirectory, ".build", "Setup", ISettings.GoalFileName);
			if (fileSystem.File.Exists(setupBuildPath))
			{
				var prParser = container.GetInstance<PrParser>();
				var goal = prParser.ParsePrFile(setupBuildPath);
				if (goal == null || goal.Injections.Count == 0) return;

				foreach (var injection in goal.Injections)
				{
					RegisterForPLangUserInjections(container, injection.Type, injection.Path, injection.IsGlobal);
				}
			}
		}

		public static void RegisterForPLangUserInjections(this IServiceContainer container, string injectorType, string pathToModule, bool isGlobalForApp)
		{

			var context = container.GetInstance<PLangAppContext>();
			if (injectorType.ToLower() == "db")
			{
				var type = GetInjectionType(container, typeof(IDbConnection), pathToModule);
				if (type != null)
				{
					container.Register(typeof(IDbConnection), type, type.FullName);
					if (isGlobalForApp)
					{
						AppContext.SetData(ReservedKeywords.Inject_IDbConnection, type.FullName);
						injectedTypes.AddOrReplace(type.FullName, new InjectedType(typeof(IDbConnection), type));
					}
					context.AddOrReplace(ReservedKeywords.Inject_IDbConnection, type.FullName);
				}
			}

			if (injectorType.ToLower() == "settings")
			{
				var type = GetInjectionType(container, typeof(ISettingsRepository), pathToModule);
				if (type != null)
				{
					container.Register(typeof(ISettingsRepository), type, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(type.FullName, new InjectedType(typeof(ISettingsRepository), type));
						context.AddOrReplace(ReservedKeywords.Inject_SettingsRepository, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "caching")
			{
				var type = GetInjectionType(container, typeof(IAppCache), pathToModule);
				if (type != null)
				{
					container.Register(typeof(IAppCache), type, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(type.FullName, new InjectedType(typeof(IAppCache), type));
						context.AddOrReplace(ReservedKeywords.Inject_Caching, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "logger")
			{
				var type = GetInjectionType(container, typeof(ILogger), pathToModule);
				if (type != null)
				{
					container.Register(typeof(ILogger), type, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(type.FullName, new InjectedType(typeof(ILogger), type));
						context.AddOrReplace(ReservedKeywords.Inject_Logger, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "llm")
			{
				var type = GetInjectionType(container, typeof(ILlmService), pathToModule);
				if (type != null)
				{
					container.Register(typeof(ILlmService), type, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(type.FullName, new InjectedType(typeof(ILlmService), type));
						context.AddOrReplace(ReservedKeywords.Inject_LLMService, type.FullName);
					}
					else
					{
						context.AddOrReplace(PrefixForTempInjection + ReservedKeywords.Inject_LLMService, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "askuser")
			{
				var type = GetInjectionType(container, typeof(IAskUserHandler), pathToModule);
				if (type != null)
				{
					container.Register(typeof(IAskUserHandler), type, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(type.FullName, new InjectedType(typeof(IAskUserHandler), type));
						context.AddOrReplace(ReservedKeywords.Inject_AskUserHandler, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "encryption")
			{
				var type = GetInjectionType(container, typeof(IEncryption), pathToModule);
				if (type != null)
				{
					container.Register(typeof(IEncryption), type, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(type.FullName, new InjectedType(typeof(IEncryption), type));
						context.AddOrReplace(ReservedKeywords.Inject_EncryptionService, type.FullName);
					}
				}
			}

			if (injectorType.ToLower() == "archiver")
			{
				var type = GetInjectionType(container, typeof(IArchiver), pathToModule);
				if (type != null)
				{
					container.Register(typeof(IArchiver), type, type.FullName);
					if (isGlobalForApp)
					{
						injectedTypes.AddOrReplace(type.FullName, new InjectedType(typeof(IArchiver), type));
						context.AddOrReplace(ReservedKeywords.Inject_Archiving, type.FullName);
					}
				}
			}

		}

		public static Type? GetInjectionType(IServiceContainer container, Type typeToFind, string injectorType)
		{
			var settings = container.GetInstance<ISettings>();
			var logger = container.GetInstance<ILogger>();
			var fileSystem = container.GetInstance<IPLangFileSystem>();

			if (!fileSystem.Directory.Exists("services"))
			{
				logger.LogError($"services folder not found in {fileSystem.RootDirectory}");
				return null;
			}

			//injectorType = (injectorType.EndsWith(".dll")) ? injectorType : injectorType + ".dll";

			string dllFilePath = Path.Combine(settings.GoalsPath, "services", injectorType);
			string[] dllFiles = new string[] { dllFilePath };
			if (!fileSystem.File.Exists(dllFilePath))
			{
				//var dirName = Path.GetDirectoryName(injectorType);
				var moduleFolderPath = Path.Combine(settings.GoalsPath, "services", dllFilePath);
				if (!fileSystem.Directory.Exists(moduleFolderPath))
				{
					logger.LogError($"{injectorType} injection folder could not be found");
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

			logger.LogError($"Cannot find {injectorType} in {dllFilePath}. Make sure that the class inherits from {typeToFind.Name} and the name of the dll is {injectorType}.dll");
			return null;
		}

	}
}
