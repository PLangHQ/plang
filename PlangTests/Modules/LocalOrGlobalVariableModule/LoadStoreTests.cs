using LightInject;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Events;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.Services.DbService;
using PLang.Services.EventSourceService;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Data;
using System.Reflection;
using static PLang.Modules.DbModule.ModuleSettings;
using DbProgram = PLang.Modules.DbModule.Program;
using VariableProgram = PLang.Modules.VariableModule.Program;

namespace PLangTests.Modules.LocalOrGlobalVariableModule
{
	// load/store of %variables% against a real sqlite file. Each "run" gets a fresh memory stack,
	// so a value only survives from one run to the next through the datasource.
	[TestClass]
	public class LoadStoreTests
	{
		private string dir = "";
		private string dataDb = "";

		private class RealSqliteFactory : IDbServiceFactory
		{
			public IDbConnection CreateHandler(DataSource dataSource, MemoryStack memoryStack, bool ReadOnly = false)
			{
				return new SqliteConnection(dataSource.ConnectionString);
			}
		}

		private class NoHistoryEventSourceFactory : IEventSourceFactory
		{
			public IEventSourceRepository CreateHandler(DataSource dataSource) => new DisableEventSourceRepository();
		}

		// The runtime's choice: a datasource with KeepHistory records every write in __Events__.
		private class KeepHistoryEventSourceFactory : IEventSourceFactory
		{
			private readonly IPLangFileSystem fileSystem;
			private readonly IEncryption encryption;

			public KeepHistoryEventSourceFactory(IPLangFileSystem fileSystem)
			{
				this.fileSystem = fileSystem;
				encryption = Substitute.For<IEncryption>();
				encryption.Encrypt(Arg.Any<object>(), Arg.Any<string?>()).Returns("encrypted");
				encryption.GetKeyHash().Returns("keyhash");
			}

			public IEventSourceRepository CreateHandler(DataSource dataSource)
			{
				if (!dataSource.KeepHistory) return new DisableEventSourceRepository();
				return new SqliteEventSourceRepository(fileSystem, encryption);
			}
		}

		[TestInitialize]
		public void Init()
		{
			SqliteConnection.ClearAllPools();
			dir = Path.Combine(Path.GetTempPath(), "plang-loadstore-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			dataDb = Path.Combine(dir, "data.sqlite");
		}

		[TestCleanup]
		public void Cleanup()
		{
			SqliteConnection.ClearAllPools();
			try { Directory.Delete(dir, true); } catch { }
		}

		private record Run(VariableProgram Variables, MemoryStack MemoryStack, PLangContext Context, DbProgram Db, ModuleSettings DbSettings);

		// A fresh runtime run: new memory stack and context, the app's datasources are "data" (the
		// default) and nothing else, and the context has no datasource set by an earlier step.
		// keepHistory turns on event sourcing for "data", as the runtime does for an app's default datasource.
		private Run NewRun(bool keepHistory = false)
		{
			var realFs = new System.IO.Abstractions.FileSystem();
			var fileSystem = Substitute.For<IPLangFileSystem>();
			fileSystem.Path.Returns(realFs.Path);
			fileSystem.File.Returns(realFs.File);
			fileSystem.Directory.Returns(realFs.Directory);
			var settings = Substitute.For<ISettings>();
			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(new List<DataSource>
			{
				new DataSource("data", typeof(SqliteConnection).FullName!, $"Data Source={dataDb};Pooling=False", "data", "", "", KeepHistory: keepHistory, IsDefault: true, LocalPath: dataDb)
			});
			var logger = Substitute.For<ILogger>();
			var typeHelper = Substitute.For<ITypeHelper>();
			var appContext = new PLangAppContext();
			var prParser = new PrParser(fileSystem, logger);
			var engine = Substitute.For<IEngine>();
			var contextAccessor = Substitute.For<IPLangContextAccessor>();
			var variableHelper = new VariableHelper(settings, logger);
			var memoryStack = new MemoryStack(Substitute.For<IPseudoRuntime>(), engine, settings, variableHelper, contextAccessor);
			var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);
			contextAccessor.Current.Returns(context);
			var dbFactory = new RealSqliteFactory();
			var appCache = Substitute.For<IAppCache>();
			var llmServiceFactory = Substitute.For<ILlmServiceFactory>();
			IEventSourceFactory eventSourceFactory = keepHistory ? new KeepHistoryEventSourceFactory(fileSystem) : new NoHistoryEventSourceFactory();

			var moduleSettings = new ModuleSettings(fileSystem, settings, appContext, llmServiceFactory, logger, typeHelper, prParser,
				contextAccessor, variableHelper, dbFactory, appCache);

			var container = Substitute.For<IServiceContainer>();
			var programFactory = new ProgramFactory(container);
			var instances = new Dictionary<Type, object>
			{
				[typeof(IDbServiceFactory)] = dbFactory,
				[typeof(PLangAppContext)] = appContext,
				[typeof(IPLangFileSystem)] = fileSystem,
				[typeof(ISettings)] = settings,
				[typeof(ILlmServiceFactory)] = llmServiceFactory,
				[typeof(IEventSourceFactory)] = eventSourceFactory,
				[typeof(ILogger)] = logger,
				[typeof(ITypeHelper)] = typeHelper,
				[typeof(ModuleSettings)] = moduleSettings,
				[typeof(PrParser)] = prParser,
				[typeof(ProgramFactory)] = programFactory,
				[typeof(IPLangContextAccessor)] = contextAccessor,
				[typeof(IEventRuntime)] = Substitute.For<IEventRuntime>(),
				[typeof(IAppCache)] = appCache,
				[typeof(IEngine)] = engine,
			};
			instances[typeof(DbProgram)] = new DbProgram(dbFactory, appContext, fileSystem, settings, llmServiceFactory, eventSourceFactory,
				logger, typeHelper, moduleSettings, prParser, programFactory);
			container.GetInstance(Arg.Any<Type>()).Returns(ci => instances.TryGetValue(ci.Arg<Type>(), out var obj) ? obj : null!);

			var step = new GoalStep { Text = "load or store", RelativePrPath = "LoadStoreTests.pr", Goal = new Goal { GoalName = "Tick", AbsoluteAppStartupFolderPath = dir } };
			context.CallStack.EnterGoal(step.Goal);
			var variables = new VariableProgram(settings, programFactory, variableHelper);
			SetField(variables, "context", context);
			SetField(variables, "memoryStack", memoryStack);
			SetField(variables, "goalStep", step);
			SetField(variables, "goal", step.Goal);
			return new Run(variables, memoryStack, context, (DbProgram)instances[typeof(DbProgram)], moduleSettings);
		}

