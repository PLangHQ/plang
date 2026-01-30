using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Modules.DbModule;
using PLang.SafeFileSystem;
using PLang.Services.LlmService;
using PLang.Services.EventSourceService;
using PLang.Utils;
using static PLang.Modules.DbModule.ModuleSettings;
using static PLang.Modules.DbModule.Program;
using static PLang.Services.EncryptionService.Encryption;
using PLang.Services.EncryptionService;

namespace PLangTests.Modules.DbModule
{
	[TestClass]
	public class ProgramTest : BasePLangTest
	{
		List<DataSource> datasources = new List<DataSource>();
		Program p;
		[TestInitialize]
		public void Init()
		{
			base.Initialize();

			
			datasources.Add(new DataSource("local", "Microsoft.Data.Sqlite.SqliteConnection", "Data Source=:memory:", "", "", ""));
			datasources.Add(new DataSource("MainDb", "", "", "", "", ""));
			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(datasources);

			var fileSystem = new PLangFileSystem(Environment.CurrentDirectory, "./");
		

			
		}

		[TestMethod]
		public async Task TestSetDatasource()
		{
			db = new SqliteConnection("DataSource=:memory:");
			eventSourceFactory = new EventSourceFactory(container);
			p = new Program(dbFactory, appContext, fileSystem, settings, llmServiceFactory, eventSourceFactory, logger, typeHelper, null, prParser, programFactory);

			var dataSources = new List<DataSource>();
			dataSources.Add(new DataSource("db", "Microsoft.Data.Sqlite.SqliteConnection", "", "", "", ""));
			dataSources.Add(new DataSource("MainDb", "Microsoft.Data.Sqlite.SqliteConnection", "", "", "", ""));

			settings.GetValues<DataSource>(typeof(ModuleSettings)).Returns(dataSources);

			await p.SetDataSourceNames(["MainDb"]);

			var dataSource = context[ReservedKeywords.CurrentDataSource] as DataSource;
			Assert.AreEqual("MainDb", dataSource.Name);
		}

		[TestMethod]
		public async Task TestTransaction()
		{
			db = new SqliteConnection("DataSource=:memory:");
			eventSourceFactory = new EventSourceFactory(container);
			p = new Program(dbFactory, appContext, fileSystem, settings, llmServiceFactory, eventSourceFactory, logger, typeHelper, null, prParser, programFactory);
			await p.BeginTransaction();

			await p.CreateTable("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT, address TEXT, phone TEXT)");

			var dict = new List<ParameterInfo>();
			dict.Add(new ParameterInfo("System.String", "name", "Dwight Schrute"));
			dict.Add(new ParameterInfo("System.String", "address", "1725 Slough Avenue in Scranton, PA"));
			dict.Add(new ParameterInfo("System.String", "phone", "717 555 0177"));
			var id = await p.InsertAndSelectIdOfInsertedRow("data", "INSERT INTO users (name, address, phone) VALUES (@name, @address, @phone);SELECT last_insert_rowid();", dict);

			var result = (dynamic) await p.Select("data", "SELECT * FROM users");
			Assert.AreEqual(1, result.Count);

			dict = new List<ParameterInfo>();
			dict.Add(new ParameterInfo("System.Int64", "id", id));
			dict.Add(new ParameterInfo("System.String", "name", "Micheal Scott"));
			await p.Update("data", "UPDATE users SET name=@name WHERE id=@id", dict);

			dict = new List<ParameterInfo>();
			dict.Add(new ParameterInfo("System.Int64", "id", id));
			var user = (dynamic)await p.Select("data", "SELECT * FROM users WHERE id=@id", dict);
			Assert.AreEqual("Micheal Scott", user[0].name);

			dict = new List<ParameterInfo>();
			dict.Add(new ParameterInfo("System.String", "name", "Jim Harper"));
			dict.Add(new ParameterInfo("System.String", "address", "1725 Slough Avenue in Scranton, PA"));
			dict.Add(new ParameterInfo("System.String", "phone", "717 555 0178"));
			long rows = (await p.Insert("data", "INSERT INTO users (name, address, phone) VALUES (@name, @address, @phone);SELECT last_insert_rowid();", dict)).rowsAffected;
			Assert.AreEqual(1, rows);

			result = await p.Select("data", "SELECT * FROM users");
			Assert.AreEqual(2, result.Count);

			dict = new List<ParameterInfo>();
			dict.Add(new ParameterInfo("System.Int64", "id", id));
			await p.Delete("data", "DELETE FROM users where id=@id", dict);

			result = await p.Select("data", "SELECT * FROM users");
			Assert.AreEqual(1, result.Count);

			await p.EndTransaction();
		}

