namespace app.module.screen.code.wayland;

/// <summary>
/// Something plang-screen shows under a window's title bar, above everything (one at a time): the
/// address field, the window's menu. It draws itself where its window is, follows it, answers the
/// pointer and — while open — may take the keyboard.
/// </summary>
internal abstract class Panel(Window owner) : IPart
{
    internal Window Owner { get; } = owner;
    protected Display Display => Owner.Display;
    internal Picture Picture { get; private set; } = Picture.None;

    protected abstract Canvas Paint();
    protected abstract Point Corner { get; }

    internal void Draw()
    {
        var old = Picture.Rect;
        Picture = Paint().Picture(Corner);
        Display.Frame.Redraw(old, Picture.Rect);
    }

    /// <summary>Its window moved or changed size: it goes along.</summary>
    internal void Follow(Window window)
    {
        if (ReferenceEquals(window, Owner)) Draw();
    }

    /// <summary>A window went away (or was minimized): its panel with it.</summary>
    internal void Closed(Window window)
    {
        if (ReferenceEquals(window, Owner)) Close();
    }

    internal void Close()
    {
        if (!ReferenceEquals(Display.Panel, this)) return;
        Display.Panel = null;
        Display.Frame.Redraw(Picture.Rect, default);
    }

    /// <summary>A key while open; true when the panel took it.</summary>
    internal abstract bool Key(uint scancode, bool extended, int mods, bool down);

    internal virtual void Type(string text) { }
}

/// <summary>
/// The address field that drops down under a title bar: the window's address, all of it selected
/// (typing replaces it). Enter asks PLang to go there; Esc closes. A copy button at its right end
/// copies the address — the address copies itself (<see cref="Address.Copy"/>); what was typed
/// instead is copied as typed.
/// </summary>
internal sealed class AddressField : Panel, IPart
{
    private const int Height = 40;
    private const int CopyWidth = 40;
    private string text;
    private int caret;             // in characters
    private bool selected = true;
    private bool copied, copyHover;

    internal AddressField(Window owner) : base(owner)
    {
        text = owner.Address.Path;
        caret = text.Length;
    }

    private int Width => Math.Clamp(Owner.Size.Width - 40, 200, 760);
    protected override Point Corner => new(Owner.At.X + 76, Owner.At.Y - 2);

    private bool OnCopy(Point at) => at.X >= Picture.Rect.X + 4 + Width - CopyWidth && at.X < Picture.Rect.X + 4 + Width;

    public string? Cursor => copyHover ? "default" : "text";

    public void Over(Point at)
    {
        var over = OnCopy(at);
        if (over == copyHover) return;
        copyHover = over;
        Draw();
    }

    public void Down(Click click)
    {
        if (!copyHover) return;
        copied = true;
        Copy();
        Draw();
    }

    private void Copy() => Display.Frame.Clipboard(text == Owner.Address.Path ? Owner.Address.Copy() : text);

    internal override bool Key(uint scancode, bool extended, int mods, bool down)
    {
        if (!down) return true;
        var ctrl = (mods & 2) != 0;
        switch (scancode, extended)
        {
            case (0x1C, _):   // Enter: somewhere else — the address as it is goes nowhere new
                Close();
                if (text.Trim().Length > 0 && text != Owner.Address.Path) Owner.Navigate(text.Trim());
                return true;
            case (0x01, false): Close(); return true;   // Esc
            case (0x0E, false): Backspace(); break;
            case (0x53, true): Delete(); break;
            case (0x4B, true): Step(-1); break;
            case (0x4D, true): Step(1); break;
            case (0x47, true): selected = false; caret = 0; break;
            case (0x4F, true): selected = false; caret = text.Length; break;
            case (0x1E, false) when ctrl: selected = true; break;                                   // Ctrl+A
            case (0x2F, false) when ctrl: Type(Display.Clipboard.Host.Replace("\r", " ").Replace("\n", " ")); return true;   // Ctrl+V
            case (0x2E, false) when ctrl: Copy(); return true;                                      // Ctrl+C
            default: return true;
        }
        Draw();
        return true;
    }

    internal override void Type(string typed)
    {
        Cut();
        text = text.Insert(caret, typed);
        caret += typed.Length;
        Draw();
    }

    /// <summary>Typing over a selection replaces it.</summary>
    private void Cut()
    {
        if (!selected) return;
        text = "";
        caret = 0;
        selected = false;
    }

    private void Backspace()
    {
        if (selected) { Cut(); return; }
        if (caret == 0) return;
        text = text.Remove(--caret, 1);
    }

    private void Delete()
    {
        if (selected) { Cut(); return; }
        if (caret < text.Length) text = text.Remove(caret, 1);
    }

    private void Step(int by)
    {
        caret = selected ? (by < 0 ? 0 : text.Length) : Math.Clamp(caret + by, 0, text.Length);
        selected = false;
    }

