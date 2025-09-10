using NBitcoin.Secp256k1;
using Newtonsoft.Json.Linq;
using PLang.Runtime;
using PLang.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLang.Modules.ConditionalModule.Program;

namespace PLang.Modules.ConditionalModule
{
	public static class ConditionEvaluator
	{
		public enum ConditionKind { Simple, Compound }
		[Description(@"
For CompundCondition, use Conditions list to construct the condition. LeftValue and RightValue are used only at in SimpleCondition
Operator: ==|!=|<|>|<=|>=|in|isEmpty|contains|startswith|endswith|indexOf

")]
		public record CompoundCondition : Condition;
		[Description("Operator: ==|!=|<|>|<=|>=|in|isEmpty|contains|startswith|endswith|indexOf")]
		public record SimpleCondition : Condition;
		public record Condition
		{
			public required ConditionKind Kind { get; init; }

			// simple-node fields
			public object? LeftValue { get; init; }
			public string? Operator { get; init; }
			public object? RightValue { get; init; }

			// compound-node fields
			public string? Logic { get; init; }                    // "AND" | "OR"
			public IReadOnlyList<Condition>? Conditions { get; init; }
			public bool IsNot { get; set; }
		}
		/// Static engine with separate evaluators
		public static class ConditionEngine
		{
			public static bool Evaluate(Condition c)
			{
				
				(var left, var right) = TypeHelper.TryConvertToMatchingType(c.LeftValue, c.RightValue);
				c = c with { LeftValue = left, RightValue = right };
				

				var result = c.Kind == ConditionKind.Simple
					? EvaluateSimple(c)
					: EvaluateCompound(c);
				if (c.IsNot) return !result;
				return result;
			}

			public static bool EvaluateSimple(Condition n) => n.Operator switch
			{
				

				"==" => Equals(n.LeftValue, n.RightValue),
				"!=" => !Equals(n.LeftValue, n.RightValue),
				">" => Cmp(n) > 0,
				"<" => Cmp(n) < 0,
				">=" => Cmp(n) >= 0,
				"<=" => Cmp(n) <= 0,
				"in" => n.RightValue is IEnumerable r && r.Cast<object>().Contains(n.LeftValue),
				"isEmpty" => IsEmpty(n.LeftValue, n.RightValue),
				"contains" => Has(n.LeftValue, n.RightValue),
				"startsWith" => Str(n, (s, x) => s.StartsWith(x, StringComparison.Ordinal)),
				"endsWith" => Str(n, (s, x) => s.EndsWith(x, StringComparison.Ordinal)),
				"indexOf" => Str(n, (s, x) => s.IndexOf(x, StringComparison.Ordinal) >= 0),
				_ => throw new NotSupportedException($"Op '{n.Operator}'")
			};

			private static bool IsEmpty(object? leftValue, object? rightValue)
			{
				object? value = leftValue;
				if (value == null) value = rightValue;

				if (value != null)
				{
					if (value is ObjectValue ovLeft) return ovLeft.IsEmpty;
					if (value == null) return true;
					if (value is string str) return string.IsNullOrWhiteSpace(str);
					if (value is IList list) return list.Count == 0;
					if (value is IDictionary dict) return dict.Count == 0;
				}

				return true;
			}

			public static bool EvaluateCompound(Condition n) => n.Logic!.ToUpperInvariant() switch
			{
				"&&" => n.Conditions!.All(Evaluate),
				"AND" => n.Conditions!.All(Evaluate),
				"OR" => n.Conditions!.Any(Evaluate),
				"||" => n.Conditions!.Any(Evaluate),
				_ => throw new NotSupportedException($"Logic '{n.Logic}'")
			};

			/* helpers */
			static int? Cmp(Condition n)
			{
				int? result = n.LeftValue != null && n.LeftValue is IComparable l &&
											 n.RightValue != null && n.RightValue is IComparable r ? l.CompareTo(r)
											 : null;
				return result;
			}
			static bool Has(object? c, object? i) =>
				c switch
				{
					string s when i is string sub => s.Contains(sub, StringComparison.Ordinal),
					IEnumerable coll => coll.Cast<object>().Contains(i),
					_ => false
				};
			static bool Str(Condition n, Func<string, string, bool> f) =>
				n.LeftValue is string s && n.RightValue is string x && f(s, x);
		}
	}
}
