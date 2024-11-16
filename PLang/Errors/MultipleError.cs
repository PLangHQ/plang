using PLang.Building.Model;
using PLang.Utils;

namespace PLang.Errors;

public record GroupedErrors(
    string Key = "GroupedErrors",
    int StatusCode = 400,
    string? FixSuggestion = null,
    string? HelpfulLinks = null) : IError
{
    protected List<IError> errors = new();

    public List<IError> Errors => errors;


    public int Count => errors.Count;

    public string Message
    {
        get
        {
            var message = string.Empty;
            foreach (var error in errors) message += error.Message + Environment.NewLine;
            return message;
        }
    }

    public GoalStep? Step { get; set; }
    public Goal? Goal { get; set; }

    public object ToFormat(string contentType = "text")
    {
        if (contentType == "text")
        {
            var str = "";
            foreach (var error in errors) str += $"\t- {error.Message}" + Environment.NewLine;
            str += Environment.NewLine;
            foreach (var error in errors) str += error.ToFormat() + Environment.NewLine;
        }

        return ErrorHelper.ToFormat(contentType, this);
    }

    public Exception? Exception { get; }

    public void Add(IError error)
    {
        errors.Add(error);
        if (errors.Count == 1)
        {
            Step = error.Step;
            Goal = error.Goal;
        }
    }

    public override string ToString()
    {
        return ToFormat().ToString();
    }
}

public record MultipleError(
    IError InitialError,
    string Key = "MultipleError",
    int StatusCode = 400,
    string? FixSuggestion = null,
    string? HelpfulLinks = null) : IError
{
    protected List<IError> errors = new();

    public List<IError> Errors => errors;


    public int Count => errors.Count;

    public string Message
    {
        get
        {
            var message = InitialError.Message + Environment.NewLine;
            foreach (var error in errors) message += error.Message + Environment.NewLine;
            return message;
        }
    }

    public GoalStep? Step { get; set; } = InitialError.Step;
    public Goal? Goal { get; set; } = InitialError.Step?.Goal;

    public object ToFormat(string contentType = "text")
    {
        if (contentType == "text")
        {
            var str = $@"{errors.Count + 1} errors occured:
	- {InitialError.Message}";
            str += $"\t- {InitialError.Message}" + Environment.NewLine;
            foreach (var error in errors) str += $"\t- {error.Message}" + Environment.NewLine;
            str += Environment.NewLine;
            str += InitialError.ToFormat() + Environment.NewLine;
            foreach (var error in errors) str += error.ToFormat() + Environment.NewLine;
        }

        return ErrorHelper.ToFormat(contentType, this);
    }

    public Exception? Exception { get; }

    public void Add(IError error)
    {
        if (error != InitialError) errors.Add(error);
    }

    public override string ToString()
    {
        return ToFormat().ToString();
    }
}