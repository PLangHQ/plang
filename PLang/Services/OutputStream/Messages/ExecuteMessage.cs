using System.ComponentModel;

namespace PLang.Services.OutputStream.Messages;

[Description(@"Function of the will be called on the client side
Data is the payload sent as paramter into the function
Target defines where in the UI to write the content, this can be null and will be controlled by external system
Level: trace|debug|info|warning|error|critical. info is default. when user defines a level without a channel, assume channel=log
Channel: default|log|audit|security|metric or custom defined by user
Actor: user|system => user is the default actor when Channel=default, for other channels use system as actor unless defined by user.
")]
public sealed record ExecuteMessage(
	string Function,     
	object? Data,    
	string Level = "info",
	int StatusCode = 200,
	string? Target = null, string Channel = "default", string Actor = "user",
	IReadOnlyDictionary<string, object?>? Meta = null)
	: OutMessage(MessageKind.Execute, Level, StatusCode, Target, Array.Empty<string>(), Channel, Actor, Meta);

