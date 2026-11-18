using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using PLang.Interfaces;
using PLang.Models;
using PLang.Modules.DbModule;
using PLang.Services.LlmService;
using static PLang.Modules.DbModule.Program;

namespace PLangTests.Modules.DbModule
{
	[TestClass]
	public class SqlVariablesTests
	{
		private static ParameterInfo P(string name, string variable, string type = "System.String") => new(type, name, variable);

		[TestMethod]
		public void Find_VariableInCase_IsValue()
		{
			var found = SqlVariables.Find("select case %tab% when 'a' then 1 else 2 end as d");
			Assert.AreEqual(1, found.Count);
			Assert.AreEqual("tab", found[0].Name);
			Assert.IsTrue(found[0].IsValue);
		}

		[TestMethod]
		public void Find_LikePatternsAndStringLiterals_AreNotVariables()
		{
			var found = SqlVariables.Find("select * from t where a like 'x%' or b like '%y%' or c = '%name%' or d like '%' || @p || '%'");
			Assert.AreEqual(0, found.Count);
		}

		[TestMethod]
		public void Find_CommentsAndQuotedIdentifiers_AreSkipped()
		{
			var found = SqlVariables.Find("select \"%a%\", [%b%] -- %c%\n, 1 /* %d% */ where x = %e%");
			Assert.AreEqual(1, found.Count);
			Assert.AreEqual("e", found[0].Name);
		}

		[TestMethod]
		public void Find_ModuloWithSpaces_IsNotVariable()
		{
			Assert.AreEqual(0, SqlVariables.Find("select a % b % c from t").Count);
		}

		[TestMethod]
		public void Find_EscapedQuoteInLiteral_DoesNotEndLiteral()
		{
			var found = SqlVariables.Find("select 'it''s %x%' as a, %y% as b");
			Assert.AreEqual(1, found.Count);
			Assert.AreEqual("y", found[0].Name);
		}

		[TestMethod]
		public void Find_TableAndPartOfName_AreNotValues()
		{
			var found = SqlVariables.Find("select * from %table% join users_%year% u on 1 = 1 where a = %v%");
			Assert.AreEqual(3, found.Count);
			Assert.IsFalse(found[0].IsValue);
			Assert.IsFalse(found[1].IsValue);
			Assert.IsTrue(found[2].IsValue);
		}

		[TestMethod]
		public void Fix_DeclaredParameterAndRawVariable_ReplacesEveryOccurrence()
		{
			var result = SqlVariables.Fix(
				"select 1 where server = %name% and x in (select y from z where server = %name%) and case %tab% when 'a' then 1 end = 1",
				[P("@name", "%name%"), P("@tab", "%tab%")],
				"select ... %name% ... %tab%, write to %rows%");

			Assert.IsNull(result.Error);
			Assert.AreEqual("select 1 where server = @name and x in (select y from z where server = @name) and case @tab when 'a' then 1 end = 1", result.Sql);
			Assert.AreEqual(2, result.Parameters.Count);
		}

		[TestMethod]
		public void Fix_VariableOfTheStepWithoutParameter_AddsTheParameter()
		{
			var result = SqlVariables.Fix("select * from t where a = %user.id% and b = %Name%", [], "select %user.id% %Name%");

			Assert.IsNull(result.Error);
			Assert.AreEqual("select * from t where a = @userid and b = @Name", result.Sql);
			CollectionAssert.AreEqual(new[] { "@userid", "@Name" }, result.Added);
			Assert.AreEqual("%user.id%", result.Parameters[0].VariableNameOrValue);
		}

		[TestMethod]
		public void Fix_LikeVariableInLiteralIsLeftAlone()
		{
			var result = SqlVariables.Fix("select * from t where a like '%' || @q || '%' and b = '%x%'", [P("@q", "%q%")], "like %q%");
			Assert.IsNull(result.Error);
			Assert.AreEqual("select * from t where a like '%' || @q || '%' and b = '%x%'", result.Sql);
		}

