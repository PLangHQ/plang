using PLang.Attributes;
using System.ComponentModel;

namespace PLang.Services.OutputStream.Messages;

[Description(@"Content is what gets rendered or written: the name of a template file, the text itself, or the variable holding what is to be written, e.g. `write out %result%` has Content %result%. What it is never is the variable the step writes its result INTO, the one named after `write to` or `into`, and never a css selector or an action name.
Target is where in the UI it goes, a css selector the step writes as cssSelector: #main or as `to #main`. Null unless the step names one.
Actions are what is done with the content: append is the default, then prepend, replace, replaceSelf, clear, remove, scrollIntoView, focus, highlight, show, hide, notify, alert, badge, vibrate, navigate, reload, open, close. A step may name several.
Level: trace|debug|info|warning|error|critical, info unless the step says otherwise; a level without a channel means channel=log.
Channel: default|log|audit|security|metric, or one the step names.
Actor: user|system, user on the default channel and system on the others, unless the step says otherwise.
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

