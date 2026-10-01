namespace app.@event.on;

/// <summary>A click: what runs when a screen's element is clicked (<c>on click on #window.bot</c>) — handed the click
/// (the window it was in, where).</summary>
public sealed class click : global::app.@event.@this
{
    internal click(binding.list.before before, binding.list.after after) : base("click", before, after) { }

    protected override global::app.@event.@this Of(global::app.@event.on.@this on) => on.click;
}
