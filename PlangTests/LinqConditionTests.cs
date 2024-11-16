using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PLangTests;

[TestClass]
public class LinqConditionTests
{
    [TestMethod]
    public void TestCondition()
    {
        /*
        string jsonList = "[{\"zip\":250}, {\"zip\":310}, {\"zip\":290}]";
        var list = JsonConvert.DeserializeObject<List<dynamic>>(jsonList);


        string condition = "j[\"zip\"].Value<int>() > 300";
        var jsonArray = JArray.Parse(JsonConvert.SerializeObject(list));

        // Project the JArray into a collection of dynamic objects with strongly-typed properties
        var projected = jsonArray.Select(j => new
        {
            zip = j["zip"].Value<int>() // Assuming zip is an integer
                                        // You can project other properties here as needed
        });
        var result = projected.AsQueryable().Where(condition).ToList();

        Assert.IsNotNull(result);
        */
    }
}