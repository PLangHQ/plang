using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Building.Model;
using PLang.Building.Parsers;
using PLang.Interfaces;
using PLang.Modules.DbModule;
using PLang.Runtime;
using PLang.Services.CachingService;
using PLang.Services.DbService;
using PLang.Services.LlmService;
using PLang.Utils;
using System.Data;
using static PLang.Modules.DbModule.ModuleSettings;

namespace PLangTests.Modules.DbModule
{
	[TestClass]
	public class DataSourceNameTests
	{
		private class SqliteFactory : IDbServiceFactory
		{
			public IDbConnection CreateHandler(DataSource dataSource, MemoryStack memoryStack, bool ReadOnly = false)
				=> new SqliteConnection(dataSource.ConnectionString);
		}

		private static DataSource Sqlite(string name) =>
			new DataSource(name, typeof(SqliteConnection).FullName!, "Data Source=./.db/" + name + "/data.sqlite;", "data", "", "", false, false, "/.db/" + name + "/data.sqlite");

		private static (ModuleSettings ModuleSettings, ISettings Settings, IPLangFileSystem FileSystem) NewModuleSettings(List<DataSource> registered, List<Goal>? goals = null, string[]? dbFolders = null)
		{
			var fileSystem = Substitute.For<IPLangFileSystem>();
			var realFs = new System.IO.Abstractions.FileSystem();
			fileSystem.Path.Returns(realFs.Path);
			fileSystem.RootDirectory.Returns("/app");

			var directory = Substitute.For<System.IO.Abstractions.IDirectory>();
			directory.Exists(Arg.Any<string>()).Returns(false);
			directory.Exists(Arg.Is<string>(s => s.EndsWith(".db"))).Returns(true);
			directory.GetDirectories(Arg.Any<string>()).Returns(dbFolders ?? Array.Empty<string>());
			fileSystem.Directory.Returns(directory);

			var file = Substitute.For<System.IO.Abstractions.IFile>();
			file.Exists(Arg.Any<string>()).Returns(true);
			fileSystem.File.Returns(file);

			var settings = Substitute.For<ISettings>();
			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(_ => registered.ToList());

			var logger = Substitute.For<ILogger>();
			var prParser = new PrParser(fileSystem, logger);

			var contextAccessor = Substitute.For<IPLangContextAccessor>();
			var engine = Substitute.For<IEngine>();
			var variableHelper = new VariableHelper(settings, logger);
			var memoryStack = new MemoryStack(Substitute.For<IPseudoRuntime>(), engine, settings, variableHelper, contextAccessor);
			var context = new PLangContext(memoryStack, engine, ExecutionMode.Console);
			contextAccessor.Current.Returns(context);

			var moduleSettings = new ModuleSettings(fileSystem, settings, new PLangAppContext(), Substitute.For<ILlmServiceFactory>(), logger,
				Substitute.For<ITypeHelper>(), prParser, contextAccessor, variableHelper, new SqliteFactory(), Substitute.For<IAppCache>());
			return (moduleSettings, settings, fileSystem);
		}

		[DataTestMethod]
		[DataRow("VitalSource", "VitalSource", true)]
		[DataRow("VitalSource", "Vitalsource", true)]
		[DataRow("VitalSource", "vitalsource", true)]
		[DataRow("VitalSource", "VITALSOURCE", true)]
		[DataRow("VitalSource", "Vital", false)]
		[DataRow("VitalSource", null, false)]
		[DataRow(null, null, true)]
		public void IsSameDataSourceName_ignores_case(string? a, string? b, bool expected)
		{
			Assert.AreEqual(expected, ModuleSettings.IsSameDataSourceName(a, b));
		}

