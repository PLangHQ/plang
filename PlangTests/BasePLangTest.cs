﻿using LightInject;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NSubstitute;
using PLang.Building.Parsers;
using PLang.Container;
using PLang.Errors.Handlers;
using PLang.Events;
using PLang.Exceptions.AskUser;
using PLang.Interfaces;
using PLang.Models;
using PLang.Runtime;
using PLang.Services.AppsRepository;
using PLang.Services.CachingService;
using PLang.Services.LlmService;
using PLang.Services.OpenAi;
using PLang.Services.OutputStream;
using PLang.Services.SettingsService;
using PLang.Services.SigningService;
using PLang.Utils;
using PLangTests.Mocks;
using System.Data;
using System.Runtime.CompilerServices;
using static PLang.Modules.BaseBuilder;

namespace PLangTests
{
	public class BasePLangTest
	{
		protected IServiceContainer container;

		protected MockLogger logger;
		protected PLangMockFileSystem fileSystem;
		protected ILlmService llmService;
		protected ILlmServiceFactory llmServiceFactory;
		protected IPseudoRuntime pseudoRuntime;
		protected IEngine engine;
		protected ISettingsRepository settingsRepository;
		protected ISettings settings;
		protected IEventRuntime eventRuntime;
		protected ITypeHelper typeHelper;
		protected PrParser prParser;
		protected PLangAppContext context;
		protected HttpClient httpClient;
		protected LlmCaching llmCaching;
		protected IServiceContainerFactory containerFactory;
		protected MemoryStack memoryStack;
		protected VariableHelper variableHelper;
		protected IDbConnection db;
		protected IArchiver archiver;
		protected IEventSourceRepository eventSourceRepository;
		protected IEncryption encryption;
		protected IEncryptionFactory encryptionFactory;
		protected IOutputStream outputStream;
		protected IOutputStream outputSystemStream;
		protected IOutputStreamFactory outputStreamFactory;
		protected IOutputSystemStreamFactory outputSystemStreamFactory;
		protected IAppCache appCache;
		protected IPLangIdentityService identityService;
		protected IPLangSigningService signingService;
		protected IPLangAppsRepository appsRepository;
		protected IHttpClientFactory httpClientFactory;
		protected IAskUserHandlerFactory askUserHandlerFactory;
		protected IAskUserHandler askUserHandler;
		protected IErrorHandler errorHandler;
		protected IErrorHandlerFactory errorHandlerFactory;
		protected ISettingsRepositoryFactory settingsRepositoryFactory;
		protected void Initialize()
		{

			container = CreateServiceContainer();

		}

