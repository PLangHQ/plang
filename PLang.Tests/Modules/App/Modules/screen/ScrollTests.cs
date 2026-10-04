using Rect = app.module.screen.type.screen.display.code.Rect;
using Scroll = app.module.screen.type.screen.display.code.Scroll;

namespace PLang.Tests.App.actions.screen;

/// <summary>
/// A scrolled page found by its rows: the band of rows the host already shows a few rows up or down, and how far
/// they moved — so the host moves them itself and only the new strip is sent. A rectangle whose rows are all new (a
/// video) or that didn't move is no scroll.
/// </summary>
public class ScrollTests
{
    private const int Width = 400;
    private static readonly Rect Area = new(10, 20, 300, 200);

    // the host's picture: every row of the area different (a page of text)
    private static byte[] Screen(Random random)
    {
        var screen = new byte[Width * 300 * 4];
        random.NextBytes(screen);
        return screen;
    }

    private static Span<byte> Row(byte[] screen, int y) => screen.AsSpan((y * Width + Area.X) * 4, Area.Width * 4);

    // the area composed anew: row i is the host's row source(i) of the area, or new pixels where source gives -1
    private static byte[] Composed(byte[] screen, Func<int, int> source, Random random)
    {
        var row = Area.Width * 4;
        var composed = new byte[Area.Height * row];
        for (var i = 0; i < Area.Height; i++)
        {
            var target = composed.AsSpan(i * row, row);
            var from = source(i);
            if (from < 0) random.NextBytes(target);
            else Row(screen, Area.Y + from).CopyTo(target);
        }
        return composed;
    }

    [Test]
    public async Task APageScrolledUp_IsItsRowsMovedUp_AndTheNewStripBelow()
    {
        var random = new Random(1);
        var screen = Screen(random);
        var composed = Composed(screen, i => i + 6 < Area.Height ? i + 6 : -1, random);
        await Assert.That(Scroll.Find(Area, composed, screen, Width)).IsEqualTo((Area.X, Area.Right, Area.Y, Area.Y + Area.Height - 6, -6));
    }

    [Test]
    public async Task APageScrolledDown_IsItsRowsMovedDown()
    {
        var random = new Random(2);
        var screen = Screen(random);
        var composed = Composed(screen, i => i >= 10 ? i - 10 : -1, random);
        await Assert.That(Scroll.Find(Area, composed, screen, Width)).IsEqualTo((Area.X, Area.Right, Area.Y + 10, Area.Y + Area.Height, 10));
    }

    [Test]
    public async Task UnderAHeaderThatStays_OnlyTheRowsBelowItMove()
    {
        var random = new Random(3);
        var screen = Screen(random);
        var composed = Composed(screen, i => i < 40 ? i : i + 6 < Area.Height ? i + 6 : -1, random);
        await Assert.That(Scroll.Find(Area, composed, screen, Width)).IsEqualTo((Area.X, Area.Right, Area.Y + 40, Area.Y + Area.Height - 6, -6));
    }

    // a page's column scrolls beside a sidebar that stays (Wikipedia's contents): only the column moves
    [Test]
    public async Task BesideASidebarThatStays_OnlyTheColumnMoves()
    {
        var random = new Random(8);
        var screen = Screen(random);
        var composed = Composed(screen, i => i + 6 < Area.Height ? i + 6 : -1, random);
        // the left 80 pixels of every row are the sidebar: as the host shows them
        var row = Area.Width * 4;
        for (var i = 0; i < Area.Height; i++) Row(screen, Area.Y + i)[..(80 * 4)].CopyTo(composed.AsSpan(i * row, 80 * 4));
        var found = Scroll.Find(Area, composed, screen, Width);
        await Assert.That(found).IsEqualTo((Area.X + 80, Area.Right, Area.Y, Area.Y + Area.Height - 6, -6));
    }

    [Test]
    public async Task AllRowsNew_LikeAVideo_IsNoScroll()
    {
        var random = new Random(4);
        var screen = Screen(random);
        await Assert.That(Scroll.Find(Area, Composed(screen, _ => -1, random), screen, Width)).IsNull();
    }

    [Test]
    public async Task NothingMoved_IsNoScroll()
    {
        var random = new Random(5);
        var screen = Screen(random);
        await Assert.That(Scroll.Find(Area, Composed(screen, i => i, random), screen, Width)).IsNull();
    }

    [Test]
    public async Task ASmallArea_IsNotLookedAt()
    {
        var random = new Random(6);
        var screen = Screen(random);
        var small = Area with { Width = 100 };
        var row = small.Width * 4;
        var composed = new byte[small.Height * row];
        for (var i = 0; i < small.Height - 6; i++)
            screen.AsSpan(((small.Y + i + 6) * Width + small.X) * 4, row).CopyTo(composed.AsSpan(i * row, row));
        await Assert.That(Scroll.Find(small, composed, screen, Width)).IsNull();
    }
}
