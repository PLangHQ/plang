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
		// Both records inherit every field of Condition and add none, so the only thing telling
		// them apart is what is written here. Left to say it in prose, a step joining two tests
		// came back as one flattened simple condition carrying a stray Logic: AND, because the
		// two definitions read as the same shape. Each now states its own shape and says which
		// fields it never has.
		[Description(@"Two or more tests joined together. Shape:
{""Kind"":""Compound"",""Logic"":""AND"",""IsNot"":false,""Conditions"":[{""Kind"":""Simple"",...},{""Kind"":""Simple"",...}]}
Logic is AND or OR and is required. Conditions holds the tests being joined.
Each test inside is read from its own words, e.g. `%city% is empty and %name% is not empty` joins one with Operator isEmpty and one with Operator isNotEmpty. Joining tests does not make them positive.
A Compound NEVER has LeftValue, Operator or RightValue of its own: those belong to the Simple conditions inside it.")]
		public record CompoundCondition : Condition;

		[Description(@"One test. Shape:
{""Kind"":""Simple"",""LeftValue"":""%age%"",""Operator"":"">="",""RightValue"":18,""IsNot"":false}
Operator: ==|!=|<|>|<=|>=|in|notIn|isEmpty|isNotEmpty|contains|notContains|startswith|notStartswith|endswith|notEndswith|indexOf.
A test written in the negative takes the negative operator, and IsNot stays false:
  `%city% is empty`               => Operator isEmpty
  `%city% is not empty`           => Operator isNotEmpty
  `%city% contains ""x""`           => Operator contains
  `%city% does not contain ""x""`   => Operator notContains
  `%city% does not start with ""x""`=> Operator notStartswith
IsNot exists for a negation the operators cannot express and is false otherwise. Never write a positive operator for a test the step writes in the negative: that is the opposite step, and it runs without anything noticing.
A Simple NEVER has Logic or Conditions: a step joining two tests is a Compound.")]
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

			public static bool EvaluateSimple(Condition n) => n.Operator.ToLowerInvariant() switch
			{
				

				"==" => Equals(n.LeftValue, n.RightValue),
				"!=" => !Equals(n.LeftValue, n.RightValue),
				">" => Cmp(n) > 0,
				"<" => Cmp(n) < 0,
				">=" => Cmp(n) >= 0,
				"<=" => Cmp(n) <= 0,
				"in" => n.RightValue is IEnumerable r && r.Cast<object>().Contains(n.LeftValue),
				"isempty" => IsEmpty(n.LeftValue, n.RightValue),
				"contains" => Has(n.LeftValue, n.RightValue),
				"startswith" => Str(n, (s, x) => s.StartsWith(x, StringComparison.OrdinalIgnoreCase)),
				"endswith" => Str(n, (s, x) => s.EndsWith(x, StringComparison.OrdinalIgnoreCase)),
				"indexof" => Str(n, (s, x) => s.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0),

				// A test written in the negative can say so in the operator instead of setting IsNot
				// on the side. IsNot is a separate flag the builder has to remember to flip, and
				// when it forgets, the step means the opposite of what it says and nothing notices:
				// measured on one goal, `is not empty` inside a compound came back with IsNot false
				// once in three builds. One token cannot be half right. IsNot still works and every
				// .pr already written keeps running.
				"isnotempty" => !IsEmpty(n.LeftValue, n.RightValue),
				"notin" => !(n.RightValue is IEnumerable nr && nr.Cast<object>().Contains(n.LeftValue)),
				"notcontains" => !Has(n.LeftValue, n.RightValue),
				"notstartswith" => !Str(n, (s, x) => s.StartsWith(x, StringComparison.OrdinalIgnoreCase)),
				"notendswith" => !Str(n, (s, x) => s.EndsWith(x, StringComparison.OrdinalIgnoreCase)),
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
					if (TypeHelper.IsConsideredPrimitive(value.GetType())) return string.IsNullOrEmpty(value.ToString());
				}

				return true;
			}

			public static bool EvaluateCompound(Condition n) => n.Logic!.ToUpperInvariant() switch
			{
				"&&" or "AND" => EvaluateAll(n.Conditions!),
				"OR" or "||" => n.Conditions!.Any(Evaluate),
				_ => throw new NotSupportedException($"Logic '{n.Logic}'")
			};

			private static bool EvaluateAll(IEnumerable<Condition> conditions)
			{
				foreach (var condition in conditions)
				{
					if (!Evaluate(condition))
						return false;
				}
				return true;
			}

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
					string s when i is string sub => s.Contains(sub, StringComparison.OrdinalIgnoreCase),
					IEnumerable coll => coll.Cast<object>().Contains(i),
					_ => false
				};
			static bool Str(Condition n, Func<string, string, bool> f)
			{
				string? leftValue = TypeHelper.ConvertToType(n.LeftValue, typeof(string)) as string;
				string? rightValue = TypeHelper.ConvertToType(n.RightValue, typeof(string)) as string;
				if (leftValue == null || rightValue == null) return false;

				return f(leftValue, rightValue);
			}
		}
	}
}
