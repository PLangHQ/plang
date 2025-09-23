namespace PLang.Services.OutputStream.Messages;

public sealed record ErrorMessage(
	string Message, string Key = "UserDefinedError", string Level = "error", int StatusCode = 400,
	string? Target = null, string Channel = "default", string Actor = "user", IReadOnlyList<string>? Actions = null,
	string? Fix = null, string? Links = null, IReadOnlyDictionary<string, object?>? Meta = null)
	: OutMessage(MessageKind.Error, Level, StatusCode, Target, Actions ?? new[] { "notify" }, Channel, Actor, Meta);