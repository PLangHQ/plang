Data — one line of JSON for the screen: an input event (`{"mouse":…}`, `{"key":…}`, `{"text":…}`, `{"clipboard":…}`) or a window command (`{"window":"focus|minimize|maximize|restore|close","id":…}`) · say: send <data> · builder: the value right after "send"
Screen — the screen to give it to, from screen.open · say: to %screen%
Returns — nothing on success; a ScreenNotOpen error when the screen isn't open.