		private static void SetField(object target, string name, object value)
		{
			var type = target.GetType();
			while (type != null)
			{
				var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (field != null) { field.SetValue(target, value); return; }
				type = type.BaseType;
			}
			throw new InvalidOperationException(name);
		}

		private static long ToLong(object? value) => Convert.ToInt64(value is JValue jv ? jv.Value : value);

		[TestMethod]
		public async Task LoadWithDefault_NoDataSourceName_UsesDefaultDataSource_AndGivesDefaultWhenNothingStored()
		{
			var run = NewRun();
			var (_, error) = await run.Variables.LoadWithDefaultValue(new Dictionary<string, object?> { ["%round%"] = 0L });

			Assert.IsNull(error, error?.Message);
			Assert.AreEqual(0L, ToLong(run.MemoryStack.Get("round")));
		}

		[TestMethod]
		public async Task StoreThenLoad_NoDataSourceName_ValueSurvivesAcrossRuns()
		{
			for (long expected = 1; expected <= 3; expected++)
			{
				var run = NewRun();
				var (_, loadError) = await run.Variables.LoadWithDefaultValue(new Dictionary<string, object?> { ["%round%"] = 0L, ["%samples%"] = new JArray() });
				Assert.IsNull(loadError, loadError?.Message);

				var round = ToLong(run.MemoryStack.Get("round")) + 1;
				var samples = (JArray)run.MemoryStack.Get("samples")!;
				samples.Add("r" + round);
				run.MemoryStack.Put("round", round);
				run.MemoryStack.Put("samples", samples);

				var storeError = await run.Variables.Store(new List<string> { "%round%", "%samples%" });
				Assert.IsNull(storeError, storeError?.Message);
				Assert.AreEqual(expected, round);
			}

			var last = NewRun();
			var (_, error) = await last.Variables.Load(new List<string> { "%round%", "%samples%" });
			Assert.IsNull(error, error?.Message);
			Assert.AreEqual(3L, ToLong(last.MemoryStack.Get("round")));
			CollectionAssert.AreEqual(new[] { "r1", "r2", "r3" }, ((JArray)last.MemoryStack.Get("samples")!).Select(p => p.ToString()).ToArray());
		}

		[TestMethod]
		public async Task StoreThenLoad_NamedDataSource_UsesThatDataSource()
		{
			var first = NewRun();
			first.MemoryStack.Put("round", 7L);
			var storeError = await first.Variables.Store(new List<string> { "%round%" }, "data");
			Assert.IsNull(storeError, storeError?.Message);

			var second = NewRun();
			var (_, error) = await second.Variables.LoadWithDefaultValue(new Dictionary<string, object?> { ["%round%"] = 0L }, "data");
			Assert.IsNull(error, error?.Message);
			Assert.AreEqual(7L, ToLong(second.MemoryStack.Get("round")));
		}

		private long Count(string sql)
		{
			using var connection = new SqliteConnection($"Data Source={dataDb};Pooling=False");
			connection.Open();
			using var exists = connection.CreateCommand();
			var table = sql.Split(' ').Last();
			exists.CommandText = $"SELECT count(*) FROM sqlite_master WHERE type='table' AND name='{table}'";
			if (Convert.ToInt64(exists.ExecuteScalar()) == 0) return 0;
			using var command = connection.CreateCommand();
			command.CommandText = sql;
			return Convert.ToInt64(command.ExecuteScalar());
		}