		[DataTestMethod]
		[DataRow("VitalSource")]
		[DataRow("Vitalsource")]
		[DataRow("vitalsource")]
		public void FindSetupGoal_matches_every_spelling(string nameInStep)
		{
			var setup = new Goal { GoalName = "Setup", IsSetup = true, DataSourceName = "VitalSource" };
			var other = new Goal { GoalName = "Setup", IsSetup = true, DataSourceName = "data" };
			var notSetup = new Goal { GoalName = "Overview", IsSetup = false, DataSourceName = "VitalSource" };

			Assert.AreSame(setup, ModuleSettings.FindSetupGoal(new[] { other, notSetup, setup }, nameInStep));
			Assert.AreSame(notSetup, ModuleSettings.FindSetupGoal(new[] { other, notSetup }, nameInStep, mustBeSetup: false));
			Assert.IsNull(ModuleSettings.FindSetupGoal(new[] { other, notSetup }, nameInStep));
			Assert.IsNull(ModuleSettings.FindSetupGoal(new[] { setup }, null));
		}

		[DataTestMethod]
		[DataRow("VitalSource")]
		[DataRow("Vitalsource")]
		[DataRow("vitalsource")]
		public async Task GetDataSource_finds_the_registered_datasource_in_every_spelling(string nameInStep)
		{
			var (moduleSettings, _, _) = NewModuleSettings(new List<DataSource> { Sqlite("data"), Sqlite("VitalSource") });

			var (dataSource, error) = await moduleSettings.GetDataSource(nameInStep);

			Assert.IsNull(error);
			Assert.AreEqual("VitalSource", dataSource!.Name, "the registered (first created) spelling is the name");
			Assert.AreEqual("/.db/VitalSource/data.sqlite", dataSource.LocalPath);
			Assert.AreEqual(nameInStep, dataSource.NameInStep);
		}

		[TestMethod]
		public async Task GetDataSource_unknown_datasource_is_not_found()
		{
			var (moduleSettings, _, _) = NewModuleSettings(new List<DataSource> { Sqlite("data") });

			var (dataSource, error) = await moduleSettings.GetDataSource("Vitalsource");

			Assert.IsNull(dataSource);
			Assert.AreEqual("DataSourceNotFound", error!.Key);
		}

		[DataTestMethod]
		[DataRow("Vitalsource")]
		[DataRow("vitalsource")]
		[DataRow("VitalSource")]
		public async Task CreateDataSource_reuses_the_folder_that_is_on_disk(string spelling)
		{
			var (moduleSettings, settings, _) = NewModuleSettings(new List<DataSource>(), dbFolders: new[] { "/.db/data", "/.db/VitalSource" });

			var (dataSource, error) = await moduleSettings.CreateDataSource(spelling);

			Assert.IsNull(error);
			Assert.AreEqual("VitalSource", dataSource!.Name);
			Assert.AreEqual("/.db/VitalSource/data.sqlite", dataSource.LocalPath);
		}

		[TestMethod]
		public async Task CreateDataSource_with_a_name_not_on_disk_uses_the_name_as_written()
		{
			var (moduleSettings, _, _) = NewModuleSettings(new List<DataSource>(), dbFolders: new[] { "/.db/data" });

			var (dataSource, error) = await moduleSettings.CreateDataSource("VitalSource");

			Assert.IsNull(error);
			Assert.AreEqual("VitalSource", dataSource!.Name);
			Assert.AreEqual("/.db/VitalSource/data.sqlite", dataSource.LocalPath);
		}

		[DataTestMethod]
		[DataRow("Vitalsource")]
		[DataRow("vitalsource")]
		public async Task CreateDataSource_of_a_registered_datasource_in_another_spelling_adds_no_second_one(string spelling)
		{
			var registered = new List<DataSource> { Sqlite("VitalSource") };
			var (moduleSettings, settings, _) = NewModuleSettings(registered);

			var (dataSource, error) = await moduleSettings.CreateDataSource(spelling);

			Assert.AreEqual("VitalSource", dataSource!.Name);
			Assert.AreEqual("DataSourceExists", error!.Key);
			Assert.AreEqual("/.db/VitalSource/data.sqlite", dataSource.LocalPath);
		}

		[TestMethod]
		public async Task RemoveDataSource_ignores_case()
		{
			var registered = new List<DataSource> { Sqlite("data"), Sqlite("VitalSource") };
			var (moduleSettings, settings, _) = NewModuleSettings(registered);

			await moduleSettings.RemoveDataSource("vitalsource");

			settings.Received(1).SetList(typeof(ModuleSettings), Arg.Is<List<DataSource>>(l => l.Count == 1 && l[0].Name == "data"));
		}
	}
}
