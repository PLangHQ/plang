using AngleSharp.Dom;
using Castle.DynamicProxy;
using LightInject;
using Microsoft.Data.Sqlite;
using Namotion.Reflection;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.RPC.Accounts;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nostr.Client.Client;
using PLang.Building;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Modules;
using PLang.Modules.MessageModule;
using PLang.Modules.WebserverModule;
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
using PLang.Services.OutputStream.Sinks;
using PLang.Services.SettingsService;
using PLang.Services.SigningService;
using PLang.Utils;
using System.Data;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Websocket.Client.Logging;
using static PLang.Modules.DbModule.ModuleSettings;


namespace PLang.Container
{
	public static class Instance
	{
		public record InjectedType(string InjectorName, Type ServiceType, Type ImplementationType);
		private static readonly Dictionary<Type, InjectedType> injectedTypes = [];


		public static void RegisterForPLang(this ServiceContainer container, string absoluteAppStartupPath, string relativeAppStartupPath,
			IEngine parentEngine)
		{
			container.RegisterBaseForPLang(absoluteAppStartupPath, relativeAppStartupPath, parentEngine);
			RegisterModules(container);

			container.RegisterForPLangInternal(absoluteAppStartupPath, relativeAppStartupPath);

			var engine = container.GetInstance<IEngine>();
			
			var memoryStack = parentEngine.Context.MemoryStack.Clone(engine);

			engine.AppContext = parentEngine.AppContext;
			engine.Context = new PLangContext(memoryStack, engine, ExecutionMode.Console)
			{
				Output = Output.CreateFromAppOutput(parentEngine.Context.Output, engine: engine)
			};
			var contextAccessor = container.GetInstance<IPLangContextAccessor>();
			contextAccessor.Current = engine.Context;
			

			RegisterEventRuntime(container);
			RegisterBaseVariables(container, parentEngine);
		}

		public static void RegisterForPLangWebserver(this ServiceContainer container, GoalStep step, IEngine parentEngine, PLangContext context)
		{
			container.RegisterBaseForPLang(step.Goal.AbsoluteAppStartupFolderPath, step.Goal.RelativeGoalFolderPath, parentEngine);
			RegisterModules(container);
			container.RegisterForPLangInternal(step.Goal.AbsoluteAppStartupFolderPath, step.Goal.RelativeGoalFolderPath);

			var engine = container.GetInstance<IEngine>();
			engine.AppContext = parentEngine.AppContext;

			container.Register<RequestHandler>(factory =>
					new RequestHandler(step,
									factory.GetInstance<ILogger>(),
									factory.GetInstance<IPLangFileSystem>(),
									factory.GetInstance<Modules.IdentityModule.Program>(),
									engine.PrParser));
			/*
		container.RegisterAskUserHandlerFactory(typeof(AskUserConsoleHandler), true, new AskUserConsoleHandler(container.GetInstance<IOutputSystemStreamFactory>()));
			*/

			RegisterEventRuntime(container);
			RegisterBaseVariables(container);
		}
		public static void RegisterForPLangWindowApp(this ServiceContainer container, string appStartupPath, string relativeAppPath,
			IAskUserDialog askUserDialog, IErrorDialog errorDialog, IForm iForm)
		{
			container.RegisterBaseForPLang(appStartupPath, relativeAppPath);
			RegisterModules(container);
			container.RegisterForPLangInternal(appStartupPath, relativeAppPath);

			var engine = container.GetInstance<IEngine>();
			var memoryStack = MemoryStack.New(container, engine);
			var msa = container.GetInstance<IMemoryStackAccessor>();
			msa.Current = memoryStack;

			var sink = new AppOutputSink(container.GetInstance<IPLangFileSystem>(), iForm);

			// Create app-level output with trusted user
			engine.AppContext = new PLangAppContext
			{
				Output = Output.CreateForTrustedUser(sink, engine: engine)
			};

			// Create execution context that shares the same actors
			engine.Context = new PLangContext(memoryStack, engine, ExecutionMode.Console)
			{
				Output = Output.CreateFromAppOutput(engine.AppContext.Output, engine: engine)
			}; 
			var contextAccessor = container.GetInstance<IPLangContextAccessor>();
			contextAccessor.Current = engine.Context;


			RegisterEventRuntime(container);
			RegisterBaseVariables(container);
		}

