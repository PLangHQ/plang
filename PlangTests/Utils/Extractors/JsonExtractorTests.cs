using Microsoft.VisualStudio.TestTools.UnitTesting;
using PLang.Services.CompilerService;

namespace PLang.Utils.Extractors.Tests;

[TestClass]
public class JsonExtractorTests
{
    [TestMethod]
    public void ExtractTest()
    {
        var json =
            "{\r\n\"Name\": \"StepCommentStartsWithHttp\",\r\n\"Implementation\": @\"\r\npublic static class StepCommentStartsWithHttp\r\n{\r\n    public static bool Process(string stepαComment)\r\n    {\r\n        return stepαComment.StartsWith(\\\"http\\\");\r\n    }\r\n}\",\r\n\"Using\": [\"System\"],\r\n\"Assemblies\": []\r\n}";

        var JsonExtractor = new JsonExtractor();
        JsonExtractor.Extract(json, typeof(CodeImplementationResponse));
    }

    [TestMethod]
    public void ExtractTest2()
    {
        var json =
            "{\"StepName\": \"ParseContent\",\r\n\"StepDescription\": \"Parse the content variable into a list of objects with properties: startTime, endTime, and speakerNumber. The regex pattern to be used is: (\\d{2}:\\d{2}:\\d{2},\\d{3}) --> (\\d{2}:\\d{2}:\\d{2},\\d{3}).*?Speaker\\s+#(\\d). The parsed data is then written to the list variable.\",\r\n\"Modules\": [\"PLang.Modules.CodeModule\"],\r\n\"WaitForExecution\": true}";

        var JsonExtractor = new JsonExtractor();
        JsonExtractor.Extract(json, typeof(CodeImplementationResponse));
    }
}