		protected void LoadOpenAI()
		{
			settings.Get(typeof(OpenAiService), "Global_AIServiceKey", Arg.Any<string>(), Arg.Any<string>()).Returns(Environment.GetEnvironmentVariable("OpenAIKey"));

			var llmService = new OpenAiService(settings, logger, llmCaching, context);
			llmServiceFactory.CreateHandler().Returns(llmService);
		}
		protected IServiceContainer CreateServiceContainer()
		{
			AppContext.SetSwitch(ReservedKeywords.Test, true);
			container = new ServiceContainer();
			context = new PLangAppContext();
			fileSystem = new PLangMockFileSystem();
			fileSystem.AddFile(Path.Join(Environment.CurrentDirectory, ".build", "info.txt"), Guid.NewGuid().ToString());


			container.RegisterInstance<IPLangFileSystem>(fileSystem);
			container.RegisterInstance<IServiceContainer>(container);
			this.settingsRepository = new SqliteSettingsRepository(fileSystem, context, logger);
			container.RegisterInstance<ISettingsRepository>(settingsRepository);


			settingsRepositoryFactory = Substitute.For<ISettingsRepositoryFactory>();
			settingsRepositoryFactory.CreateHandler().Returns(settingsRepository);
			container.RegisterInstance<ISettingsRepositoryFactory>(settingsRepositoryFactory);

			containerFactory = Substitute.For<IServiceContainerFactory>();
			containerFactory.CreateContainer(Arg.Any<PLangAppContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IOutputStreamFactory>(), Arg.Any<IOutputSystemStreamFactory>(), Arg.Any<IErrorHandlerFactory>(), Arg.Any<IAskUserHandlerFactory>()).Returns(p =>
			{
				var container = CreateServiceContainer();

				IEngine engine = container.GetInstance<IEngine>();
				engine.GetMemoryStack().Returns(a =>
				{
					return new MemoryStack(pseudoRuntime, engine, settings, context);
				});
				return container;
			});
			container.RegisterInstance<IServiceContainerFactory>(containerFactory);

			context = new PLangAppContext();
			container.RegisterInstance(context);

			context.AddOrReplace(ReservedKeywords.Inject_Caching, typeof(InMemoryCaching).FullName);

			appCache = new InMemoryCaching();
			container.RegisterInstance<IAppCache>(appCache, "PLang.Services.CachingService.InMemoryCaching");

			logger = Substitute.For<MockLogger>();
			//logger = new PLang.Utils.Logger<BasePLangTest>();
			container.RegisterInstance<ILogger>(logger);

			identityService = Substitute.For<IPLangIdentityService>();
			container.RegisterInstance(identityService);
			signingService = Substitute.For<IPLangSigningService>();
			container.RegisterInstance(signingService);

			llmService = Substitute.For<ILlmService>();
			container.RegisterInstance(llmService);
			llmServiceFactory = Substitute.For<ILlmServiceFactory>();
			llmServiceFactory.CreateHandler().Returns(llmService);
			container.RegisterInstance(llmServiceFactory);

			askUserHandler = Substitute.For<IAskUserHandler>();
			container.RegisterInstance(askUserHandler);
			askUserHandlerFactory = Substitute.For<IAskUserHandlerFactory>();
			askUserHandlerFactory.CreateHandler().Returns(askUserHandler);
			container.RegisterInstance(askUserHandlerFactory);

			outputStream = Substitute.For<IOutputStream>();
			container.RegisterInstance(outputStream);
			outputStreamFactory = Substitute.For<IOutputStreamFactory>();
			outputStreamFactory.CreateHandler().Returns(outputStream);
			container.RegisterInstance(outputStreamFactory);

			outputSystemStream = Substitute.For<IOutputStream>();
			container.RegisterInstance(outputSystemStream);
			outputSystemStreamFactory = Substitute.For<IOutputSystemStreamFactory>();
			outputSystemStreamFactory.CreateHandler().Returns(outputStream);
			container.RegisterInstance(outputStreamFactory);


			httpClientFactory = Substitute.For<IHttpClientFactory>();
			container.RegisterInstance(httpClientFactory);

			encryption = Substitute.For<IEncryption>();
			container.RegisterInstance(encryption);
			encryptionFactory = Substitute.For<IEncryptionFactory>();
			encryptionFactory.CreateHandler().Returns(encryption);

			container.RegisterInstance(encryptionFactory);

			appsRepository = Substitute.For<IPLangAppsRepository>();
			container.RegisterInstance(appsRepository);

			engine = Substitute.For<IEngine>();
			container.RegisterInstance(engine);

			settings = Substitute.For<ISettings>();

			container.RegisterInstance<ISettings>(settings);
			pseudoRuntime = Substitute.For<IPseudoRuntime>();
			container.RegisterInstance(pseudoRuntime);

			eventRuntime = Substitute.For<IEventRuntime>();
			container.RegisterInstance(eventRuntime);

			errorHandler = Substitute.For<IErrorHandler>();
			container.RegisterInstance(errorHandler);
			errorHandlerFactory = Substitute.For<IErrorHandlerFactory>();
			container.RegisterInstance(errorHandlerFactory);

			db = Substitute.For<IDbConnection>();
			//container.RegisterInstance(db);

			eventSourceRepository = Substitute.For<IEventSourceRepository>();
			container.RegisterInstance(eventSourceRepository);

			container.Register<EventBuilder>();

			container.Register<IGoalParser, GoalParser>();

			typeHelper = Substitute.For<ITypeHelper>();
			container.RegisterInstance(typeHelper);

			memoryStack = new MemoryStack(pseudoRuntime, engine, settings, context);
			container.RegisterInstance(memoryStack);

			archiver = Substitute.For<IArchiver>();
			container.RegisterInstance(archiver);

			prParser = new PrParser(fileSystem);
			container.RegisterInstance(prParser);

			llmCaching = new LlmCaching(fileSystem, settings);
			container.RegisterInstance(llmCaching);

			variableHelper = new VariableHelper(context, memoryStack, settings);
			container.RegisterInstance(variableHelper);

			container.RegisterInstance(prParser);
			return container;
		}