		public static void RegisterForPLangConsole(this ServiceContainer container, string appStartupPath, string relativeAppPath)
		{
			container.RegisterBaseForPLang(appStartupPath, relativeAppPath);
			RegisterModules(container);
			container.RegisterForPLangInternal(appStartupPath, relativeAppPath);
			var engine = container.GetInstance<IEngine>();

			RegisterConsoleContext(container, engine);

			RegisterEventRuntime(container);

			RegisterBaseVariables(container);

		}

		public static void RegisterForPLangBuilderConsole(this ServiceContainer container, string appStartupPath, string relativeAppPath)
		{
			container.RegisterBaseForPLang(appStartupPath, relativeAppPath);
			RegisterModules(container);
			container.RegisterForPLangInternal(appStartupPath, relativeAppPath);
			var engine = container.GetInstance<IEngine>();

			RegisterConsoleContext(container, engine);
			RegisterEventRuntime(container, true);

			RegisterBaseVariables(container);
		}


		public static void RegisterConsoleContext(ServiceContainer container, IEngine engine)
		{
			var memoryStack = MemoryStack.New(container, engine);
			var msa = container.GetInstance<IMemoryStackAccessor>();
			msa.Current = memoryStack;

			var consoleSink = new ConsoleSink();

			// Create app-level output with trusted user
			engine.AppContext = new PLangAppContext
			{
				Output = Output.CreateForTrustedUser(consoleSink, engine: engine)
			};

			// Create execution context that shares the same actors
			engine.Context = new PLangContext(memoryStack, engine, ExecutionMode.Console)
			{
				Output = Output.CreateFromAppOutput(engine.AppContext.Output, engine: engine)
			};

			var contextAccessor = container.GetInstance<IPLangContextAccessor>();
			contextAccessor.Current = engine.Context;
		}

		private static void RegisterBaseForPLang(this ServiceContainer container, string absoluteAppStartupPath, string relativeStartupAppPath, IEngine? parentEngine = null)
		{
			if (parentEngine != null)
			{
				container.RegisterInstance<IEngine>(parentEngine, "ParentEngine");
			}

			//container.Register<IServiceContainerFactory, ServiceContainerFactory>();
			container.RegisterSingleton<PLangAppContext>();
			container.RegisterSingleton<IPLangFileSystem>(factory =>
			{
				return new PLangFileSystem(absoluteAppStartupPath, relativeStartupAppPath);
			});
			container.RegisterSingleton<IPLangFileSystemFactory>(factory =>
			{
				return new PlangFileSystemFactory(container);
			});


			container.RegisterSingleton<ILogger, Services.LoggerService.Logger<Executor>>(typeof(Logger).FullName);
			container.RegisterSingleton(factory =>
			{
				var context = factory.GetInstance<PLangAppContext>();
				var type = GetImplementation(context, ReservedKeywords.Inject_Logger, typeof(Logger));
				return factory.GetInstance<ILogger>(type);
			});

			container.RegisterSingleton<DependancyHelper>();

		}

		private static void RegisterEventRuntime(this ServiceContainer container, bool isBuilder = false)
		{
			var appContext = container.GetInstance<PLangAppContext>();
			var fileSystem = container.GetInstance<IPLangFileSystem>();
			var engine = container.GetInstance<IEngine>();

			if (!isBuilder)
			{
				container.RegisterSingleton<IEventRuntime, EventRuntime>();
				return;
			}


			container.RegisterSingleton<IEventRuntime>(factory =>
			{

				var runtimeContainer = new ServiceContainer();

				runtimeContainer.RegisterBaseForPLang(fileSystem.RootDirectory, fileSystem.RelativeAppPath);
				runtimeContainer.RegisterForPLangInternal(fileSystem.RootDirectory, fileSystem.RelativeAppPath, engine);
				runtimeContainer.RegisterSingleton<IEventRuntime, EventRuntime>();

				var fileAccessHandler2 = runtimeContainer.GetInstance<IFileAccessHandler>();
				fileAccessHandler2.GiveAccess(Environment.CurrentDirectory, fileSystem.SystemDirectory);

				var engine2 = runtimeContainer.GetInstance<IEngine>();

				//Engine.InitPerRequest(runtimeContainer);

				engine2.Init(runtimeContainer);



				var eventRuntime = runtimeContainer.GetInstance<IEventRuntime>();
				return eventRuntime;
			});


		}

