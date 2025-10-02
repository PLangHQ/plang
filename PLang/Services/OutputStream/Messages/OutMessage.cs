namespace PLang.Services.OutputStream.Messages;
public enum MessageKind { Text, Render, Error, Execute, Ask, Stream }

public abstract record OutMessage(
	MessageKind Kind,
	string Level,
	int StatusCode,
	string? Target,
	IReadOnlyList<string>? Actions,
	string Channel = "default", string Actor = "user",
	IReadOnlyDictionary<string, object?>? Properties = null);
