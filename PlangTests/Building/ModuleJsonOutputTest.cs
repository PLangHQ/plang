using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors;
using PLang.Errors.Methods;
using PLang.Services.Channels;
using PLang.Utils;
using PLangTests;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace PLang.Building.Tests;

[TestClass]
public class ModuleJsonOutputTest : BasePLangTest
{
    [TestInitialize]
    public void Initialize()
    {
        base.Initialize();
    }

    [TestMethod]
    public void ModuleJsonOutputTest_Test()
    {
        var json = TypeHelper.GetJsonSchema(typeof(MethodExecution));

        int i = 0;
    }

    [TestMethod]
    public void ModuleJsonOutputTest_Test2()
    {
        var type = typeof(PLang.Modules.FileModule.Program);
        var json = TypeHelper.GetMethodAsJson(type, "DeleteFile");


        int i = 0;
    }


    [TestMethod]
    public void ModuleJsonOutputTest_Test4()
    {
        var type = typeof(PLang.Modules.DbModule.Program);
        string methodName = "Insert";
        var result = TypeHelper.GetMethodDescription(type, methodName);

        JsonSerializerOptions options = new JsonSerializerOptions()
        {
            WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
        var json = JsonSerializer.Serialize(result.Item1, options);


        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, // Enable type metadata for polymorphism
            Formatting = Formatting.Indented
        };

        string json2 = JsonConvert.SerializeObject(result.Item1, settings);

        int i = 0;
    }