		private static void RegisterBaseVariables(ServiceContainer container, IEngine? parentEngine = null)
		{
			container.RegisterSingleton<IInterceptor, ErrorHandlingInterceptor>();
			container.RegisterSingleton<ProgramFactory>(factory =>
			{
				return new ProgramFactory(container);
			});

			var context = container.GetInstance<PLangAppContext>();

			var fileSystem = container.GetInstance<IPLangFileSystem>();
			context.AddOrReplace("!plang.osPath", fileSystem.SystemDirectory);
			context.AddOrReplace("!plang.rootPath", parentEngine?.FileSystem?.RootDirectory ?? fileSystem.RootDirectory);


			var fileAccessHandler = container.GetInstance<IFileAccessHandler>();
			fileAccessHandler.GiveAccess(fileSystem.RootDirectory, fileSystem.SystemDirectory);
		}

		private static void RegisterForPLangInternal(this ServiceContainer container, string absoluteAppStartupPath, string relativeStartupAppPath, IEngine? parentEngine = null)
		{
			JsonConvert.DefaultSettings = () => new JsonSerializerSettings
			{
				ReferenceLoopHandling = ReferenceLoopHandling.Ignore
			};

			container.Register<IMemoryStackAccessor, MemoryStackAccessor>();
			container.RegisterSingleton<IPLangContextAccessor, ContextAccessor>();


			container.RegisterSingleton<IFileAccessHandler, FileAccessHandler>();

			container.RegisterSingleton<IEngine, Engine>();

			SetupAssemblyResolve(container.GetInstance<IPLangFileSystem>(), container.GetInstance<ILogger>(), container.GetInstance<DependancyHelper>(), container.GetInstance<IEngine>());

			/*
			container.RegisterSingleton<EnginePool>(factory =>
			{
				return new EnginePool(10, () => container.GetInstance<IEngine>(), container);
			});*/

			//container.RegisterSingleton<ISettings, Settings>(); Moved to engine.Settings.
			container.RegisterSingleton<IBuilder, Building.Builder>();
			container.RegisterSingleton<ITypeHelper, TypeHelper>();
			container.RegisterSingleton<IPseudoRuntime, PseudoRuntime>();
			container.RegisterSingleton<IBuilderFactory>(factory =>
			{
				return new BuilderFactory(container, container.GetInstance<ITypeHelper>(), container.GetInstance<ILogger>());
			});


			container.RegisterSingleton<ICertHelper, CertHelper>();
			//container.RegisterOutputStreamFactory(typeof(MemoryOutputStream), false, new MemoryOutputStream());
			container.RegisterSingleton<IEventBuilder, EventBuilder>();
			if (AppContext.TryGetSwitch("Builder", out bool isBuilder) && !isBuilder)
			{

			}
			container.RegisterSingleton<IPLangIdentityService, PLangIdentityService>();
			container.RegisterSingleton<IPLangSigningService, PLangSigningService>();
			container.Register<IPublicPrivateKeyCreator, PublicPrivateKeyCreator>();



			container.RegisterSingleton<IGoalParser>(factory =>
			{
				return new GoalParser(container, container.GetInstance<IPLangFileSystem>(), container.GetInstance<ISettings>(), container.GetInstance<ILogger>());
			});
			container.Register<IGoalBuilder, GoalBuilder>();
			container.Register<IStepBuilder, StepBuilder>();
			container.Register<IInstructionBuilder, InstructionBuilder>();

			container.Register<LlmCaching, LlmCaching>();
			container.Register<VariableHelper, VariableHelper>();

			container.RegisterSingleton<MethodHelper>();
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
				if (parentEngine != null)
				{
					return parentEngine.AppCache;
				}

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
				new SqliteSettingsRepository(container.GetInstance<IPLangFileSystemFactory>(), context, container.GetInstance<ILogger>()));

			container.RegisterSingleton(factory =>
			{
				string type = GetImplementation(context, ReservedKeywords.Inject_SettingsRepository, typeof(SqliteSettingsRepository));
				return factory.GetInstance<ISettingsRepository>(type);
			});

