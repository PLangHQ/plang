using Jil;
using LightInject;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using PLang.Attributes;
using PLang.Errors;
using PLang.Errors.Builder;
using PLang.Errors.Runtime;
using PLang.Exceptions;
using PLang.Interfaces;
using PLang.Models;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using static PLang.Modules.BaseBuilder;

namespace PLang.Building.Model
{

	public static class InstructionCreator
	{
		public static (Instruction? Instruction, IError? Error) Create(string absolutePath, IPLangFileSystem fileSystem)
		{
			try
			{
				string json = fileSystem.File.ReadAllText(absolutePath);
				var instruction = JsonConvert.DeserializeObject<Instruction>(json);

				return (instruction, null);
			}
			catch (Exception ex)
			{
				return (null, new ExceptionError(ex, "Instruction file is invalid", Key: "InvalidInstructionFile"));
			}
		}
		public static Instruction Create(IGenericFunction function, GoalStep step, LlmRequest llmRequest)
		{
			return Create(function, step, [llmRequest]);
		}
		public static Instruction Create(IGenericFunction function, GoalStep step, List<LlmRequest> llmRequest)
		{
			var instruction = new Instruction();
			instruction.Function = function;
			instruction.Text = step.Text;
			instruction.LlmRequest.AddRange(llmRequest);
			return instruction;
		}
		public static Instruction Create(object obj, Type type, GoalStep step, LlmRequest llmRequest)
		{
			var instruction = new Instruction();
			instruction.Text = step.Text;
			instruction.LlmRequest.Add(llmRequest);
			instruction.Step = step;

			var function = (IGenericFunction)Convert.ChangeType(obj, type);

			// llm may return null on a parameter/return values, instead of doing new request to llm, just remove them.
			function.Parameters?.RemoveAll(p => p == null);
			function.ReturnValues?.RemoveAll(p => p == null || string.IsNullOrEmpty(p.VariableName));

			function.Instruction = instruction;
			instruction.Function = function;


			return instruction;
		}
	}

	public record Instruction
	{

		public string Text { get; set; }
		public string ModuleType { get; set; }

		[LlmIgnore]
		public bool Reload { get; set; }

		[LlmIgnore]
		public List<LlmRequest> LlmRequest { get; set; } = new();

		[LlmIgnore]
		public Dictionary<string, object?> Properties { get; set; } = new();

		[IgnoreWhenInstructed]
		public Stopwatch Stopwatch { get; set; }

		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public GoalStep Step { get; set; }

		public string GenericFunctionType
		{
			get; set;
		}

		IGenericFunction function;
		[Newtonsoft.Json.JsonIgnore]
		[IgnoreDataMemberAttribute]
		[System.Text.Json.Serialization.JsonIgnore]
		public IGenericFunction Function
		{
			get
			{
				return function;
			}
			set { 
				function = value;

				GenericFunctionType = value.GetType().FullName!;
				functionJson = JObject.FromObject(value);
			}
		}

		private JToken? functionJson;
		[JsonProperty("Function")]
		public JToken? FunctionJson
		{
			get { return functionJson; }

			set
			{
				functionJson = value;

				var functionType = Type.GetType(GenericFunctionType);
				function = (IGenericFunction)value.ToObject(functionType);				
			}
		}


		/*
		public (IGenericFunction[] Functions, IBuilderError? Error) GetFunctions()
		{
			return ([Function], null);
			/*
			try
			{
				string? action = Action.ToString();
				if (string.IsNullOrWhiteSpace(action)) return (null, new BuilderError("The .pr file seem to be invalid", Key: "InvalidInstructionFile"));

				if (Action is JArray jArray)
				{
					var gf = jArray.ToObject<GenericFunction[]>();
					return (gf, ValidateGenericFunction(gf));
				}
				else if (Action is JObject jObject)
				{
					var gf = jObject.ToObject<GenericFunction>();					
					return ([gf], ValidateGenericFunction([gf]));
				} else if (Action is GenericFunction gf)
				{
					return ([gf], null);
				}

					return (null, ValidateGenericFunction(null));
			} catch (JsonSerializationException ex)
			{
				if (ex.Message.Contains("Could not find member")) throw new InvalidInstructionFileException("Instruction file was not valid. You might need to rebuild your code", ex);
				throw;
			}*/
		/*
		}*/

		BuilderError invalidInstructionFileError = new BuilderError("The .pr file seem to be invalid", Key: "InvalidInstructionFile");
		public IBuilderError? ValidateGenericFunction(GenericFunction[]? gfs)
		{
			if (gfs == null || gfs.Length == 0)
				return invalidInstructionFileError;
			if (string.IsNullOrEmpty(gfs[0].Name))
				return invalidInstructionFileError;
			return null;
		}
	};
}
