using Damage = app.module.screen.type.screen.display.code.Damage;
using Rect = app.module.screen.type.screen.display.code.Rect;

namespace PLang.Tests.App.actions.screen;

/// <summary>
/// What changed in a composed area, as narrow rectangles: a scrollbar's thumb and a page's new strip are two small
/// rectangles, each with exactly its new pixels, and the host's picture ends up as what was composed.
/// </summary>
public class DamageTests
{
    private const int Width = 400;
    private static readonly Rect Area = new(10, 20, 300, 200);

    private static (byte[] screen, byte[] composed) Changed(params Rect[] changes)
    {
        var random = new Random(7);
        var screen = new byte[Width * 300 * 4];
        random.NextBytes(screen);
        var row = Area.Width * 4;
        var composed = new byte[Area.Height * row];
        for (var i = 0; i < Area.Height; i++)
            screen.AsSpan(((Area.Y + i) * Width + Area.X) * 4, row).CopyTo(composed.AsSpan(i * row, row));
        foreach (var c in changes)
            for (var y = c.Y; y < c.Bottom; y++)
                random.NextBytes(composed.AsSpan((y - Area.Y) * row + (c.X - Area.X) * 4, c.Width * 4));
        return (screen, composed);
    }

    private static byte[] Area_(byte[] screen)
    {
        var row = Area.Width * 4;
        var area = new byte[Area.Height * row];
        for (var i = 0; i < Area.Height; i++)
            screen.AsSpan(((Area.Y + i) * Width + Area.X) * 4, row).CopyTo(area.AsSpan(i * row, row));
        return area;
    }

    [Test]
    public async Task TwoSmallChangesFarApart_AreTwoNarrowRectangles_WithTheirPixels()
    {
        var thumb = new Rect(Area.X + 290, Area.Y + 30, 10, 40);
        var strip = new Rect(Area.X, Area.Y + 190, 300, 10);
        var (screen, composed) = Changed(thumb, strip);
        var expected = (byte[])composed.Clone();
        var found = Damage.Find(Area, composed, screen, Width, all: false);

        await Assert.That(found.Select(f => f.Rect)).IsEquivalentTo(new[] { thumb, strip });
        foreach (var (rect, at) in found)
            for (var y = 0; y < rect.Height; y++)
            {
                var want = expected.AsSpan((rect.Y + y - Area.Y) * Area.Width * 4 + (rect.X - Area.X) * 4, rect.Width * 4);
                var got = composed.AsSpan(at + y * rect.Width * 4, rect.Width * 4);
                await Assert.That(got.SequenceEqual(want)).IsTrue().Because($"row {y} of {rect}");
            }
        await Assert.That(Area_(screen).SequenceEqual(expected)).IsTrue().Because("the host's picture is what was composed");
    }

    [Test]
    public async Task NothingChanged_IsNothing()
    {
        var (screen, composed) = Changed();
        await Assert.That(Damage.Find(Area, composed, screen, Width, all: false)).IsEmpty();
    }

    [Test]
    public async Task All_IsTheWholeArea()
    {
        var (screen, composed) = Changed();
        var found = Damage.Find(Area, composed, screen, Width, all: true);
        await Assert.That(found.Select(f => f.Rect)).IsEquivalentTo(new[] { Area });
    }
}