			container.Register<IDbConnection, SqliteConnection>(typeof(SqliteConnection).FullName);
			container.Register<IDbConnection, DbConnectionUndefined>(typeof(DbConnectionUndefined).FullName);

			container.Register(factory =>
			{
				var type = GetImplementation(context, ReservedKeywords.Inject_IDbConnection, typeof(SqliteConnection));
				return factory.GetInstance<IDbConnection>(type);
			});
			container.RegisterDbFactory(typeof(SqliteConnection), true);

			/*
			container.Register(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				var fileSystem = container.GetInstance<IPLangFileSystem>();
				var settings = container.GetInstance<ISettings>();
				var llmServiceFactory = container.GetInstance<ILlmServiceFactory>();
				var logger = container.GetInstance<ILogger>();
				var typeHelper = container.GetInstance<ITypeHelper>();

				throw new Exception("Here?");

				IDbConnection? dbConnection = GetDbConnection(factory, context);
				if (dbConnection != null) return dbConnection;

				dbConnection = factory.GetInstance<IDbConnection>(typeof(DbConnectionUndefined).FullName);
				var moduleSettings = container.GetInstance<Modules.DbModule.ModuleSettings>();

				dbConnection = moduleSettings.GetDefaultDbConnection(factory).Result;
				if (dbConnection != null) return dbConnection;

				var supportedTypes = moduleSettings.GetSupportedDbTypes();
				if (supportedTypes.Count == 1)
				{
					moduleSettings.CreateDataSource("data", "sqlite", true, true).Wait();
					dbConnection = GetDbConnection(factory, context);
					if (dbConnection != null) return dbConnection;
				}

				if (AppContext.TryGetSwitch("Builder", out bool isBuilder) && isBuilder)
				{
					(var dataSource, var error) = moduleSettings.GetCurrentDataSource().Result;
					if (error != null) throw new ExceptionWrapper(error);

					dbConnection = factory.GetInstance<IDbConnection>(dataSource.TypeFullName);
					dbConnection.ConnectionString = dataSource.ConnectionString;
				}

				return dbConnection;
			});*/

