

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
		public async Task<(double?, IError?)> SolveExpression(string expression)
		{
			//Console.WriteLine("expression: " + expression);

			if (expression == "")
				return (null, new ProgramError("Could not use empty variable", goalStep, function, FixSuggestion: $"The variable value is: '{expression}' (without quotes)"));

			var solveFor = new NCalc.Expression(expression);
			double? value = EvaluateCustomExpressions(solveFor);
				
			if (value == null)
				return (null, new ProgramError("Could not evaluate expression", goalStep, function, FixSuggestion: $"The variable value is: '{expression}' (without quotes)"));
			

			return (value, null);
		}

		//Number theory

		[Description("Find the fibonacci number of a given variable or number.")]
		public async Task<(int?, IError?)> Fibonacci(int variable)
		{
			if (variable == 0)
				return (1, null);

			int value = Convert.ToInt32((1 / Math.Sqrt(5))*(Math.Pow( ((1 + Math.Sqrt(5)) / 2), variable ) - Math.Pow( ((1 - Math.Sqrt(5)) / 2), variable )));

			return (value, null);
		}

		[Description("Find the first n prime numbers where n is a given number.")]
		public async Task<(List<int>, IError?)> PrimeNumbers(int number)
		{
			//size estimation formula only works for n >= 6
			if (number <= 5)
			{
				switch (number)
				{
					case 1:
						return ([2], null);
						break;
					case 2:
						return ([2, 3], null);
						break;
					case 3:
						return ([2, 3, 5], null);
						break;
					case 4:
						return ([2, 3, 5, 7], null);
						break;
					case 5:
						return ([2, 3, 5, 7, 11], null);
						break;
					default:
						return ([], new ProgramError("Could not convert variable to integer", goalStep, function, FixSuggestion: $"The variable value is: '{number}' (without quotes)"));
						break;
				}
			}

			//formula to find an overestimate for the size of the array needed for Sieve of Eratosthenes to find the first n prime numbers
			int size = Convert.ToInt32(Math.Ceiling(number * (Math.Log(number) + Math.Log(Math.Log(number)))));
			Console.WriteLine("Size of array for n = " + number + " is: " + size);

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

		public double? EvaluateCustomExpressions(NCalc.Expression expression)
		{
			double value = 0;

			try
			{
				value = Convert.ToDouble(expression.Evaluate());
				value = Math.Round(value, 3, MidpointRounding.AwayFromZero);
			}
			catch (EvaluateException e)
			{
				return null;
			}

			return value;
		}
	}
}