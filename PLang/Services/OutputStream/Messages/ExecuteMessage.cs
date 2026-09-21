using PLang.Attributes;
using System.ComponentModel;

namespace PLang.Services.OutputStream.Messages;

[Description(@"Function is the name of the javascript function to call on the client, e.g. navigate, reload, focus. It is not a url or a path: a url the step names is Data, the payload passed into the function
Data is the payload sent as paramter into the function
Target defines where in the UI to write the content. It is a css selector such as #main or #ideaChat, which a step writes as cssSelector: #main or as `to #main`. This can be null and will be controlled by external system
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
	[LlmIgnore]
	IReadOnlyDictionary<string, object?>? Properties = null)
	: OutMessage(MessageKind.Execute, Level, StatusCode, Target, Array.Empty<string>(), Channel, Actor, Properties);

