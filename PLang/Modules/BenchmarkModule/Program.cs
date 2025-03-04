
/*
 * 
 * Need to think about this, not so straight forward
 * 
 * BaseProgram
 * var benchmark = context.GetOrDefault<bool>(PLang.Modules.BenchmarkModule.Program.BenchmarkContextKey, false);
				if (benchmark) {
					
					var methods = context.GetOrDefault<List<string>>(PLang.Modules.BenchmarkModule.Program.BenchmarkMethodsContextKey, new List<string>()) ?? new();
					if (methods.Count > 0)
					{
						if (!methods.Contains(function.FunctionName))
						{
							benchmark = false;
						}
					}
				}

				Task? task = null;
				if (benchmark)
				{
					var benchmarkInstance = new ReflectionBenchmark(this, method, parameterValues.Values.ToArray());
					var summary = BenchmarkRunner.Run<ReflectionBenchmark>(new SingleRunConfig());
					BenchmarkModule.Program.AddSummary(summary, context);
					task = benchmarkInstance.Task;
				}
				else
				{
					task = method.Invoke(this, parameterValues.Values.ToArray()) as Task;
				}
 * 
 * 
 * 
 * 
 *

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using PLang.Attributes;
using PLang.Interfaces;
using System.Reflection;


namespace PLang.Modules.BenchmarkModule
{
	[MemoryDiagnoser] // Optional: Tracks memory usage
	public class ReflectionBenchmark
	{
		private static object _instance;
		private static MethodInfo _method;
		private static object?[] _parameters;
		private Task task;

		public ReflectionBenchmark() { } // Required by BenchmarkDotNet

		public ReflectionBenchmark(object instance, MethodInfo method, object?[] parameters)
		{
			_instance = instance;
			_method = method;
			_parameters = parameters;
		}

		[Benchmark]
		public async Task Execute()
		{
			var result = _method.Invoke(_instance, _parameters);
			if (result is Task taskResult)
			{
				await taskResult;
				this.task = taskResult;
			}
		}

		public Task Task { get { return task; } }
	}


	public class SingleRunConfig : ManualConfig
	{
		public SingleRunConfig()
		{
			AddDiagnoser(MemoryDiagnoser.Default); // Tracks memory usage

			AddColumnProvider(DefaultColumnProviders.Instance); // Default columns
			AddColumn(StatisticColumn.Mean);
			AddColumn(StatisticColumn.Median);
			AddColumn(StatisticColumn.Min);
			AddColumn(StatisticColumn.Max);
			AddColumn(StatisticColumn.StdDev);

			AddJob(Job.Default
				.WithWarmupCount(0)        // No warmup runs
				.WithIterationCount(1)     // Run only once
				.WithInvocationCount(1)    // Ensure it invokes only once
				.WithUnrollFactor(1)
				.WithCustomBuildConfiguration("Debug")
				.WithEnvironmentVariable("DOTNET_EnableDiagnostics", "0") // Reduce noise
				.WithRuntime(CoreRuntime.Latest)); // Ensures latest .NET Core runtime


			
		}
	}

	public class Program : BaseProgram
	{
		public static readonly string BenchmarkContextKey = "!BenchmarkContextKey";
		public static readonly string BenchmarkMethodsContextKey = "!BenchmarkMethodsContextKey";
		public static readonly string BenchmarkSummaryContextKey = "!BenchmarkSummaryContextKey";
		public Program()
		{
		}

		internal static void AddSummary(Summary summary, PLangAppContext context)
		{
			var summaries = context[BenchmarkSummaryContextKey] as List<Summary> ?? new();
			summaries.Add(summary);
			context[BenchmarkSummaryContextKey] = summaries;
		}


		[MethodSettings(CanBeCached = false, CanBeAsync = false)]
		public async Task<List<Summary>> GetAllBenchmarks() { 
			return context[BenchmarkSummaryContextKey] as List<Summary> ?? new();
		}

		[MethodSettings(CanBeCached = false, CanBeAsync = false)]
		public async Task EnableBenchmark(string? methodName = null)
		{
			context[BenchmarkContextKey] = true;
			if (methodName != null)
			{
				var methods = context[BenchmarkMethodsContextKey] as List<string> ?? new();
				if (!methodName.Contains(methodName))
				{
					methods.Add(methodName);
				}

				context[BenchmarkMethodsContextKey] = methods;
			}
		}

		[MethodSettings(CanBeCached = false, CanBeAsync = false)]
		public async Task DisableBenchmark(string? methodName = null)
		{
			if (methodName == null)
			{
				context.Remove(BenchmarkContextKey);
				context.Remove(BenchmarkMethodsContextKey);
			}

			if (methodName != null)
			{
				var methods = context[BenchmarkMethodsContextKey] as List<string> ?? new();
				methods.Remove(methodName);
				
				if (methods.Count == 0)
				{
					context.Remove(BenchmarkContextKey);
					context.Remove(BenchmarkMethodsContextKey);
				}
				else
				{
					context[BenchmarkMethodsContextKey] = methods;
				}
			}
		}

	}
}*/
