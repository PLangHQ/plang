using PLang.Attributes;

namespace PLang.Services.OutputStream.Messages;

public enum StreamPhase { Start, Chunk, End, Abort }

public sealed record StreamMessage(
	string StreamId,                               // correlation across messages
	StreamPhase Phase,                             // Start | Chunk | End | Abort
	ReadOnlyMemory<byte>? Bytes = null,            // optional binary
	string? Text = null,                           // optional text chunk
	string ContentType = "application/octet-stream",
	string Level = "info",
	int StatusCode = 200,
	string? Target = null,
	IReadOnlyList<string>? Actions = null,       
	string Channel = "default", string Actor = "user",
	IReadOnlyDictionary<string, object?>? Meta = null)
	: OutMessage(MessageKind.Stream, Level, StatusCode, Target, Actions ?? new[] { "stream" }, Channel, Actor, Meta)
{
	public bool HasBinary => Bytes.HasValue && !Bytes.Value.IsEmpty;
	public bool HasText => !string.IsNullOrEmpty(Text);
}