			container.Register<IEventSourceRepository, SqliteEventSourceRepository>(typeof(SqliteEventSourceRepository).FullName);
			container.Register<IEventSourceRepository, DisableEventSourceRepository>(typeof(DisableEventSourceRepository).FullName);
			container.Register(factory =>
			{
				var context = container.GetInstance<PLangAppContext>();
				var fileSystem = container.GetInstance<IPLangFileSystem>();
				var settings = container.GetInstance<ISettings>();
				var logger = container.GetInstance<ILogger>();
				var llmServiceFactory = container.GetInstance<ILlmServiceFactory>();
				var typeHelper = container.GetInstance<ITypeHelper>();

				var moduleSettings = container.GetInstance<Modules.DbModule.ModuleSettings>();
				var dataSources = moduleSettings.GetAllDataSources().Result;
				if (dataSources.Count == 0)
				{
					return factory.GetInstance<IEventSourceRepository>(typeof(DisableEventSourceRepository).FullName);
				}

				(var dataSource, var error) = moduleSettings.GetDataSourceOrDefault().Result;
				if (error != null) throw new ExceptionWrapper(error);

				if (!dataSource.KeepHistory)
				{
					context.AddOrReplace(ReservedKeywords.Inject_IEventSourceRepository, typeof(DisableEventSourceRepository).FullName);
					return factory.GetInstance<IEventSourceRepository>(typeof(DisableEventSourceRepository).FullName);
				}
				else if (dataSource.KeepHistory && dataSource.TypeFullName == typeof(SqliteConnection).FullName)
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

			context.TryGetValue(reservedKeyword, out object? obj);
			if (obj != null)
			{
				return obj.ToString()!;
			}
			if (defaultType != null)
			{
				context.AddOrReplace(reservedKeyword, defaultType.FullName);
				return defaultType.FullName!;
			}

			throw new RuntimeException($"Could not get implementaion name for {reservedKeyword}");
		}

		private static void SetupAssemblyResolve(IPLangFileSystem fileSystem, ILogger logger, DependancyHelper dependancyHelper, IEngine engine)
		{
			engine.AsmHandler = (sender, resolveArgs) =>
			{
				if (AppContext.TryGetSwitch("InternalGoalRun", out bool isEnabled) && isEnabled) return null;

				if (resolveArgs.Name.Contains("PLangLibrary", StringComparison.OrdinalIgnoreCase))
				{
					var loadedAssembly = AppDomain.CurrentDomain.GetAssemblies()
						.FirstOrDefault(a => a.GetName().Name.Contains("PLangLibrary", StringComparison.OrdinalIgnoreCase));

					if (loadedAssembly != null)
					{
						return loadedAssembly;
					}
				}


				string[] moduleAndServiceFolders = [".services", ".modules"];
				foreach (var folder in moduleAndServiceFolders)
				{
					string assemblyPath = fileSystem.Path.Join(fileSystem.RootDirectory, folder, new AssemblyName(resolveArgs.Name).Name + ".dll");
					if (fileSystem.File.Exists(assemblyPath))
					{
						return Assembly.LoadFile(assemblyPath);
					}

					try
					{
						string assemblyName = new AssemblyName(resolveArgs.Name).Name;
						var baseDirectory = fileSystem.Path.Join(fileSystem.RootDirectory, folder);
						if (!fileSystem.Directory.Exists(baseDirectory)) return null;

						var files = fileSystem.Directory.GetFiles(baseDirectory, assemblyName + ".dll", SearchOption.AllDirectories);

						if (files.Length > 0)
						{

							var netVersionPattern = @"net(?<version>\d+(\.\d+)?)(coreapp)?";

							// Parse and prioritize .NET versions in descending order (e.g., net8.0, net7.0, etc.)
							var matchedAssembly = files
								.Select(file => new { File = file, Match = Regex.Match(file, netVersionPattern) })
								.Where(x => x.Match.Success)
								.OrderByDescending(x => GetVersionPriority(x.Match.Groups["version"].Value))
								.Select(x => x.File)
								.FirstOrDefault();

							return Assembly.LoadFile(matchedAssembly);
						}
						else
						{
							var depsFiles = fileSystem.Directory.GetFiles(baseDirectory, "*.deps.json", SearchOption.AllDirectories);

							foreach (var dep in depsFiles)
							{
								var content = fileSystem.File.ReadAllText(dep);
								var json = JsonConvert.DeserializeObject(content) as JObject;
								var libs = json.GetValue("libraries") as JObject;

								if (libs == null) continue;

								var dirPath = fileSystem.Path.GetDirectoryName(dep);

								foreach (var prop in libs.Properties())
								{
									if (prop.Name.StartsWith(assemblyName + "/"))
									{
										var name = prop.Name.Substring(0, prop.Name.IndexOf("/"));

										return dependancyHelper.InstallDependancy(dirPath, dep, name);
									}
								}

								var foundAssemblyName = FindAssemblyPackage(json, assemblyName + ".dll");
								if (foundAssemblyName != null)
								{
									var assmebly = dependancyHelper.InstallDependancy(dirPath, dep, foundAssemblyName);
									return assmebly;
								}
								int i = 0;
							}
						}
					}
					catch (Exception ex)
					{
						logger.LogError(ex, ex.Message);
						return null;
					}
				}
				return null;
			};

			AppDomain.CurrentDomain.AssemblyResolve += engine.AsmHandler;
		}

		private static void RegisterModules(ServiceContainer container)
		{

			var currentAssembly = Assembly.GetExecutingAssembly();
			var moduleSettingsFromCurrentAssembly = currentAssembly.GetTypes()
																			.Where(t => !t.IsAbstract && !t.IsInterface &&
																			(typeof(IModuleSettings).IsAssignableFrom(t)))
																			.ToList();

			// Register these types with the DI container
			foreach (var type in moduleSettingsFromCurrentAssembly)
			{
				container.Register(type);
				container.Register(type, type, serviceName: type.FullName);
			}
			var factoriesFromCurrentAssembly = currentAssembly.GetTypes()
																			.Where(t => !t.IsAbstract && !t.IsInterface &&
																			(typeof(BaseFactory).IsAssignableFrom(t)))
																			.ToList();

			// Register these types with the DI container
			foreach (var type in factoriesFromCurrentAssembly)
			{
				container.Register(type, factory =>
				{
					var instance = Activator.CreateInstance(type, [container]) as BaseFactory;
					return instance;
				});
			}

			// Scan the current assembly for types that inherit from BaseBuilder
			var modulesFromCurrentAssembly = currentAssembly.GetTypes()
																.Where(t => !t.IsAbstract && !t.IsInterface &&
																(typeof(BaseBuilder).IsAssignableFrom(t) || typeof(BaseProgram).IsAssignableFrom(t)))
																.ToList();

			// Register these types with the DI container
			foreach (var type in modulesFromCurrentAssembly)
			{
				container.Register(type);
				//container.Register(type, type, serviceName: type.FullName); 
				/*container.Register(type, factory =>
				{
					var instance = Activator.CreateInstance(type) as BaseProgram;
					var context = container.GetInstance<PLangAppContext>();

					instance.Init(container, context["!Goal"] as Goal, context["!Step"] as GoalStep, context["!Instruction"] as Building.Model.Instruction, null);	
					return instance;
				}, serviceName: type.FullName + "Factory");*/


			}
			container.Register<BaseBuilder, BaseBuilder>();

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
		static double GetVersionPriority(string version)
		{
			if (double.TryParse(version, out double parsedVersion))
			{
				return parsedVersion; // Return parsed version as the priority
			}

			return 0; // Default if parsing fails
		}
		private static string? FindAssemblyPackage(JObject jsonObject, string assemblyName)
		{
			var targets = jsonObject["targets"];

			if (targets != null)
			{
				foreach (var target in targets.Children<JProperty>())
				{
					foreach (var library in target.Value.Children<JProperty>())
					{
						var runtime = library.Value["runtime"];
						if (runtime != null)
						{
							foreach (var assembly in runtime.Children<JProperty>())
							{
								if (assembly.Name.EndsWith(assemblyName, StringComparison.OrdinalIgnoreCase))
								{
									return library.Name.Substring(0, library.Name.IndexOf("/"));
								}
							}
						}
					}
				}
			}

			return null;
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
			string[] dllFiles;
			string extension = fileSystem.Path.GetExtension(injectorType);
			if (!string.IsNullOrEmpty(injectorType) && !string.IsNullOrEmpty(extension))
			{
				string dllFilePath = fileSystem.Path.Join(fileSystem.GoalsPath, ".services", injectorType);
				if (!fileSystem.File.Exists(dllFilePath))
				{
					var logger = container.GetInstance<ILogger>();
					logger.LogWarning($"{injectorType} injection folder could not be found. Path {dllFilePath}");
					return null;
				}
				dllFiles = [dllFilePath];
			}
			else
			{
				string dllFolderPath = fileSystem.Path.Join(fileSystem.GoalsPath, ".services", injectorType);
				if (!fileSystem.Directory.Exists(dllFolderPath))
				{
					var logger = container.GetInstance<ILogger>();
					logger.LogWarning($"{injectorType} injection folder could not be found. Path {dllFolderPath}");
					return null;
				}

				dllFiles = fileSystem.Directory.GetFiles(dllFolderPath, "*.dll", SearchOption.AllDirectories);
			}

			foreach (var dllFile in dllFiles)
			{
				Assembly assembly = Assembly.LoadFile(dllFile);
				var type = assembly.GetTypes().FirstOrDefault(p => p.GetInterfaces().Contains(typeToFind));
				if (type != null) return type;
			}

			throw new RuntimeException($"Cannot find {injectorType} in {string.Join(", ", dllFiles)}. Make sure that the class inherits from {typeToFind.Name} and the name of the dll is {injectorType}.dll");
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
			}
		}


		private static IDbConnection? GetDbConnection(IServiceFactory factory, PLangAppContext context)
		{
			throw new Exception("HUh??");
			DataSource? dataSource = null;
			if (context.TryGetValue(ReservedKeywords.CurrentDataSource, out object? obj) && obj != null)
			{
				dataSource = obj as DataSource;
			}

			if (dataSource == null) return null;

			var dbConnection = factory.GetInstance<IDbConnection>(dataSource.TypeFullName);
			dbConnection.ConnectionString = dataSource.ConnectionString;
			return dbConnection;

		}

	}
}