    [TestMethod]
    public void ModuleJsonOutputTest_Test3()
    {
        string llmResponse = @"{
""ClassName"": ""PLang.Modules.DbModule.Program"",
  ""MethodName"": ""Insert"",
  ""Parameters"": [
    {
      ""Type"": ""System.String"",
      ""Name"": ""sql"",
      ""Value"": ""insert into users (name) values (%name%)""
    },
    {
      ""Type"": ""System.Collections.Generic.List`1[PLang.Modules.DbModule.Program+ParameterInfo]"",
      ""Name"": ""SqlParameters"",
      ""Value"": [
        {
          ""ParameterName"": ""name"",
          ""VariableNameOrValue"": ""%name%"",
          ""TypeFullName"": ""System.String""
        }
      ]
    },
    {
      ""Type"": ""System.String"",
      ""Name"": ""dataSourceName"",
      ""Value"": null
    }
  ],
  ""ReturnType"": {
    ""Type"": ""int"",
    ""VariableName"": ""%id%""
  }
}";

        var methodResponse = JsonConvert.DeserializeObject<MethodExecution>(llmResponse);


        string sql = methodResponse.Parameters[0].GetValue<string>();
        var sqlParams = methodResponse.Parameters[1].GetValue<List<PLang.Modules.DbModule.Program.ParameterInfo>>();

        var targetType = methodResponse.Parameters[1].GetType();
        var sqlParams2 = methodResponse.Parameters[1].GetValue(targetType);

        var isValid = IsValid(methodResponse, typeof(PLang.Modules.DbModule.Program));

        int i = 0;
    }


    
    
    
    
    [TestMethod]
    public void ModuleJsonOutputTest_Test5()
    {
        var type = typeof(PLang.Modules.LocalOrGlobalVariableModule.Program);
        var result = TypeHelper.GetMethodDescription(type, "SetVariables");

        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, // Enable type metadata for polymorphism
            Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore
        };

        string json2 = JsonConvert.SerializeObject(result.Item1, settings);
        int i = 0;
    }
    
    
    
    [TestMethod]
    public void ModuleJsonOutputTest_Test6()
    {
        var type = typeof(Program2);
        var result = TypeHelper.GetMethodDescription(type, "SetAddresses");

        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, // Enable type metadata for polymorphism
            Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore
        };

        string json2 = JsonConvert.SerializeObject(result.Item1, settings);
        int i = 0;
    } 
    
    [TestMethod]
    public void ModuleJsonOutputTest_Test6_1()
    {
        var type = typeof(Program2);
        var result = TypeHelper.GetMethodDescription(type, "SetAddresses_obj");

        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, // Enable type metadata for polymorphism
            Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore
        };

        string json2 = JsonConvert.SerializeObject(result.Item1, settings);
        int i = 0;
    }

    [TestMethod]
    public void ModuleJsonOutputTest_Test7()
    {
        string llmResponse = @"
{
  ""MethodName"": ""SetAddresses"",
  ""Parameters"": [
    {
      ""Type"": ""Dictionary<String, Address>"",
      ""Name"": ""addresses"",
      ""Value"": {
        ""kalli"": {
          ""Street"": ""%address1%"",
          ""Postcode"": ""%postcode1%""
        },
        ""siggi"": {
          ""Street"": ""%address2%"",
          ""Postcode"": ""%postcode2%""
        }
      }
    }
  ],
  ""ReturnType"": {
    ""Type"": ""String"",
    ""VariableName"": ""%addressDict%""
  }
}";
        
        var methodResponse = JsonConvert.DeserializeObject<MethodExecution>(llmResponse);


        var addresses = methodResponse.Parameters[0].GetValue<Dictionary<string, Address>>();
 
        var isValid = IsValid(methodResponse, typeof(Program2));

        int i =0;
        
        
    }
    
    [TestMethod]
         public void ModuleJsonOutputTest_Test7_obj()
         {
             string llmResponse = @"{
    ""MethodName"": ""SetAddresses_obj"",
    ""Parameters"": [
        {
            ""Type"": ""Dictionary<String, Object>"",
            ""Name"": ""addresses"",
            ""Value"": {
                ""kalli"": [
                    ""%address1%"",
                    ""%postcode1%""
                ],
                ""siggi"": [
                    ""%address2%"",
                    ""%postcode2%""
                ]
            }
        }
    ],
    ""ReturnType"": {
        ""Type"": ""System.String"",
        ""VariableName"": ""%addressDict%""
    }
}";
             
             var methodResponse = JsonConvert.DeserializeObject<MethodExecution>(llmResponse);
     
     
             var addresses = methodResponse.Parameters[0].GetValue<Dictionary<string, object>>();
      
             var isValid = IsValid(methodResponse, typeof(Program2));
     
             int i =0;
             
             
         }
         
         
         
            
    [TestMethod]
    public void ModuleJsonOutputTest_Test8()
    {
        var type = typeof(Program2);
        var result = TypeHelper.GetMethodDescription(type, "WriteOut");

        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None, // Enable type metadata for polymorphism
            Formatting = Formatting.Indented, NullValueHandling = NullValueHandling.Ignore
        };

        string json2 = JsonConvert.SerializeObject(result.Item1, settings);
        int i = 0;
    } 
}

public class Program2
{
    [Microsoft.VisualStudio.TestTools.UnitTesting.Description("Does some stuff")]
    public Task<string> DoStuff(Temp temp, TestObject obj, string? stringParamWithNull = null)
    {
        return Task.FromResult("");
    }
    [Microsoft.VisualStudio.TestTools.UnitTesting.Description("Sets an address list of strings")]
    public Task<string> SetAddresses_obj(Dictionary<string, object> addresses)
    {
        return Task.FromResult("");
    }
    
    [Microsoft.VisualStudio.TestTools.UnitTesting.Description("Sets an address list of strings")]
    public Task<string> SetAddresses(Dictionary<string, Address> addresses)
    {
        return Task.FromResult("");
    }
    
    [Microsoft.VisualStudio.TestTools.UnitTesting.Description("writes data to some output")]
    public Task<string> WriteOut(OutputObj output)
    {
        return Task.FromResult("");
    }
}

public class Address
{
    public string Street { get; set; }
    public string Postcode { get; set; }
}

public class Temp
{
    public List<Item> TempList { get; set; } = new();
    public Dictionary<string, Item> TempDict { get; set; } = new();
}

public class Item
{
    public string Name { get; set; }
    public string Address { get; set; }
}

public class OutputObj
{
    public string Data { get; set; }
    public MessageType? MessageTypeWithNull { get; set; }
}

public class TestObject
{
    public object DataWithoutNull { get; set; }
    public object? DataWithNull { get; set; }
    public MessageType? MessageTypeWithNull { get; set; }
    public MessageType MessageTypeWithoutNull { get; set; } = MessageType.SystemAudit;
    public int StatusCode { get; set; } = 200;
    public string? StringWithNull { get; set; }
    public string StringWithoutNull { get; set; } = "hello";
    public Dictionary<string, object>? Args = null;
    public List<string>? ListWithNull { get; set; }
    public List<string> ListWithoutNull { get; set; } = new List<string>();
}