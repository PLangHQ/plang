using System.Linq.Dynamic.Core;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace PLang.Modules.ConditionalModule;

public class Condition
{
    private static readonly Regex PlaceholderRegex = new(@"%(\w+(\.\w+)*)%", RegexOptions.Compiled);
    private readonly string _expression;

    public Condition(string expression)
    {
        _expression = expression;
    }

    public bool Evaluate(JObject data)
    {
        var replacedExpression = ReplacePlaceholders(_expression, data);
        var lambda = DynamicExpressionParser.ParseLambda<JObject, bool>(new ParsingConfig(), false, replacedExpression);
        return lambda.Compile().Invoke(data);
    }

    public bool Evaluate(JArray data)
    {
        var replacedExpression = ReplacePlaceholders(_expression, data);
        var lambda = DynamicExpressionParser.ParseLambda<JArray, bool>(new ParsingConfig(), false, replacedExpression);
        return lambda.Compile().Invoke(data);
    }

    private string ReplacePlaceholders(string expression, JToken data)
    {
        return PlaceholderRegex.Replace(expression, match =>
        {
            var placeholder = match.Groups[1].Value;
            var value = GetJsonValue(data, placeholder);
            return value?.ToString();
        });
    }

    private JToken GetJsonValue(JToken token, string path)
    {
        var properties = path.Split('.');
        foreach (var property in properties)
        {
            if (token == null) return null;
            token = token[property];
        }

        return token;
    }
}