		public record TestResponse(string stepText, string response, DateTimeOffset? created = null);

		public void Store(string stepText, string? response, [CallerMemberName] string caller = "")
		{
			if (string.IsNullOrWhiteSpace(response)) return;

			if (string.IsNullOrWhiteSpace(stepText)) throw new Exception("stepText cannot be empty");
			if (string.IsNullOrWhiteSpace(caller)) throw new Exception("caller cannot be empty");

			var dir = GetSourceResponseDir();
			if (!Directory.Exists(dir))
			{
				Directory.CreateDirectory(dir);
			}

			string filePath = Path.Combine(dir, caller + ".json");

			List<TestResponse> responses = new List<TestResponse>();
			if (File.Exists(filePath))
			{
				var jsonFile = File.ReadAllText(filePath);
				responses = JsonConvert.DeserializeObject<List<TestResponse>>(jsonFile) ?? new List<TestResponse>();
				var idx = responses.FindIndex(p => p.stepText == stepText);
				if (idx != -1)
				{
					return;
				}
			}
			responses.Add(new TestResponse(stepText, response, DateTimeOffset.Now));
			File.WriteAllText(filePath, JsonConvert.SerializeObject(responses, Formatting.Indented));
		}
		public ILlmService? GetLlmService(string stepText, string caller = "", Type? type = null)
		{
			var testResponse = GetLlmTestResponse(stepText, caller);
			if (testResponse == null) return null;

			if (type == null) type = typeof(GenericFunction);

			var llmService = Substitute.For<ILlmService>();
			llmService.Query(Arg.Any<LlmRequest>(), type).Returns(p =>
			{
				return JsonConvert.DeserializeObject(testResponse, type);
			});
			llmServiceFactory.CreateHandler().Returns(llmService);

			return llmService;

		}
		public string? GetLlmTestResponse(string stepText, [CallerMemberName] string caller = "")
		{
			if (string.IsNullOrWhiteSpace(caller)) throw new Exception("caller cannot be empty");

			var dir = GetSourceResponseDir();
			if (!Directory.Exists(dir))
			{
				return null;
			}
			string filePath = Path.Combine(dir, caller + ".json");
			if (!File.Exists(filePath)) return null;

			var jsonFile = File.ReadAllText(filePath);
			var responses = JsonConvert.DeserializeObject<List<TestResponse>>(jsonFile) ?? new List<TestResponse>();
			var testReponse = responses.FirstOrDefault(p => p.stepText == stepText);
			if (testReponse == null) return null;
			return testReponse.response;
		}

		public string GetSourceResponseDir()
		{
			string derivedClassPath = this.GetType().Assembly.Location;
			string moduleFolder = this.GetType().Namespace.Replace("PLang.Modules.", "").Replace(".Tests", "");
			if (derivedClassPath.ToLower().Contains("ncrunch"))
			{
				string testPath = Environment.GetEnvironmentVariable("PlangTestPath");
				if (string.IsNullOrEmpty(testPath)) throw new Exception("You must set the PlangTestPath environment variable. I should point to PlangTests folder. The PlangTests folder contains Modules folder");
				return Path.Combine(testPath, "Modules", moduleFolder, "responses");
			}
			else
			{
				string derivedClassDirectory = Path.GetDirectoryName(derivedClassPath);

				string responsesDir = Path.GetFullPath(Path.Combine(derivedClassDirectory, $"../../../Modules/{moduleFolder}/responses"));
				return responsesDir;
			}
		}

	}
}