    protected override Canvas Paint()
    {
        var font = Display.Font;
        var c = new Canvas(Width + 8, Height + 8);
        c.Round(new Rect(4, 6, Width, Height), 10, Color.Shadow);
        c.Round(new Rect(4, 4, Width, Height), 10, Color.Accent);
        c.Round(new Rect(5, 5, Width - 2, Height - 2), 9, Color.Field);
        var mid = 4 + Height / 2f;
        const float x = 18;
        var maxX = Width + 4 - CopyWidth - 6;
        if (selected && text.Length > 0)
        {
            var end = x + Math.Min(Canvas.Measure(font, text), maxX - x);
            c.Round(new Rect((int)x - 2, 13, (int)(end - x) + 4, Height - 18), 3, Color.Selection);
        }
        var starts = c.Text(font, text, x, mid, maxX, Color.Ink);
        if (!selected)
        {
            var cx = caret < starts.Count ? starts[caret] : starts[^1];
            c.Line(cx, mid - 8, cx, mid + 8, 1.2f, Color.Accent);
        }
        // the copy button: two sheets; a check mark once copied
        var bx = Width + 4 - CopyWidth;
        if (copyHover) c.Round(new Rect(bx + 4, 9, CopyWidth - 8, Height - 10), 6, Color.Hover);
        float kx = bx + CopyWidth / 2f, ky = mid;
        if (copied)
        {
            c.Line(kx - 5, ky, kx - 1.5f, ky + 4, 1.6f, Color.Accent);
            c.Line(kx - 1.5f, ky + 4, kx + 5.5f, ky - 4.5f, 1.6f, Color.Accent);
        }
        else
        {
            c.Outline(kx - 5.5f, ky - 3.5f, 8, 10, Color.InkInactive);
            c.Fill(new Rect((int)kx - 3, (int)ky - 6, 9, 11), Color.Field);
            c.Outline(kx - 2.5f, ky - 6.5f, 8, 10, Color.Ink);
        }
        return c;
    }
}

/// <summary>An item of a window's menu: what it says, its keys, and what it does to the window.</summary>
internal sealed record MenuItem(string Label, string Keys, Action<Window> Act);

/// <summary>A window's menu (☰): Chromium's tools, by their keys or pages.</summary>
internal sealed class WindowMenu(Window owner) : Panel(owner), IPart
{
    private const int Width = 250, Row = 32, Gap = 9, Pad = 6;

    // null: a separator
    private static readonly MenuItem?[] Items =
    [
        new("New window", "", w => w.OpenAnother()),
        new("Reload", "F5", w => w.Press(Keyboard.F5)),
        null,
        new("Find…", "Ctrl+F", w => w.Press(Keyboard.F, Keyboard.LeftCtrl)),
        new("Zoom in", "Ctrl +", w => w.Press(Keyboard.KeypadPlus, Keyboard.LeftCtrl)),    // the keypad's: the same on every layout
        new("Zoom out", "Ctrl −", w => w.Press(Keyboard.KeypadMinus, Keyboard.LeftCtrl)),
        new("Actual size", "Ctrl+0", w => w.Press(Keyboard.Zero, Keyboard.LeftCtrl)),
        new("Print…", "Ctrl+P", w => w.Press(Keyboard.P, Keyboard.LeftCtrl)),
        null,
        // Chromium's own pages don't open as app windows: the window goes there (Back returns)
        new("History", "", w => w.Navigate("chrome://history")),
        new("Downloads", "", w => w.Navigate("chrome://downloads")),
        new("Settings", "", w => w.Navigate("chrome://settings")),
        null,
        new("Developer tools", "F12", w => w.Press(Keyboard.F12)),
    ];

    private int? hover;

    private static int HeightOf => Items.Sum(i => i == null ? Gap : Row) + 2 * Pad;

    protected override Point Corner
    {
        get
        {
            var right = Owner.At.X + Owner.Size.Width - 3 * Button.Caption - 6;   // the menu button's right edge
            return new Point(Math.Max(0, right - Width - 4), Owner.At.Y - 2);
        }
    }

    /// <summary>The item at <paramref name="at"/>, by its index.</summary>
    private int? At(Point at)
    {
        if (at.X < Picture.Rect.X + 4 || at.X >= Picture.Rect.X + 4 + Width) return null;
        var top = Picture.Rect.Y + 4 + Pad;
        for (var i = 0; i < Items.Length; i++)
        {
            var h = Items[i] == null ? Gap : Row;
            if (at.Y >= top && at.Y < top + h) return Items[i] == null ? null : i;
            top += h;
        }
        return null;
    }

    public void Over(Point at)
    {
        var now = At(at);
        if (now == hover) return;
        hover = now;
        Draw();
    }

    public void Up(Click click)
    {
        if (hover is not { } i || Items[i] is not { } item) return;
        Close();
        item.Act(Owner);
    }

    /// <summary>Any key closes the menu; Esc does only that, others go on to the window.</summary>
    internal override bool Key(uint scancode, bool extended, int mods, bool down)
    {
        if (!down) return false;
        Close();
        return scancode == 0x01;
    }

    protected override Canvas Paint()
    {
        var font = Display.Font;
        var height = HeightOf;
        var c = new Canvas(Width + 8, height + 8);
        c.Round(new Rect(4, 6, Width, height), 10, Color.Shadow);
        c.Round(new Rect(4, 4, Width, height), 10, Color.Edge);
        c.Round(new Rect(5, 5, Width - 2, height - 2), 9, Color.Field);
        var top = 4 + Pad;
        for (var i = 0; i < Items.Length; i++)
        {
            if (Items[i] is not { } item)
            {
                var y = top + Gap / 2f;
                c.Line(16, y, Width - 8, y, 1, Color.Rule);
                top += Gap;
                continue;
            }
            if (hover == i) c.Round(new Rect(4 + Pad, top, Width - 2 * Pad, Row), 6, Color.Hover);
            var mid = top + Row / 2f;
            c.Text(font, item.Label, 20, mid, Width - 70, Color.Ink);
            if (item.Keys.Length > 0)
                c.Text(font, item.Keys, Width - 12 - Canvas.Measure(font, item.Keys), mid, Width, Color.InkInactive);
            top += Row;
        }
        return c;
    }
}
