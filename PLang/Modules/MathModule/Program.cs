

using IdGen;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NBitcoin.Protocol;
using NCalc;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Utilities;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Runtime;
using PLang.Models;
using PLang.Runtime;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;

namespace PLang.Modules.MathModule
{
	[Description("Solves math expressions")]
	public class Program : BaseProgram
	{
		public Program() : base()
		{

		}

		[Description("Solve a complex math expression given as a string. Please capitalize any functions called like sqrt() into Sqrt()")]
		public async Task<(object?, IError?)> SolveExpression(string expression, int decimalRound = 2, MidpointRounding? midpointRounding = null)
		{
			if (string.IsNullOrEmpty(expression))
				return (null, new ProgramError("Could not use empty variable", goalStep, function, FixSuggestion: $"The variable value is: '{expression}' (without quotes)"));

			var solveFor = new NCalc.Expression(expression, ExpressionOptions.IgnoreCaseAtBuiltInFunctions);
			return EvaluateCustomExpressions(solveFor, decimalRound, midpointRounding);
		}

		//Number theory

		[Description("Find the fibonacci number of a given variable or number.")]
		public async Task<(int?, IError?)> Fibonacci(int variable)
		{
			if (variable == 0)
				return (1, null);

			int value = Convert.ToInt32((1 / Math.Sqrt(5)) * (Math.Pow(((1 + Math.Sqrt(5)) / 2), variable) - Math.Pow(((1 - Math.Sqrt(5)) / 2), variable)));

			return (value, null);
		}

		[Description("Find the first n prime numbers where n is a given number.")]
		public async Task<(List<int>, IError?)> PrimeNumbers(int number)
		{
			//formula to find an overestimate for the size of the array needed for Sieve of Eratosthenes to find the first n prime numbers
			int size = 13;
			if (number >= 6)
			{
				size = Convert.ToInt32(Math.Ceiling(number * (Math.Log(number) + Math.Log(Math.Log(number)))));
			}

			bool[] isPrimeList = new bool[size];

			//0 and 1 are not prime numbers
			for (int i = 2; i < isPrimeList.Count(); i++)
			{
				isPrimeList[i] = true;
			}

			//do Sieve of Eratosthenes algorithm
			for (int i = 0; i < isPrimeList.Count(); i++)
			{
				if (i * 2 > isPrimeList.Count())
				{
					break;
				}

				if (isPrimeList[i] == true)
				{
					for (int j = i * 2; j < isPrimeList.Count(); j += i)
					{
						isPrimeList[j] = false;
					}
				}
			}

			List<int> primeList = new List<int>();

			for (int i = 0; i < isPrimeList.Count(); i++)
			{
				if (isPrimeList[i] == true)
				{
					primeList.Add(i);
				}

				if (primeList.Count() == number)
					break;
			}

			return (primeList, null);
		}

		private (object?, IError?) EvaluateCustomExpressions(NCalc.Expression expression, int decimalRound = 2, MidpointRounding? midpointRounding = null)
		{
			try
			{
				if (midpointRounding == null) midpointRounding = MidpointRounding.AwayFromZero;

				var result = expression.Evaluate();
				if (result == null) return (null, null);

				// Round based on the specific floating-point type
				switch (result)
				{
					case double d:
						return (Math.Round(d, decimalRound, midpointRounding.Value), null);
					case float f:
						return ((float)Math.Round(f, decimalRound, midpointRounding.Value), null);
					case decimal m:
						return (Math.Round(m, decimalRound, midpointRounding.Value), null);
					default:
						return (result, null);
				}
			}
			catch (Exception e)
			{
				return (null, new ProgramError(e.Message, goalStep, function, FixSuggestion: $"The expression value is: {expression}", Exception: e));
			}
		}
	}
}