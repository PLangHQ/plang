# Logger for Debugging

I have an app running in production that was crashing for some reason, but I wasn't receiving any error messages.

To troubleshoot, I ran the app with the logger in debug mode:

```bash
plang --logger=debug
```

This provided detailed information on every step and function being executed, allowing me to pinpoint exactly where the error occurred and resolve it.

![Logger debug](logger_in_debug.png)

The blue text is from the logger, while the white text is a simple `write out`.

I also fixed the issue where `plang` wasn't showing any errors. It turns out that the error was being logged to an old location. I suppose some mistakes never change! :)
