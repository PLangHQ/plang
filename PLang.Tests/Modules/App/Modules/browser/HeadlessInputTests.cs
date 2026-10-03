using Asked = app.module.browser.type.browser.headless.@this.Asked;
using Input = global::app.type.item.input;

namespace PLang.Tests.App.Modules.browser;

/// <summary>
/// A headless browser takes an input value whole and lowers each variant to its DevTools request — never by reading a
/// JSON line's keys: the mouse to Input.dispatchMouseEvent (the modifier keys as DevTools' bits), a named key to
/// Input.dispatchKeyEvent (Enter with its text), a key that types text to nothing (its text comes as text), text to
/// Input.insertText, back/forward/reload to the page's history or a reload.
/// </summary>
public class HeadlessInputTests
{
    private static Asked Lowered(Input.@this input)
    {
        var asked = new Asked();
        input.Apply(asked);
        return asked;
    }

    [Test]
    public async Task TheMouse_IsADispatchedMouseEvent_WithItsModifierBits()
    {
        // alt 1 + shift 8: the wire's bits, which are DevTools' too
        var down = Lowered(new Input.mouse.@this(Input.mouse.Gesture.down, 10, 20, Input.mouse.Button.left, 2, mods: 1 | 8));
        await Assert.That(down.Method).IsEqualTo("Input.dispatchMouseEvent");
        await Assert.That(down.Parameters!.ToJsonString())
            .IsEqualTo("{\"x\":10,\"y\":20,\"modifiers\":9,\"type\":\"mousePressed\",\"button\":\"left\",\"clickCount\":2}");
        await Assert.That(down.Moved).IsNull();

        var moved = Lowered(new Input.mouse.@this(Input.mouse.Gesture.move, 3, 4));
        await Assert.That(moved.Parameters!["type"]!.GetValue<string>()).IsEqualTo("mouseMoved");
        await Assert.That(moved.Moved).IsEqualTo((3, 4)).Because("after a move the pointer under it is asked for");

        var wheel = Lowered(new Input.mouse.@this(Input.mouse.Gesture.wheel, 0, 0, dy: -120));
        await Assert.That(wheel.Parameters!.ToJsonString()).IsEqualTo("{\"x\":0,\"y\":0,\"modifiers\":0,\"type\":\"mouseWheel\",\"deltaX\":0,\"deltaY\":-120}");
    }

    [Test]
    public async Task ANamedKey_IsADispatchedKeyEvent_AndATextKeyIsNothing()
    {
        var enter = Lowered(new Input.key.@this(down: true, scancode: 0x1C, vk: 0x0D));
        await Assert.That(enter.Parameters!.ToJsonString())
            .IsEqualTo("{\"type\":\"keyDown\",\"key\":\"Enter\",\"code\":\"Enter\",\"text\":\"\\r\",\"windowsVirtualKeyCode\":13,\"modifiers\":0}");

        var leftUp = Lowered(new Input.key.@this(down: false, scancode: 0x4B, extended: true, vk: 0x25));
        await Assert.That(leftUp.Parameters!.ToJsonString())
            .IsEqualTo("{\"type\":\"keyUp\",\"key\":\"ArrowLeft\",\"windowsVirtualKeyCode\":37,\"modifiers\":0}");

        var a = Lowered(new Input.key.@this(down: true, scancode: 0x1E, vk: 0x41));
        await Assert.That(a.Method).IsNull().Because("a key that types text has no name: its character comes as text");
    }

    [Test]
    public async Task TextIsInserted_AndBackForwardReloadGoToThePage()
    {
        var typed = Lowered(new Input.text.@this("á"));
        await Assert.That(typed.Method).IsEqualTo("Input.insertText");
        await Assert.That(typed.Parameters!["text"]!.GetValue<string>()).IsEqualTo("á");

        await Assert.That(Lowered(new Input.navigate.@this(Input.navigate.Direction.back)).Parameters!["expression"]!.GetValue<string>())
            .IsEqualTo("history.back()");
        await Assert.That(Lowered(new Input.navigate.@this(Input.navigate.Direction.reload)).Method).IsEqualTo("Page.reload");
    }
}