		private long EventCount() => Count("SELECT count(*) FROM __Events__");
		private long VariableCount() => Count("SELECT count(*) FROM __Variables__");

		[TestMethod]
		public async Task Store_EventSourcedDataSource_WritesNoEvents()
		{
			for (long round = 1; round <= 10; round++)
			{
				var run = NewRun(keepHistory: true);
				var (_, loadError) = await run.Variables.LoadWithDefaultValue(new Dictionary<string, object?> { ["%round%"] = 0L });
				Assert.IsNull(loadError, loadError?.Message);
				Assert.AreEqual(round - 1, ToLong(run.MemoryStack.Get("round")));

				run.MemoryStack.Put("round", round);
				run.MemoryStack.Put("samples", new JArray(Enumerable.Range(1, (int)round).Select(i => "r" + i)));
				var storeError = await run.Variables.Store(new List<string> { "%round%", "%samples%" });
				Assert.IsNull(storeError, storeError?.Message);
			}

			Assert.AreEqual(0L, EventCount());
			Assert.AreEqual(2L, VariableCount());

			var last = NewRun(keepHistory: true);
			var (_, error) = await last.Variables.Load(new List<string> { "%round%", "%samples%" });
			Assert.IsNull(error, error?.Message);
			Assert.AreEqual(10L, ToLong(last.MemoryStack.Get("round")));
			Assert.AreEqual(10, ((JArray)last.MemoryStack.Get("samples")!).Count);
		}

		[TestMethod]
		public async Task SetValueAndStore_EventSourcedDataSource_WritesNoEvents()
		{
			var run = NewRun(keepHistory: true);
			var storeError = await run.Variables.SetValueAndStore(new Dictionary<string, object> { ["%round%"] = 5L });
			Assert.IsNull(storeError, storeError?.Message);

			Assert.AreEqual(0L, EventCount());
			Assert.AreEqual(1L, VariableCount());
		}

		[TestMethod]
		public async Task Store_EventSourcedDataSource_OtherTablesStillWriteEvents()
		{
			var run = NewRun(keepHistory: true);
			run.MemoryStack.Put("round", 1L);
			var storeError = await run.Variables.Store(new List<string> { "%round%" });
			Assert.IsNull(storeError, storeError?.Message);
			Assert.AreEqual(0L, EventCount());

			var (_, createError) = await run.Db.Execute("data", "CREATE TABLE things (id INTEGER PRIMARY KEY, name TEXT)", new List<string> { "things" });
			Assert.IsNull(createError, createError?.Message);
			var (_, insertError) = await run.Db.Insert("data", "INSERT INTO things (id, name) VALUES (@id, @name)",
				new List<DbProgram.ParameterInfo> { new("System.Int64", "id", 1L), new("System.String", "name", "a") });
			Assert.IsNull(insertError, insertError?.Message);

			Assert.AreEqual(2L, EventCount());
		}

		[TestMethod]
		[DataRow(true)]
		[DataRow(false)]
		public async Task Store_InOpenTransaction_FollowsTheTransaction(bool commit)
		{
			// Creates __Variables__ outside the transaction.
			var setup = NewRun(keepHistory: true);
			var (_, loadError) = await setup.Variables.LoadWithDefaultValue(new Dictionary<string, object?> { ["%round%"] = 0L });
			Assert.IsNull(loadError, loadError?.Message);

			var run = NewRun(keepHistory: true);
			var (dataSource, dsError) = await run.DbSettings.GetDataSourceOrDefault();
			Assert.IsNull(dsError, dsError?.Message);
			var connection = new SqliteConnection(dataSource!.ConnectionString);
			connection.Open();
			dataSource.Transaction = connection.BeginTransaction();
			typeof(PLangContext).GetProperty("DataSource")!.SetValue(run.Context, dataSource);

			run.MemoryStack.Put("round", 3L);
			var storeError = await run.Variables.Store(new List<string> { "%round%" });
			Assert.IsNull(storeError, storeError?.Message);

			if (commit) dataSource.Transaction.Commit(); else dataSource.Transaction.Rollback();
			dataSource.Transaction = null;
			connection.Close();

			Assert.AreEqual(commit ? 1L : 0L, VariableCount());
			Assert.AreEqual(0L, EventCount());
		}

		[TestMethod]
		public async Task Load_DataSourceThatDoesNotExist_GivesDataSourceNotFound()
		{
			var run = NewRun();
			var (_, error) = await run.Variables.LoadWithDefaultValue(new Dictionary<string, object?> { ["%round%"] = 0L }, "system");

			Assert.IsNotNull(error);
			Assert.AreEqual(404, error.StatusCode);
			StringAssert.Contains(error.Message, "'system' does not exists");
		}
	}
}
