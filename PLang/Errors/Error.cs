using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json;
using PLang.Attributes;
using PLang.Building.Model;
using PLang.Errors.Events;
using PLang.Errors.Runtime;
using PLang.Runtime;
using PLang.Utils;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using static PLang.Utils.StepHelper;

namespace PLang.Errors
{
	public interface IError
	{
		public string Id { get; }
		public int StatusCode { get; }
		public string Key { get; }
		public string Message { get; }
		public string? FixSuggestion { get; }
		public string? HelpfulLinks { get; }
		public GoalStep? Step { get; set; }
		public Goal? Goal { get; set; }
		public DateTime CreatedUtc { get; init; }
		public Exception? Exception { get; }
		public List<IError> ErrorChain { get; set; }
		public string MessageOrDetail { get; }

		[IgnoreWhenInstructed]
		public bool Handled { get; set; }
		public List<ObjectValue> Variables { get; set; }
		public object ToFormat(string contentType = "text");
		public object AsData();
	}
	public record Error(object error) : IError
	{
		public Error(string Message, string Key = "GeneralError", int StatusCode = 400, Exception? Exception = null,
		string? FixSuggestion = null, string? HelpfulLinks = null, object? Data = null, Dictionary<string, object?>? Properties = null)
			: this(new
			{
				Message,
				Key,
				StatusCode,
				Properties,
				Data,
				Exception,
				FixSuggestion,
				HelpfulLinks,
			})
		{
			this.Message = Message;
			this.Key = Key;
			this.StatusCode = StatusCode;	
			this.Exception = Exception;
			this.FixSuggestion = FixSuggestion;
			this.HelpfulLinks = HelpfulLinks;
		}
		//public Error(IErrorReporting error)
		public string Id { get; } = Guid.NewGuid().ToString();
		public virtual GoalStep? Step { get; set; }
		public virtual Goal? Goal { get; set; }
		public string? FixSuggestion { get; set; }
		public string? HelpfulLinks { get; set; }
		public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
		public virtual object ToFormat(string contentType = "text")
		{
			return ErrorHelper.ToFormat(contentType, this);
		}

		public List<IError> ErrorChain { get; set; } = new();

		public int StatusCode {get;set;}

		public string Key { get; set; }

		public string Message { get; set; }


		[System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.Always)]
		public Exception? Exception { get; set; }

		[JsonPropertyName("exception")]
		public ExceptionDto? ExceptionInfo => ExceptionDto.FromException(Exception);
		[IgnoreWhenInstructed]
		public bool Handled { get; set; }
		public string MessageOrDetail {
			get
			{
				AppContext.TryGetSwitch(ReservedKeywords.DetailedError, out bool isEnabled);
				if (isEnabled)
				{
					return ErrorHelper.ToFormat("text", this).ToString();
				} else
				{
					return Message.MaxLength(80);
				}
			}
			
		}
		[IgnoreWhenInstructed]
		public bool IsHandled { get; set; } = false;

		public List<ObjectValue> Variables { get; set; } = new();

		public override string? ToString()
		{
			return ErrorHelper.ToFormat("text", this).ToString();
		}
		public virtual object AsData()
		{
			return this;
		}


	}

	public interface IErrorHandled : IEventError, IError { }


	public class ExceptionDto
	{
		public string Type { get; init; }
		public string Message { get; init; }
		public string? StackTrace { get; init; }
		public ExceptionDto? Inner { get; init; }

		public static ExceptionDto? FromException(Exception? ex) =>
			ex is null ? null : new()
			{
				Type = ex.GetType().FullName,
				Message = ex.Message,
				StackTrace = ex.StackTrace,
				Inner = FromException(ex.InnerException)
			};
	}

	public record EndGoal(Goal EndingGoal, GoalStep Step, string Message, int StatusCode  = 200, int Levels = 0) : StepError(Message, Step, "EndGoal", StatusCode), IErrorHandled
	{
		public override GoalStep? Step { get; set; } = Step;
		public override Goal? Goal { get; set; } = Step.Goal;
		public int Levels { get; set; } = Levels;

		public bool IgnoreError => false;

		public IError? InitialError { get; } = null;
	}
	public class ObjectValueConverter : System.Text.Json.Serialization.JsonConverter<ObjectValue>
	{
		public override ObjectValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> throw new NotImplementedException();

		public override void Write(Utf8JsonWriter writer, ObjectValue value, JsonSerializerOptions options)
		{
			System.Text.Json.JsonSerializer.Serialize(writer, value.Value, options);
		}
	}
	public class IErrorConverter : System.Text.Json.Serialization.JsonConverter<IError>
	{
		public override IError Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
			=> throw new NotImplementedException();

		public override void Write(Utf8JsonWriter writer, IError value, JsonSerializerOptions options)
		{
			writer.WriteStartObject();
			writer.WriteString("message", value.Message);
			writer.WriteString("details", ErrorHelper.ToFormat("text", value).ToString());
			writer.WriteString("key", value.Key);
			writer.WriteString("type", value.GetType().Name);
			if (value is UserInputError uie)
			{
				writer.WriteString("callback", JsonConvert.SerializeObject(uie.Callback).ToBase64());

			}
			writer.WriteString("statusCode", value.StatusCode.ToString());
			writer.WriteEndObject();
		}
	}
}
