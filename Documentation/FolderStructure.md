Root folder: contains Start.goal, Setup.goal, .build, .db folders.
Root example: c:\apps\TestApp (TestApp is root).
.db folder: has system.sqlite, data.sqlite (if app has tables).

API folder: holds REST services, location adjustable.
Public goals in API: include API details in goal name (e.g., GetList - POST, max length=1mb, public cache).

UI folder: holds User interface files, defines data structure, not device-specific.
Default UI build: HTML/JavaScript/CSS, Bootstrap, Font Awesome.

Events folder: contains application events (refer to Events.md).