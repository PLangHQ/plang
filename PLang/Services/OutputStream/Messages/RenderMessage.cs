using PLang.Attributes;
using System.ComponentModel;

namespace PLang.Services.OutputStream.Messages;

[Description(@"Content is what gets rendered or written: the name of a template file, or the text itself. It is never the variable the step writes its result into, and never a css selector or an action name. 
Target defines where in the UI to write the content. It is a css selector such as #main or #ideaChat, which a step writes as cssSelector: #main or as `to #main`. This can be null and will be controlled by external system
Actions are actions executed on the content, built in actions are: 'replace, replaceSelf, append, prepend, appendOrReplace, prependOrReplace, scrollToTop, scrollIntoView, focus, highlight, show, hide, showDialog, hideDialog, showModal, hideModal, notify, alert, vibrate, navigate, replaceState, reload, open, close'. 
A user can define multiple actions, user:`render 'product.html' to #main, replace the content, navigate and scroll into view => Actions:[""replace"", ""navigate"", ""scrollIntoView""]
Level: trace|debug|info|warning|error|critical. info is default. when user defines a level without a channel, assume channel=log
Channel: default|log|audit|security|metric or custom defined by user
Actor: user|system => user is the default actor when Channel=default, for other channels use system as actor unless defined by user.
")]
[Example(@"render ""template.html"", hide modal and show modal", @"Content=""template.html"", Actions=[""hideModal"", ""showModal""]")]
[Example(@"render ""template.html"", navigate and scroll", @"Content=""template.html"", Actions=[""navigate"", ""scrollToTop""]")]
public sealed record RenderMessage(
	string Content, string? Target = null, IReadOnlyList<string>? Actions = null, 
	string Level = "info", int StatusCode = 200, string Channel = "default", string Actor = "user",
	[LlmIgnore]
	IReadOnlyDictionary<string, object?>? Properties = null)
	: OutMessage(MessageKind.Render, Level, StatusCode, Target, Actions ?? new[] { "replace" }, Channel, Actor, Properties);
