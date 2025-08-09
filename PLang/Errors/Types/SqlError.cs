using Dapper;
using Newtonsoft.Json;
using PLang.Building.Model;
using PLang.Errors.Runtime;
using PLang.Modules;
using PLang.Utils;
using static PLang.Modules.DbModule.Program;

namespace PLang.Errors.Types
{
	public record SqlError : ProgramError
	{
		public SqlError(string Message, string Sql, List<ParameterInfo>? Parameters, GoalStep? Step = null, BaseBuilder.IGenericFunction? GenericFunction = null, 
			IDictionary<string, object?>? ParameterValues = null, string Key = "SqlError", int StatusCode = 500, Exception? Exception = null, 
			string? FixSuggestion = null, string? HelpfulLinks = null) : base(Message, Step, GenericFunction, ParameterValues, Key, StatusCode, Exception, FixSuggestion, HelpfulLinks)
		{
			if (this.ParameterValues  == null) this.ParameterValues = new Dictionary<string, object?>();
			this.ParameterValues.Add("Sql", Sql);
			this.ParameterValues.Add("Parameters", Parameters);

			this.FixSuggestion += $"\nFollowing are the sql and parameters used for this sql statement:\n\tSql:{Sql}";
			if (Parameters != null)
			{
				foreach (var parameter in Parameters)
				{
					string? value = parameter.VariableNameOrValue?.ToString()?.ReplaceLineEndings("").MaxLength(150);
					if (value == null) value = "[null]";

					this.FixSuggestion += $"\n\t - ({parameter.TypeFullName}) {parameter.ParameterName} = {value}";
				}
			}

		}

		public override string ToString()
		{
			return base.ToString();
		}

	}
}
