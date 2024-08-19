using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Modules.ConditionalModule
{
	using System;
	using System.Linq.Dynamic.Core;
	using System.Text.RegularExpressions;
	using Newtonsoft.Json.Linq;

	public class Condition
	{
		private string _expression;
		private static readonly Regex PlaceholderRegex = new Regex(@"%(\w+(\.\w+)*)%", RegexOptions.Compiled);

		public Condition(string expression)
		{
			_expression = expression;
		}

		public bool Evaluate(JObject data)
		{
			var replacedExpression = ReplacePlaceholders(_expression, data);
			var lambda = System.Linq.Dynamic.Core.DynamicExpressionParser.ParseLambda<JObject, bool>(new ParsingConfig(), false, replacedExpression);
			return lambda.Compile().Invoke(data);
		}

		public bool Evaluate(JArray data)
		{
			var replacedExpression = ReplacePlaceholders(_expression, data);
			var lambda = System.Linq.Dynamic.Core.DynamicExpressionParser.ParseLambda<JArray, bool>(new ParsingConfig(), false, replacedExpression);
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

}
