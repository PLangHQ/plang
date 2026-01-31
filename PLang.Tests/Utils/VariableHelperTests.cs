using Newtonsoft.Json.Linq;
using PLang.Utils;

namespace PLang.Tests.Utils;

public class VariableHelperTests
{
    #region IsVariable tests

    [Test]
    [Arguments("%name%", true)]
    [Arguments("%Name%", true)]
    [Arguments("%user.name%", true)]
    [Arguments("%list[0]%", true)]
    [Arguments("%Settings.Key%", true)]
    [Arguments("notavariable", false)]
    [Arguments("%incomplete", false)]
    [Arguments("incomplete%", false)]
    [Arguments("", false)]
    [Arguments("plain text %variable% more text", false)] // contains but isn't entirely a variable
    public async Task IsVariable_VariousInputs_ReturnsExpected(string input, bool expected)
    {
        var result = VariableHelper.IsVariable(input);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task IsVariable_WithNull_ReturnsFalse()
    {
        var result = VariableHelper.IsVariable(null);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsVariable_WithNonString_ReturnsFalse()
    {
        var result = VariableHelper.IsVariable(123);
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region ContainsVariable tests

    [Test]
    [Arguments("%name%", true)]
    [Arguments("Hello %name%!", true)]
    [Arguments("Multiple %var1% and %var2%", true)]
    [Arguments("No variables here", false)]
    [Arguments("", false)]
    public async Task ContainsVariable_VariousInputs_ReturnsExpected(string input, bool expected)
    {
        var result = VariableHelper.ContainsVariable(input);
        await Assert.That(result).IsEqualTo(expected);
    }

    [Test]
    public async Task ContainsVariable_WithNull_ReturnsFalse()
    {
        var result = VariableHelper.ContainsVariable(null);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ContainsVariable_WithNonString_ReturnsFalse()
    {
        var result = VariableHelper.ContainsVariable(new { Name = "test" });
        await Assert.That(result).IsFalse();
    }

    #endregion

    #region GetVariablesInText tests

    [Test]
    public async Task GetVariablesInText_SingleVariable_ReturnsIt()
    {
        var result = VariableHelper.GetVariablesInText("Hello %name%!");
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0]).IsEqualTo("%name%");
    }

    [Test]
    public async Task GetVariablesInText_MultipleVariables_ReturnsAll()
    {
        var result = VariableHelper.GetVariablesInText("Hello %firstName% %lastName%!");
        await Assert.That(result).HasCount().EqualTo(2);
        await Assert.That(result).Contains("%firstName%");
        await Assert.That(result).Contains("%lastName%");
    }

    [Test]
    public async Task GetVariablesInText_NoVariables_ReturnsEmptyList()
    {
        var result = VariableHelper.GetVariablesInText("Hello world!");
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task GetVariablesInText_EmptyString_ReturnsEmptyList()
    {
        var result = VariableHelper.GetVariablesInText("");
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task GetVariablesInText_NullString_ReturnsEmptyList()
    {
        var result = VariableHelper.GetVariablesInText(null!);
        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task GetVariablesInText_NestedPropertyVariable_ReturnsIt()
    {
        var result = VariableHelper.GetVariablesInText("User: %user.name%");
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0]).IsEqualTo("%user.name%");
    }

    [Test]
    public async Task GetVariablesInText_ArrayIndexVariable_ReturnsIt()
    {
        var result = VariableHelper.GetVariablesInText("Item: %items[0]%");
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0]).IsEqualTo("%items[0]%");
    }

    [Test]
    public async Task GetVariablesInText_SettingsVariable_ReturnsIt()
    {
        var result = VariableHelper.GetVariablesInText("API Key: %Settings.ApiKey%");
        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0]).IsEqualTo("%Settings.ApiKey%");
    }

    #endregion

    #region IsSetting tests

    [Test]
    [Arguments("Settings.Key", true)]
    [Arguments("%Settings.Key%", true)]
    [Arguments("Settings.SomeLongKey", true)]
    [Arguments("%Settings.Get('key', 'default', 'explain')%", true)]
    [Arguments("NotSettings.Key", false)]
    [Arguments("%name%", false)]
    [Arguments("regular text", false)]
    public async Task IsSetting_VariousInputs_ReturnsExpected(string input, bool expected)
    {
        var result = VariableHelper.IsSetting(input);
        await Assert.That(result).IsEqualTo(expected);
    }

    #endregion

    #region Clean tests

    [Test]
    public async Task Clean_VariableWithPercents_RemovesPercents()
    {
        var result = VariableHelper.Clean("%name%");
        await Assert.That(result).IsEqualTo("name");
    }

    [Test]
    public async Task Clean_VariableWithAtSign_RemovesAtSign()
    {
        var result = VariableHelper.Clean("@name");
        await Assert.That(result).IsEqualTo("name");
    }

    [Test]
    public async Task Clean_VariableWithDollarDot_RemovesDollarDot()
    {
        var result = VariableHelper.Clean("$.name");
        await Assert.That(result).IsEqualTo("name");
    }

    [Test]
    public async Task Clean_VariableWithTrailingPlus_RemovesPlus()
    {
        var result = VariableHelper.Clean("count+");
        await Assert.That(result).IsEqualTo("count");
    }

    [Test]
    public async Task Clean_VariableWithTrailingMinus_RemovesMinus()
    {
        var result = VariableHelper.Clean("count-");
        await Assert.That(result).IsEqualTo("count");
    }

    [Test]
    public async Task Clean_EmptyString_ReturnsEmpty()
    {
        var result = VariableHelper.Clean("");
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task Clean_NullString_ReturnsEmpty()
    {
        var result = VariableHelper.Clean(null!);
        await Assert.That(result).IsEqualTo("");
    }

    [Test]
    public async Task Clean_AlphaNotation_ConvertsToArrayAndDot()
    {
        // α is used as escape for . in variable names
        var result = VariableHelper.Clean("userαname");
        await Assert.That(result).IsEqualTo("user.name");
    }

    [Test]
    public async Task Clean_AlphaWithNumbers_ConvertsToBrackets()
    {
        // α0α should become [0]
        var result = VariableHelper.Clean("itemsα0α");
        await Assert.That(result).IsEqualTo("items[0]");
    }

    [Test]
    public async Task Clean_WhitespaceAroundVariable_TrimsWhitespace()
    {
        var result = VariableHelper.Clean("  %name%  ");
        await Assert.That(result).IsEqualTo("name");
    }

    #endregion

    #region IsEmpty tests

    [Test]
    public async Task IsEmpty_WithNull_ReturnsTrue()
    {
        var result = VariableHelper.IsEmpty(null);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsEmpty_WithEmptyString_ReturnsTrue()
    {
        var result = VariableHelper.IsEmpty("");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsEmpty_WithWhitespace_ReturnsTrue()
    {
        var result = VariableHelper.IsEmpty("   ");
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsEmpty_WithNonEmptyString_ReturnsFalse()
    {
        var result = VariableHelper.IsEmpty("hello");
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsEmpty_WithEmptyList_ReturnsTrue()
    {
        var result = VariableHelper.IsEmpty(new List<string>());
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsEmpty_WithNonEmptyList_ReturnsFalse()
    {
        var result = VariableHelper.IsEmpty(new List<string> { "item" });
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsEmpty_WithEmptyDictionary_ReturnsTrue()
    {
        var result = VariableHelper.IsEmpty(new Dictionary<string, object>());
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsEmpty_WithNonEmptyDictionary_ReturnsFalse()
    {
        var dict = new Dictionary<string, object> { ["key"] = "value" };
        var result = VariableHelper.IsEmpty(dict);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsEmpty_WithJTokenNull_ReturnsTrue()
    {
        var token = JValue.CreateNull();
        var result = VariableHelper.IsEmpty(token);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsEmpty_WithEmptyJObject_ReturnsTrue()
    {
        var token = new JObject();
        var result = VariableHelper.IsEmpty(token);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsEmpty_WithNonEmptyJObject_ReturnsFalse()
    {
        var token = JObject.Parse("{\"key\": \"value\"}");
        var result = VariableHelper.IsEmpty(token);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task IsEmpty_WithEmptyJArray_ReturnsTrue()
    {
        var token = new JArray();
        var result = VariableHelper.IsEmpty(token);
        await Assert.That(result).IsTrue();
    }

    [Test]
    public async Task IsEmpty_WithNonEmptyJArray_ReturnsFalse()
    {
        var token = JArray.Parse("[1, 2, 3]");
        var result = VariableHelper.IsEmpty(token);
        await Assert.That(result).IsFalse();
    }

    #endregion
}
