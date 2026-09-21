using PLang.Attributes;
using System.ComponentModel;

namespace PLang.Services.OutputStream.Messages;

[Description(@"Content is what gets rendered: a template file name, the text itself, or the variable holding it. Never the variable the step writes its result INTO, the one after `write to` or `into`, and never a css selector or an action name.
Target is where in the UI it goes, a css selector the step writes as cssSelector: #main or as `to #main`. Null unless the step names one.
Actions are what is done with the content: replace, replaceSelf, append, prepend, appendOrReplace, prependOrReplace, scrollToTop, scrollIntoView, focus, highlight, show, hide, showDialog, hideDialog, showModal, hideModal, notify, alert, vibrate, navigate, replaceState, reload, open, close. A step may name several.
Level: trace|debug|info|warning|error|critical. A step that names a level but no channel goes to channel=log. A step that names neither is level=info on channel=default.
Channel: default|log|audit|security|metric, or one the step names. default unless the step names a level or a channel.
Actor: user|system, user on the default channel and system on the others, unless the step says otherwise.
")]
[Example(@"render ""template.html"", hide modal and show modal", @"Content=""template.html"", Actions=[""hideModal"", ""showModal""]")]
[Example(@"render ""template.html"", navigate and scroll", @"Content=""template.html"", Actions=[""navigate"", ""scrollToTop""]")]
public sealed record RenderMessage(
	string Content, string? Target = null, IReadOnlyList<string>? Actions = null, 
	string Level = "info", int StatusCode = 200, string Channel = "default", string Actor = "user",
	[LlmIgnore]
	IReadOnlyDictionary<string, object?>? Properties = null)
	: OutMessage(MessageKind.Render, Level, StatusCode, Target, Actions ?? new[] { "replace" }, Channel, Actor, Properties);
