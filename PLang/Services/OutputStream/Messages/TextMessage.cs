using PLang.Attributes;
using System.ComponentModel;

namespace PLang.Services.OutputStream.Messages;

[Description(@"Content can be a filename or a text that will be written to stream. 
Target defines where in the UI to write the content, this can be null and will be controlled by external system
Actions are actions executed on the content. null is default value unless defined by user. 
Built in actions are: 'append(default), prepend, replace, replaceSelf, clear, remove, scrollIntoView, focus, highlight, show, hide, notify, alert, badge, vibrate, navigate, reload, open, close'. 
A user can define multiple actions, user:`render 'product.html' to #main, replace the content, navigate and scroll into view => Actions:[""replace"", ""navigate"", ""scrollIntoView""]
Level: trace|debug|info|warning|error|critical. info is default. when user defines a level without a channel, assume channel=log
Channel: default|log|audit|security|metric or custom defined by user intent
Actor: user|system => user is the default actor when Channel=default, for other channels use 'system' as actor unless defined by user intent, e.g. `write out to user log ""analyzing""` => actor=user, channel=log
SkipNewline: default false, unless defined by user")]
public sealed record TextMessage(
	string Content, string Level = "info", int StatusCode = 200,
	string? Target = null, IReadOnlyList<string>? Actions = null,
	string Channel = "default", string Actor = "user",
	bool SkipNewline = false,
	[LlmIgnore]
	IReadOnlyDictionary<string, object?>? Properties = null,
	[LlmIgnore]
	string? Path = null)
	: OutMessage(MessageKind.Text, Level, StatusCode, Target, Actions, Channel, Actor, Properties);

