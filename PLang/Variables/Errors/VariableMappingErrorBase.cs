namespace PLang.Variables.Errors;

using PLang.Building.Model;
using PLang.Errors;
using PLang.Runtime;
using System;
using System.Collections.Generic;

public abstract class VariableMappingErrorBase : IError
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
	public string MessageOrDetail => Message;
	public bool Handled { get; set; }
	public List<ObjectValue> Variables { get; set; }

	protected VariableMappingErrorBase(string message, string key, string fixSuggestion = null, string helpfulLinks = null)
	{
		Id = Guid.NewGuid().ToString();
		StatusCode = 400;
		Key = key;
		Message = message;
		FixSuggestion = fixSuggestion;
		HelpfulLinks = helpfulLinks;
		CreatedUtc = DateTime.UtcNow;
		ErrorChain = new List<IError>();
		Variables = new List<ObjectValue>();
	}

	public virtual object ToFormat(string contentType = "text")
	{
		return contentType == "json"
			? new { Id, StatusCode, Key, Message, FixSuggestion, HelpfulLinks }
			: Message;
	}

	public virtual object AsData()
	{
		return new { Id, StatusCode, Key, Message, FixSuggestion, HelpfulLinks };
	}
}