		[TestMethod]
		public async Task TestConnectionWithEventSourcing()
		{
			var encryptionKeys = new List<EncryptionKey>();
			encryptionKeys.Add(new EncryptionKey("T2meytdPPw5IlsRf0FGpIXiv4eAMmCV5Ec4WhUdpyEI="));
			settings.GetValues<EncryptionKey>(typeof(Encryption)).Returns(encryptionKeys);

			var encryption = new Encryption(settings);
			
			db = new SqliteConnection("DataSource=:memory:");
			eventSourceFactory = new EventSourceFactory(container);
			eventSourceRepository = new SqliteEventSourceRepository(fileSystem, encryption);

			p = new Program(dbFactory, appContext, fileSystem, settings, llmServiceFactory, eventSourceFactory, logger, typeHelper, null, prParser, programFactory);
			await p.BeginTransaction();

			await p.CreateTable("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT, address TEXT, phone TEXT)");

			var dict = new List<ParameterInfo>();
			dict.Add(new ParameterInfo("System.Int64", "id", "12"));
			dict.Add(new ParameterInfo("System.String", "name", "Dwight Schrute"));
			dict.Add(new ParameterInfo("System.String", "address", "1725 Slough Avenue in Scranton, PA"));
			dict.Add(new ParameterInfo("System.String", "phone", "717 555 0177"));
			var id = await p.InsertAndSelectIdOfInsertedRow("data", "INSERT INTO users (id, name, address, phone) VALUES (@id, @name, @address, @phone);", dict);



			var result = (dynamic)await p.Select("data", "SELECT * FROM users");
			Assert.AreEqual(1, result.Count);

			dict = new List<ParameterInfo>();
			dict.Add(new ParameterInfo("System.Int64", "id", id));
			dict.Add(new ParameterInfo("System.String", "name", "Micheal Scott"));
			await p.Update("data", "UPDATE users SET name=@name WHERE id=@id", dict);

			dict = new List<ParameterInfo>();
			dict.Add(new ParameterInfo("System.Int64", "id", id));
			var user = (dynamic)await p.Select("data", "SELECT * FROM users WHERE id=@id", dict);
			Assert.AreEqual("Micheal Scott", user[0].name);

			dict = new List<ParameterInfo>();
			dict.Add(new ParameterInfo("System.String", "name", "Jim Harper"));
			dict.Add(new ParameterInfo("System.String", "address", "1725 Slough Avenue in Scranton, PA"));
			dict.Add(new ParameterInfo("System.String", "phone", "717 555 0178"));
			long rows = (await p.Insert("data", "INSERT INTO users (name, address, phone) VALUES (@name, @address, @phone);SELECT last_insert_rowid();", dict)).rowsAffected;
			Assert.AreEqual(1, rows);

			result = await p.Select("data", "SELECT * FROM users");
			Assert.AreEqual(2, result.Count);

			dict = new List<ParameterInfo>();
			dict.Add(new ParameterInfo("System.Int64", "id", id));
			await p.Delete("data", "DELETE FROM users where id=@id", dict);

			result = await p.Select("data", "SELECT * FROM users");
			Assert.AreEqual(1, result.Count);

			var events = (dynamic)await p.Select("data", "SELECT * from __Events__");
			Assert.AreEqual(5, events.Count);

			await p.EndTransaction();
		}

	}
}
