using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using PLang.Modules.DbModule;

namespace PLangTests.Modules.DbModule
{
	[TestClass]
	public class SqlListParameterTests
	{
		[DataTestMethod]
		[DataRow("select * from t where id in @ids", "@ids")]
		[DataRow("select * from t where id IN (@ids)", "@ids")]
		[DataRow("select * from t where id in ( @ids )", "@ids")]
		[DataRow("select * from t where id not in @ids", "@ids")]
		[DataRow("select * from t where a = 1 and id in @ids and b = 2", "@ids")]
		public void UsedAsList_InClause_IsAList(string sql, string name)
		{
			Assert.IsTrue(SqlListParameter.UsedAsList(sql, name));
		}

		[DataTestMethod]
		[DataRow("select count(*) from people, json_each(@json)", "@json")]
		[DataRow("select json_extract(@json, '$[0]')", "@json")]
		[DataRow("select json_array_length(@json)", "@json")]
		[DataRow("select * from t where data = @json", "@json")]
		[DataRow("insert into t (data) values (@json)", "@json")]
		[DataRow("select * from t, json_each(@before) where id in @ids", "@before")]
		[DataRow("select * from t where id in @ids2", "@ids")]
		public void UsedAsList_OtherUse_IsOneValue(string sql, string name)
		{
			Assert.IsFalse(SqlListParameter.UsedAsList(sql, name));
		}

		[TestMethod]
		public void ToJsonText_JsonArray_IsItsJson()
		{
			Assert.AreEqual("[1,2,3]", SqlListParameter.ToJsonText(JArray.Parse("[ 1, 2, 3 ]")));
		}

		[TestMethod]
		public void ToJsonText_List_IsItsJson()
		{
			Assert.AreEqual("[\"a\",\"b\"]", SqlListParameter.ToJsonText(new List<string> { "a", "b" }));
		}

		[TestMethod]
		public void ToJsonText_Scalar_IsLeftAlone()
		{
			Assert.AreEqual("x", SqlListParameter.ToJsonText("x"));
			Assert.IsNull(SqlListParameter.ToJsonText(null));
		}
	}
}