		[TestMethod]
		public void Fix_VariableNotInStep_IsAnError()
		{
			var result = SqlVariables.Fix("select * from t where a = %ghost%", [], "select from t");
			Assert.IsNotNull(result.Error);
			StringAssert.Contains(result.Error, "%ghost%");
		}

		[TestMethod]
		public void Fix_ParameterTheSqlDoesNotUse_IsAnError()
		{
			var result = SqlVariables.Fix("select * from t where a = @name", [P("@name", "%name%"), P("@tab", "%tab%")], "%name% %tab%");
			Assert.IsNotNull(result.Error);
			StringAssert.Contains(result.Error, "@tab");
			Assert.IsFalse(result.Error.Contains("@name is listed"));
		}

		[TestMethod]
		public void Fix_AutoIdParameter_IsNotReportedAsUnused()
		{
			var result = SqlVariables.Fix("insert into t (a) values (@a)", [P("@a", "%a%"), new ParameterInfo("System.Int64", "@id", "auto")], "insert %a%");
			Assert.IsNull(result.Error);
		}

		[TestMethod]
		public void Fix_TableNameVariable_IsLeftInTheSql()
		{
			var result = SqlVariables.Fix("select * from %table% where a = %v%", [], "from %table% where a = %v%", ["%table%"]);
			Assert.IsNull(result.Error);
			Assert.AreEqual("select * from %table% where a = @v", result.Sql);
		}

		[TestMethod]
		public void Apply_RewritesSqlAndParametersOfTheFunction()
		{
			var gf = new Builder.DbGenericFunction("", "Select", new List<Builder.Parameter>
			{
				new("System.String", "dataSourceName", "data"),
				new("System.String", "sql", "select case %tab% when 'a' then 1 end from t where s = %name%"),
				new("System.Collections.Generic.List`1[PLang.Modules.DbModule.Program+ParameterInfo]", "sqlParameters", new List<ParameterInfo> { P("@name", "%name%") }),
			});

			var (fixedGf, error) = SqlVariables.Apply(gf, "select %tab% %name%");

			Assert.IsNull(error);
			Assert.AreEqual("select case @tab when 'a' then 1 end from t where s = @name", fixedGf.GetParameter<string>("sql"));
			var parameters = fixedGf.GetParameter<List<ParameterInfo>>("sqlParameters");
			Assert.AreEqual(2, parameters!.Count);
		}

		[TestMethod]
		public void Apply_SqlThatIsAVariable_IsLeftAlone()
		{
			var gf = new Builder.DbGenericFunction("", "Select", new List<Builder.Parameter> { new("System.String", "sql", "%sql%") });
			var (fixedGf, error) = SqlVariables.Apply(gf, "run %sql%");
			Assert.IsNull(error);
			Assert.AreSame(gf, fixedGf);
		}

		[TestMethod]
		public void LlmCaching_SetCachedQuestion_ReplacesTheAnswerOfTheSameRequest()
		{
			var dir = Path.Combine(Path.GetTempPath(), "llmcache" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(dir);
			try
			{
				var fileSystem = Substitute.For<IPLangFileSystem>();
				fileSystem.SharedPath.Returns(dir);
				var settings = Substitute.For<ISettings>();
				settings.AppId.Returns("app");
				var caching = new LlmCaching(fileSystem, settings);

				LlmRequest Make(string answer) => new("t", new List<LlmMessage> { new("user", "same question") }) { RawResponse = answer };

				caching.SetCachedQuestion("app", Make("old"));
				Assert.AreEqual("old", caching.GetCachedQuestion("app", Make("x"))!.RawResponse);

				caching.SetCachedQuestion("app", Make("new"));
				Assert.AreEqual("new", caching.GetCachedQuestion("app", Make("x"))!.RawResponse);
			}
			finally
			{
				Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
				try { Directory.Delete(dir, true); } catch { }
			}
		}
	}
}
