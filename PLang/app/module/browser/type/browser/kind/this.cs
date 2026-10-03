namespace app.module.browser.type.browser.kind;

/// <summary>
/// A kind of browser — <c>headless</c> (one page rendered off-screen, its frames to OnFrame) or <c>screen</c> (Chromium
/// drawing onto PlangOS's display, its pages windows). A goal reads it as <c>%browser!type.kind%</c>, or asks
/// <c>if %browser% is headless</c>.
/// </summary>
public abstract class @this : global::app.type.kind.@this
{
    protected @this(string name) : base(name) { }

    protected internal override string Owner => "browser";
}
