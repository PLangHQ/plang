using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Errors;
using PLang.Interfaces;
using PLang.Models;
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

namespace PLangTests.Modules.DbModule
{
	[TestClass]
	public class AttachDbTests
	{
		private string dir = "";
		private string userDb = "";
		private string dataDb = "";

		private class RealSqliteFactory : IDbServiceFactory
		{
			public IDbConnection CreateHandler(DataSource dataSource, MemoryStack memoryStack, bool ReadOnly = false)
			{
				return new SqliteConnection(dataSource.ConnectionString);
			}
		}

		[TestInitialize]
		public void Init()
		{
			SqliteConnection.ClearAllPools();
			dir = Path.Combine(Path.GetTempPath(), "plang-attachdb-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			userDb = Path.Combine(dir, "user.sqlite");
			dataDb = Path.Combine(dir, "data.sqlite");

			Run(userDb, "create table u(id integer primary key, v text); insert into u(id, v) values (1, 'a'), (2, 'b');");
			Run(dataDb, "create table d(id integer primary key, name text); insert into d(id, name) values (1, 'one'), (2, 'two');");
		}

		[TestCleanup]
		public void Cleanup()
		{
			SqliteConnection.ClearAllPools();
			try { Directory.Delete(dir, true); } catch { }
		}

		private static void Run(string file, string sql)
		{
			using var con = new SqliteConnection($"Data Source={file};Pooling=False");
			con.Open();
			using var cmd = con.CreateCommand();
			cmd.CommandText = sql;
			cmd.ExecuteNonQuery();
		}

		private DataSource NewUserDataSource()
		{
			return new DataSource("users/1", typeof(SqliteConnection).FullName!, $"Data Source={userDb}", "data", "", "", LocalPath: userDb);
		}

		private DbProgram NewProgram(DataSource shared)
		{
			var realFs = new System.IO.Abstractions.FileSystem();
			var fileSystem = Substitute.For<IPLangFileSystem>();
			fileSystem.Path.Returns(realFs.Path);
			fileSystem.File.Returns(realFs.File);
			fileSystem.Directory.Returns(realFs.Directory);
			var settings = Substitute.For<ISettings>();
			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(new List<DataSource>
			{
				new DataSource("users/%variable0%", typeof(SqliteConnection).FullName!, $"Data Source={userDb}", "data", "", "", LocalPath: userDb),
				new DataSource("data", typeof(SqliteConnection).FullName!, $"Data Source={dataDb}", "data", "", "", LocalPath: dataDb)
			});
			var appCache = Substitute.For<IAppCache>();
			appCache.Get("__plang_DataSource__users/1").Returns(Task.FromResult<object?>(shared));
			var logger = Substitute.For<ILogger>();
			var typeHelper = Substitute.For<ITypeHelper>();
			var appContext = new PLangAppContext();
			var prParser = new PrParser(fileSystem, logger);
			var engine = Substitute.For<IEngine>();
			var contextAccessor = Substitute.For<IPLangContextAccessor>();
			var memoryStack = new MemoryStack(Substitute.For<IPseudoRuntime>(), engine, settings, new VariableHelper(settings, logger), contextAccessor);
			var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);
			contextAccessor.Current.Returns(context);
			var dbFactory = new RealSqliteFactory();

			var moduleSettings = new ModuleSettings(fileSystem, settings, appContext, Substitute.For<ILlmServiceFactory>(), logger, typeHelper, prParser,
				contextAccessor, new VariableHelper(settings, logger), dbFactory, appCache);
			memoryStack.Put("uid", "1");

			var program = new DbProgram(dbFactory, appContext, fileSystem, settings, Substitute.For<ILlmServiceFactory>(), Substitute.For<IEventSourceFactory>(),
				logger, typeHelper, moduleSettings, prParser, null!);

			var step = new GoalStep { Text = "select cross datasource", RelativePrPath = "AttachDbTests.pr", Goal = new Goal { AbsoluteAppStartupFolderPath = "" } };
			SetField(program, "context", context);
			SetField(program, "memoryStack", memoryStack);
			SetField(program, "goalStep", step);
			SetField(program, "goal", step.Goal);
			return program;
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

		private const string CrossSql = "select u.id, d.name from main.u u join data.d d on d.id = u.id order by u.id";

		// `select ..., ds: users/%uid%, data` : the public step method, which resolves the two datasources (the user one comes out of the 5 minute app cache, so one instance is shared by every request of that user), attaches the second to the first and runs Select.
		private async Task<(int Rows, string? Error)> CrossSelect(DataSource shared, string sql = CrossSql, List<DbProgram.ParameterInfo>? parameters = null)
		{
			var program = NewProgram(shared);
			var result = await program.SelectWithMultipleDataSources(["users/%uid%", "data"], sql, parameters);
			if (result.Error != null)
			{
				var text = result.Error.Message + " | " + string.Join(" | ", result.Error.ErrorChain.Select(p => p.Message));
				return (0, text);
			}
			return (result.Table!.Count, null);
		}

		[TestMethod]
		public async Task Sequential_CrossDatasourceSelect_Works_Repeatedly()
		{
			var shared = NewUserDataSource();
			for (int i = 0; i < 10; i++)
			{
				var (rows, error) = await CrossSelect(shared);
				Assert.IsNull(error, $"call {i}: {error}");
				Assert.AreEqual(2, rows);
			}
		}

		[TestMethod]
		public async Task PoisonedPooledConnection_NextCrossSelect_Works()
		{
			var shared = NewUserDataSource();

			using (var poisoned = new SqliteConnection(shared.ConnectionString))
			{
				poisoned.Open();
				using var cmd = poisoned.CreateCommand();
				cmd.CommandText = $"ATTACH DATABASE '{dataDb}' AS data;";
				cmd.ExecuteNonQuery();
			}

			for (int i = 0; i < 5; i++)
			{
				var (rows, error) = await CrossSelect(shared);
				Assert.IsNull(error, $"call {i}: {error}");
				Assert.AreEqual(2, rows);
			}
		}

		[TestMethod]
		public async Task FailedSelect_DoesNotPoisonPool()
		{
			var shared = NewUserDataSource();

			for (int round = 0; round < 3; round++)
			{
				var bad = await CrossSelect(shared, "select * from data.does_not_exist");
				Assert.IsNotNull(bad.Error);

				var (rows, error) = await CrossSelect(shared);
				Assert.IsNull(error, $"round {round}: {error}");
				Assert.AreEqual(2, rows);
			}
		}

		[TestMethod]
		public async Task ParameterErrorAfterAttach_DoesNotPoisonPool()
		{
			var shared = NewUserDataSource();
			var emptyList = new List<DbProgram.ParameterInfo> { new("System.Array", "@ids", "") };

			for (int round = 0; round < 3; round++)
			{
				var bad = await CrossSelect(shared, CrossSql, emptyList);
				Assert.IsNotNull(bad.Error);

				var (rows, error) = await CrossSelect(shared);
				Assert.IsNull(error, $"round {round}: {error}");
				Assert.AreEqual(2, rows);
			}
		}

		[TestMethod]
		public async Task ParameterErrorAfterAttach_ThenCacheExpiredDataSource_DoesNotPoisonPool()
		{
			var emptyList = new List<DbProgram.ParameterInfo> { new("System.Array", "@ids", "") };

			var bad = await CrossSelect(NewUserDataSource(), CrossSql, emptyList);
			Assert.IsNotNull(bad.Error);

			var (rows, error) = await CrossSelect(NewUserDataSource());
			Assert.IsNull(error, error);
			Assert.AreEqual(2, rows);
		}

		[TestMethod]
		public async Task Transaction_WithAttachedDatasource_CrossSelectInside_ThenOutside()
		{
			var shared = NewUserDataSource();
			var program = NewProgram(shared);

			var begin = await program.BeginTransaction(["users/%uid%", "data"]);
			Assert.IsNull(begin, begin?.Message);

			var inside = await program.SelectWithMultipleDataSources(["users/%uid%", "data"], CrossSql);
			Assert.IsNull(inside.Error, inside.Error?.Message);
			Assert.AreEqual(2, inside.Table!.Count);

			var end = await program.EndTransaction();
			Assert.IsNull(end, end?.Message);

			for (int i = 0; i < 3; i++)
			{
				var (rows, error) = await CrossSelect(shared);
				Assert.IsNull(error, $"after transaction, call {i}: {error}");
				Assert.AreEqual(2, rows);
			}
		}

		[TestMethod]
		public async Task Concurrent_CrossDatasourceSelect_SameUserDataSource_NoErrors()
		{
			var shared = NewUserDataSource();
			var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
			int ok = 0;

			for (int wave = 0; wave < 5; wave++)
			{
				var tasks = Enumerable.Range(0, 40).Select(_ => Task.Run(async () =>
				{
					var (rows, error) = await CrossSelect(shared);
					if (error != null) errors.Add(error);
					else if (rows == 2) Interlocked.Increment(ref ok);
					else errors.Add("wrong row count " + rows);
				})).ToArray();
				await Task.WhenAll(tasks);
			}

			var summary = string.Join("\n", errors.GroupBy(e => e).Select(g => $"{g.Count()} x {g.Key}"));
			Console.WriteLine($"ok={ok} errors={errors.Count}\n{summary}");
			Assert.AreEqual(0, errors.Count, summary);
			Assert.AreEqual(200, ok);
		}
	}
}
