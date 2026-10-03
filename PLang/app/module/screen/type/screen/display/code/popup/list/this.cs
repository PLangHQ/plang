namespace app.module.screen.type.screen.display.code.popup.list;

/// <summary>The menus (popups), in the order they opened: later ones on top.</summary>
internal sealed class @this
{
    private readonly List<XdgPopup> _open = new();

    internal void Add(XdgPopup popup) => _open.Add(popup);
    internal bool Remove(XdgPopup popup) => _open.Remove(popup);

    internal IPart? Hit(int x, int y)
    {
        for (var i = _open.Count - 1; i >= 0; i--)
            if (_open[i].Picture.Rect.Contains(x, y))
                return new Content(null, new Target(_open[i].Surface, _open[i].Picture.Rect.Corner));
        return null;
    }

    internal void Draw(int y, int x0, Span<byte> line)
    {
        foreach (var p in _open) p.Picture.Draw(y, x0, line);
    }
}
